namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
/// Initialiser for the RTCSessionDescription instance.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#rtcsessiondescription-class.
/// </remarks>
internal class RTCSessionDescriptionInit
{
    /// <summary>
    /// The type of the Session Description.
    /// </summary>
    public RTCSdpType type { get; set; }

    /// <summary>
    /// A string representation of the Session Description.
    /// </summary>
    public string sdp { get; set; }
}