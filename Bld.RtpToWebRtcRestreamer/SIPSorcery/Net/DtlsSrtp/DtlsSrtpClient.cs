//-----------------------------------------------------------------------------
// Filename: DtlsSrtpClient.cs
//
// Description: This class represents the DTLS SRTP client connection handler.
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal class DtlsSrtpClient : DefaultTlsClient, IDtlsSrtpPeer
{
    private static readonly ILogger Logger = Log.Logger;

    private readonly UseSrtpData _clientSrtpData;

    // Asymmetric shared keys derived from the DTLS handshake and used for the SRTP encryption/
    private byte[] _srtpMasterClientKey;
    private byte[] _srtpMasterServerKey;
    private byte[] _srtpMasterClientSalt;
    private byte[] _srtpMasterServerSalt;
    private byte[] _masterSecret;

    // Policies
    private SrtpPolicy _srtpPolicy;
    private SrtpPolicy _srtcpPolicy;

    /// <summary>
    /// Parameters:
    ///  - alert level,
    ///  - alert type,
    ///  - alert description.
    /// </summary>
    public event Action<AlertLevelsEnum, AlertTypesEnum, string> OnAlert;

    public DtlsSrtpClient(Certificate certificateChain, AsymmetricKeyParameter privateKey)
        : base(new BcTlsCrypto())
    {
        if (certificateChain == null && privateKey == null)
        {
            (certificateChain, privateKey) = DtlsUtils.CreateSelfSignedTlsCert(ProtocolVersion.DTLSv12, m_context.Crypto);
        }

        var random = new SecureRandom();
        int[] protectionProfiles = { SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80 };
        var mki = new byte[(SrtpParameters.SrtpAes128CmHmacSha180.GetCipherKeyLength() +
                            SrtpParameters.SrtpAes128CmHmacSha180.GetCipherSaltLength()) / 8];
        random.NextBytes(mki); // Reusing our secure random for generating the key.
        _clientSrtpData = new UseSrtpData(protectionProfiles, mki);

        PrivateKey = privateKey;
        CertificateChain = certificateChain;
    }

    public TlsServerCertificate ServerCertificate { get; set; }
    public Certificate CertificateChain { get; }
    public AsymmetricKeyParameter PrivateKey { get; }
    public TlsClientContext TlsContext => m_context;
    public Certificate RemoteCertificate => ServerCertificate.Certificate;
    public bool IsClient => true;
    public SrtpPolicy SrtpPolicy => _srtpPolicy;
    public SrtpPolicy SrtcpPolicy => _srtcpPolicy;
    public byte[] SrtpMasterServerKey => _srtpMasterServerKey;
    public byte[] SrtpMasterServerSalt => _srtpMasterServerSalt;
    public byte[] SrtpMasterClientKey => _srtpMasterClientKey;
    public byte[] SrtpMasterClientSalt => _srtpMasterClientSalt;

    public override IDictionary<int, byte[]> GetClientExtensions()
    {
        var clientExtensions = base.GetClientExtensions();
        if (TlsSrtpUtilities.GetUseSrtpExtension(clientExtensions) == null)
        {
            if (clientExtensions == null)
            {
                clientExtensions = new Dictionary<int, byte[]>();
            }

            TlsSrtpUtilities.AddUseSrtpExtension(clientExtensions, _clientSrtpData);
        }
        return clientExtensions;
    }

    public override TlsAuthentication GetAuthentication()
    {
        return new DtlsSrtpTlsAuthentication(this);
    }

    public override void NotifyHandshakeComplete()
    {
        base.NotifyHandshakeComplete();

        //Copy master Secret (will be inaccessible after this call)
        _masterSecret = new byte[m_context.SecurityParameters.MasterSecret != null ? m_context.SecurityParameters.MasterSecret.Length : 0];
        Buffer.BlockCopy(m_context.SecurityParameters.MasterSecret.Extract(), 0, _masterSecret, 0, _masterSecret.Length);

        //Prepare Srtp Keys (we must to it here because master key will be cleared after that)
        PrepareSrtpSharedSecret();
    }

    public override bool RequiresExtendedMasterSecret()
    {
        return true;
    }

    protected override ProtocolVersion[] GetSupportedVersions()
    {
        return ProtocolVersion.DTLSv12.DownTo(ProtocolVersion.DTLSv10);
    }

    public override int[] GetCipherSuites()
    {
        return new []
        {
            //CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
            //CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA,
            //CipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384,
            //CipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256,
            CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256,
            CipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA,
            CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA,
            //CipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384,
            //CipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256,
            CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256,
            CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA,
            CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA
        };
    }

    public override TlsSession GetSessionToResume()
    {
        return null;
    }

    public override void NotifyAlertRaised(short alertLevel, short alertDescription, string message, Exception cause)
    {
        string description = null;
        if (message != null)
        {
            description += message;
        }
        if (cause != null)
        {
            description += cause;
        }

        var alertMessage = $"{AlertLevel.GetText(alertLevel)}, {AlertDescription.GetText(alertDescription)}";
        alertMessage += !string.IsNullOrEmpty(description) ? $", {description}." : ".";

        if (alertDescription == AlertTypesEnum.CloseNotify.GetHashCode())
        {
            Logger.LogDebug($"DTLS client raised close notification: {alertMessage}");
        }
        else
        {
            Logger.LogWarning($"DTLS client raised unexpected alert: {alertMessage}");
        }
    }

    private byte[] GetKeyingMaterial(string asciiLabel, byte[] contextValue, int length)
    {
        if (contextValue != null && !TlsUtilities.IsValidUint16(contextValue.Length))
        {
            throw new ArgumentException("must have length less than 2^16 (or be null)", "contextValue");
        }

        var sp = m_context.SecurityParameters;
        if (!sp.IsExtendedMasterSecret && RequiresExtendedMasterSecret())
        {
            /*
             * RFC 7627 5.4. If a client or server chooses to continue with a full handshake without
             * the extended master secret extension, [..] the client or server MUST NOT export any
             * key material based on the new master secret for any subsequent application-level
             * authentication. In particular, it MUST disable [RFC5705] [..].
             */
            throw new InvalidOperationException("cannot export keying material without extended_master_secret");
        }

        byte[] cr = sp.ClientRandom, sr = sp.ServerRandom;

        var seedLength = cr.Length + sr.Length;
        if (contextValue != null)
        {
            seedLength += 2 + contextValue.Length;
        }

        var seed = new byte[seedLength];
        var seedPos = 0;

        Array.Copy(cr, 0, seed, seedPos, cr.Length);
        seedPos += cr.Length;
        Array.Copy(sr, 0, seed, seedPos, sr.Length);
        seedPos += sr.Length;
        if (contextValue != null)
        {
            TlsUtilities.WriteUint16(contextValue.Length, seed, seedPos);
            seedPos += 2;
            Array.Copy(contextValue, 0, seed, seedPos, contextValue.Length);
            seedPos += contextValue.Length;
        }

        if (seedPos != seedLength)
        {
            throw new InvalidOperationException("error in calculation of seed for export");
        }

        return TlsUtilities.Prf(m_context.SecurityParameters, sp.MasterSecret, asciiLabel, seed, length).Extract();
    }

    private void PrepareSrtpSharedSecret()
    {
        //Set master secret back to security parameters (only works in old bouncy castle versions)
        //mContext.SecurityParameters.MasterSecret = masterSecret;

        var srtpParams = SrtpParameters.GetSrtpParametersForProfile(_clientSrtpData.ProtectionProfiles[0]);
        var keyLen = srtpParams.GetCipherKeyLength();
        var saltLen = srtpParams.GetCipherSaltLength();

        _srtpPolicy = srtpParams.GetSrtpPolicy();
        _srtcpPolicy = srtpParams.GetSrtcpPolicy();

        _srtpMasterClientKey = new byte[keyLen];
        _srtpMasterServerKey = new byte[keyLen];
        _srtpMasterClientSalt = new byte[saltLen];
        _srtpMasterServerSalt = new byte[saltLen];

        // 2* (key + salt length) / 8. From http://tools.ietf.org/html/rfc5764#section-4-2
        // No need to divide by 8 here since lengths are already in bits
        var length = 2 * (keyLen + saltLen);
        var sharedSecret = GetKeyingMaterial(ExporterLabel.dtls_srtp, null, length);

        /*
         *
         * See: http://tools.ietf.org/html/rfc5764#section-4.2
         *
         * sharedSecret is an equivalent of :
         *
         * struct {
         *     client_write_SRTP_master_key[SRTPSecurityParams.master_key_len];
         *     server_write_SRTP_master_key[SRTPSecurityParams.master_key_len];
         *     client_write_SRTP_master_salt[SRTPSecurityParams.master_salt_len];
         *     server_write_SRTP_master_salt[SRTPSecurityParams.master_salt_len];
         *  } ;
         *
         * Here, client = local configuration, server = remote.
         * NOTE [ivelin]: 'local' makes sense if this code is used from a DTLS SRTP client.
         *                Here we run as a server, so 'local' referring to the client is actually confusing.
         *
         * l(k) = KEY length
         * s(k) = salt lenght
         *
         * So we have the following repartition :
         *                           l(k)                                 2*l(k)+s(k)
         *                                                   2*l(k)                       2*(l(k)+s(k))
         * +------------------------+------------------------+---------------+-------------------+
         * + local key           |    remote key    | local salt   | remote salt   |
         * +------------------------+------------------------+---------------+-------------------+
         */
        Buffer.BlockCopy(sharedSecret, 0, _srtpMasterClientKey, 0, keyLen);
        Buffer.BlockCopy(sharedSecret, keyLen, _srtpMasterServerKey, 0, keyLen);
        Buffer.BlockCopy(sharedSecret, 2 * keyLen, _srtpMasterClientSalt, 0, saltLen);
        Buffer.BlockCopy(sharedSecret, 2 * keyLen + saltLen, _srtpMasterServerSalt, 0, saltLen);
    }

}