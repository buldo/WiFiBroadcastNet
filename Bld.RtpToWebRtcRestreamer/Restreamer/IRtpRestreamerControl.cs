namespace Bld.RtpToWebRtcRestreamer.Restreamer;

public interface IRtpRestreamerControl
{
    Task<(Guid PeerId, string Sdp)> AppendClient();

    Task ProcessClientAnswerAsync(Guid peerId, string sdpString);

    Task StopAsync();
}