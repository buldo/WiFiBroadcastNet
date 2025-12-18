using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenHd.Ui.ImguiOsd;

namespace OpenHd.Ui;

internal class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddFilter("Rtl8812auNet.*", LogLevel.Warning)
            .AddConsole();

        //builder.Services.AddHostedService<WfbHost>();

        builder.Services.AddKeyedSingleton<InMemoryPipeStreamAccessor>("h264-stream");

        builder.Services.AddSingleton<UiHostFactory>();
        builder.Services.AddHostedService<UiHostBase>(CreateUiHost);

        var host = builder.Build();

        host.Run();
    }

    private static UiHostBase CreateUiHost(IServiceProvider sp)
    {
        var factory = sp.GetRequiredService<UiHostFactory>();
        return factory.CreateHost();
    }
}