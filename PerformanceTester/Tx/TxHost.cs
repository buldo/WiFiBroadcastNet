namespace PerformanceTester.Tx;

public class TxHost
{
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5002);
        });
        builder.Services.AddGrpc();
        var app = builder.Build();
        app.UseRouting();
        app.MapGrpcService<TxService>();
        await app.RunAsync();
    }
}