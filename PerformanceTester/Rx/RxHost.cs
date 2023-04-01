namespace PerformanceTester.Rx;

public class RxHost
{
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5001);
        });
        builder.Services.AddGrpc();
        var app = builder.Build();
        await app.RunAsync();
    }
}