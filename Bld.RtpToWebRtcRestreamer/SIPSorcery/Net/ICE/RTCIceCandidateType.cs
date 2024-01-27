namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// The RTCIceCandidateType represents the type of the ICE candidate.
/// </summary>
/// <remarks>
/// As defined in https://www.w3.org/TR/webrtc/#rtcicecandidatetype-enum.
/// </remarks>
internal enum RTCIceCandidateType
{
    /// <summary>
    /// A host candidate, locally gathered.
    /// </summary>
    host,

    /// <summary>
    /// A peer reflexive candidate, obtained as a result of a connectivity check 
    /// (e.g. STUN request from a previously unknown address).
    /// </summary>
    prflx,

    /// <summary>
    /// A server reflexive candidate, obtained from STUN and/or TURN (non-relay TURN).
    /// </summary>
    srflx,

    /// <summary>
    /// A relay candidate, TURN (relay).
    /// </summary>
    relay
}