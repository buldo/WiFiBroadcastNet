//-----------------------------------------------------------------------------
// Filename: SrtpParameters.cs
//
// Description: Parameters for Secure RTP (SRTP) sessions.
//
// Derived From:
// https://github.com/RestComm/media-core/blob/master/rtp/src/main/java/org/restcomm/media/core/rtp/crypto/SRTPParameters.java
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
//
// License:
// Customisations: BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// Original Source: AGPL-3.0 License
//-----------------------------------------------------------------------------

using Org.BouncyCastle.Tls;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal class SrtpParameters
{
    // DTLS derived key and salt lengths for SRTP
    // http://tools.ietf.org/html/rfc5764#section-4.1.2

    //	SRTP_AES128_CM_HMAC_SHA1_80 (SRTPProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80, SRTPPolicy.AESCM_ENCRYPTION, 128, SRTPPolicy.HMACSHA1_AUTHENTICATION, 160, 80, 80, 112),
    //	SRTP_AES128_CM_HMAC_SHA1_32 (SRTPProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_32, SRTPPolicy.AESCM_ENCRYPTION, 128, SRTPPolicy.HMACSHA1_AUTHENTICATION, 160, 32, 80, 112),
    // hrosa - converted lengths to work with bytes, not bits (1 byte = 8 bits)
    public static readonly SrtpParameters SrtpAes128CmHmacSha180 = new (
        SrtpPolicy.AescmEncryption, 16, SrtpPolicy.Hmacsha1Authentication, 20, 10, 10, 14);
    private static readonly SrtpParameters SrtpAes128CmHmacSha132 = new (
        SrtpPolicy.AescmEncryption, 16, SrtpPolicy.Hmacsha1Authentication, 20, 4, 10, 14);
    private static readonly SrtpParameters SrtpNullHmacSha180 = new (
        SrtpPolicy.NullEncryption, 0, SrtpPolicy.Hmacsha1Authentication, 20, 10, 10, 0);
    private static readonly SrtpParameters SrtpNullHmacSha132 = new (
        SrtpPolicy.NullEncryption, 0, SrtpPolicy.Hmacsha1Authentication, 20, 4, 10, 0);
    private static readonly SrtpParameters SrtpAeadAes128Gcm = new (
        SrtpPolicy.AescmEncryption, 16, SrtpPolicy.Hmacsha1Authentication, 20, 16, 16, 14);
    private static readonly SrtpParameters SrtpAeadAes256Gcm = new (
        SrtpPolicy.AescmEncryption, 32, SrtpPolicy.Hmacsha1Authentication, 20, 16, 16, 14);

    private readonly int _encType;
    private readonly int _encKeyLength;
    private readonly int _authType;
    private readonly int _authKeyLength;
    private readonly int _authTagLength;
    private readonly int _rtcpAuthTagLength;
    private readonly int _saltLength;

    private SrtpParameters(
        int newEncType,
        int newEncKeyLength,
        int newAuthType,
        int newAuthKeyLength,
        int newAuthTagLength,
        int newRtcpAuthTagLength,
        int newSaltLength)
    {
        _encType = newEncType;
        _encKeyLength = newEncKeyLength;
        _authType = newAuthType;
        _authKeyLength = newAuthKeyLength;
        _authTagLength = newAuthTagLength;
        _rtcpAuthTagLength = newRtcpAuthTagLength;
        _saltLength = newSaltLength;
    }

    public int GetCipherKeyLength()
    {
        return _encKeyLength;
    }

    public int GetCipherSaltLength()
    {
        return _saltLength;
    }

    public static SrtpParameters GetSrtpParametersForProfile(int profileValue)
    {
        switch (profileValue)
        {
            case SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_80:
                return SrtpAes128CmHmacSha180;
            case SrtpProtectionProfile.SRTP_AES128_CM_HMAC_SHA1_32:
                return SrtpAes128CmHmacSha132;
            case SrtpProtectionProfile.SRTP_NULL_HMAC_SHA1_80:
                return SrtpNullHmacSha180;
            case SrtpProtectionProfile.SRTP_NULL_HMAC_SHA1_32:
                return SrtpNullHmacSha132;
            case SrtpProtectionProfile.SRTP_AEAD_AES_128_GCM:
                return SrtpAeadAes128Gcm;
            case SrtpProtectionProfile.SRTP_AEAD_AES_256_GCM:
                return SrtpAeadAes256Gcm;
            default:
                throw new Exception($"SRTP Protection Profile value {profileValue} is not allowed for DTLS SRTP. See http://tools.ietf.org/html/rfc5764#section-4.1.2 for valid values.");
        }
    }

    public SrtpPolicy GetSrtpPolicy()
    {
        var sp = new SrtpPolicy(_encType, _encKeyLength, _authType, _authKeyLength, _authTagLength, _saltLength);
        return sp;
    }

    public SrtpPolicy GetSrtcpPolicy()
    {
        var sp = new SrtpPolicy(_encType, _encKeyLength, _authType, _authKeyLength, _rtcpAuthTagLength, _saltLength);
        return sp;
    }

}