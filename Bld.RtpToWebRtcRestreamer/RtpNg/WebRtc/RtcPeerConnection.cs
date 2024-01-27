using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Networking;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;

/// <summary>
///     Represents a WebRTC RTCPeerConnection.
/// </summary>
/// <remarks>
///     Interface is defined in https://www.w3.org/TR/webrtc/#interface-definition.
///     The Session Description offer/answer mechanisms are detailed in
///     https://tools.ietf.org/html/rfc8829 "JavaScript Session Establishment Protocol (JSEP)".
/// </remarks>
internal class RtcPeerConnection
{
    private readonly Func<RtcPeerConnection, RTCPeerConnectionState, Task> _peerConnectionChangeHandler;
    private static readonly ILogger Logger = Log.Logger;
    private readonly Certificate _dtlsCertificate;

    /// <summary>
    ///     The fingerprint of the certificate being used to negotiate the DTLS handshake with the
    ///     remote peer.
    /// </summary>
    private readonly RTCDtlsFingerprint _dtlsCertificateFingerprint;

    private readonly AsymmetricKeyParameter _dtlsPrivateKey;

    private readonly string _localSdpSessionId;

    [NotNull] private readonly MultiplexedRtpChannel _rtpIceChannel;

    private readonly List<List<SDPSsrcAttribute>> _videoRemoteSdpSsrcAttributes = new();

    /// <summary>
    ///     List of all Video Streams for this session
    /// </summary>
    [NotNull] private readonly VideoStream _videoStream;

    private RTCPeerConnectionState _connectionState = RTCPeerConnectionState.@new;

    /// <summary>
    ///     The ICE role the peer is acting in.
    /// </summary>
    private IceRolesEnum _iceRole = IceRolesEnum.actpass;

    private RTCSessionDescription _remoteDescription;

    /// <summary>
    ///     The DTLS fingerprint supplied by the remote peer in their SDP. Needs to be checked
    ///     that the certificate supplied during the DTLS handshake matches.
    /// </summary>
    private RTCDtlsFingerprint _remotePeerDtlsFingerprint;

    /// <summary>
    ///     The SDP offered by the remote call party for this session.
    /// </summary>
    private SDP _remoteSdp;

    private RTCSignalingState _signalingState = RTCSignalingState.closed;

    [CanBeNull] private DtlsSrtpTransport _dtlsHandle;

    /// <summary>
    ///     Constructor to create a new RTC peer connection instance.
    /// </summary>
    public RtcPeerConnection(
        [NotNull] MediaStreamTrack videoTrack,
        UdpSocket udpSocket,
        Func<RtcPeerConnection, RTCPeerConnectionState, Task> peerConnectionChangeHandler)
    {
        _peerConnectionChangeHandler = peerConnectionChangeHandler;
        // No certificate was provided so create a new self signed one.
        (_dtlsCertificate, _dtlsPrivateKey) =
            DtlsUtils.CreateSelfSignedTlsCert(ProtocolVersion.DTLSv12, new BcTlsCrypto());

        _dtlsCertificateFingerprint = DtlsUtils.Fingerprint(_dtlsCertificate);

        _localSdpSessionId = Crypto.GetRandomInt(5).ToString();

        _rtpIceChannel = new MultiplexedRtpChannel(udpSocket, OnRTPDataReceived);
        _rtpIceChannel.OnIceConnectionStateChange += IceConnectionStateChange;

        _videoStream = new VideoStream(videoTrack, _rtpIceChannel);

        _rtpIceChannel.Start();
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    ///     Indicates whether the session has been closed. Once a session is closed it cannot
    ///     be restarted.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    ///     Indicates whether this session is using video.
    /// </summary>
    private bool HasVideo => _videoStream.HasVideo;

    /// <summary>
    ///     Event handler for ICE connection state changes.
    /// </summary>
    private async void IceConnectionStateChange(RTCIceConnectionState iceState)
    {
        if (iceState == RTCIceConnectionState.connected && _rtpIceChannel.NominatedEntry != null)
        {
            if (_dtlsHandle != null)
            {
                if (_videoStream.DestinationEndPoint?.Address.Equals(_rtpIceChannel.NominatedEntry.RemoteCandidate
                        .DestinationEndPoint.Address) == false ||
                    _videoStream.DestinationEndPoint?.Port !=
                    _rtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint.Port)
                {
                    // Already connected and this event is due to change in the nominated remote candidate.
                    var connectedEP = _rtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint;

                    SetGlobalDestination(connectedEP);
                    Logger.LogInformation($"ICE changing connected remote end point to {connectedEP}.");
                }

                if (_connectionState == RTCPeerConnectionState.disconnected ||
                    _connectionState == RTCPeerConnectionState.failed)
                {
                    await SetConnectionStateAsync(RTCPeerConnectionState.connected);
                }
            }
            else
            {
                await SetConnectionStateAsync(RTCPeerConnectionState.connecting);

                var connectedEP = _rtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint;

                SetGlobalDestination(connectedEP);
                Logger.LogInformation($"ICE connected to remote end point {connectedEP}.");

                if (_iceRole == IceRolesEnum.active)
                {
                    _dtlsHandle = new DtlsSrtpTransport(
                        new DtlsSrtpClient(_dtlsCertificate, _dtlsPrivateKey),
                        async memory => await _rtpIceChannel.SendAsync(_videoStream.DestinationEndPoint, memory));
                }
                else
                {
                    _dtlsHandle = new DtlsSrtpTransport(
                        new DtlsSrtpServer(_dtlsCertificate, _dtlsPrivateKey),
                        async memory => await _rtpIceChannel.SendAsync(_videoStream.DestinationEndPoint, memory));
                }

                _dtlsHandle.OnAlert += OnDtlsAlert;

                Logger.LogDebug($"Starting DLS handshake with role {_iceRole}.");

                try
                {
                    // TODO: looks like not working without task
                    var handshakeResult = await Task.Run(async () => await DoDtlsHandshake(_dtlsHandle)).ConfigureAwait(false);

                    await SetConnectionStateAsync(handshakeResult ? RTCPeerConnectionState.connected : RTCPeerConnectionState.failed);
                }
                catch (Exception excp)
                {
                    Logger.LogWarning(excp, $"RTCPeerConnection DTLS handshake failed. {excp.Message}");

                    //connectionState = RTCPeerConnectionState.failed;
                    //onconnectionstatechange?.Invoke(connectionState);

                    await CloseAsync("dtls handshake failed");
                }
            }
        }

        if (_rtpIceChannel.IceConnectionState == RTCIceConnectionState.disconnected)
        {
            if (_connectionState == RTCPeerConnectionState.connected)
            {
                await SetConnectionStateAsync(RTCPeerConnectionState.disconnected);
            }
            else
            {
                await SetConnectionStateAsync(RTCPeerConnectionState.failed);
            }
        }
        else if (_rtpIceChannel.IceConnectionState == RTCIceConnectionState.failed)
        {
            await SetConnectionStateAsync(RTCPeerConnectionState.failed);
        }
    }

    /// <summary>
    ///     Updates the session after receiving the remote SDP.
    /// </summary>
    /// <param name="init">The answer/offer SDP from the remote party.</param>
    public SetDescriptionResultEnum SetRemoteDescription(RTCSessionDescriptionInit init)
    {
        _remoteDescription = new RTCSessionDescription { sdp = SDP.ParseSDPDescription(init.sdp) };

        var remoteSdp = _remoteDescription.sdp;

        var sdpType = init.type == RTCSdpType.offer ? SdpType.offer : SdpType.answer;

        if (_signalingState == RTCSignalingState.have_local_offer && sdpType == SdpType.offer)
        {
            Logger.LogWarning(
                $"RTCPeerConnection received an SDP offer but was already in {_signalingState} state. Remote offer rejected.");
            return SetDescriptionResultEnum.WrongSdpTypeOfferAfterOffer;
        }

        var setResult = SetRemoteDescription(remoteSdp);

        if (setResult == SetDescriptionResultEnum.OK)
        {
            var remoteIceUser = remoteSdp.IceUfrag;
            var remoteIcePassword = remoteSdp.IcePwd;
            var dtlsFingerprint = remoteSdp.DtlsFingerprint;
            var remoteIceRole = remoteSdp.IceRole;

            foreach (var ann in remoteSdp.Media)
            {
                if (remoteIceUser == null || remoteIcePassword == null || dtlsFingerprint == null ||
                    remoteIceRole == null)
                {
                    remoteIceUser = remoteIceUser ?? ann.IceUfrag;
                    remoteIcePassword = remoteIcePassword ?? ann.IcePwd;
                    dtlsFingerprint = dtlsFingerprint ?? ann.DtlsFingerprint;
                    remoteIceRole = remoteIceRole ?? ann.IceRole;
                }

                // Check for data channel announcements.
                if (ann.Media == SDPMediaTypesEnum.application &&
                    ann.MediaFormats.Count == 1 &&
                    ann.ApplicationMediaFormats.Single().Key == RtcPeerConnectionConstants.SDP_DATA_CHANNEL_FORMAT_ID)
                {
                    if (ann.Transport == RtcPeerConnectionConstants.RTP_MEDIA_DATA_CHANNEL_DTLS_PROFILE ||
                        ann.Transport == RtcPeerConnectionConstants.RTP_MEDIA_DATA_CHANNEL_UDP_DTLS_PROFILE)
                    {
                        dtlsFingerprint = dtlsFingerprint ?? ann.DtlsFingerprint;
                        remoteIceRole = remoteIceRole ?? remoteSdp.IceRole;
                    }
                    else
                    {
                        Logger.LogWarning(
                            $"The remote SDP requested an unsupported data channel transport of {ann.Transport}.");
                        return SetDescriptionResultEnum.DataChannelTransportNotSupported;
                    }
                }
            }

            if (remoteSdp.IceImplementation == IceImplementationEnum.lite)
            {
                _rtpIceChannel.IsController = true;
            }

            if (init.type == RTCSdpType.answer)
            {
                _rtpIceChannel.IsController = true;
                _iceRole = remoteIceRole == IceRolesEnum.passive ? IceRolesEnum.active : IceRolesEnum.passive;
            }
            //As Chrome does not support changing IceRole while renegotiating we need to keep same previous IceRole if we already negotiated before
            else
            {
                // Set DTLS role as client.
                _iceRole = IceRolesEnum.active;
            }

            if (remoteIceUser != null && remoteIcePassword != null)
            {
                _rtpIceChannel.SetRemoteCredentials(remoteIceUser, remoteIcePassword);
            }

            if (!string.IsNullOrWhiteSpace(dtlsFingerprint))
            {
                dtlsFingerprint = dtlsFingerprint.Trim().ToLower();
                if (RTCDtlsFingerprint.TryParse(dtlsFingerprint, out var remoteFingerprint))
                {
                    _remotePeerDtlsFingerprint = remoteFingerprint;
                }
                else
                {
                    Logger.LogWarning("The DTLS fingerprint was invalid or not supported.");
                    return SetDescriptionResultEnum.DtlsFingerprintDigestNotSupported;
                }
            }
            else
            {
                Logger.LogWarning("The DTLS fingerprint was missing from the remote party's session description.");
                return SetDescriptionResultEnum.DtlsFingerprintMissing;
            }

            // All browsers seem to have gone to trickling ICE candidates now but just
            // in case one or more are given we can start the STUN dance immediately.
            if (remoteSdp.IceCandidates != null)
            {
                foreach (var iceCandidate in remoteSdp.IceCandidates)
                {
                    AddIceCandidate(new RTCIceCandidateInit { candidate = iceCandidate });
                }
            }


            _videoRemoteSdpSsrcAttributes.Clear();
            foreach (var media in remoteSdp.Media)
            {
                if (media.IceCandidates != null)
                {
                    foreach (var iceCandidate in media.IceCandidates)
                    {
                        AddIceCandidate(new RTCIceCandidateInit { candidate = iceCandidate });
                    }
                }

                if (media.Media == SDPMediaTypesEnum.video)
                {
                    _videoRemoteSdpSsrcAttributes.Add(media.SsrcAttributes);
                }
            }

            Logger.LogDebug($"SDP:[{remoteSdp}]");
            LogRemoteSDPSsrcAttributes();

            if (init.type == RTCSdpType.offer)
            {
                _signalingState = RTCSignalingState.have_remote_offer;
            }
            else
            {
                _signalingState = RTCSignalingState.stable;
            }
        }

        return setResult;
    }

    /// <summary>
    ///     Close the session including the underlying RTP session and channels.
    /// </summary>
    /// <param name="reason">An optional descriptive reason for the closure.</param>
    public async Task CloseAsync(string reason)
    {
        if (!IsClosed)
        {
            Logger.LogDebug($"Peer connection closed with reason {(reason != null ? reason : "<none>")}.");

            _dtlsHandle?.Close();
            await _rtpIceChannel.CloseAsync("reason");
            IsClosed = true;
            _videoStream.IsClosed = true;

            await SetConnectionStateAsync(RTCPeerConnectionState.closed);
        }
    }

    /// <summary>
    ///     Generates the SDP for an offer that can be made to a remote peer.
    /// </summary>
    /// <remarks>
    ///     As specified in https://www.w3.org/TR/webrtc/#dom-rtcpeerconnection-createoffer.
    /// </remarks>
    /// <param name="options">
    ///     Optional. If supplied the options will be sued to apply additional
    ///     controls over the generated offer SDP.
    /// </param>
    public RTCSessionDescriptionInit CreateOffer()
    {
        var mediaStreamList = new List<MediaStream>();

        if (_videoStream.LocalTrack != null)
        {
            mediaStreamList.Add(_videoStream);
        }

        //Revert to DefaultStreamStatus
        foreach (var mediaStream in mediaStreamList)
        {
            if (mediaStream.LocalTrack != null && mediaStream.LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive)
            {
                mediaStream.LocalTrack.StreamStatus = mediaStream.LocalTrack.DefaultStreamStatus;
            }
        }

        var offerSdp = new SDP(IPAddress.Loopback)
        {
            SessionId = _localSdpSessionId
        };

        var dtlsFingerprint = _dtlsCertificateFingerprint.ToString();
        var iceCandidatesAdded = false;


        // Local function to add ICE candidates to one of the media announcements.
        void AddIceCandidates(SDPMediaAnnouncement announcement)
        {
            if (_rtpIceChannel.Candidates?.Count > 0)
            {
                //announcement.IceCandidates = new List<string>();

                // Add ICE candidates.
                foreach (var iceCandidate in _rtpIceChannel.Candidates)
                {
                    announcement.IceCandidates.Add(iceCandidate.ToString());
                }

                if (_rtpIceChannel.IceGatheringState == RTCIceGatheringState.complete)
                {
                    announcement.AddExtra($"a={SDP.END_ICE_CANDIDATES_ATTRIBUTE}");
                }
            }
        }

        // Media announcements must be in the same order in the offer and answer.
        var mediaIndex = 0;
        var audioMediaIndex = 0;
        var videoMediaIndex = 0;
        foreach (var mediaStream1 in mediaStreamList)
        {
            var mindex = 0;
            var midTag = "0";

            if (_remoteSdp == null)
            {
                mindex = mediaIndex;
                midTag = mediaIndex.ToString();
            }
            else
            {
                if (mediaStream1.LocalTrack.Kind == SDPMediaTypesEnum.audio)
                {
                    (mindex, midTag) =
                        _remoteSdp.GetIndexForMediaType(mediaStream1.LocalTrack.Kind, audioMediaIndex);
                    audioMediaIndex++;
                }
                else if (mediaStream1.LocalTrack.Kind == SDPMediaTypesEnum.video)
                {
                    (mindex, midTag) =
                        _remoteSdp.GetIndexForMediaType(mediaStream1.LocalTrack.Kind, videoMediaIndex);
                    videoMediaIndex++;
                }
            }

            mediaIndex++;

            if (mindex == SDP.MEDIA_INDEX_NOT_PRESENT)
            {
                Logger.LogWarning(
                    $"Media announcement for {mediaStream1.LocalTrack.Kind} omitted due to no reciprocal remote announcement.");
            }
            else
            {
                var announcement = new SDPMediaAnnouncement(
                    mediaStream1.LocalTrack.Kind,
                    SDP.IGNORE_RTP_PORT_NUMBER,
                    mediaStream1.LocalTrack.Capabilities);

                announcement.Transport = RtcPeerConnectionConstants.RTP_MEDIA_NON_FEEDBACK_PROFILE;
                announcement.Connection = new SDPConnectionInformation(IPAddress.Any);
                announcement.AddExtra(RtcPeerConnectionConstants.RTCP_MUX_ATTRIBUTE);
                announcement.AddExtra(RtcPeerConnectionConstants.RtcpAttribute);
                announcement.MediaStreamStatus = mediaStream1.LocalTrack.StreamStatus;
                announcement.MediaID = midTag;
                announcement.MLineIndex = mindex;

                announcement.IceUfrag = _rtpIceChannel.LocalIceUser;
                announcement.IcePwd = _rtpIceChannel.LocalIcePassword;
                announcement.IceOptions = RtcPeerConnectionConstants.ICE_OPTIONS;
                announcement.IceRole = _iceRole;
                announcement.DtlsFingerprint = dtlsFingerprint;

                if (iceCandidatesAdded == false)
                {
                    AddIceCandidates(announcement);
                    iceCandidatesAdded = true;
                }

                if (mediaStream1.LocalTrack.Ssrc != 0)
                {
                    var trackCname = mediaStream1.LocalTrack.Cname;

                    if (trackCname != null)
                    {
                        announcement.SsrcAttributes.Add(new SDPSsrcAttribute(mediaStream1.LocalTrack.Ssrc, trackCname));
                    }
                }

                offerSdp.Media.Add(announcement);
            }
        }

        // Set the Bundle attribute to indicate all media announcements are being multiplexed.
        if (offerSdp.Media?.Count > 0)
        {
            offerSdp.Group = RtcPeerConnectionConstants.BUNDLE_ATTRIBUTE;
            foreach (var ann1 in offerSdp.Media.OrderBy(x => x.MLineIndex).ThenBy(x => x.MediaID))
            {
                offerSdp.Group += $" {ann1.MediaID}";
            }

            foreach (var ann in offerSdp.Media)
            {
                ann.IceRole = _iceRole;
            }
        }

        return new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offerSdp.ToString()
        };
    }

    /// <summary>
    ///     From RFC5764:
    ///     +----------------+
    ///     | 127 < B< 192  -+--> forward to RTP
    ///     |                |
    ///     packet -->  |  19 < B< 64   -+--> forward to DTLS
    ///     |                |
    ///     |       B< 2    -+--> forward to STUN
    ///     +----------------+
    /// </summary>
    /// <param name="remoteEndPoint">The remote end point the packet was received from.</param>
    /// <param name="buffer">The data received.</param>
    private async Task OnRTPDataReceived(IPEndPoint remoteEndPoint, byte[] buffer)
    {
        //logger.LogDebug($"RTP channel received a packet from {remoteEP}, {buffer?.Length} bytes.");

        // By this point the RTP ICE channel has already processed any STUN packets which means
        // it's only necessary to separate RTP/RTCP from DTLS.
        // Because DTLS packets can be fragmented and RTP/RTCP should never be use the RTP/RTCP
        // prefix to distinguish.

        if (buffer?.Length > 0)
        {
            try
            {
                if (buffer.Length > RtpHeader.MIN_HEADER_LEN && buffer[0] >= 128 && buffer[0] <= 191)
                {
                    // RTP/RTCP packet.
                    await OnReceive(remoteEndPoint, buffer);
                }
                else
                {
                    if (_dtlsHandle != null)
                    {
                        //logger.LogDebug($"DTLS transport received {buffer.Length} bytes from {AudioDestinationEndPoint}.");
                        _dtlsHandle.WriteToRecvStream(buffer);
                    }
                    else
                    {
                        Logger.LogWarning(
                            $"DTLS packet received {buffer.Length} bytes from {remoteEndPoint} but no DTLS transport available.");
                    }
                }
            }
            catch (Exception excp)
            {
                Logger.LogError($"Exception RTCPeerConnection.OnRTPDataReceived {excp.Message}");
            }
        }
    }

    /// <summary>
    ///     Used to add remote ICE candidates to the peer connection's checklist.
    /// </summary>
    /// <param name="candidateInit">The remote ICE candidate to add.</param>
    private void AddIceCandidate(RTCIceCandidateInit candidateInit)
    {
        var candidate = new RTCIceCandidate(candidateInit);

        if (_rtpIceChannel.Component == candidate.component)
        {
            _rtpIceChannel.AddRemoteCandidate(candidate);
        }
        else
        {
            Logger.LogWarning(
                $"Remote ICE candidate not added as no available ICE session for component {candidate.component}.");
        }
    }

    /// <summary>
    ///     DtlsHandshake requires DtlsSrtpTransport to work.
    ///     DtlsSrtpTransport is similar to C++ DTLS class combined with Srtp class and can perform
    ///     Handshake as Server or Client in same call. The constructor of transport require a DtlsStrpClient
    ///     or DtlsSrtpServer to work.
    /// </summary>
    /// <param name="dtlsHandle">The DTLS transport handle to perform the handshake with.</param>
    /// <returns>True if the DTLS handshake is successful or false if not.</returns>
    private async Task<bool> DoDtlsHandshake(DtlsSrtpTransport dtlsHandle)
    {
        Logger.LogDebug("RTCPeerConnection DoDtlsHandshake started.");

        var handshakeResult = dtlsHandle.DoHandshake(out var handshakeError);

        if (!handshakeResult)
        {
            handshakeError = handshakeError ?? "unknown";
            Logger.LogWarning($"RTCPeerConnection DTLS handshake failed with error {handshakeError}.");
            await CloseAsync("dtls handshake failed");
            return false;
        }

        Logger.LogDebug(
            $"RTCPeerConnection DTLS handshake result {true}, is handshake complete {dtlsHandle.IsHandshakeComplete()}.");

        var expectedFp = _remotePeerDtlsFingerprint;
        var remoteFingerprint = DtlsUtils.Fingerprint(expectedFp.algorithm, dtlsHandle.RemoteCertificate);

        if (remoteFingerprint.value?.ToUpper() != expectedFp.value?.ToUpper())
        {
            Logger.LogWarning(
                $"RTCPeerConnection remote certificate fingerprint mismatch, expected {expectedFp}, actual {remoteFingerprint}.");
            await CloseAsync("dtls fingerprint mismatch");
            return false;
        }

        Logger.LogDebug(
            $"RTCPeerConnection remote certificate fingerprint matched expected value of {remoteFingerprint.value} for {remoteFingerprint.algorithm}.");

        SetGlobalSecurityContext(
            dtlsHandle,
            dtlsHandle.UnprotectRtcp);

        return true;
    }

    /// <summary>
    ///     Event handler for TLS alerts from the DTLS transport.
    /// </summary>
    /// <param name="alertLevel">The level of the alert: warning or critical.</param>
    /// <param name="alertType">The type of the alert.</param>
    /// <param name="alertDescription">An optional description for the alert.</param>
    private void OnDtlsAlert(AlertLevelsEnum alertLevel, AlertTypesEnum alertType, string alertDescription)
    {
        if (alertType == AlertTypesEnum.CloseNotify)
        {
            Logger.LogDebug("SCTP closing transport as a result of DTLS close notification.");
        }
        else
        {
            var alertMsg = !string.IsNullOrEmpty(alertDescription) ? $": {alertDescription}" : ".";
            Logger.LogWarning($"DTLS unexpected {alertLevel} alert {alertType}{alertMsg}");
        }
    }

    /// <summary>
    ///     If this session is using a secure context this flag MUST be set to indicate
    ///     the security delegate (SrtpProtect, SrtpUnprotect etc) have been set.
    /// </summary>
    private bool IsSecureContextReady()
    {
        if (HasVideo && !_videoStream.IsSecurityContextReady())
        {
            return false;
        }

        return true;
    }

    private void LogRemoteSDPSsrcAttributes()
    {
        var str = "Video: [ ";
        foreach (var videoRemoteSdpSsrcAttribute in _videoRemoteSdpSsrcAttributes)
        {
            str += " [";
            foreach (var attr in videoRemoteSdpSsrcAttribute)
            {
                str += attr.SSRC + " - ";
            }

            str += "] ";
        }

        str += " ]";
        Logger.LogDebug($"LogRemoteSDPSsrcAttributes: {str}");
    }

    /// <summary>
    ///     Sets the remote SDP description for this session.
    /// </summary>
    /// <param name="sessionDescription">The SDP that will be set as the remote description.</param>
    /// <returns>If successful an OK enum result. If not an enum result indicating the failure cause.</returns>
    private SetDescriptionResultEnum SetRemoteDescription(SDP sessionDescription)
    {
        try
        {
            if (sessionDescription.Media?.Count == 0)
            {
                return SetDescriptionResultEnum.NoRemoteMedia;
            }

            if (sessionDescription.Media?.Count == 1)
            {
                var remoteMediaType = sessionDescription.Media.First().Media;

                if (remoteMediaType == SDPMediaTypesEnum.video && _videoStream.LocalTrack == null)
                {
                    return SetDescriptionResultEnum.NoMatchingMediaType;
                }
            }

            // Pre-flight checks have passed. Move onto matching up the local and remote media streams.
            IPAddress connectionAddress = null;
            if (sessionDescription.Connection != null &&
                !string.IsNullOrEmpty(sessionDescription.Connection.ConnectionAddress))
            {
                connectionAddress = IPAddress.Parse(sessionDescription.Connection.ConnectionAddress);
            }

            //foreach (var announcement in sessionDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.audio || x.Media == SDPMediaTypesEnum.video))
            foreach (var announcement in sessionDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.video))
            {
                MediaStream currentMediaStream = _videoStream;

                var capabilities =
                    // As proved by Azure implementation, we need to send based on capabilities of remote track. Azure return SDP with only one possible Codec (H264 107)
                    // but we receive frames based on our LocalRemoteTracks, so its possiblet o receive a frame with ID 122, for exemple, even when remote annoucement only have 107
                    // Thats why we changed line below to keep local track capabilities untouched as we can always do it during send/receive moment
                    currentMediaStream.LocalTrack?.Capabilities;
                //Keep same order of LocalTrack priority to prevent incorrect sending format
                SDPAudioVideoMediaFormat.SortMediaCapability(capabilities, currentMediaStream.LocalTrack?.Capabilities);

                var remoteRtpEp = GetAnnouncementRTPDestination(announcement, connectionAddress);
                if (currentMediaStream.LocalTrack != null)
                {
                    if (currentMediaStream.LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive)
                    {
                        currentMediaStream.LocalTrack.StreamStatus = currentMediaStream.LocalTrack.DefaultStreamStatus;
                    }

                    if (remoteRtpEp != null)
                    {
                        if (IPAddress.Any.Equals(remoteRtpEp.Address) ||
                            IPAddress.IPv6Any.Equals(remoteRtpEp.Address))
                        {
                            // A connection address of 0.0.0.0 or [::], which is unreachable, means the media is inactive, except
                            // if a special port number is used (defined as "9") which indicates that the media announcement is not
                            // responsible for setting the remote end point for the audio stream. Instead it's most likely being set
                            // using ICE.
                            if (remoteRtpEp.Port != SDP.IGNORE_RTP_PORT_NUMBER)
                            {
                                currentMediaStream.LocalTrack.StreamStatus = MediaStreamStatusEnum.Inactive;
                            }
                        }
                        else if (remoteRtpEp.Port == 0)
                        {
                            currentMediaStream.LocalTrack.StreamStatus = MediaStreamStatusEnum.Inactive;
                        }
                    }
                }

                if (currentMediaStream.MediaType == SDPMediaTypesEnum.audio)
                {
                    if (capabilities?.Count(x => x.Name().ToLower() != SDP.TELEPHONE_EVENT_ATTRIBUTE) == 0)
                    {
                        return SetDescriptionResultEnum.AudioIncompatible;
                    }
                }
                else if (capabilities?.Count == 0 || (currentMediaStream.LocalTrack == null &&
                                                      currentMediaStream.LocalTrack != null &&
                                                      currentMediaStream.LocalTrack.Capabilities?.Count == 0))
                {
                    return SetDescriptionResultEnum.VideoIncompatible;
                }
            }

            _remoteSdp = sessionDescription;

            return SetDescriptionResultEnum.OK;
        }
        catch (Exception excp)
        {
            Logger.LogError($"Exception in RTPSession SetRemoteDescription. {excp.Message}.");
            return SetDescriptionResultEnum.Error;
        }
    }

    /// <summary>
    ///     Gets the RTP end point for an SDP media announcement from the remote peer.
    /// </summary>
    /// <param name="announcement">The media announcement to get the connection address for.</param>
    /// <param name="connectionAddress">The remote SDP session level connection address. Will be null if not available.</param>
    /// <returns>An IP end point for an SDP media announcement from the remote peer.</returns>
    private IPEndPoint GetAnnouncementRTPDestination(SDPMediaAnnouncement announcement, IPAddress connectionAddress)
    {
        var kind = announcement.Media;
        IPEndPoint rtpEndPoint = null;

        var remoteAddr = announcement.Connection != null
            ? IPAddress.Parse(announcement.Connection.ConnectionAddress)
            : connectionAddress;

        if (remoteAddr != null)
        {
            if (announcement.Port < IPEndPoint.MinPort || announcement.Port > IPEndPoint.MaxPort)
            {
                Logger.LogWarning($"Remote {kind} announcement contained an invalid port number {announcement.Port}.");

                // Set the remote port number to "9" which means ignore and wait for it be set some other way
                // such as when a remote RTP packet or arrives or ICE negotiation completes.
                rtpEndPoint = new IPEndPoint(remoteAddr, SDP.IGNORE_RTP_PORT_NUMBER);
            }
            else
            {
                rtpEndPoint = new IPEndPoint(remoteAddr, announcement.Port);
            }
        }

        return rtpEndPoint;
    }

    private void SetGlobalDestination(IPEndPoint rtpEndPoint)
    {
        _videoStream.SetDestination(rtpEndPoint);
    }

    private void SetGlobalSecurityContext(
        DtlsSrtpTransport rtpTransport,
        ProtectRtpPacket unprotectRtcp)
    {
        _videoStream.SetSecurityContext(rtpTransport, unprotectRtcp);
    }

    public async Task SendVideoAsync(RtpPacket packet)
    {
        await _videoStream.SendRtpRawFromPacketAsync(packet);
    }

    private async Task OnReceive(IPEndPoint remoteEndPoint, byte[] buffer)
    {
        if (remoteEndPoint.Address.IsIPv4MappedToIPv6)
        {
            // Required for matching existing RTP end points (typically set from SDP) and
            // whether or not the destination end point should be switched.
            remoteEndPoint.Address = remoteEndPoint.Address.MapToIPv4();
        }

        // Quick sanity check on whether this is not an RTP or RTCP packet.
        if (buffer?.Length > RtpHeader.MIN_HEADER_LEN && buffer[0] >= 128 && buffer[0] <= 191)
        {
            if (!IsSecureContextReady())
            {
                Logger.LogWarning("RTP or RTCP packet received before secure context ready.");
            }
            else
            {
                if (Enum.IsDefined(typeof(RtcpReportTypes), buffer[1]))
                {
                    // Only call OnReceiveRTCPPacket for supported RTCPCompoundPacket types
                    if (buffer[1] == (byte)RtcpReportTypes.SR ||
                        buffer[1] == (byte)RtcpReportTypes.RR ||
                        buffer[1] == (byte)RtcpReportTypes.SDES ||
                        buffer[1] == (byte)RtcpReportTypes.BYE ||
                        buffer[1] == (byte)RtcpReportTypes.PSFB ||
                        buffer[1] == (byte)RtcpReportTypes.RTPFB)
                    {
                        await OnReceiveRTCPPacket(buffer);
                    }
                }
            }
        }
    }

    private async Task OnReceiveRTCPPacket(byte[] buffer)
    {
        var secureContext = _videoStream.SecurityContext;
        if (secureContext != null)
        {
            var res = secureContext.UnprotectRtcpPacket(buffer, buffer.Length, out var outBufLen);
            if (res != 0)
            {
                Logger.LogWarning($"SRTCP unprotect failed for {_videoStream.MediaType} track, result {res}.");
                return;
            }

            buffer = buffer.Take(outBufLen).ToArray();
        }

        var rtcpPkt = new RtcpCompoundPacket(buffer);
        if (rtcpPkt.Bye != null)
        {
            Logger.LogDebug($"RTCP BYE received for SSRC {rtcpPkt.Bye.Ssrc}, reason {rtcpPkt.Bye.Reason}.");

            // In some cases, such as a SIP re-INVITE, it's possible the RTP session
            // will keep going with a new remote SSRC.
            // We close peer connection only if there is no more local/remote tracks on the primary stream
            if (_videoStream.LocalTrack == null)
            {
                await CloseAsync(rtcpPkt.Bye.Reason);
            }
        }
    }

    private async Task SetConnectionStateAsync(RTCPeerConnectionState value)
    {
        if (_connectionState != value)
        {
            _connectionState = value;
            await _peerConnectionChangeHandler(this, _connectionState);
        }
    }

}