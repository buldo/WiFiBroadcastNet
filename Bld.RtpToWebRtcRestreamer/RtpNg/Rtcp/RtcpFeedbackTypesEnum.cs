using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// The different types of Feedback Message Types. (RFC4585)
/// https://tools.ietf.org/html/rfc4585#page-35
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum RtcpFeedbackTypesEnum
{
    unassigned = 0,     // Unassigned
    NACK = 1,   		// Generic NACK	Generic negative acknowledgment		    [RFC4585]
    // reserved = 2		// Reserved												[RFC5104]
    TMMBR = 3, 			// Temporary Maximum Media Stream Bit Rate Request		[RFC5104]
    TMMBN = 4,			// Temporary Maximum Media Stream Bit Rate Notification	[RFC5104]
    RTCP_SR_REQ = 5, 	// RTCP Rapid Resynchronisation Request					[RFC6051]
    RAMS = 6,			// Rapid Acquisition of Multicast Sessions				[RFC6285]
    TLLEI = 7, 			// Transport-Layer Third-Party Loss Early Indication	[RFC6642]
    RTCP_ECN_FB = 8,	// RTCP ECN Feedback 									[RFC6679]
    PAUSE_RESUME = 9,   // Media Pause/Resume									[RFC7728]

    DBI = 10			// Delay Budget Information (DBI) [3GPP TS 26.114 v16.3.0][Ozgur_Oyman]
    // 11-30			// Unassigned
    // Extension = 31	// Reserved for future extensions						[RFC4585]
}