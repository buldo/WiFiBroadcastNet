using OpenHd.Ui.Configuration;
using OpenHd.Ui.ImguiOsd;
using OpenHd.Ui.TestRx;
using SharpVideo.Decoding.V4l2.Discovery;

namespace OpenHd.Ui;

internal class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging
            .AddConsole();

        builder.Services.Configure<RemoteOpenHdConfiguration>(
            builder.Configuration.GetSection(RemoteOpenHdConfiguration.Key));

        //builder.Services.AddHostedService<WfbHost>();

        builder.Services.AddKeyedSingleton<InMemoryPipeStreamAccessor>("h264-stream");
        builder.Services.AddSingleton<V4l2H264DecoderProvider>();
        builder.Services.AddSingleton<DecodersFactory>();

        builder.Services.AddSingleton<UiHostFactory>();
        builder.Services.AddHostedService<IUiHost>(CreateUiHost);

        builder.Services.AddHostedService<RemoteOpenHdConnector>();

        var host = builder.Build();

        host.Run();
    }

    private static IUiHost CreateUiHost(IServiceProvider sp)
    {
        var factory = sp.GetRequiredService<UiHostFactory>();
        return factory.CreateHost();
    }
}