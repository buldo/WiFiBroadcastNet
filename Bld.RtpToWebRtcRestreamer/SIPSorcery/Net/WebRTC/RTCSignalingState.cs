using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
/// Signalling states for a WebRTC peer connection.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#dom-rtcsignalingstate.
/// </remarks>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum RTCSignalingState
{
    stable,
    have_local_offer,
    have_remote_offer,
    have_local_pranswer,
    have_remote_pranswer,
    closed
}