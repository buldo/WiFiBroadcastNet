namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// Represents an ICE candidate and associated properties that link it to the SDP.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#dom-rtcicecandidateinit.
/// </remarks>
internal class RTCIceCandidateInit
{
    public string candidate { get; init; }
    public ushort sdpMLineIndex { get; init; }
}