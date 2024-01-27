using System.Net;
using System.Net.Sockets;
using Bld.RtpToWebRtcRestreamer.RtpNg.Networking;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.Restreamer;

internal class RtpRestreamer
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RtpRestreamer> _logger;
    private readonly PooledUdpSource _receiver;
    private readonly StreamMultiplexer _streamMultiplexer;

    public RtpRestreamer(
        IPEndPoint rtpListenEndpoint,
        ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<RtpRestreamer>();

        _receiver = new PooledUdpSource(rtpListenEndpoint, _loggerFactory.CreateLogger<PooledUdpSource>());
        _streamMultiplexer = new StreamMultiplexer(_loggerFactory.CreateLogger<StreamMultiplexer>());
    }

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted = true;

        _receiver.Start(RtpProcessorAsync);
    }

    public async Task StopAsync()
    {
        if (!IsStarted)
        {
            return;
        }

        IsStarted = false;
        await _receiver.StopAsync();
        foreach (var peerConnection in _streamMultiplexer.GetAllPeers())
        {
            await _streamMultiplexer.ClosePeerAsync(peerConnection.Peer.Id);
        }
    }

    public async Task<(Guid PeerId, string Sdp)> AppendClient()
    {
        var videoTrack = new MediaStreamTrack(
            new VideoFormat(VideoCodecsEnum.H264, 96),
            MediaStreamStatusEnum.SendOnly);
        var socket = new UdpSocket(new UdpClient(new IPEndPoint(IPAddress.Any, 0)), _loggerFactory.CreateLogger<UdpSocket>());
        var peerConnection = new RtcPeerConnection(videoTrack, socket, PeerConnectionChangeHandler);
        _streamMultiplexer.RegisterPeer(peerConnection);

        var answer = peerConnection.CreateOffer();

        return (peerConnection.Id, answer.sdp);
    }

    private async Task PeerConnectionChangeHandler(RtcPeerConnection peerConnection, RTCPeerConnectionState state)
    {
        _logger.LogDebug("Peer connection state change to {state}.", state);

        if (state == RTCPeerConnectionState.connected)
        {
            _streamMultiplexer.StartPeerTransmit(peerConnection.Id);
        }
        else if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.disconnected)
        {
            await _streamMultiplexer.ClosePeerAsync(peerConnection.Id);
        }
    }

    public async Task ProcessClientAnswerAsync(Guid peerId, string sdpString)
    {
        var peer = _streamMultiplexer.GetById(peerId);
        if (peer != null)
        {
            var result = peer.Peer.SetRemoteDescription(new RTCSessionDescriptionInit
            {
                sdp = sdpString,
                type = RTCSdpType.answer
            });
            _logger.LogDebug("SetRemoteDescription result: {@result}", result);
        }
    }

    private async Task RtpProcessorAsync(RtpPacket packet)
    {
        await _streamMultiplexer.SendVideoPacketAsync(packet);
        _receiver.ReusePacket(packet);
    }
}