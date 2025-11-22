using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        builder.Services.AddHostedService<WfbHost>();

        var host = builder.Build();
        host.Run();
    }
}