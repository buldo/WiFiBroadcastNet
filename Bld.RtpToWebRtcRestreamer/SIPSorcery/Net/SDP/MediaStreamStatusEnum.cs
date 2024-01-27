namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

/// <summary>
/// The types of status that a media stream can have. Note that the stream status can
/// be defined with an attribute at session level or at media element level. If no 
/// attribute is defined then the default is "sendrecv".
/// Note that this status applies to RTP streams only. If there is an RTCP stream 
/// associated with the RTP it should carry on as normal.
/// See https://tools.ietf.org/html/rfc4566#section-6
/// </summary>
public enum MediaStreamStatusEnum
{
    SendRecv = 0,   // The offerer is prepared to send and receive packets.
    SendOnly = 1,   // The offerer only wishes to send RTP packets. They will probably ignore any received.
    RecvOnly = 2,   // The offerer only wishes to receive RTP packets. They will not send.
    Inactive = 3    // The offerer is not ready to send or receive packets.
}