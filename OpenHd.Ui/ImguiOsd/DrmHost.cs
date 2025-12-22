using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenHd.Ui.ImguiOsd;

internal class DrmHost : UiHostBase
{
    public DrmHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        ILogger<DrmHost> logger)
        : base(h264Stream, logger)
    {

    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
        throw new NotImplementedException();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
        throw new NotImplementedException();
    }

    protected override void ProcessNalu(ReadOnlySpan<byte> nalu)
    {
        throw new NotImplementedException();
    }
}