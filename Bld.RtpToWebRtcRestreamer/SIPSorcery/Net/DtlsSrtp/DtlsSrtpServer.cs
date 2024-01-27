//-----------------------------------------------------------------------------
// Filename: DtlsSrtpServer.cs
//
// Description: This class represents the DTLS SRTP server connection handler.
//
// Derived From:
// https://github.com/RestComm/media-core/blob/master/rtp/src/main/java/org/restcomm/media/core/rtp/crypto/DtlsSrtpServer.java
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// Original Source: AGPL-3.0 License
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.Utilities;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal sealed class DtlsSrtpServer : DefaultTlsServer, IDtlsSrtpPeer
{
    private static readonly int[] DefaultCipherSuites = {
        /*
         * TLS 1.3
         */
        //CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        //CipherSuite.TLS_AES_256_GCM_SHA384,
        //CipherSuite.TLS_AES_128_GCM_SHA256,

        /*
         * pre-TLS 1.3
         */
        CipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
        //CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        //CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA,
        CipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
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

    private static readonly ILogger Logger = Log.Logger;

    private readonly Certificate _mCertificateChain;
    private readonly AsymmetricKeyParameter _mPrivateKey;

    private UseSrtpData _serverSrtpData;

    private byte[] _srtpMasterClientKey;
    private byte[] _srtpMasterServerKey;
    private byte[] _srtpMasterClientSalt;
    private byte[] _srtpMasterServerSalt;

    // Policies
    private SrtpPolicy _srtpPolicy;
    private SrtpPolicy _srtcpPolicy;

    public DtlsSrtpServer(Certificate certificateChain, AsymmetricKeyParameter privateKey) : base(new BcTlsCrypto())
    {
        if (certificateChain == null && privateKey == null)
        {
            (certificateChain, privateKey) = DtlsUtils.CreateSelfSignedTlsCert(ProtocolVersion.DTLSv12, m_context.Crypto);
        }

        _mPrivateKey = privateKey;
        _mCertificateChain = certificateChain;
    }

    public event Action<AlertLevelsEnum, AlertTypesEnum, string> OnAlert;

    public Certificate RemoteCertificate => ClientCertificate;
    public bool IsClient => false;
    public SrtpPolicy SrtpPolicy => _srtpPolicy;
    public SrtpPolicy SrtcpPolicy => _srtcpPolicy;
    public byte[] SrtpMasterServerKey => _srtpMasterServerKey;
    public byte[] SrtpMasterServerSalt => _srtpMasterServerSalt;
    public byte[] SrtpMasterClientKey => _srtpMasterClientKey;
    public byte[] SrtpMasterClientSalt => _srtpMasterClientSalt;

    private Certificate ClientCertificate { get; set; }

    protected override int[] GetSupportedCipherSuites()
    {
        return TlsUtilities.GetSupportedCipherSuites(Crypto, DefaultCipherSuites);
    }

    protected override ProtocolVersion[] GetSupportedVersions()
    {
        return ProtocolVersion.DTLSv12.DownTo(ProtocolVersion.DTLSv10);
    }

    public override int GetSelectedCipherSuite()
    {
        /*
         * TODO RFC 5246 7.4.3. In order to negotiate correctly, the server MUST check any candidate cipher suites against the
         * "signature_algorithms" extension before selecting them. This is somewhat inelegant but is a compromise designed to
         * minimize changes to the original cipher suite design.
         */

        /*
         * RFC 4429 5.1. A server that receives a ClientHello containing one or both of these extensions MUST use the client's
         * enumerated capabilities to guide its selection of an appropriate cipher suite. One of the proposed ECC cipher suites
         * must be negotiated only if the server can successfully complete the handshake while using the curves and point
         * formats supported by the client [...].
         */

        var cipherSuites = GetCipherSuites();
        for (var i = 0; i < cipherSuites.Length; ++i)
        {
            var cipherSuite = cipherSuites[i];

            if (Arrays.Contains(m_offeredCipherSuites, cipherSuite)
                && TlsUtilities.IsValidVersionForCipherSuite(cipherSuite, m_context.ServerVersion))
            {
                return m_selectedCipherSuite = cipherSuite;
            }
        }
        throw new TlsFatalAlert(AlertDescription.handshake_failure);
    }

    public override CertificateRequest GetCertificateRequest()
    {
        var serverSigAlgs = new List<SignatureAndHashAlgorithm>();

        if (TlsUtilities.IsSignatureAlgorithmsExtensionAllowed(m_context.ServerVersion))
        {
            short[] hashAlgorithms = { HashAlgorithm.sha512, HashAlgorithm.sha384, HashAlgorithm.sha256, HashAlgorithm.sha224, HashAlgorithm.sha1 };
            short[] signatureAlgorithms = { SignatureAlgorithm.rsa, SignatureAlgorithm.ecdsa };

            serverSigAlgs = new List<SignatureAndHashAlgorithm>();
            for (var i = 0; i < hashAlgorithms.Length; ++i)
            {
                for (var j = 0; j < signatureAlgorithms.Length; ++j)
                {
                    serverSigAlgs.Add(new SignatureAndHashAlgorithm(hashAlgorithms[i], signatureAlgorithms[j]));
                }
            }
        }
        return new CertificateRequest(new[] { ClientCertificateType.rsa_sign, ClientCertificateType.ecdsa_sign }, serverSigAlgs, null);
    }

    public override void NotifyClientCertificate(Certificate clientCertificate)
    {
        ClientCertificate = clientCertificate;
    }

    public override IDictionary<int, byte[]> GetServerExtensions()
    {
        var serverExtensions = base.GetServerExtensions();
        if (TlsSrtpUtilities.GetUseSrtpExtension(serverExtensions) == null)
        {
            if (serverExtensions == null)
            {
                serverExtensions = new Dictionary<int, byte[]>();
            }
            TlsSrtpUtilities.AddUseSrtpExtension(serverExtensions, _serverSrtpData);
        }
        return serverExtensions;
    }

    public override void ProcessClientExtensions(IDictionary<int, byte[]> clientExtensions)
    {
        base.ProcessClientExtensions(clientExtensions);

        // set to some reasonable default value
        var chosenProfile = SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80;
        var clientSrtpData = TlsSrtpUtilities.GetUseSrtpExtension(clientExtensions);

        foreach (var profile in clientSrtpData.ProtectionProfiles)
        {
            switch (profile)
            {
                case SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80:
                case SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_32:
                case SrtpProtectionProfile.SRTP_NULL_HMAC_SHA1_80:
                case SrtpProtectionProfile.SRTP_NULL_HMAC_SHA1_32:
                //case SrtpProtectionProfile.SRTP_AEAD_AES_128_GCM:
                //case SrtpProtectionProfile.SRTP_AEAD_AES_256_GCM:
                    chosenProfile = profile;
                    break;
            }
        }

        // server chooses a mutually supported SRTP protection profile
        // http://tools.ietf.org/html/draft-ietf-avt-dtls-srtp-07#section-4.1.2
        int[] protectionProfiles = { chosenProfile };

        // server agrees to use the MKI offered by the client
        _serverSrtpData = new UseSrtpData(protectionProfiles, clientSrtpData.Mki);
    }

    protected override TlsCredentialedSigner GetECDsaSignerCredentials()
    {
        return DtlsUtils.LoadSignerCredentials(m_context, _mCertificateChain, _mPrivateKey, new SignatureAndHashAlgorithm(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa));
    }

    protected override TlsCredentialedDecryptor GetRsaEncryptionCredentials()
    {
        return DtlsUtils.LoadEncryptionCredentials(m_context, _mCertificateChain, _mPrivateKey);
    }

    protected override TlsCredentialedSigner GetRsaSignerCredentials()
    {
        /*
         * TODO Note that this code fails to provide default value for the client supported
         * algorithms if it wasn't sent.
         */
        SignatureAndHashAlgorithm signatureAndHashAlgorithm = null;
        var sigAlgs = TlsUtilities.GetDefaultSupportedSignatureAlgorithms(m_context);
        if (sigAlgs != null)
        {
            foreach (var sigAlgUncasted in sigAlgs)
            {
                if (sigAlgUncasted != null && sigAlgUncasted.Signature == SignatureAlgorithm.rsa)
                {
                    signatureAndHashAlgorithm = sigAlgUncasted;
                    break;
                }
            }

            if (signatureAndHashAlgorithm == null)
            {
                return null;
            }
        }
        return DtlsUtils.LoadSignerCredentials(m_context, _mCertificateChain, _mPrivateKey, signatureAndHashAlgorithm);
    }
    public override bool RequiresExtendedMasterSecret()
    {
        return true;
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

    public override void NotifyHandshakeComplete()
    {
        //Copy master Secret (will be inaccessible after this call)
        //if (m_context.SecurityParameters.MasterSecret != null)
        //{
        //    _masterSecret = m_context.SecurityParameters.MasterSecret.Extract().AsSpan().ToArray();
        //}

        //Prepare Srtp Keys (we must to it here because master key will be cleared after that)
        PrepareSrtpSharedSecret();
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

        var alertMsg = $"{AlertLevel.GetText(alertLevel)}, {AlertDescription.GetText(alertDescription)}";
        alertMsg += !string.IsNullOrEmpty(description) ? $", {description}." : ".";

        if (alertDescription == AlertTypesEnum.CloseNotify.GetHashCode())
        {
            Logger.LogDebug($"DTLS server raised close notify: {alertMsg}");
        }
        else
        {
            Logger.LogWarning($"DTLS server raised unexpected alert: {alertMsg}");
        }
    }

    public override void NotifyAlertReceived(short alertLevel, short alertDescription)
    {
        var description = AlertDescription.GetText(alertDescription);

        var level = AlertLevelsEnum.Warning;
        var alertType = AlertTypesEnum.Unknown;

        if (Enum.IsDefined(typeof(AlertLevelsEnum), alertLevel))
        {
            level = (AlertLevelsEnum)alertLevel;
        }

        if (Enum.IsDefined(typeof(AlertTypesEnum), alertDescription))
        {
            alertType = (AlertTypesEnum)alertDescription;
        }

        var alertMsg = $"{AlertLevel.GetText(alertLevel)}";
        alertMsg += !string.IsNullOrEmpty(description) ? $", {description}." : ".";

        if (alertType == AlertTypesEnum.CloseNotify)
        {
            Logger.LogDebug($"DTLS server received close notification: {alertMsg}");
        }
        else
        {
            Logger.LogWarning($"DTLS server received unexpected alert: {alertMsg}");
        }

        OnAlert?.Invoke(level, alertType, description);
    }

    /// <summary>
    /// This override prevents a TLS fault from being generated if a "Client Hello" is received that
    /// does not support TLS renegotiation (https://tools.ietf.org/html/rfc5746).
    /// This override is required to be able to complete a DTLS handshake with the Pion WebRTC library,
    /// see https://github.com/pion/dtls/issues/274.
    /// </summary>
    public override void NotifySecureRenegotiation(bool secureRenegotiation)
    {
        if (!secureRenegotiation)
        {
            Logger.LogWarning("DTLS server received a client handshake without renegotiation support.");
        }
    }

    private void PrepareSrtpSharedSecret()
    {
        //Set master secret back to security parameters (only works in old bouncy castle versions)
        //mContext.SecurityParameters.masterSecret = masterSecret;

        var srtpParams = SrtpParameters.GetSrtpParametersForProfile(_serverSrtpData.ProtectionProfiles[0]);
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
        var sharedSecret = GetKeyingMaterial(2 * (keyLen + saltLen));

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
         * s(k) = salt length
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

    private byte[] GetKeyingMaterial(int length)
    {
        return GetKeyingMaterial(ExporterLabel.dtls_srtp, null, length);
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
}