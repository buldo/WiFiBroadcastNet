using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN;

/// <summary>
/// The class is interpreted from the message type. It does not get explicitly
/// set in the STUN header.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum STUNClassTypesEnum
{
    Request = 0,
    Indication = 1,
    SuccessResponse = 2,
    ErrorResponse = 3
}