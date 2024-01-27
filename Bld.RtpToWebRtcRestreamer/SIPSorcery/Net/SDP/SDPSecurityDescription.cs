//-----------------------------------------------------------------------------
// Filename: SDP.cs
//
// Description: (SDP) Security Descriptions for Media Streams implementation as basically defined in RFC 4568.
// https://tools.ietf.org/html/rfc4568
//
// Author(s):
// rj2

using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

/// <summary>
/// (SDP) Security Descriptions for Media Streams implementation as basically defined in RFC 4568.
/// <code>
/// Example 1: Parse crypto attribute
///
/// string crypto = "a=crypto:1 AES_256_CM_HMAC_SHA1_80 inline:GTuZoqOsesiK4wfyL7Rsq6uHHwhqVGA+aVuAUnsmWktYacZyJu6/6tUQeUti0Q==";
/// SDPSecurityDescription localcrypto = SDPSecurityDescription.Parse(crypto);
///
/// </code>
/// <code>
/// Example 2: Parse crypto attribute
///
/// SDPMediaAnnouncement mediaAudio = new SDPMediaAnnouncement();
/// //[...]set some SDPMediaAnnouncement properties
/// SDPSecurityDescription localcrypto = SDPSecurityDescription.CreateNew();
/// localcrypto.KeyParams.Clear();
/// localcrypto.KeyParams.Add(SDPSecurityDescription.KeyParameter.CreateNew(SDPSecurityDescription.CryptoSuites.AES_CM_128_HMAC_SHA1_32));
/// mediaAudio.SecurityDescriptions.Add(localcrypto);
/// mediaAudio.ToString();
///
/// string crypto = "a=crypto:1 AES_256_CM_HMAC_SHA1_80 inline:GTuZoqOsesiK4wfyL7Rsq6uHHwhqVGA+aVuAUnsmWktYacZyJu6/6tUQeUti0Q==";
/// SDPSecurityDescription desc = SDPSecurityDescription.Parse(crypto);
///
/// </code>
/// </summary>
internal class SDPSecurityDescription
{
    public const string CRYPTO_ATTRIBUE_PREFIX = "a=crypto:";
    private readonly char[] WHITE_SPACES = { ' ', '\t' };
    private const char SEMI_COLON = ';';
    private const string COLON = ":";
    private const string WHITE_SPACE = " ";
    public enum CryptoSuites
    {
        unknown,
        AES_CM_128_HMAC_SHA1_80, //https://tools.ietf.org/html/rfc4568
        AES_CM_128_HMAC_SHA1_32, //https://tools.ietf.org/html/rfc4568
        F8_128_HMAC_SHA1_80, //https://tools.ietf.org/html/rfc4568
        AEAD_AES_128_GCM, //https://tools.ietf.org/html/rfc7714
        AEAD_AES_256_GCM, //https://tools.ietf.org/html/rfc7714
        AES_192_CM_HMAC_SHA1_80, //https://tools.ietf.org/html/rfc6188
        AES_192_CM_HMAC_SHA1_32, //https://tools.ietf.org/html/rfc6188
        AES_256_CM_HMAC_SHA1_80, //https://tools.ietf.org/html/rfc6188
        AES_256_CM_HMAC_SHA1_32, //https://tools.ietf.org/html/rfc6188
        //duplicates, for wrong spelling in Ozeki-voip-sdk and who knows where else
        AES_CM_192_HMAC_SHA1_80, //https://tools.ietf.org/html/rfc6188
        AES_CM_192_HMAC_SHA1_32, //https://tools.ietf.org/html/rfc6188
        AES_CM_256_HMAC_SHA1_80, //https://tools.ietf.org/html/rfc6188
        AES_CM_256_HMAC_SHA1_32 //https://tools.ietf.org/html/rfc6188
    }
    internal class KeyParameter
    {
        private const string PIPE = "|";
        private const string KEY_METHOD = "inline";
        private byte[] m_key;
        //128 bit for AES_CM_128_HMAC_SHA1_80, AES_CM_128_HMAC_SHA1_32, F8_128_HMAC_SHA1_80, AEAD_AES_128_GCM
        //192 bit for AES_192_CM_HMAC_SHA1_80, AES_192_CM_HMAC_SHA1_32
        //256 bit for AEAD_AES_256_GCM, AES_256_CM_HMAC_SHA1_80, AES_256_CM_HMAC_SHA1_32
        //
        private byte[] Key
        {
            get
            {
                return m_key;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("Key", "Key must have a value");
                }

                if (value.Length < 16)
                {
                    throw new ArgumentOutOfRangeException("Key", "Key must be at least 16 characters long");
                }

                m_key = value;
            }
        }
        private byte[] m_salt;
        //112 bit for AES_CM_128_HMAC_SHA1_80, AES_CM_128_HMAC_SHA1_32, F8_128_HMAC_SHA1_80
        //112 bit for AES_192_CM_HMAC_SHA1_80,AES_192_CM_HMAC_SHA1_32 , AES_256_CM_HMAC_SHA1_80, AES_256_CM_HMAC_SHA1_32
        //96 bit for AEAD_AES_128_GCM
        //
        private byte[] Salt
        {
            get
            {
                return m_salt;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("Salt", "Salt must have a value");
                }

                if (value.Length < 12)
                {
                    throw new ArgumentOutOfRangeException("Salt", "Salt must be at least 12 characters long");
                }

                m_salt = value;
            }
        }
        private string KeySaltBase64
        {
            get
            {
                var b = new byte[Key.Length + Salt.Length];
                Array.Copy(Key, 0, b, 0, Key.Length);
                Array.Copy(Salt, 0, b, Key.Length, Salt.Length);
                var s64 = Convert.ToBase64String(b);
                //removal of Padding-Characters "=" happens when decoding of Base64-String
                //https://tools.ietf.org/html/rfc4568 page 13
                //s64 = s64.TrimEnd('=');
                return s64;
            }
        }
        private ulong m_lifeTime;
        private ulong LifeTime
        {
            get
            {
                return m_lifeTime;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException("LifeTime", "LifeTime value must be power of 2");
                }

                var ul = value;
                var i = 0;
                for (; i < 64; i++)
                {
                    if ((ul & 0x1) == 0x1)
                    {
                        if (i == 0)//2^0 wollen wir nicht
                        {
                            throw new ArgumentOutOfRangeException("LifeTime", "LifeTime value must be power of 2");
                        }

                        ul = ul >> 1;
                        break;
                    }

                    ul = ul >> 1;
                }
                if (ul == 0)
                {
                    m_lifeTime = value;
                    m_sLifeTime = $"2^{i}";
                }
                else
                {
                    throw new ArgumentOutOfRangeException("LifeTime", "LifeTime value must be power of 2");
                }
            }
        }
        private string m_sLifeTime;
        private string LifeTimeString
        {
            get
            {
                return m_sLifeTime;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException("LifeTimeString", "LifeTimeString value must be power of 2 string");
                }

                if (!value.StartsWith("2^"))
                {
                    throw new ArgumentException("LifeTimeString must begin with 2^", "LifeTimeString");
                }

                double d = ulong.Parse(value.Substring(2)); //let .net throw an exception if given value is not a number
                if (d < 1)
                {
                    throw new ArgumentOutOfRangeException("LifeTimeString", "LifeTimeString value must be power of 2");
                }

                m_lifeTime = (ulong)Math.Pow(2, d);
                m_sLifeTime = $"2^{(ulong)d}";
            }
        }
        private uint MkiValue
        {
            get;
            set;
        }
        private uint m_mkiLength;
        private uint MkiLength
        {
            get
            {
                return m_mkiLength;
            }
            set
            {
                if (value > 0 && value <= 128)
                {
                    m_mkiLength = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("MkiLength", "MkiLength value must between 1 and 128");
                }
            }
        }
        private KeyParameter(byte[] key, byte[] salt)
        {
            Key = key;
            Salt = salt;
        }

        public override string ToString()
        {
            var s = KEY_METHOD + COLON + KeySaltBase64;
            if (!string.IsNullOrWhiteSpace(LifeTimeString))
            {
                s += PIPE + LifeTimeString;
            }
            else if (LifeTime > 0)
            {
                s += PIPE + LifeTime;
            }

            if (MkiLength > 0 && MkiValue > 0)
            {
                s += PIPE + MkiValue + COLON + MkiLength;
            }

            return s;
        }

        public static KeyParameter Parse(string keyParamString, CryptoSuites cryptoSuite = CryptoSuites.AES_CM_128_HMAC_SHA1_80)
        {
            if (!string.IsNullOrWhiteSpace(keyParamString))
            {
                var p = keyParamString.Trim();
                try
                {
                    if (p.StartsWith(KEY_METHOD))
                    {
                        var sKeyMethod = KEY_METHOD;
                        var poscln = p.IndexOf(COLON);
                        if (poscln == sKeyMethod.Length)
                        {
                            var sKeyInfo = p.Substring(poscln + 1);
                            if (!sKeyInfo.Contains(";"))
                            {
                                string sMkiVal, sMkiLen, sLifeTime, sBase64KeySalt;
                                checkValidKeyInfoCharacters(keyParamString, sKeyInfo);
                                parseKeyInfo(keyParamString, sKeyInfo, out sMkiVal, out sMkiLen, out sLifeTime, out sBase64KeySalt);
                                if (!string.IsNullOrWhiteSpace(sBase64KeySalt))
                                {
                                    byte[] bKey, bSalt;
                                    parseKeySaltBase64(cryptoSuite, sBase64KeySalt, out bKey, out bSalt);

                                    var kp = new KeyParameter(bKey, bSalt);
                                    if (!string.IsNullOrWhiteSpace(sMkiVal) && !string.IsNullOrWhiteSpace(sMkiLen))
                                    {
                                        kp.MkiValue = uint.Parse(sMkiVal);
                                        kp.MkiLength = uint.Parse(sMkiLen);
                                    }
                                    if (!string.IsNullOrWhiteSpace(sLifeTime))
                                    {
                                        if (sLifeTime.Contains('^'))
                                        {
                                            kp.LifeTimeString = sLifeTime;
                                        }
                                        else
                                        {
                                            kp.LifeTime = uint.Parse(sLifeTime);
                                        }
                                    }
                                    return kp;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    //catch all errors and throw own FormatException
                }
            }
            throw new FormatException($"keyParam '{keyParamString}' is not recognized as a valid KEY_PARAM ");
        }

        private static void parseKeySaltBase64(CryptoSuites cryptoSuite, string base64KeySalt, out byte[] key, out byte[] salt)
        {
            var keysalt = Convert.FromBase64String(base64KeySalt);
            key = null;
            switch (cryptoSuite)
            {
                case CryptoSuites.AES_CM_128_HMAC_SHA1_32:
                case CryptoSuites.AES_CM_128_HMAC_SHA1_80:
                case CryptoSuites.F8_128_HMAC_SHA1_80:
                case CryptoSuites.AEAD_AES_128_GCM:
                    key = new byte[128 / 8];
                    Array.Copy(keysalt, 0, key, 0, 128 / 8);
                    break;
                case CryptoSuites.AES_192_CM_HMAC_SHA1_80:
                case CryptoSuites.AES_192_CM_HMAC_SHA1_32:
                case CryptoSuites.AES_CM_192_HMAC_SHA1_80:
                case CryptoSuites.AES_CM_192_HMAC_SHA1_32:
                    key = new byte[192 / 8];
                    Array.Copy(keysalt, 0, key, 0, 192 / 8);
                    break;
                case CryptoSuites.AEAD_AES_256_GCM:
                case CryptoSuites.AES_256_CM_HMAC_SHA1_80:
                case CryptoSuites.AES_256_CM_HMAC_SHA1_32:
                case CryptoSuites.AES_CM_256_HMAC_SHA1_80:
                case CryptoSuites.AES_CM_256_HMAC_SHA1_32:
                    key = new byte[256 / 8];
                    Array.Copy(keysalt, 0, key, 0, 256 / 8);
                    break;
            }
            salt = null;
            switch (cryptoSuite)
            {
                case CryptoSuites.AES_CM_128_HMAC_SHA1_32:
                case CryptoSuites.AES_CM_128_HMAC_SHA1_80:
                case CryptoSuites.F8_128_HMAC_SHA1_80:
                    salt = new byte[112 / 8];
                    Array.Copy(keysalt, 128 / 8, salt, 0, 112 / 8);
                    break;
                case CryptoSuites.AES_192_CM_HMAC_SHA1_80:
                case CryptoSuites.AES_192_CM_HMAC_SHA1_32:
                case CryptoSuites.AES_CM_192_HMAC_SHA1_80:
                case CryptoSuites.AES_CM_192_HMAC_SHA1_32:
                    salt = new byte[112 / 8];
                    Array.Copy(keysalt, 192 / 8, salt, 0, 112 / 8);
                    break;
                case CryptoSuites.AES_256_CM_HMAC_SHA1_80:
                case CryptoSuites.AES_256_CM_HMAC_SHA1_32:
                case CryptoSuites.AES_CM_256_HMAC_SHA1_80:
                case CryptoSuites.AES_CM_256_HMAC_SHA1_32:
                    salt = new byte[256 / 8];
                    Array.Copy(keysalt, 256 / 8, salt, 0, 112 / 8);
                    break;
                case CryptoSuites.AEAD_AES_256_GCM:
                case CryptoSuites.AEAD_AES_128_GCM:
                    salt = new byte[96 / 8];
                    Array.Copy(keysalt, 128 / 8, salt, 0, 96 / 8);
                    break;
            }
        }

        private static void checkValidKeyInfoCharacters(string keyParameter, string keyInfo)
        {
            foreach (var c in keyInfo)
            {
                if (c < 0x21 || c > 0x7e)
                {
                    throw new FormatException($"keyParameter '{keyParameter}' is not recognized as a valid KEY_INFO ");
                }
            }
        }

        private static void parseKeyInfo(string keyParamString, string keyInfo, out string mkiValue, out string mkiLen, out string lifeTimeString, out string base64KeySalt)
        {
            mkiValue = null;
            mkiLen = null;
            lifeTimeString = null;
            base64KeySalt = null;
            //KeyInfo must only contain visible printing characters
            //and 40 char long, as its is the base64representation of concatenated Key and Salt
            var pospipe1 = keyInfo.IndexOf(PIPE);
            if (pospipe1 > 0)
            {
                base64KeySalt = keyInfo.Substring(0, pospipe1);
                //find lifetime and mki
                //both may be omitted, but mki is recognized by a colon
                //usually lifetime comes before mki, if specified
                var posclnmki = keyInfo.IndexOf(COLON, pospipe1 + 1);
                var pospipe2 = keyInfo.IndexOf(PIPE, pospipe1 + 1);
                if (posclnmki > 0 && pospipe2 < 0)
                {
                    mkiValue = keyInfo.Substring(pospipe1 + 1, posclnmki - pospipe1 - 1);
                    mkiLen = keyInfo.Substring(posclnmki + 1);
                }
                else if (posclnmki > 0 && pospipe2 < posclnmki)
                {
                    lifeTimeString = keyInfo.Substring(pospipe1 + 1, pospipe2 - pospipe1 - 1);
                    mkiValue = keyInfo.Substring(pospipe2 + 1, posclnmki - pospipe2 - 1);
                    mkiLen = keyInfo.Substring(posclnmki + 1);
                }
                else if (posclnmki > 0 && pospipe2 > posclnmki)
                {
                    mkiValue = keyInfo.Substring(pospipe1 + 1, posclnmki - pospipe1 - 1);
                    mkiLen = keyInfo.Substring(posclnmki + 1, pospipe2 - posclnmki - 1);
                    lifeTimeString = keyInfo.Substring(pospipe2 + 1);
                }
                else if (posclnmki < 0 && pospipe2 < 0)
                {
                    lifeTimeString = keyInfo.Substring(pospipe1 + 1);
                }
                else if (posclnmki < 0 && pospipe2 > 0)
                {
                    throw new FormatException($"keyParameter '{keyParamString}' is not recognized as a valid SRTP_KEY_INFO ");
                }
            }
            else
            {
                base64KeySalt = keyInfo;
            }
        }

    }

    internal class SessionParameter
    {
        private enum SrtpSessionParams
        {
            unknown,
            kdr,
            UNENCRYPTED_SRTP,
            UNENCRYPTED_SRTCP,
            UNAUTHENTICATED_SRTP,
            fec_order,
            fec_key,
            wsh
        }
        private SrtpSessionParams SrtpSessionParam
        {
            get;
        }


        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private enum FecTypes
        {
            unknown,
            FEC_SRTP,
            SRTP_FEC
        }
        private FecTypes FecOrder
        {
            get;
            set;
        }
        private const string FEC_KEY_PREFIX = "FEC_KEY=";
        private const string FEC_ORDER_PREFIX = "FEC_ORDER=";
        private const string WSH_PREFIX = "WSH=";
        private const string KDR_PREFIX = "KDR=";
        private ulong m_kdr;
        private ulong Kdr
        {
            get
            {
                return m_kdr;
            }
            set
            {
                /*if(value < 1 || value > Math.Pow(2, 24))
                    throw new ArgumentOutOfRangeException("Kdr", "Kdr must be power of 2 and less than 2^24");
                ulong ul = value;
                for(int i = 0; i < 64; i++)
                {
                    if((ul & 0x1) == 0x1)
                    {
                        if(i == 0)//2^0 wollen wir nicht
                            throw new ArgumentOutOfRangeException("Kdr", "Kdr must be power of 2 and less than 2^24");
                        else
                        {
                            ul = ul >> 1;
                            break;
                        }
                    }
                    else
                    {
                        ul = ul >> 1;
                    }
                }
                if(ul == 0)
                    this.m_kdr = value;
                else
                    throw new ArgumentOutOfRangeException("Kdr", "Kdr must be power of 2 and less than 2^24");
                */
                if (value > 24)
                {
                    throw new ArgumentOutOfRangeException("Kdr", "Kdr must be between 0 and 24");
                }

                m_kdr = value;
            }
        }
        private ulong m_wsh = 64;
        private ulong Wsh
        {
            get
            {
                return m_wsh;
            }
            set
            {
                if (value < 64)
                {
                    throw new ArgumentOutOfRangeException("WSH", "WSH must be greater than 64");
                }

                m_wsh = value;
            }
        }

        private KeyParameter FecKey
        {
            get;
            set;
        }

        private SessionParameter(SrtpSessionParams paramType)
        {
            SrtpSessionParam = paramType;
        }
        public override string ToString()
        {
            if (SrtpSessionParam == SrtpSessionParams.unknown)
            {
                return "";
            }

            switch (SrtpSessionParam)
            {
                case SrtpSessionParams.UNAUTHENTICATED_SRTP:
                case SrtpSessionParams.UNENCRYPTED_SRTP:
                case SrtpSessionParams.UNENCRYPTED_SRTCP:
                    return SrtpSessionParam.ToString();
                case SrtpSessionParams.wsh:
                    return $"{WSH_PREFIX}{Wsh}";
                case SrtpSessionParams.kdr:
                    return $"{KDR_PREFIX}{Kdr}";
                case SrtpSessionParams.fec_order:
                    return $"{FEC_ORDER_PREFIX}{FecOrder.ToString()}";
                case SrtpSessionParams.fec_key:
                    return $"{FEC_KEY_PREFIX}{FecKey}";
            }
            return "";
        }

        public static SessionParameter Parse(string sessionParam, CryptoSuites cryptoSuite = CryptoSuites.AES_CM_128_HMAC_SHA1_80)
        {
            if (string.IsNullOrWhiteSpace(sessionParam))
            {
                return null;
            }

            var p = sessionParam.Trim();
            try
            {
                if (p.StartsWith(KDR_PREFIX))
                {
                    var sKdr = p.Substring(KDR_PREFIX.Length);
                    if (uint.TryParse(sKdr, out var kdr))
                    {
                        return new SessionParameter(SrtpSessionParams.kdr) { Kdr = kdr };
                    }
                }
                else if (p.StartsWith(WSH_PREFIX))
                {
                    var sWsh = p.Substring(WSH_PREFIX.Length);
                    if (uint.TryParse(sWsh, out var wsh))
                    {
                        return new SessionParameter(SrtpSessionParams.wsh) { Wsh = wsh };
                    }
                }
                else if (p.StartsWith(FEC_KEY_PREFIX))
                {
                    var sFecKey = p.Substring(FEC_KEY_PREFIX.Length);
                    var fecKey = KeyParameter.Parse(sFecKey, cryptoSuite);
                    return new SessionParameter(SrtpSessionParams.fec_key) { FecKey = fecKey };
                }
                else if (p.StartsWith(FEC_ORDER_PREFIX))
                {
                    var sFecOrder = p.Substring(FEC_ORDER_PREFIX.Length);
                    var fecOrder = (from e in Enum.GetNames(typeof(FecTypes)) where e.CompareTo(sFecOrder) == 0 select (FecTypes)Enum.Parse(typeof(FecTypes), e)).FirstOrDefault();
                    if (fecOrder == FecTypes.unknown)
                    {
                        throw new FormatException($"sessionParam '{sessionParam}' is not recognized as a valid SRTP_SESSION_PARAM ");
                    }

                    return new SessionParameter(SrtpSessionParams.fec_order) { FecOrder = fecOrder };
                }
                else
                {
                    var paramType = (from e in Enum.GetNames(typeof(SrtpSessionParams)) where e.CompareTo(p) == 0 select (SrtpSessionParams)Enum.Parse(typeof(SrtpSessionParams), e)).FirstOrDefault();
                    if (paramType == SrtpSessionParams.unknown)
                    {
                        throw new FormatException($"sessionParam '{sessionParam}' is not recognized as a valid SRTP_SESSION_PARAM ");
                    }

                    switch (paramType)
                    {
                        case SrtpSessionParams.UNAUTHENTICATED_SRTP:
                        case SrtpSessionParams.UNENCRYPTED_SRTCP:
                        case SrtpSessionParams.UNENCRYPTED_SRTP:
                            return new SessionParameter(paramType);
                    }
                }
            }
            catch
            {
                //catch all errors and throw own FormatException
            }

            throw new FormatException($"sessionParam '{sessionParam}' is not recognized as a valid SRTP_SESSION_PARAM ");
        }
    }


    private uint m_iTag = 1;
    private uint Tag
    {
        get
        {
            return m_iTag;
        }
        set
        {
            if (value > 0 && value < 1000000000)
            {
                m_iTag = value;
            }
            else
            {
                throw new ArgumentOutOfRangeException("Tag", "Tag value must be greater than 0 and not exceed 9 digits");
            }
        }
    }


    private CryptoSuites CryptoSuite
    {
        get;
        set;
    }

    private List<KeyParameter> KeyParams
    {
        get;
    }
    private SessionParameter SessionParam
    {
        get;
        set;
    }
    private SDPSecurityDescription() : this(1, CryptoSuites.AES_CM_128_HMAC_SHA1_80)
    {

    }
    private SDPSecurityDescription(uint tag, CryptoSuites cryptoSuite)
    {
        Tag = tag;
        CryptoSuite = cryptoSuite;
        KeyParams = new List<KeyParameter>();
    }

    public override string ToString()
    {
        if (Tag < 1 || CryptoSuite == CryptoSuites.unknown || KeyParams.Count < 1)
        {
            return null;
        }

        var s = CRYPTO_ATTRIBUE_PREFIX + Tag + WHITE_SPACE + CryptoSuite + WHITE_SPACE;
        for (var i = 0; i < KeyParams.Count; i++)
        {
            if (i > 0)
            {
                s += SEMI_COLON;
            }

            s += KeyParams[i].ToString();
        }
        if (SessionParam != null)
        {
            s += WHITE_SPACE + SessionParam;
        }
        return s;
    }

    public static SDPSecurityDescription Parse(string cryptoLine)
    {
        if (string.IsNullOrWhiteSpace(cryptoLine))
        {
            return null;
        }

        if (!cryptoLine.StartsWith(CRYPTO_ATTRIBUE_PREFIX))
        {
            throw new FormatException($"cryptoLine '{cryptoLine}' is not recognized as a valid SDP Security Description ");
        }

        var sCryptoValue = cryptoLine.Substring(cryptoLine.IndexOf(COLON) + 1);

        var sdpCryptoAttribute = new SDPSecurityDescription();
        var sCryptoParts = sCryptoValue.Split(sdpCryptoAttribute.WHITE_SPACES, StringSplitOptions.RemoveEmptyEntries);
        if (sCryptoValue.Length < 2)
        {
            throw new FormatException($"cryptoLine '{cryptoLine}' is not recognized as a valid SDP Security Description ");
        }

        try
        {
            sdpCryptoAttribute.Tag = uint.Parse(sCryptoParts[0]);
            sdpCryptoAttribute.CryptoSuite = (from e in Enum.GetNames(typeof(CryptoSuites)) where e.CompareTo(sCryptoParts[1]) == 0 select (CryptoSuites)Enum.Parse(typeof(CryptoSuites), e)).FirstOrDefault();

            if (sdpCryptoAttribute.CryptoSuite == CryptoSuites.unknown)
            {
                throw new FormatException($"cryptoLine '{cryptoLine}' is not recognized as a valid SDP Security Description ");
            }

            var sKeyParams = sCryptoParts[2].Split(SEMI_COLON);
            if (sKeyParams.Length < 1)
            {
                throw new FormatException($"cryptoLine '{cryptoLine}' is not recognized as a valid SDP Security Description ");
            }

            foreach (var kp in sKeyParams)
            {
                var keyParam = KeyParameter.Parse(kp, sdpCryptoAttribute.CryptoSuite);
                sdpCryptoAttribute.KeyParams.Add(keyParam);
            }
            if (sCryptoParts.Length > 3)
            {
                sdpCryptoAttribute.SessionParam = SessionParameter.Parse(sCryptoParts[3], sdpCryptoAttribute.CryptoSuite);
            }

            return sdpCryptoAttribute;
        }
        catch
        {
            //catch all errors and throw own FormatException
        }
        throw new FormatException($"cryptoLine '{cryptoLine}' is not recognized as a valid SDP Security Description ");
    }
}