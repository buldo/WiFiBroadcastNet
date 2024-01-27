using Org.BouncyCastle.Tls;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal interface IDtlsSrtpPeer
{
    event Action<AlertLevelsEnum, AlertTypesEnum, string> OnAlert;
    SrtpPolicy SrtpPolicy { get; }
    SrtpPolicy SrtcpPolicy { get; }
    byte[] SrtpMasterServerKey { get; }
    byte[] SrtpMasterServerSalt { get; }
    byte[] SrtpMasterClientKey { get; }
    byte[] SrtpMasterClientSalt { get; }
    bool IsClient { get; }
    Certificate RemoteCertificate { get; }
}