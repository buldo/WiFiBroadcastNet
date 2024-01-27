using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum SDPMediaTypesEnum
{
    invalid = 0,
    audio = 1,
    video = 2,
    application = 3,
    data = 4,
    control = 5,
    image = 6,
    message = 7
}