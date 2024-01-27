using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// The RTCIceTcpCandidateType represents the type of the ICE TCP candidate.
/// </summary>
/// <remarks>
/// As defined in https://www.w3.org/TR/webrtc/#rtcicetcpcandidatetype-enum.
/// </remarks>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum RTCIceTcpCandidateType
{
    /// <summary>
    /// An active TCP candidate is one for which the transport will attempt to 
    /// open an outbound connection but will not receive incoming connection requests.
    /// </summary>
    active,

    /// <summary>
    /// A passive TCP candidate is one for which the transport will receive incoming 
    /// connection attempts but not attempt a connection.
    /// </summary>
    passive,

    /// <summary>
    /// An so candidate is one for which the transport will attempt to open a connection 
    /// simultaneously with its peer.
    /// </summary>
    so
}