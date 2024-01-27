using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;
internal static class RtcPeerConnectionConstants
{

    // SDP constants.
    //private new const string RTP_MEDIA_PROFILE = "RTP/SAVP";
    public const string RTP_MEDIA_NON_FEEDBACK_PROFILE = "UDP/TLS/RTP/SAVP";
    public const string RTP_MEDIA_DATA_CHANNEL_DTLS_PROFILE = "DTLS/SCTP"; // Legacy.
    public const string RTP_MEDIA_DATA_CHANNEL_UDP_DTLS_PROFILE = "UDP/DTLS/SCTP";
    public const string SDP_DATA_CHANNEL_FORMAT_ID = "webrtc-datachannel";

    public const string
        RTCP_MUX_ATTRIBUTE = "a=rtcp-mux"; // Indicates the media announcement is using multiplexed RTCP.

    public const string BUNDLE_ATTRIBUTE = "BUNDLE";
    public const string ICE_OPTIONS = "ice2,trickle"; // Supported ICE options.

    /// <summary>
    ///     From libsrtp: SRTP_MAX_TRAILER_LEN is the maximum length of the SRTP trailer
    ///     (authentication tag and MKI) supported by libSRTP.This value is
    ///     the maximum number of octets that will be added to an RTP packet by
    ///     srtp_protect().
    ///     srtp_protect():
    ///     @warning This function assumes that it can write SRTP_MAX_TRAILER_LEN
    ///     into the location in memory immediately following the RTP packet.
    ///     Callers MUST ensure that this much writeable memory is available in
    ///     the buffer that holds the RTP packet.
    ///     srtp_protect_rtcp():
    ///     @warning This function assumes that it can write SRTP_MAX_TRAILER_LEN+4
    ///     to the location in memory immediately following the RTCP packet.
    ///     Callers MUST ensure that this much writeable memory is available in
    ///     the buffer that holds the RTCP packet.
    /// </summary>
    public const int SRTP_MAX_PREFIX_LENGTH = 148;

    public static readonly string RtcpAttribute = $"a=rtcp:{SDP.IGNORE_RTP_PORT_NUMBER} IN IP4 0.0.0.0";
}
