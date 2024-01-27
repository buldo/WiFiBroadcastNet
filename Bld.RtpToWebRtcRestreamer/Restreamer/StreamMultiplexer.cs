#nullable enable
using System.Collections.Immutable;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.Restreamer;

internal class StreamMultiplexer
{
    private readonly ILogger<StreamMultiplexer> _logger;
    private ImmutableDictionary<Guid, MultiplexedPeer> _peers = new Dictionary<Guid, MultiplexedPeer>().ToImmutableDictionary();

    public StreamMultiplexer(ILogger<StreamMultiplexer> logger)
    {
        _logger = logger;
    }

    public void RegisterPeer(RtcPeerConnection peer)
    {
        _peers = _peers.Add(peer.Id, new MultiplexedPeer(peer));
    }

    public void StartPeerTransmit(Guid peerId)
    {
        if (_peers.TryGetValue(peerId, out var multiplexedPeer))
        {
            multiplexedPeer.Start();
            _logger.LogDebug("Streaming for peer started");
        }
        else
        {
            _logger.LogError("Failed to get peer to start");
        }
    }

    public async Task ClosePeerAsync(Guid peerId)
    {
        if (_peers.TryGetValue(peerId, out var multiplexedPeer))
        {
            await multiplexedPeer.ClosePeerAsync();
            _peers = _peers.Remove(peerId);
            _logger.LogDebug("Streaming for peer stopped");
        }
        else
        {
            _logger.LogError("Failed to get peer to stop");
        }
    }

    public async Task SendVideoPacketAsync(RtpPacket rtpPacket)
    {
        foreach (var pair in _peers)
        {
            await pair.Value.SendVideoAsync(rtpPacket);
        }
    }

    public List<MultiplexedPeer> GetAllPeers()
    {
        return _peers.Values.ToList();
    }

    public MultiplexedPeer? GetById(Guid id)
    {
        return _peers.TryGetValue(id, out var peer) ? peer : null;
    }
}