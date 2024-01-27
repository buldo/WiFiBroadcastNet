namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class SrtpTransformEngine
{
    /// <summary>
    /// Construct a SRTPTransformEngine based on given master encryption key,
    /// master salt key and SRTP/SRTCP policy.
    /// </summary>
    /// <param name="masterKey">The master encryption key</param>
    /// <param name="masterSalt">The master salt key</param>
    /// <param name="srtpPolicy">SRTP policy</param>
    /// <param name="srtcpPolicy">SRTCP policy</param>
    public SrtpTransformEngine(byte[] masterKey, byte[] masterSalt, SrtpPolicy srtpPolicy, SrtpPolicy srtcpPolicy)
    {
        DefaultContext = new SrtpCryptoContext(0, 0, masterKey, masterSalt, srtpPolicy);
        DefaultContextControl = new SrtcpCryptoContext(masterKey, masterSalt, srtcpPolicy);
    }

    public SrtpCryptoContext DefaultContext { get; }

    public SrtcpCryptoContext DefaultContextControl { get; }

    public SrtcpTransformer GetRtcpTransformer()
    {
        return new SrtcpTransformer(this);
    }

    public SrtpTransformer GetRTPTransformer()
    {
        return new SrtpTransformer(this);
    }
}