using System.Collections.Concurrent;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class SrtcpTransformer
{
    private int _isLocked;
    private readonly RawPacket _packet;

    private readonly SrtpTransformEngine _reverseEngine;

    /** All the known SSRC's corresponding SRTCPCryptoContexts */
    private readonly ConcurrentDictionary<long, SrtcpCryptoContext> _contexts;

    public SrtcpTransformer(SrtpTransformEngine engine)
    {
        _packet = new RawPacket();
        _reverseEngine = engine;
        _contexts = new ConcurrentDictionary<long, SrtcpCryptoContext>();
    }

    public byte[] ReverseTransform(byte[] pkt, int length)
    {
        var isLocked = Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0;
        try
        {
            // wrap data into raw packet for readable format
            var packet = !isLocked ? _packet : new RawPacket();
            packet.Wrap(pkt, length);

            // Associate the packet with its encryption context
            long ssrc = packet.GetRtcpssrc();
            _contexts.TryGetValue(ssrc, out var context);

            if (context == null)
            {
                context = _reverseEngine.DefaultContextControl.DeriveContext();
                context.DeriveSrtcpKeys();
                _contexts.AddOrUpdate(ssrc, context, (_, _) => context);
            }

            // Decode packet to RTCP format
            byte[] result = null;
            var reversed = context.ReverseTransformPacket(packet);
            if (reversed)
            {
                result = packet.CopyData();
            }
            return result;
        }
        finally
        {
            //Unlock
            if (!isLocked)
                Interlocked.CompareExchange(ref _isLocked, 0, 1);
        }
    }
}