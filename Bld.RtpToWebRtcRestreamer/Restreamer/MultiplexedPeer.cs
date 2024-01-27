using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;

namespace Bld.RtpToWebRtcRestreamer.Restreamer;

internal class MultiplexedPeer
{
    private bool _isStarted;

    public MultiplexedPeer(RtcPeerConnection peer)
    {
        Peer = peer;
    }

    public RtcPeerConnection Peer { get;}

    public async Task SendVideoAsync(RtpPacket packet)
    {
        if (!_isStarted)
        {
            return;
        }

        await Peer.SendVideoAsync(packet);
    }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
    }

    public async Task ClosePeerAsync()
    {
        if (!_isStarted)
        {
            return;
        }
        _isStarted = false;

        await Peer.CloseAsync("");
    }
}