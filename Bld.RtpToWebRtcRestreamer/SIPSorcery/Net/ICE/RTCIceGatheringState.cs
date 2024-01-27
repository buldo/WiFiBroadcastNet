namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// The gathering states an ICE session transitions through.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#dom-rtcicegatheringstate.
/// </remarks>
internal enum RTCIceGatheringState
{
    @new,
    gathering,
    complete
}