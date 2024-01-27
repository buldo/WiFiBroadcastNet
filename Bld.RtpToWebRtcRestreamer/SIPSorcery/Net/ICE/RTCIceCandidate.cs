//-----------------------------------------------------------------------------
// Filename: RTCIceCandidate.cs
//
// Description: Represents a candidate used in the Interactive Connectivity
// Establishment (ICE) negotiation to set up a usable network connection
// between two peers as per RFC8445 https://tools.ietf.org/html/rfc8445
// (previously implemented for RFC5245 https://tools.ietf.org/html/rfc5245).
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 26 Feb 2016	Aaron Clauson	Created, Hobart, Australia.
// 15 Mar 2020  Aaron Clauson   Updated for RFC8445.
// 17 Mar 2020  Aaron Clauson   Renamed from IceCandidate to RTCIceCandidate.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using System.Text;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

internal class RTCIceCandidate
{
    private const string TCP_TYPE_KEY = "tcpType";
    private const string REMOTE_ADDRESS_KEY = "raddr";
    private const string REMOTE_PORT_KEY = "rport";

    public ushort SDPMLineIndex { get; }

    /// <summary>
    /// Composed of 1 to 32 chars. It is an
    /// identifier that is equivalent for two candidates that are of the
    /// same type, share the same base, and come from the same STUN
    /// server.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc8445#section-5.1.1.3.
    /// </remarks>
    private string foundation { get; set; }

    /// <summary>
    ///  Is a positive integer between 1 and 256 (inclusive)
    /// that identifies the specific component of the data stream for
    /// which this is a candidate.
    /// </summary>
    public RTCIceComponent component { get; private set; } = RTCIceComponent.rtp;

    /// <summary>
    /// A positive integer between 1 and (2**31 - 1) inclusive.
    /// This priority will be used by ICE to determine the order of the
    /// connectivity checks and the relative preference for candidates.
    /// Higher-priority values give more priority over lower values.
    /// </summary>
    /// <remarks>
    /// See specification at https://tools.ietf.org/html/rfc8445#section-5.1.2.
    /// </remarks>
    public uint priority { get; private set; }

    /// <summary>
    /// The address or hostname for the candidate.
    /// </summary>
    public string address { get; private set; }

    /// <summary>
    /// The transport protocol for the candidate, supported options are UDP and TCP.
    /// </summary>
    public RTCIceProtocol protocol { get; private set; }

    /// <summary>
    /// The local port the candidate is listening on.
    /// </summary>
    public ushort port { get; private set; }

    /// <summary>
    /// The type of ICE candidate, host, srflx etc.
    /// </summary>
    public RTCIceCandidateType type { get; private set; }

    /// <summary>
    /// For TCP candidates the role they are fulfilling (client, server or both).
    /// </summary>
    private RTCIceTcpCandidateType tcpType { get; }

    private string relatedAddress { get; set; }

    private ushort relatedPort { get; set; }

    /// <summary>
    /// This is the end point to use for a remote candidate. The address supplied for an ICE
    /// candidate could be a hostname or IP address. This field will be set before the candidate
    /// is used.
    /// </summary>
    public IPEndPoint DestinationEndPoint { get; private set; }

    private RTCIceCandidate()
    { }

    public RTCIceCandidate(RTCIceCandidateInit init)
    {
        SDPMLineIndex = init.sdpMLineIndex;

        if (!string.IsNullOrEmpty(init.candidate))
        {
            var iceCandidate = Parse(init.candidate);
            foundation = iceCandidate.foundation;
            priority = iceCandidate.priority;
            component = iceCandidate.component;
            address = iceCandidate.address;
            port = iceCandidate.port;
            type = iceCandidate.type;
            tcpType = iceCandidate.tcpType;
            relatedAddress = iceCandidate.relatedAddress;
            relatedPort = iceCandidate.relatedPort;
        }
    }

    public void SetAddressProperties(
        RTCIceProtocol cProtocol,
        IPAddress cAddress,
        ushort cPort,
        RTCIceCandidateType cType,
        IPAddress cRelatedAddress,
        ushort cRelatedPort)
    {
        protocol = cProtocol;
        address = cAddress.ToString();
        port = cPort;
        type = cType;
        relatedAddress = cRelatedAddress?.ToString();
        relatedPort = cRelatedPort;

        foundation = GetFoundation();
        priority = GetPriority();
    }

    private static RTCIceCandidate Parse(string candidateLine)
    {
        if (string.IsNullOrEmpty(candidateLine))
        {
            throw new ArgumentNullException(nameof(candidateLine),"Cant parse ICE candidate from empty string.");
        }

        candidateLine = candidateLine.Replace("candidate:", "");

        var candidate = new RTCIceCandidate();

        var candidateFields = candidateLine.Trim().Split(' ');

        candidate.foundation = candidateFields[0];

        if (Enum.TryParse<RTCIceComponent>(candidateFields[1], out var candidateComponent))
        {
            candidate.component = candidateComponent;
        }

        if (Enum.TryParse<RTCIceProtocol>(candidateFields[2], out var candidateProtocol))
        {
            candidate.protocol = candidateProtocol;
        }

        if (uint.TryParse(candidateFields[3], out var candidatePriority))
        {
            candidate.priority = candidatePriority;
        }

        candidate.address = candidateFields[4];
        candidate.port = Convert.ToUInt16(candidateFields[5]);

        if (Enum.TryParse<RTCIceCandidateType>(candidateFields[7], out var candidateType))
        {
            candidate.type = candidateType;
        }

        // TCP Candidates require extra steps to be parsed
        // {"candidate":"candidate:4 1 TCP 2105458943 10.0.1.16 9 typ host tcptype active","sdpMid":"sdparta_0","sdpMLineIndex":0}
        var parseIndex = 8;
        if (candidate.protocol == RTCIceProtocol.tcp)
        {
            if (candidateFields.Length > parseIndex && candidateFields[parseIndex] == TCP_TYPE_KEY)
            {
                candidate.relatedAddress = candidateFields[parseIndex + 1];
            }
            parseIndex += 2;
        }

        if (candidateFields.Length > parseIndex && candidateFields[parseIndex] == REMOTE_ADDRESS_KEY)
        {
            candidate.relatedAddress = candidateFields[parseIndex+1];
        }

        if (candidateFields.Length > parseIndex+2 && candidateFields[parseIndex+2] == REMOTE_PORT_KEY)
        {
            candidate.relatedPort = Convert.ToUInt16(candidateFields[parseIndex+3]);
        }

        return candidate;
    }

    /// <summary>
    /// Serialises an ICE candidate to a string that's suitable for inclusion in an SDP session
    /// description payload.
    /// </summary>
    /// <remarks>
    /// The specification regarding how an ICE candidate should be serialised in SDP is at
    /// https://tools.ietf.org/html/draft-ietf-mmusic-ice-sip-sdp-39#section-5.1.
    /// </remarks>
    /// <returns>A string representing the ICE candidate suitable for inclusion in an SDP session
    /// description.</returns>
    public override string ToString()
    {
        if (type == RTCIceCandidateType.host || type == RTCIceCandidateType.prflx)
        {
            string candidateStr;
            if (protocol == RTCIceProtocol.tcp)
            {
                candidateStr =
                    $"{foundation} {component.GetHashCode()} tcp {priority} {address} {port} typ {type} tcptype {tcpType}";
            }
            else
            {
                candidateStr = $"{foundation} {component.GetHashCode()} udp {priority} {address} {port} typ {type}";
            }

            return candidateStr;
        }
        else
        {
            var relAddr = relatedAddress;

            if (string.IsNullOrWhiteSpace(relAddr))
            {
                relAddr = IPAddress.Any.ToString();
            }

            string candidateStr;
            if (protocol == RTCIceProtocol.tcp)
            {
                candidateStr =
                    $"{foundation} {component.GetHashCode()} udp {priority} {address} {port} typ {type} tcptype {tcpType} raddr {relAddr} rport {relatedPort}";
            }
            else
            {
                candidateStr =
                    $"{foundation} {component.GetHashCode()} udp {priority} {address} {port} typ {type} raddr {relAddr} rport {relatedPort}";
            }

            return candidateStr;
        }
    }

    /// <summary>
    /// Sets the remote end point for a remote candidate.
    /// </summary>
    /// <param name="destinationEP">The resolved end point for the candidate.</param>
    public void SetDestinationEndPoint(IPEndPoint destinationEP)
    {
        DestinationEndPoint = destinationEP;
    }

    private string GetFoundation()
    {
        var serverProtocol = "udp";
        var builder = new StringBuilder();
        builder = builder.Append(type).Append(address).Append(protocol).Append(serverProtocol);
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        return UpdateCrc32(0, bytes).ToString();

        /*int addressVal = !String.IsNullOrEmpty(address) ? Crypto.GetSHAHash(address).Sum(x => (byte)x) : 0;
        int svrVal = (type == RTCIceCandidateType.relay || type == RTCIceCandidateType.srflx) ?
            Crypto.GetSHAHash(IceServerConsts != null ? IceServerConsts._uri.ToString() : "").Sum(x => (byte)x) : 0;
        return (type.GetHashCode() + addressVal + svrVal + protocol.GetHashCode()).ToString();*/
    }

    private uint GetPriority()
    {
        uint localPreference = 0;
        IPAddress addr;

        //Calculate our LocalPreference Priority
        if (IPAddress.TryParse(address, out addr))
        {
            var addrPref = IPAddressHelper.IPAddressPrecedence(addr);

            // relay_preference in original code was sorted with params:
            // UDP == 2
            // TCP == 1
            // TLS == 0
            var relayPreference = protocol == RTCIceProtocol.udp ? 2u : 1u;

            // TODO: Original implementation consider network adapter preference as strength of wifi
            // We will ignore it as its seems to not be a trivial implementation for use in net-standard 2.0
            uint networkAdapterPreference = 0;

            localPreference = ((networkAdapterPreference << 8) | addrPref) + relayPreference;
        }

        // RTC 5245 Define priority for RTCIceCandidateType
        // https://datatracker.ietf.org/doc/html/rfc5245
        uint typePreference = 0;
        switch (type)
        {
            case RTCIceCandidateType.host:
                typePreference = 126;
                break;
            case RTCIceCandidateType.prflx:
                typePreference = 110;
                break;
            case RTCIceCandidateType.srflx:
                typePreference = 100;
                break;
        }

        //Use formula found in RFC 5245 to define candidate priority
        return (uint)((typePreference << 24) | (localPreference << 8) | (256u - component.GetHashCode()));
    }

    /// <summary>
    /// Checks the candidate to identify whether it is equivalent to the specified
    /// protocol and IP end point. Primary use case is to check whether a candidate
    /// is a match for a remote end point that a message has been received from.
    /// </summary>
    /// <param name="epProtocol">The protocol to check equivalence for.</param>
    /// <param name="ep">The IP end point to check equivalence for.</param>
    /// <returns>True if the candidate is deemed equivalent or false if not.</returns>
    public bool IsEquivalentEndPoint(RTCIceProtocol epProtocol, IPEndPoint ep)
    {
        if (protocol == epProtocol && DestinationEndPoint != null &&
            ep.Address.Equals(DestinationEndPoint.Address) && DestinationEndPoint.Port == ep.Port)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a short description for the candidate that's helpful for log messages.
    /// </summary>
    /// <returns>A short string describing the key properties of the candidate.</returns>
    public string ToShortString()
    {
        var epDescription = $"{address}:{port}";
        if (IPAddress.TryParse(address, out var ipAddress))
        {
            var ep = new IPEndPoint(ipAddress, port);
            epDescription = ep.ToString();
        }

        return $"{protocol}:{epDescription} ({type})";
    }

    //CRC32 implementation from C++ to calculate foundation
    private const uint kCrc32Polynomial = 0xEDB88320;
    private static uint[] LoadCrc32Table()
    {
        var kCrc32Table = new uint[256];
        for (uint i = 0; i < kCrc32Table.Length; ++i)
        {
            var c = i;
            for (var j = 0; j < 8; ++j)
            {
                if ((c & 1) != 0)
                {
                    c = kCrc32Polynomial ^ (c >> 1);
                }
                else
                {
                    c >>= 1;
                }
            }
            kCrc32Table[i] = c;
        }
        return kCrc32Table;
    }

    private uint UpdateCrc32(uint start, byte[] buf)
    {
        var kCrc32Table = LoadCrc32Table();

        long c = (int)(start ^ 0xFFFFFFFF);
        var u = buf;
        for (var i = 0; i < buf.Length; ++i)
        {
            c = kCrc32Table[(c ^ u[i]) & 0xFF] ^ (c >> 8);
        }
        return (uint)(c ^ 0xFFFFFFFF);
    }

}