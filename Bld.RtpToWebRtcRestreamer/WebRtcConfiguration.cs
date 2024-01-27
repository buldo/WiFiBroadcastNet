#nullable enable
using System.Net;

namespace Bld.RtpToWebRtcRestreamer;

internal class WebRtcConfiguration
{
    public required IPEndPoint RtpListenEndpoint { get; init; }
}