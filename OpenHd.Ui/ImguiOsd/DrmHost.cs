namespace OpenHd.Ui.ImguiOsd;

internal class DrmHost : UiHostBase
{
    public DrmHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILogger<DrmHost> logger)
        : base(h264Stream, decodersFactory, logger)
    {

    }

    protected override void Start()
    {
        return;
        throw new NotImplementedException();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
        throw new NotImplementedException();
    }
}