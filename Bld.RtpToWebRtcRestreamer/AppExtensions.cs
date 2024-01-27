using System.Net;
using Bld.RtpToWebRtcRestreamer.Restreamer;
using Microsoft.Extensions.DependencyInjection;

namespace Bld.RtpToWebRtcRestreamer;

public static class AppExtensions
{
    public static void AddRtpRestreamer(
        this IServiceCollection services,
        IPEndPoint rtpListenEndpoint)
    {
        var config = new WebRtcConfiguration
        {
            RtpListenEndpoint = rtpListenEndpoint
        };

        services.AddSingleton(config);
        services.AddSingleton<WebRtcHostedService>();
        services.AddHostedService(provider => provider.GetRequiredService<WebRtcHostedService>());
        services.AddSingleton<IRtpRestreamerControl, RtpRestreamerControl>();
    }
}