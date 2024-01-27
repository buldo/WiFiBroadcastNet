using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// The different types of Feedback Message Types. (RFC4585)
/// https://tools.ietf.org/html/rfc4585#page-35
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum PSFBFeedbackTypesEnum : byte
{
    unassigned = 0,     // Unassigned
    PLI = 1,            // Picture Loss Indication                              [RFC4585]
    SLI = 2,            // Slice Loss Indication   [RFC4585]
    RPSI = 3,           // Reference Picture Selection Indication  [RFC4585]
    FIR = 4,            // Full Intra Request Command  [RFC5104]
    TSTR = 5,           // Temporal-Spatial Trade-off Request  [RFC5104]
    TSTN = 6,           // Temporal-Spatial Trade-off Notification [RFC5104]
    VBCM = 7,           // Video Back Channel Message  [RFC5104]
    PSLEI = 8,          // Payload-Specific Third-Party Loss Early Indication  [RFC6642]
    ROI = 9,            // Video region-of-interest (ROI)	[3GPP TS 26.114 v16.3.0][Ozgur_Oyman]
    LRR = 10,           // Layer Refresh Request Command   [RFC-ietf-avtext-lrr-07]
    // 11-14		    // Unassigned
    AFB = 15            // Application Layer Feedback  [RFC4585]
    // 16-30		    // Unassigned
    // Extension = 31   //Extension   Reserved for future extensions  [RFC4585]
}