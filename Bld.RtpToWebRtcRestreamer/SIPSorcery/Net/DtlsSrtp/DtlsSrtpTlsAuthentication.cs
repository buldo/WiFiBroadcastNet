using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Utilities;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal class DtlsSrtpTlsAuthentication
    : TlsAuthentication
{
    private readonly DtlsSrtpClient _mClient;
    private readonly TlsContext _mContext;

    internal DtlsSrtpTlsAuthentication(DtlsSrtpClient client)
    {
        _mClient = client;
        _mContext = client.TlsContext;
    }

    public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
    {
        //Console.WriteLine("DTLS client received server certificate chain of length " + chain.Length);

        _mClient.ServerCertificate = serverCertificate;
    }

    public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
    {
        var certificateTypes = certificateRequest.CertificateTypes;
        if (certificateTypes == null || !Arrays.Contains(certificateTypes, ClientCertificateType.rsa_sign))
        {
            return null;
        }

        return DtlsUtils.LoadSignerCredentials(_mContext,
            certificateRequest.SupportedSignatureAlgorithms,
            SignatureAlgorithm.rsa,
            _mClient.CertificateChain,
            _mClient.PrivateKey);
    }
}