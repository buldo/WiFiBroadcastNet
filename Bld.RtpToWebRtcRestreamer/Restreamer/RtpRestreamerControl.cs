namespace Bld.RtpToWebRtcRestreamer.Restreamer;

internal class RtpRestreamerControl : IRtpRestreamerControl
{
    private readonly WebRtcHostedService _service;

    public RtpRestreamerControl(WebRtcHostedService service)
    {
        _service = service;
    }

    public async Task<(Guid PeerId, string Sdp)> AppendClient()
    {
        return await _service.AppendClient();
    }

    public async Task ProcessClientAnswerAsync(Guid peerId, string sdpString)
    {
        await _service.ProcessClientAnswerAsync(peerId, sdpString);
    }

    public async Task StopAsync()
    {
        await _service.StopStreamerAsync();
    }
}