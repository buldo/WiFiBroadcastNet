using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// The different types of RTCP packets as defined in RFC3550.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum RtcpReportTypes : byte
{
    SR = 200,     // Send Report.
    RR = 201,     // Receiver Report.
    SDES = 202,   // Session Description.
    BYE = 203,    // Goodbye.
    APP = 204,    // Application-defined.

    // From RFC5760: https://tools.ietf.org/html/rfc5760
    // "RTP Control Protocol (RTCP) Extensions for
    // Single-Source Multicast Sessions with Unicast Feedback"

    RTPFB = 205,    // Generic RTP feedback
    PSFB = 206,     // Payload-specific feedback
    XR = 207 // RTCP Extension
}