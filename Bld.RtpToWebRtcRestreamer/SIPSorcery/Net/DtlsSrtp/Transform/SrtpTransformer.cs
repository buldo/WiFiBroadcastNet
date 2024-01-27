using System.Collections.Concurrent;

using Microsoft.Extensions.ObjectPool;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class SrtpTransformer
{
    private readonly ConcurrentDictionary<long, SrtpCryptoContext> _contexts;
    private readonly SrtpTransformEngine _forwardEngine;

    private readonly ObjectPool<RawPacket> _packetsPool =
        new DefaultObjectPool<RawPacket>(new DefaultPooledObjectPolicy<RawPacket>());

    public SrtpTransformer(SrtpTransformEngine forwardEngine)
    {
        _forwardEngine = forwardEngine;
        _contexts = new ConcurrentDictionary<long, SrtpCryptoContext>();
    }

    public ReadOnlyMemory<byte> Transform(long ssrc, byte[] pkt, int length)
    {
        var rawPacket = _packetsPool.Get();
        try
        {
            rawPacket.WrapNoCopy(pkt, length);

            // Associate packet to a crypto context
            if (!_contexts.TryGetValue(ssrc, out var context))
            {
                context = _forwardEngine.DefaultContext.DeriveContext(0, 0);
                context.DeriveSrtpKeys(0);
                _contexts.AddOrUpdate(ssrc, context, (_, _) => context);
            }

            // Transform RTP packet into SRTP
            context.TransformPacket(rawPacket);

            return rawPacket.GetMemory();
        }
        finally
        {
            _packetsPool.Return(rawPacket);
        }
    }
}