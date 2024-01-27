//-----------------------------------------------------------------------------
// Filename: RtpIceChannel.cs
//
// Description: Represents an RTP channel with ICE connectivity checks as
// described in the Interactive Connectivity Establishment RFC8445
// https://tools.ietf.org/html/rfc8445.
//
// Remarks:
//
// Support for the following standards or proposed standards
// is included:
//
// - "Trickle ICE" as per draft RFC
//   https://tools.ietf.org/html/draft-ietf-ice-trickle-21.
//
// - "WebRTC IP Address Handling Requirements" as per draft RFC
//   https://tools.ietf.org/html/draft-ietf-rtcweb-ip-handling-12
//   SECURITY NOTE: See https://tools.ietf.org/html/draft-ietf-rtcweb-ip-handling-12#section-5.2
//   for recommendations on how a WebRTC application should expose a
//   hosts IP address information. This implementation is using Mode 2.
//
// - Session Traversal Utilities for NAT (STUN)
//   https://tools.ietf.org/html/rfc8553
//
// - Traversal Using Relays around NAT (TURN): Relay Extensions to
//   Session Traversal Utilities for NAT (STUN)
//   https://tools.ietf.org/html/rfc5766
//
// - Using Multicast DNS to protect privacy when exposing ICE candidates
//   draft-ietf-rtcweb-mdns-ice-candidates-04 [ed. not implemented as of 26 Jul 2020].
//   https://tools.ietf.org/html/draft-ietf-rtcweb-mdns-ice-candidates-04
//
// - Multicast DNS
//   https://tools.ietf.org/html/rfc6762
//
// Notes:
// The source from Chromium that performs the equivalent of this class
// (and much more) is:
// https://chromium.googlesource.com/external/webrtc/+/refs/heads/master/p2p/base/p2p_transport_channel.cc
//
// Multicast DNS: Chromium (and possibly other WebRTC stacks) make use of *.local
// DNS hostnames (see Multicast RFC linked above). Support for such hostnames is
// not supported directly in this library because there is no underlying support
// in .NET Core. A callback hook is available so that an application can connect
// up an MDNS resolver if required.
// Windows 10 has recently introduced a level of support for MDNS:
// https://docs.microsoft.com/en-us/uwp/api/windows.networking.servicediscovery.dnssd?view=winrt-19041
// From a command prompt:
// c:\> dns-md -B
// c:\> dns-sd -G v4 fbba6380-2cc4-41b1-ab0d-61548dd28a29.local
// c:\> dns-sd -G v6 b1f949b8-5ec9-41a6-b3ef-eb529f217de9.local
// But it's expected that it's highly unlikely support will be added to .NET Core
// any time soon (AC 26 Jul 2020).
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 15 Mar 2020	Aaron Clauson	Created, Dublin, Ireland.
// 23 Jun 2020  Aaron Clauson   Renamed from IceSession to RtpIceChannel.
// 03 Oct 2022  Rafal Soares	Add support to TCP IceServerConsts
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Networking;

/// <summary>
///     An RTP ICE Channel carries out connectivity checks with a remote peer in an
///     attempt to determine the best destination end point to communicate with the
///     remote party.
/// </summary>
/// <remarks>
///     Local server reflexive candidates don't get added to the checklist since they are just local
///     "host" candidates with an extra NAT address mapping. The NAT address mapping is needed for the
///     remote ICE peer but locally a server reflexive candidate is always going to be represented by
///     a "host" candidate.
///     Limitations:
///     - To reduce complexity only a single checklist is used. This is based on the main
///     webrtc use case where RTP (audio and video) and RTCP are all multiplexed on a
///     single socket pair. Therefore  there only needs to be a single component and single
///     data stream. If an additional use case occurs then multiple checklists could be added.
///     Developer Notes:
///     There are 4 main tasks occurring during the ICE checks:
///     - Local candidates: ICE server checks (which can take seconds) are being carried out to
///     gather "server reflexive" and "relay" candidates.
///     - Remote candidates: the remote peer should be trickling in its candidates which need to
///     be validated and if accepted new entries added to the checklist.
///     - Checklist connectivity checks: the candidate pairs in the checklist need to have
///     connectivity checks sent.
///     - Match STUN messages: STUN requests and responses are being received and need to be
///     matched to either an ICE server check or a checklist entry check. After matching
///     action needs to be taken to update the status of the ICE server or checklist entry
///     check.
/// </remarks>
internal class MultiplexedRtpChannel
{
    private const int ICE_UFRAG_LENGTH = 4;
    private const int ICE_PASSWORD_LENGTH = 24;

    private const int
        MAX_CHECKLIST_ENTRIES = 25; // Maximum number of entries that can be added to the checklist of candidate pairs.

    private const string MDNS_TLD = ".local"; // Top Level Domain name for multicast lookups as per RFC6762.

    private const int
        CONNECTED_CHECK_PERIOD = 3; // The period in seconds to send STUN connectivity checks once connected.

    private const int SDP_MLINE_INDEX = 0;


    /// <summary>
    ///     ICE transaction spacing interval in milliseconds.
    /// </summary>
    /// <remarks>
    ///     See https://tools.ietf.org/html/rfc8445#section-14.
    /// </remarks>
    private const int Ta = 50;

    private static readonly ILogger logger = Log.Logger;

    /// <summary>
    ///     The period in seconds after which a connection will be flagged as disconnected.
    /// </summary>
    private static readonly int DISCONNECTED_TIMEOUT_PERIOD = 8;

    /// <summary>
    ///     The period in seconds after which a connection will be flagged as failed.
    /// </summary>
    private static readonly int FAILED_TIMEOUT_PERIOD = 16;

    private readonly ulong _iceTiebreaker;

    /// <summary>
    ///     For local candidates this implementation takes a shortcut to reduce complexity.
    ///     The RTP socket will always be bound to one of:
    ///     - IPAddress.IPv6Any [::],
    ///     - IPAddress.Any 0.0.0.0, or,
    ///     - a specific single IP address.
    ///     As such it's only necessary to create a single checklist entry to cover all local
    ///     Host type candidates.
    ///     Host candidates must still be generated, based on all local IP addresses, and
    ///     will need to be transmitted to the remote peer but they don't need to
    ///     be used when populating the checklist.
    /// </summary>
    private readonly RTCIceCandidate _localChecklistCandidate;

    /// <summary>
    ///     A queue of remote ICE candidates that have been added to the session and that
    ///     are waiting to be processed to determine if they will create a new checklist entry.
    /// </summary>
    private readonly ConcurrentQueue<RTCIceCandidate> _pendingRemoteCandidates = new();

    public readonly string LocalIcePassword;
    public readonly string LocalIceUser;

    private readonly List<RTCIceCandidate> _candidates;

    /// <summary>
    ///     The checklist of local and remote candidate pairs
    /// </summary>
    private readonly List<ChecklistEntry> _checklist = new();

    private DateTime _checklistStartedAt = DateTime.MinValue;

    /// <summary>
    ///     The state of the checklist as the ICE checks are carried out.
    /// </summary>
    private ChecklistState _checklistState = ChecklistState.Running;

    private bool _closed;
    private bool _isClosed;
    private readonly ConcurrentBag<RTCIceCandidate> _remoteCandidates = new();

    private readonly UdpSocket _udpSocket;
    private readonly Func<IPEndPoint, byte[], Task> _onRtpDataReceivedHandler;

    /// <summary>
    ///     The local end point the RTP socket is listening on.
    /// </summary>
    private readonly IPEndPoint _rtpLocalEndPoint;

#nullable enable
    private Task? _connectivityChecksTask;
#nullable restore

    /// <summary>
    ///     Creates a new instance of an RTP ICE channel to provide RTP channel functions
    ///     with ICE connectivity checks.
    /// </summary>
    public MultiplexedRtpChannel(
        UdpSocket udpSocket,
        Func<IPEndPoint,byte[],Task> onRtpDataReceivedHandler)
    {
        _udpSocket = udpSocket;
        _rtpLocalEndPoint = _udpSocket.LocalEndpoint;
        _onRtpDataReceivedHandler = onRtpDataReceivedHandler;

        Component = RTCIceComponent.rtp;
        _candidates = GetHostCandidates();
        logger.LogDebug($"RTP ICE Channel discovered {_candidates.Count} local candidates.");
        _iceTiebreaker = Crypto.GetRandomULong();

        LocalIceUser = Crypto.GetRandomString(ICE_UFRAG_LENGTH);
        LocalIcePassword = Crypto.GetRandomString(ICE_PASSWORD_LENGTH);

        _localChecklistCandidate = new RTCIceCandidate(new RTCIceCandidateInit
        {
            sdpMLineIndex = SDP_MLINE_INDEX
        });

        _localChecklistCandidate.SetAddressProperties(
            RTCIceProtocol.udp,
            _rtpLocalEndPoint.Address,
            (ushort)_rtpLocalEndPoint.Port,
            RTCIceCandidateType.host,
            null,
            0);
    }

    /// <summary>
    ///     The local port we are listening for RTP (and whatever else is multiplexed) packets on.
    /// </summary>
    public int RTPPort => _rtpLocalEndPoint.Port;

    public RTCIceComponent Component { get; }

    public RTCIceGatheringState IceGatheringState { get; private set; } = RTCIceGatheringState.@new;

    public RTCIceConnectionState IceConnectionState { get; private set; } = RTCIceConnectionState.@new;

    /// <summary>
    ///     True if we are the "controlling" ICE agent (we initiated the communications) or
    ///     false if we are the "controlled" agent.
    /// </summary>
    public bool IsController { get; internal set; }

    /// <summary>
    ///     The list of host ICE candidates that have been gathered for this peer.
    /// </summary>
    public List<RTCIceCandidate> Candidates => _candidates.ToList();

    /// <summary>
    ///     If the connectivity checks are successful this will hold the entry that was
    ///     nominated by the connection check process.
    /// </summary>
    public ChecklistEntry NominatedEntry { get; private set; }

    /// <summary>
    ///     Retransmission timer for STUN transactions, measured in milliseconds.
    /// </summary>
    /// <remarks>
    ///     As specified in https://tools.ietf.org/html/rfc8445#section-14.
    /// </remarks>
    private int RTO
    {
        get
        {
            if (IceGatheringState == RTCIceGatheringState.gathering)
            {
                return Math.Max(500,
                    Ta * Candidates.Count(x =>
                        x.type is RTCIceCandidateType.srflx or RTCIceCandidateType.relay));
            }

            return Math.Max(500,
                Ta * (_checklist.Count(x => x.State == ChecklistEntryState.Waiting) +
                      _checklist.Count(x => x.State == ChecklistEntryState.InProgress)));
        }
    }

    private string _remoteIceUser;
    private string _remoteIcePassword;

    public event Action<RTCIceConnectionState> OnIceConnectionStateChange;

#nullable enable

    public void Start()
    {
        logger.LogDebug($"RTPChannel for {_rtpLocalEndPoint} started.");
        _udpSocket.StartReceive(OnRTPPacketReceived);

        IceGatheringState = RTCIceGatheringState.complete;
        _connectivityChecksTask = Task.Run(async () => await DoConnectivityCheckAsync());
    }

    public async ValueTask SendAsync(IPEndPoint dstEndPoint, ReadOnlyMemory<byte> buffer)
    {
        if (_isClosed)
        {
            return;
        }

        await _udpSocket.SendToAsync(buffer, dstEndPoint);
    }
#nullable restore

    /// <summary>
    ///     Set the ICE credentials that have been supplied by the remote peer. Once these
    ///     are set the connectivity checks should be able to commence.
    /// </summary>
    /// <param name="username">The remote peer's ICE username.</param>
    /// <param name="password">The remote peer's ICE password.</param>
    public void SetRemoteCredentials(string username, string password)
    {
        logger.LogDebug("RTP ICE Channel remote credentials set.");

        _remoteIceUser = username;
        _remoteIcePassword = password;

        if (IceConnectionState == RTCIceConnectionState.@new)
        {
            // A potential race condition exists here. The remote peer can send a binding request that
            // results in the ICE channel connecting BEFORE the remote credentials get set. Since the goal
            // is to connect ICE as quickly as possible it does not seem sensible to force a wait for the
            // remote credentials to be set. The credentials will still be used on STUN binding requests
            // sent on the connected ICE channel. In the case of WebRTC transport confidentiality is still
            // preserved since the DTLS negotiation will sill need to check the certificate fingerprint in
            // supplied by the remote offer.

            _checklistStartedAt = DateTime.Now;

            // Once the remote party's ICE credentials are known connection checking can
            // commence immediately as candidates trickle in.
            IceConnectionState = RTCIceConnectionState.checking;
            OnIceConnectionStateChange?.Invoke(IceConnectionState);
        }
    }

    /// <summary>
    ///     Adds a remote ICE candidate to the RTP ICE Channel.
    /// </summary>
    /// <param name="candidate">An ICE candidate from the remote party.</param>
    public void AddRemoteCandidate(RTCIceCandidate candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.address))
        {
            // Note that the way ICE signals the end of the gathering stage is to send
            // an empty candidate or "end-of-candidates" SDP attribute.
            logger.LogWarning( "Remote ICE candidate was empty.");
        }
        else if (candidate.component != Component)
        {
            // This occurs if the remote party made an offer and assumed we couldn't multiplex the audio and video streams.
            // It will offer the same ICE candidates separately for the audio and video announcements.
            logger.LogWarning("Remote ICE candidate has unsupported component.");
        }
        else if (candidate.SDPMLineIndex != 0)
        {
            // This implementation currently only supports audio and video multiplexed on a single channel.
            logger.LogWarning($"Remote ICE candidate only supports multiplexed media, excluding remote candidate with non-zero sdpMLineIndex of {candidate.SDPMLineIndex}.");
        }
        else if (candidate.protocol != RTCIceProtocol.udp)
        {
            // This implementation currently only supports UDP for RTP communications.
            logger.LogWarning($"Remote ICE candidate has an unsupported transport protocol {candidate.protocol}.");
        }
        else if (candidate.address.Trim().ToLower().EndsWith(MDNS_TLD))
        {
            // Supporting MDNS lookups means an additional nuget dependency. Hopefully
            // support is coming to .Net Core soon (AC 12 Jun 2020).
            logger.LogWarning($"Remote ICE candidate has an unsupported MDNS hostname {candidate.address}.");
        }
        else if (IPAddress.TryParse(candidate.address, out var addr) &&
                 (IPAddress.Any.Equals(addr) || IPAddress.IPv6Any.Equals(addr)))
        {
            logger.LogWarning($"Remote ICE candidate had a wildcard IP address {candidate.address}.");
        }
        else if (candidate.port <= 0)
        {
            logger.LogWarning($"Remote ICE candidate had an invalid port {candidate.port}.");
        }
        else
        {
            // Have a remote candidate. Connectivity checks can start. Note because we support ICE trickle
            // we may also still be gathering candidates. Connectivity checks and gathering can be done in parallel.

            logger.LogDebug($"RTP ICE Channel received remote candidate: {candidate}");

            _remoteCandidates.Add(candidate);
            _pendingRemoteCandidates.Enqueue(candidate);
        }
    }

    /// <summary>
    ///     Acquires an ICE candidate for each IP address that this host has except for:
    ///     - Loopback addresses must not be included.
    ///     - Deprecated IPv4-compatible IPv6 addresses and IPv6 site-local unicast addresses
    ///     must not be included,
    ///     - IPv4-mapped IPv6 address should not be included.
    ///     - If a non-location tracking IPv6 address is available use it and do not included
    ///     location tracking enabled IPv6 addresses (i.e. prefer temporary IPv6 addresses over
    ///     permanent addresses), see RFC6724.
    ///     SECURITY NOTE: https://tools.ietf.org/html/draft-ietf-rtcweb-ip-handling-12#section-5.2
    ///     Makes recommendations about how host IP address information should be exposed.
    ///     Of particular relevance are:
    ///     Mode 1:  Enumerate all addresses: WebRTC MUST use all network
    ///     interfaces to attempt communication with STUN servers, TURN
    ///     servers, or peers.This will converge on the best media
    ///     path, and is ideal when media performance is the highest
    ///     priority, but it discloses the most information.
    ///     Mode 2:  Default route + associated local addresses: WebRTC MUST
    ///     follow the kernel routing table rules, which will typically
    ///     cause media packets to take the same route as the
    ///     application's HTTP traffic.  If an enterprise TURN server is
    ///     present, the preferred route MUST be through this TURN
    ///     server.Once an interface has been chosen, the private IPv4
    ///     and IPv6 addresses associated with this interface MUST be
    ///     discovered and provided to the application as host
    ///     candidates.This ensures that direct connections can still
    ///     be established in this mode.
    ///     This implementation implements Mode 2.
    /// </summary>
    /// <remarks>
    ///     See https://tools.ietf.org/html/rfc8445#section-5.1.1.1
    ///     See https://tools.ietf.org/html/rfc6874 for a recommendation on how scope or zone ID's
    ///     should be represented as strings in IPv6 link local addresses. Due to parsing
    ///     issues in at least two other WebRTC stacks (as of Feb 2021) any zone ID is removed
    ///     from an ICE candidate string.
    /// </remarks>
    /// <returns>A list of "host" ICE candidates for the local machine.</returns>
    private List<RTCIceCandidate> GetHostCandidates()
    {
        var hostCandidates = new List<RTCIceCandidate>();
        var init = new RTCIceCandidateInit();

        // RFC8445 states that loopback addresses should not be included in
        // host candidates. If the provided bind address is a loopback
        // address it means no host candidates will be gathered. To avoid this
        // set the desired interface address to the Internet facing address
        // in the event a loopback address was specified.
        //if (_bindAddress != null &&
        //    (IPAddress.IsLoopback(_bindAddress) ||
        //    IPAddress.Any.Equals(_bindAddress) ||
        //    IPAddress.IPv6Any.Equals(_bindAddress)))
        //{
        //    // By setting to null means the default Internet facing interface will be used.
        //    signallingDstAddress = null;
        //}

        var rtpBindAddress = _rtpLocalEndPoint.Address;

        // We get a list of local addresses that can be used with the address the RTP socket is bound on.
        List<IPAddress> localAddresses;
        if (IPAddress.Any.Equals(rtpBindAddress))
        {
            // IPv4 on 0.0.0.0 means can use all valid local IPv4 addresses.
            localAddresses = NetServices.GetLocalAddressesOnInterface()
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x)).ToList();
        }
        else
        {
            // If not bound on a [::] or 0.0.0.0 means we're only listening on a specific IP address
            // and that's the only one that can be used for the host candidate.
            localAddresses = new List<IPAddress> { rtpBindAddress };
        }

        foreach (var localAddress in localAddresses)
        {
            var hostCandidate = new RTCIceCandidate(init);
            hostCandidate.SetAddressProperties(RTCIceProtocol.udp, localAddress, (ushort)RTPPort,
                RTCIceCandidateType.host, null, 0);

            // We currently only support a single multiplexed connection for all data streams and RTCP.
            if (hostCandidate.component == RTCIceComponent.rtp && hostCandidate.SDPMLineIndex == SDP_MLINE_INDEX)
            {
                hostCandidates.Add(hostCandidate);
            }
        }

        return hostCandidates;
    }

    /// <summary>
    ///     Updates the checklist with new candidate pairs.
    /// </summary>
    /// <remarks>
    ///     From https://tools.ietf.org/html/rfc8445#section-6.1.2.2:
    ///     IPv6 link-local addresses MUST NOT be paired with other than link-local addresses.
    /// </remarks>
    /// <param name="localCandidate">The local candidate for the checklist entry.</param>
    /// <param name="remoteCandidate">
    ///     The remote candidate to attempt to create a new checklist
    ///     entry for.
    /// </param>
    private void UpdateChecklist(RTCIceCandidate localCandidate, RTCIceCandidate remoteCandidate)
    {
        if (localCandidate == null)
        {
            throw new ArgumentNullException("localCandidate",
                "The local candidate must be supplied for UpdateChecklist.");
        }

        if (remoteCandidate == null)
        {
            throw new ArgumentNullException("remoteCandidate",
                "The remote candidate must be supplied for UpdateChecklist.");
        }

        // Attempt to resolve the remote candidate address.
        if (!IPAddress.TryParse(remoteCandidate.address, out var remoteCandidateIPAddr))
        {
            if (remoteCandidate.address.ToLower().EndsWith(MDNS_TLD))
            {
                logger.LogWarning(
                    $"RTP ICE channel has no MDNS resolver set, cannot resolve remote candidate with MDNS hostname {remoteCandidate.address}.");
            }
            else
            {
                if (remoteCandidateIPAddr != null)
                {
                    remoteCandidate.SetDestinationEndPoint(new IPEndPoint(remoteCandidateIPAddr, remoteCandidate.port));
                }
            }
        }
        else
        {
            remoteCandidate.SetDestinationEndPoint(new IPEndPoint(remoteCandidateIPAddr, remoteCandidate.port));
        }

        // If the remote candidate is resolvable create a new checklist entry.
        if (remoteCandidate.DestinationEndPoint != null)
        {
            bool supportsIPv4;
            bool supportsIPv6;

            if (localCandidate.type == RTCIceCandidateType.relay)
            {
                supportsIPv4 = localCandidate.DestinationEndPoint.AddressFamily == AddressFamily.InterNetwork;
                supportsIPv6 = localCandidate.DestinationEndPoint.AddressFamily == AddressFamily.InterNetworkV6;
            }
            else
            {
                supportsIPv4 = _rtpLocalEndPoint.AddressFamily == AddressFamily.InterNetwork;
                supportsIPv6 = _rtpLocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6;
            }

            lock (_checklist)
            {
                if ((remoteCandidateIPAddr.AddressFamily == AddressFamily.InterNetwork && supportsIPv4) ||
                    (remoteCandidateIPAddr.AddressFamily == AddressFamily.InterNetworkV6 && supportsIPv6))
                {
                    var entry = new ChecklistEntry(localCandidate, remoteCandidate, IsController);

                    // Because only ONE checklist is currently supported each candidate pair can be set to
                    // a "waiting" state. If an additional checklist is ever added then only one candidate
                    // pair with the same foundation should be set to waiting across all checklists.
                    // See https://tools.ietf.org/html/rfc8445#section-6.1.2.6 for a somewhat convoluted
                    // explanation and example.
                    entry.State = ChecklistEntryState.Waiting;

                    AddChecklistEntry(entry);
                }

                // Finally sort the checklist to put it in priority order and if necessary remove lower
                // priority pairs.
                _checklist.Sort();

                while (_checklist.Count > MAX_CHECKLIST_ENTRIES)
                {
                    _checklist.RemoveAt(_checklist.Count - 1);
                }
            }
        }
        else
        {
            logger.LogWarning(
                $"RTP ICE Channel could not create a check list entry for a remote candidate with no destination end point, {remoteCandidate}.");
        }
    }

    /// <summary>
    ///     Attempts to add a checklist entry. If there is already an equivalent entry in the checklist
    ///     the entry may not be added or may replace an existing entry.
    /// </summary>
    /// <param name="entry">The new entry to attempt to add to the checklist.</param>
    private void AddChecklistEntry(ChecklistEntry entry)
    {
        // Check if there is already an entry that matches the remote candidate.
        // Note: The implementation in this class relies binding the socket used for all
        // local candidates on a SINGLE address (typically 0.0.0.0 or [::]). Consequently
        // there is no need to check the local candidate when determining duplicates. As long
        // as there is one checklist entry with each remote candidate the connectivity check will
        // work. To put it another way the local candidate information is not used on the
        // "Nominated" pair.

        var entryRemoteEP = entry.RemoteCandidate.DestinationEndPoint;

        var existingEntry = _checklist.SingleOrDefault(x =>
            x.LocalCandidate.type == entry.LocalCandidate.type
            && x.RemoteCandidate.DestinationEndPoint != null
            && x.RemoteCandidate.DestinationEndPoint.Address.Equals(entryRemoteEP.Address)
            && x.RemoteCandidate.DestinationEndPoint.Port == entryRemoteEP.Port
            && x.RemoteCandidate.protocol == entry.RemoteCandidate.protocol);

        if (existingEntry != null)
        {
            // Don't replace an existing checklist entry if it's already acting as the nominated entry.
            if (!existingEntry.Nominated)
            {
                if (entry.Priority > existingEntry.Priority)
                {
                    logger.LogDebug(
                        $"Removing lower priority entry and adding candidate pair to checklist for: {entry.RemoteCandidate}");
                    _checklist.Remove(existingEntry);
                    _checklist.Add(entry);
                }
                else
                {
                    logger.LogDebug(
                        $"Existing checklist entry has higher priority, NOT adding entry for: {entry.RemoteCandidate}");
                }
            }
        }
        else
        {
            // No existing entry.
            logger.LogDebug(
                $"Adding new candidate pair to checklist for: {entry.LocalCandidate.ToShortString()}->{entry.RemoteCandidate.ToShortString()}");
            _checklist.Add(entry);
        }
    }

    /// <summary>
    ///     The periodic logic to run to establish or monitor an ICE connection.
    /// </summary>
    private async Task DoConnectivityCheckAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Ta));
        while (true)
        {
            if (IceConnectionState == RTCIceConnectionState.failed)
            {
                timer.Dispose();
                return;
            }
            switch (IceConnectionState)
            {
                case RTCIceConnectionState.@new:
                case RTCIceConnectionState.checking:
                    await ProcessChecklistAsync();
                    break;

                case RTCIceConnectionState.connected:
                case RTCIceConnectionState.disconnected:
                    // Periodic checks on the nominated peer.
                    await SendCheckOnConnectedPairAsync(NominatedEntry);
                    break;

                case RTCIceConnectionState.failed:
                case RTCIceConnectionState.closed:
                    logger.LogDebug($"ICE RTP channel stopping connectivity checks in connection state {IceConnectionState}.");
                    break;
            }

            if (_checklistState == ChecklistState.Completed)
            {
                timer.Dispose();
                timer = new PeriodicTimer(TimeSpan.FromMilliseconds(CONNECTED_CHECK_PERIOD * 1000));
            }

            await timer.WaitForNextTickAsync();
        }
    }

    /// <summary>
    ///     Processes the checklist and sends any required STUN requests to perform connectivity checks.
    /// </summary>
    /// <remarks>
    ///     The scheduling mechanism for ICE is specified in https://tools.ietf.org/html/rfc8445#section-6.1.4.
    /// </remarks>
    private async Task ProcessChecklistAsync()
    {
        if (!_closed && (IceConnectionState == RTCIceConnectionState.@new ||
                         IceConnectionState == RTCIceConnectionState.checking))
        {
            while (_pendingRemoteCandidates.Count > 0)
            {
                if (_pendingRemoteCandidates.TryDequeue(out var candidate))
                {
                    UpdateChecklist(_localChecklistCandidate, candidate);
                }
            }

            // The connection state will be set to checking when the remote ICE user and password are available.
            // Until that happens there is no work to do.
            if (IceConnectionState == RTCIceConnectionState.checking)
            {
                if (_checklist.Count > 0)
                {
                    if (_remoteIceUser == null || _remoteIcePassword == null)
                    {
                        logger.LogWarning(
                            "ICE RTP channel checklist processing cannot occur as either the remote ICE user or password are not set.");
                        IceConnectionState = RTCIceConnectionState.failed;
                    }
                    else
                    {
                        //lock (_checklist)
                        {
                            // The checklist gets sorted into priority order whenever a remote candidate and its corresponding candidate pairs
                            // are added. At this point it can be relied upon that the checklist is correctly sorted by candidate pair priority.

                            // Do a check for any timed out entries.
                            var failedEntries = _checklist.Where(x => x.State == ChecklistEntryState.InProgress
                                                                      && DateTime.Now.Subtract(x.FirstCheckSentAt)
                                                                          .TotalSeconds > FAILED_TIMEOUT_PERIOD)
                                .ToList();

                            foreach (var failedEntry in failedEntries)
                            {
                                logger.LogDebug(
                                    $"ICE RTP channel checks for checklist entry have timed out, state being set to failed: {failedEntry.LocalCandidate.ToShortString()}->{failedEntry.RemoteCandidate.ToShortString()}.");
                                failedEntry.State = ChecklistEntryState.Failed;
                            }

                            // Move on to checking for  checklist entries that need an initial check sent.
                            var nextEntry = _checklist.FirstOrDefault(x => x.State == ChecklistEntryState.Waiting);

                            if (nextEntry != null)
                            {
                                await SendConnectivityCheckAsync(nextEntry, false);
                                return;
                            }

                            var rto = RTO;
                            // No waiting entries so check for ones requiring a retransmit.
                            var retransmitEntry = _checklist.FirstOrDefault(x => x.State == ChecklistEntryState.InProgress
                                                                        && DateTime.Now.Subtract(x.LastCheckSentAt)
                                                                            .TotalMilliseconds > rto);

                            if (retransmitEntry != null)
                            {
                                await SendConnectivityCheckAsync(retransmitEntry, false);
                                return;
                            }

                            if (IceGatheringState == RTCIceGatheringState.complete)
                            {
                                //Try force finalize process as probably we lost any RtpPacketResponse during process and we are unable to finalize process
                                if (NominatedEntry == null)
                                {
                                    // Do a check for any timed out that has succeded
                                    var failedNominatedEntries = _checklist.Where(x =>
                                        x.State == ChecklistEntryState.Succeeded
                                        && x.LastCheckSentAt > DateTime.MinValue
                                        && DateTime.Now.Subtract(x.LastCheckSentAt).TotalSeconds >
                                        FAILED_TIMEOUT_PERIOD).ToList();

                                    var requireReprocess = false;
                                    foreach (var failedNominatedEntry in failedNominatedEntries)
                                    {
                                        //Recalculate logic when we lost a nominated entry
                                        if (failedNominatedEntry.Nominated)
                                        {
                                            requireReprocess = true;
                                        }

                                        failedNominatedEntry.State = ChecklistEntryState.Failed;
                                        failedNominatedEntry.Nominated = false;

                                        logger.LogDebug(
                                            $"ICE RTP channel checks for succeded checklist entry have timed out, state being set to failed: {failedNominatedEntry.LocalCandidate.ToShortString()}->{failedNominatedEntry.RemoteCandidate.ToShortString()}.");
                                    }

                                    //Try nominate another entry
                                    if (requireReprocess)
                                    {
                                        await ProcessNominateLogicAsControllerAsync(null);
                                    }
                                }

                                // If this point is reached and all entries are in a failed state then the overall result
                                // of the ICE check is a failure.
                                if (_checklist.All(x => x.State == ChecklistEntryState.Failed))
                                {
                                    _checklistState = ChecklistState.Failed;
                                    IceConnectionState = RTCIceConnectionState.failed;
                                    OnIceConnectionStateChange?.Invoke(IceConnectionState);
                                }
                            }
                        }
                    }
                }
                else if (_checklistStartedAt != DateTime.MinValue &&
                         DateTime.Now.Subtract(_checklistStartedAt).TotalSeconds > FAILED_TIMEOUT_PERIOD)
                {
                    // No checklist entries were made available before the failed timeout.
                    logger.LogWarning(
                        $"ICE RTP channel failed to connect as no checklist entries became available within {DateTime.Now.Subtract(_checklistStartedAt).TotalSeconds:0.##}s.");

                    _checklistState = ChecklistState.Failed;
                    //IceConnectionState = RTCIceConnectionState.disconnected;
                    // No point going to and ICE disconnected state as there was never a connection and therefore
                    // nothing to monitor for a re-connection.
                    IceConnectionState = RTCIceConnectionState.failed;
                    OnIceConnectionStateChange?.Invoke(IceConnectionState);
                }
            }
        }
    }

    /// <summary>
    ///     Sets the nominated checklist entry. This action completes the checklist processing and
    ///     indicates the connection checks were successful.
    /// </summary>
    /// <param name="entry">The checklist entry that was nominated.</param>
    private void SetNominatedEntry(ChecklistEntry entry)
    {
        if (NominatedEntry == null)
        {
            logger.LogInformation(
                $"ICE RTP channel connected {entry.LocalCandidate.ToShortString()}->{entry.RemoteCandidate.ToShortString()}.");

            entry.Nominated = true;
            entry.LastConnectedResponseAt = DateTime.Now;
            _checklistState = ChecklistState.Completed;
            NominatedEntry = entry;
            IceConnectionState = RTCIceConnectionState.connected;
            OnIceConnectionStateChange?.Invoke(RTCIceConnectionState.connected);
        }
        else
        {
            // The nominated entry has been changed.
            logger.LogInformation(
                $"ICE RTP channel remote nominated candidate changed from {NominatedEntry.RemoteCandidate.ToShortString()} to {entry.RemoteCandidate.ToShortString()}.");

            entry.Nominated = true;
            entry.LastConnectedResponseAt = DateTime.Now;
            NominatedEntry = entry;
            OnIceConnectionStateChange?.Invoke(RTCIceConnectionState.connected);
        }
    }

    /// <summary>
    ///     Performs a connectivity check for a single candidate pair entry.
    /// </summary>
    /// <param name="candidatePair">The candidate pair to perform a connectivity check for.</param>
    /// <param name="setUseCandidate">
    ///     If true indicates we are acting as the "controlling" ICE agent
    ///     and are nominating this candidate as the chosen one.
    /// </param>
    /// <remarks>
    ///     As specified in https://tools.ietf.org/html/rfc8445#section-7.2.4.
    ///     Relay candidates are a special (and more difficult) case. The extra steps required to send packets via
    ///     a TURN server are:
    ///     - A Channel Bind request needs to be sent for each peer end point the channel will be used to
    ///     communicate with.
    ///     - Packets need to be sent and received as TURN Channel Data messages.
    /// </remarks>
    private async Task SendConnectivityCheckAsync(ChecklistEntry candidatePair, bool setUseCandidate)
    {
        if (_closed)
        {
            return;
        }

        if (candidatePair.FirstCheckSentAt == DateTime.MinValue)
        {
            candidatePair.FirstCheckSentAt = DateTime.Now;
            candidatePair.State = ChecklistEntryState.InProgress;
        }

        candidatePair.LastCheckSentAt = DateTime.Now;
        candidatePair.RequestTransactionId = Crypto.GetRandomString(STUNHeader.TRANSACTION_ID_LENGTH);

        var isRelayCheck = candidatePair.LocalCandidate.type == RTCIceCandidateType.relay;
        //bool isTcpProtocol = candidatePair.LocalCandidate.IceServerConsts?.Protocol == ProtocolType.Tcp;

        if (isRelayCheck && candidatePair.TurnPermissionsResponseAt == DateTime.MinValue)
        {
            if (candidatePair.TurnPermissionsRequestSent >= IceServerConsts.MAX_REQUESTS)
            {
                logger.LogWarning(
                    $"ICE RTP channel failed to get a Create Permissions response from after {candidatePair.TurnPermissionsRequestSent} attempts.");
                candidatePair.State = ChecklistEntryState.Failed;
            }
            else
            {
                // Send Create Permissions request to TURN server for remote candidate.
                candidatePair.TurnPermissionsRequestSent++;
            }
        }
        else
        {
            if (candidatePair.LocalCandidate.type == RTCIceCandidateType.relay)
            {
                logger.LogDebug(
                    $"ICE RTP channel sending connectivity check for {candidatePair.LocalCandidate.ToShortString()}->{candidatePair.RemoteCandidate.ToShortString()} from {_rtpLocalEndPoint} to relay at TODO (use candidate {setUseCandidate}).");
            }
            else
            {
                var remoteEndPoint = candidatePair.RemoteCandidate.DestinationEndPoint;
                logger.LogDebug(
                    $"ICE RTP channel sending connectivity check for {candidatePair.LocalCandidate.ToShortString()}->{candidatePair.RemoteCandidate.ToShortString()} from {_rtpLocalEndPoint} to {remoteEndPoint} (use candidate {setUseCandidate}).");
            }

            await SendSTUNBindingRequestAsync(candidatePair, setUseCandidate);
        }
    }

    /// <summary>
    ///     Builds and sends a STUN binding request to a remote peer based on the candidate pair properties.
    /// </summary>
    /// <param name="candidatePair">
    ///     The candidate pair identifying the remote peer to send the STUN Binding Request
    ///     to.
    /// </param>
    /// <param name="setUseCandidate">Set to true to add a "UseCandidate" attribute to the STUN request.</param>
    private async Task SendSTUNBindingRequestAsync(ChecklistEntry candidatePair, bool setUseCandidate)
    {
        var stunRequest = new STUNMessage(STUNMessageTypesEnum.BindingRequest);
        stunRequest.Header.TransactionId = Encoding.ASCII.GetBytes(candidatePair.RequestTransactionId);
        stunRequest.AddUsernameAttribute(_remoteIceUser + ":" + LocalIceUser);
        stunRequest.Attributes.Add(new STUNAttribute(STUNAttributeTypesEnum.Priority,
            BitConverter.GetBytes(candidatePair.LocalPriority)));

        if (IsController)
        {
            stunRequest.Attributes.Add(new STUNAttribute(STUNAttributeTypesEnum.IceControlling,
                NetConvert.GetBytes(_iceTiebreaker)));
        }
        else
        {
            stunRequest.Attributes.Add(new STUNAttribute(STUNAttributeTypesEnum.IceControlled,
                NetConvert.GetBytes(_iceTiebreaker)));
        }

        if (setUseCandidate)
        {
            stunRequest.Attributes.Add(new STUNAttribute(STUNAttributeTypesEnum.UseCandidate, null));
        }

        var stunReqBytes = stunRequest.ToByteBufferStringKey(_remoteIcePassword, true);

        if (candidatePair.LocalCandidate.type == RTCIceCandidateType.relay)
        {
        }
        else
        {
            var remoteEndPoint = candidatePair.RemoteCandidate.DestinationEndPoint;
            await SendAsync(remoteEndPoint, stunReqBytes);
        }
    }

    /// <summary>
    ///     Builds and sends the connectivity check on a candidate pair that is set
    ///     as the current nominated, connected pair.
    /// </summary>
    /// <param name="candidatePair">The pair to send the connectivity check on.</param>
    private async Task SendCheckOnConnectedPairAsync(ChecklistEntry candidatePair)
    {
        if (candidatePair == null)
        {
            logger.LogWarning("RTP ICE channel was requested to send a connectivity check on an empty candidate pair.");
        }
        else
        {
            if (DateTime.Now.Subtract(candidatePair.LastConnectedResponseAt).TotalSeconds > FAILED_TIMEOUT_PERIOD &&
                DateTime.Now.Subtract(candidatePair.LastBindingRequestReceivedAt).TotalSeconds > FAILED_TIMEOUT_PERIOD)
            {
                var duration = (int)DateTime.Now.Subtract(candidatePair.LastConnectedResponseAt).TotalSeconds;
                logger.LogWarning(
                    $"ICE RTP channel failed after {duration:0.##}s {candidatePair.LocalCandidate.ToShortString()}->{candidatePair.RemoteCandidate.ToShortString()}.");

                IceConnectionState = RTCIceConnectionState.failed;
                OnIceConnectionStateChange?.Invoke(IceConnectionState);
            }
            else
            {
                if (DateTime.Now.Subtract(candidatePair.LastConnectedResponseAt).TotalSeconds >
                    DISCONNECTED_TIMEOUT_PERIOD &&
                    DateTime.Now.Subtract(candidatePair.LastBindingRequestReceivedAt).TotalSeconds >
                    DISCONNECTED_TIMEOUT_PERIOD)
                {
                    if (IceConnectionState == RTCIceConnectionState.connected)
                    {
                        var duration = (int)DateTime.Now.Subtract(candidatePair.LastConnectedResponseAt).TotalSeconds;
                        logger.LogWarning(
                            $"ICE RTP channel disconnected after {duration:0.##}s {candidatePair.LocalCandidate.ToShortString()}->{candidatePair.RemoteCandidate.ToShortString()}.");

                        IceConnectionState = RTCIceConnectionState.disconnected;
                        OnIceConnectionStateChange?.Invoke(IceConnectionState);
                    }
                }
                else if (IceConnectionState != RTCIceConnectionState.connected)
                {
                    logger.LogDebug(
                        $"ICE RTP channel has re-connected {candidatePair.LocalCandidate.ToShortString()}->{candidatePair.RemoteCandidate.ToShortString()}.");

                    // Re-connected.
                    IceConnectionState = RTCIceConnectionState.connected;
                    OnIceConnectionStateChange?.Invoke(IceConnectionState);
                }

                candidatePair.RequestTransactionId = candidatePair.RequestTransactionId ??
                                                     Crypto.GetRandomString(STUNHeader.TRANSACTION_ID_LENGTH);
                candidatePair.LastCheckSentAt = DateTime.Now;

                await SendSTUNBindingRequestAsync(candidatePair, false);
            }
        }
    }

    /// <summary>
    ///     Processes a received STUN request or response.
    /// </summary>
    /// <remarks>
    ///     Actions to take on a successful STUN response https://tools.ietf.org/html/rfc8445#section-7.2.5.3
    ///     - Discover peer reflexive remote candidates as per https://tools.ietf.org/html/rfc8445#section-7.2.5.3.1.
    ///     - Construct a valid pair which means match a candidate pair in the check list and mark it as valid (since a
    ///     successful STUN exchange
    ///     has now taken place on it). A new entry may need to be created for this pair for a peer reflexive candidate.
    ///     - Update state of candidate pair that generated the check to Succeeded.
    ///     - If the controlling candidate set the USE_CANDIDATE attribute then the ICE agent that receives the successful
    ///     response sets the nominated
    ///     flag of the pair to true. Once the nominated flag is set it concludes the ICE processing for that component.
    /// </remarks>
    /// <param name="stunMessage">The STUN message received.</param>
    /// <param name="remoteEndPoint">The remote end point the STUN packet was received from.</param>
    private async Task ProcessStunMessageAsync(STUNMessage stunMessage, IPEndPoint remoteEndPoint)
    {
        if (_closed)
        {
            return;
        }

        remoteEndPoint = !remoteEndPoint.Address.IsIPv4MappedToIPv6
            ? remoteEndPoint
            : new IPEndPoint(remoteEndPoint.Address.MapToIPv4(), remoteEndPoint.Port);

        // If the STUN message isn't for an ICE server then it needs to be matched against a remote
        // candidate and a checklist entry and if no match a "peer reflexive" candidate may need to
        // be created.
        if (stunMessage.Header.MessageType == STUNMessageTypesEnum.BindingRequest)
        {
            await GotStunBindingRequestAsync(stunMessage, remoteEndPoint);
        }
        else if (stunMessage.Header.MessageClass == STUNClassTypesEnum.ErrorResponse ||
                 stunMessage.Header.MessageClass == STUNClassTypesEnum.SuccessResponse)
        {
            // Correlate with request using transaction ID as per https://tools.ietf.org/html/rfc8445#section-7.2.5.
            var matchingChecklistEntry = GetChecklistEntryForStunResponse(stunMessage.Header.TransactionId);

            if (matchingChecklistEntry == null)
            {
                if (IceConnectionState != RTCIceConnectionState.connected)
                {
                    // If the channel is connected a mismatched txid can result if the connection is very busy, i.e. streaming 1080p video,
                    // it's likely to only be transient and does not impact the connection state.
                    logger.LogWarning(
                        $"ICE RTP channel received a STUN {stunMessage.Header.MessageType} with a transaction ID that did not match a checklist entry.");
                }
            }
            else
            {
                matchingChecklistEntry.GotStunResponse(stunMessage, remoteEndPoint);

                if (_checklistState == ChecklistState.Running &&
                    stunMessage.Header.MessageType == STUNMessageTypesEnum.BindingSuccessResponse)
                {
                    if (matchingChecklistEntry.Nominated)
                    {
                        logger.LogDebug(
                            $"ICE RTP channel remote peer nominated entry from binding response {matchingChecklistEntry.RemoteCandidate.ToShortString()}");

                        // This is the response to a connectivity check that had the "UseCandidate" attribute set.
                        SetNominatedEntry(matchingChecklistEntry);
                    }
                    else if (IsController)
                    {
                        logger.LogDebug(
                            $"ICE RTP channel binding response state {matchingChecklistEntry.State} as Controller for {matchingChecklistEntry.RemoteCandidate.ToShortString()}");
                        await ProcessNominateLogicAsControllerAsync(matchingChecklistEntry);
                    }
                }
            }
        }
        else
        {
            logger.LogWarning(
                $"ICE RTP channel received an unexpected STUN message {stunMessage.Header.MessageType} from {remoteEndPoint}.\nJson: {stunMessage}");
        }
    }

    /// <summary>
    ///     Handles Nominate logic when Agent is the controller
    /// </summary>
    /// <param name="possibleMatchingCheckEntry">Optional initial ChecklistEntry.</param>
    private async Task ProcessNominateLogicAsControllerAsync(ChecklistEntry possibleMatchingCheckEntry)
    {
        if (IsController && (NominatedEntry == null || !NominatedEntry.Nominated ||
                             NominatedEntry.State != ChecklistEntryState.Succeeded))
        {
            _checklist.Sort();

            var findBetterOptionOrWait =
                possibleMatchingCheckEntry ==
                null; //|| possibleMatchingCheckEntry.RemoteCandidate.type == RTCIceCandidateType.relay;
            var nominatedCandidate = _checklist.Find(
                x => x.Nominated
                     && x.State == ChecklistEntryState.Succeeded
                     && (x.LastCheckSentAt == DateTime.MinValue ||
                         DateTime.Now.Subtract(x.LastCheckSentAt).TotalSeconds <= FAILED_TIMEOUT_PERIOD));

            //We already have a good candidate, discard our succeded candidate
            if (nominatedCandidate != null /*&& nominatedCandidate.RemoteCandidate.type != RTCIceCandidateType.relay*/)
            {
                possibleMatchingCheckEntry = null;
                findBetterOptionOrWait = false;
            }

            if (findBetterOptionOrWait)
            {
                //Search for another succeded non-nominated entries with better priority over our current object.
                var betterOptionEntry = _checklist.Find(x =>
                    x.State == ChecklistEntryState.Succeeded &&
                    !x.Nominated &&
                    (possibleMatchingCheckEntry == null ||
                     x.Priority >
                     possibleMatchingCheckEntry.Priority /*&& x.RemoteCandidate.type != RTCIceCandidateType.relay*/ ||
                     possibleMatchingCheckEntry.State != ChecklistEntryState.Succeeded));

                if (betterOptionEntry != null)
                {
                    possibleMatchingCheckEntry = betterOptionEntry;
                }
            }

            //Nominate Candidate if we pass in all heuristic checks from previous algorithm
            if (possibleMatchingCheckEntry is { State: ChecklistEntryState.Succeeded })
            {
                possibleMatchingCheckEntry.Nominated = true;
                await SendConnectivityCheckAsync(possibleMatchingCheckEntry, true);
            }
        }
    }

    /// <summary>
    ///     Handles STUN binding requests received from remote candidates as part of the ICE connectivity checks.
    /// </summary>
    /// <param name="bindingRequest">The binding request received.</param>
    /// <param name="remoteEndPoint">The end point the request was received from.</param>
    /// <param name="wasRelayed">
    ///     True of the request was relayed via the TURN server in use
    ///     by this ICE channel (i.e. the ICE server that this channel is acting as the client with).
    /// </param>
    private async Task GotStunBindingRequestAsync(STUNMessage bindingRequest, IPEndPoint remoteEndPoint)
    {
        if (_closed)
        {
            return;
        }

        var result = bindingRequest.CheckIntegrity(Encoding.UTF8.GetBytes(LocalIcePassword));

        if (!result)
        {
            // Send STUN error response.
            logger.LogWarning(
                $"ICE RTP channel STUN binding request from {remoteEndPoint} failed an integrity check, rejecting.");
            var stunErrResponse = new STUNMessage(STUNMessageTypesEnum.BindingErrorResponse);
            stunErrResponse.Header.TransactionId = bindingRequest.Header.TransactionId;
            await SendAsync(remoteEndPoint, stunErrResponse.ToByteBuffer(null, false));
        }
        else
        {
            ChecklistEntry matchingChecklistEntry;

            // Find the checklist entry for this remote candidate and update its status.
            lock (_checklist)
            {
                // The matching checklist entry is chosen as:
                // - The entry that has a remote candidate with an end point that matches the endpoint this STUN request came from,
                // - And if the STUN request was relayed through a TURN server then only match is the checklist local candidate is
                //   also a relay type. It is possible for the same remote end point to send STUN requests directly and via a TURN server.
                matchingChecklistEntry = _checklist.FirstOrDefault(x => x.RemoteCandidate.IsEquivalentEndPoint(RTCIceProtocol.udp, remoteEndPoint));
            }

            if (matchingChecklistEntry == null &&
                (_remoteCandidates == null || !_remoteCandidates.Any(x => x.IsEquivalentEndPoint(RTCIceProtocol.udp, remoteEndPoint))))
            {
                // This STUN request has come from a socket not in the remote ICE candidates list.
                // Add a new remote peer reflexive candidate.
                var peerRflxCandidate = new RTCIceCandidate(new RTCIceCandidateInit());
                peerRflxCandidate.SetAddressProperties(RTCIceProtocol.udp, remoteEndPoint.Address,
                    (ushort)remoteEndPoint.Port, RTCIceCandidateType.prflx, null, 0);
                peerRflxCandidate.SetDestinationEndPoint(remoteEndPoint);
                logger.LogDebug($"Adding peer reflex ICE candidate for {remoteEndPoint}.");
                _remoteCandidates.Add(peerRflxCandidate);

                // Add a new entry to the check list for the new peer reflexive candidate.
                var entry = new ChecklistEntry(_localChecklistCandidate, peerRflxCandidate, IsController)
                {
                    State = ChecklistEntryState.Waiting
                };

                AddChecklistEntry(entry);

                matchingChecklistEntry = entry;
            }

            if (matchingChecklistEntry == null)
            {
                logger.LogWarning(
                    "ICE RTP channel STUN request matched a remote candidate but NOT a checklist entry.");
                var stunErrResponse = new STUNMessage(STUNMessageTypesEnum.BindingErrorResponse);
                stunErrResponse.Header.TransactionId = bindingRequest.Header.TransactionId;
                await SendAsync(remoteEndPoint, stunErrResponse.ToByteBuffer(null, false));
            }
            else
            {
                // The UseCandidate attribute is only meant to be set by the "Controller" peer. This implementation
                // will accept it irrespective of the peer roles. If the remote peer wants us to use a certain remote
                // end point then so be it.
                if (bindingRequest.Attributes.Any(x => x.AttributeType == STUNAttributeTypesEnum.UseCandidate))
                {
                    if (IceConnectionState != RTCIceConnectionState.connected)
                    {
                        // If we are the "controlled" agent and get a "use candidate" attribute that sets the matching candidate as nominated
                        // as per https://tools.ietf.org/html/rfc8445#section-7.3.1.5.
                        logger.LogDebug(
                            $"ICE RTP channel remote peer nominated entry from binding request: {matchingChecklistEntry.RemoteCandidate.ToShortString()}.");
                        SetNominatedEntry(matchingChecklistEntry);
                    }
                    else if (matchingChecklistEntry.RemoteCandidate.ToString() !=
                             NominatedEntry.RemoteCandidate.ToString())
                    {
                        // The remote peer is changing the nominated candidate.
                        logger.LogDebug(
                            $"ICE RTP channel remote peer nominated a new candidate: {matchingChecklistEntry.RemoteCandidate.ToShortString()}.");
                        SetNominatedEntry(matchingChecklistEntry);
                    }
                }

                matchingChecklistEntry.LastBindingRequestReceivedAt = DateTime.Now;

                var stunResponse = new STUNMessage(STUNMessageTypesEnum.BindingSuccessResponse);
                stunResponse.Header.TransactionId = bindingRequest.Header.TransactionId;
                stunResponse.AddXORMappedAddressAttribute(remoteEndPoint.Address, remoteEndPoint.Port);
                var stunRespBytes = stunResponse.ToByteBufferStringKey(LocalIcePassword, true);

                await SendAsync(remoteEndPoint, stunRespBytes);
            }
        }
    }

    /// <summary>
    ///     Attempts to get the matching checklist entry for the transaction ID in a STUN response.
    /// </summary>
    /// <param name="transactionID">The STUN response transaction ID.</param>
    /// <returns>A checklist entry or null if there was no match.</returns>
    private ChecklistEntry GetChecklistEntryForStunResponse(byte[] transactionID)
    {
        var txID = Encoding.ASCII.GetString(transactionID);
        ChecklistEntry matchingChecklistEntry;

        lock (_checklist)
        {
            matchingChecklistEntry = _checklist.FirstOrDefault(x => x.IsTransactionIdMatch(txID));
        }

        return matchingChecklistEntry;
    }

    /// <summary>
    ///     Event handler for packets received on the RTP UDP socket. This channel will detect STUN messages
    ///     and extract STUN messages to deal with ICE connectivity checks and TURN relays.
    /// </summary>
    /// <param name="packet">The raw packet received (note this may not be RTP if other protocols are being multiplexed).</param>
    private async Task OnRTPPacketReceived(UdpReceiveResult packet)
    {
        var packetBuffer = packet.Buffer;
        var remoteEndPoint = packet.RemoteEndPoint;
        if (packetBuffer.Length > 0)
        {
            if (packetBuffer[0] == 0x00 || packetBuffer[0] == 0x01)
            {
                // STUN packet.
                var stunMessage = STUNMessage.ParseSTUNMessage(packetBuffer, packetBuffer.Length);
                await ProcessStunMessageAsync(stunMessage, remoteEndPoint);
            }
            else
            {
                await _onRtpDataReceivedHandler(remoteEndPoint, packetBuffer);
            }
        }
    }

    /// <summary>
    ///     Closes the session's RTP and control ports.
    /// </summary>
    public async Task CloseAsync(string reason)
    {
        if (!_isClosed)
        {
            try
            {
                var closeReason = reason ?? "normal";
                logger.LogDebug($"RTPChannel closing, RTP socket on port {RTPPort}. Reason: {closeReason}.");
                await _udpSocket.StopAsync();
                _isClosed = true;
                if (_connectivityChecksTask != null)
                {
                    await _connectivityChecksTask;
                }
            }
            catch (Exception excp)
            {
                logger.LogError("Exception RTPChannel.Close. " + excp);
            }
        }
    }
}