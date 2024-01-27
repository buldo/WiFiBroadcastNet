namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// The transport protocol types for an ICE candidate.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#rtciceprotocol-enum.
/// </remarks>
internal enum RTCIceProtocol
{
    udp,
    tcp
}