namespace PerformanceTester.Tx;

public class TxHost
{
    private readonly string _deviceName;

    public TxHost(string deviceName)
    {
        _deviceName = deviceName;
    }
    
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5002);
        });
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(new TxHostConfig { DeviceName = _deviceName });
        var app = builder.Build();
        app.UseRouting();
        app.MapGrpcService<TxService>();
        await app.RunAsync();
    }
}