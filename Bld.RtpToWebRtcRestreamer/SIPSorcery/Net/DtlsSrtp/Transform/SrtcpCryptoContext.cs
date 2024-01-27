using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class SrtcpCryptoContext
{
    /// <summary>
    /// The replay check windows size
    /// </summary>
    private const long REPLAY_WINDOW_SIZE = 64;

    /// <summary>
    /// Index received so far
    /// </summary>
    private int _receivedIndex;

    /// <summary>
    /// Bit mask for replay check
    /// </summary>
    private long _replayWindow;

    /// <summary>
    /// Master encryption key
    /// </summary>
    private readonly byte[] _masterKey;

    /// <summary>
    /// Master salting key
    /// </summary>
    private readonly byte[] _masterSalt;

    /// <summary>
    /// Derived session encryption key
    /// </summary>
    private readonly byte[] _encKey;

    /// <summary>
    /// Derived session authentication key
    /// </summary>
    private readonly byte[] _authKey;

    /// <summary>
    /// Derived session salting key
    /// </summary>
    private readonly byte[] _saltKey;

    /// <summary>
    /// Encryption / Authentication policy for this session
    /// </summary>
    private readonly SrtpPolicy _policy;

    /// <summary>
    /// The HMAC object we used to do packet authentication
    /// used for various HMAC computations
    /// </summary>
    private readonly IMac _mac;

    /// <summary>
    /// The symmetric cipher engines we need here
    /// </summary>
    private readonly IBlockCipher _cipher;

    /// <summary>
    /// Used inside F8 mode only
    /// </summary>
    private readonly IBlockCipher _cipherF8;

    /// <summary>
    /// Implements the counter cipher mode for RTP according to RFC 3711
    /// </summary>
    private readonly SrtpCipherCtr _cipherCtr = new();

    // Here some fields that a allocated here or in constructor. The methods
    // use these fields to avoid too many new operations

    private readonly byte[] _tagStore;
    private readonly byte[] _ivStore = new byte[16];
    private readonly byte[] _rbStore = new byte[4];

    // this is some working store, used by some methods to avoid new operations
    // the methods must use this only to store some results for immediate processing
    private readonly byte[] _tempStore = new byte[100];

    /**
         * Construct a normal SRTPCryptoContext based on the given parameters.
         *
         * @param ssrc
         *            the RTP SSRC that this SRTP cryptographic context protects.
         * @param masterKey
         *            byte array holding the master key for this SRTP cryptographic
         *            context. Refer to chapter 3.2.1 of the RFC about the role of
         *            the master key.
         * @param masterSalt
         *            byte array holding the master salt for this SRTP cryptographic
         *            context. It is used to computer the initialization vector that
         *            in turn is input to compute the session key, session
         *            authentication key and the session salt.
         * @param policy
         *            SRTP policy for this SRTP cryptographic context, defined the
         *            encryption algorithm, the authentication algorithm, etc
         */
    public SrtcpCryptoContext(byte[] masterK, byte[] masterS, SrtpPolicy policyIn)
    {
        _policy = policyIn;
        _masterKey = new byte[_policy.EncKeyLength];
        Array.Copy(masterK, 0, _masterKey, 0, masterK.Length);
        _masterSalt = new byte[_policy.SaltKeyLength];
        Array.Copy(masterS, 0, _masterSalt, 0, masterS.Length);

        switch (_policy.EncType)
        {
            case SrtpPolicy.NullEncryption:
                _encKey = null;
                _saltKey = null;
                break;

            case SrtpPolicy.Aesf8Encryption:
                _cipherF8 = new AesEngine();
                _cipher = new AesEngine();
                _encKey = new byte[_policy.EncKeyLength];
                _saltKey = new byte[_policy.SaltKeyLength];
                break;

            case SrtpPolicy.AescmEncryption:
                _cipher = new AesEngine();
                _encKey = new byte[_policy.EncKeyLength];
                _saltKey = new byte[_policy.SaltKeyLength];
                break;

            case SrtpPolicy.Twofishf8Encryption:
                _cipherF8 = new TwofishEngine();
                _cipher = new TwofishEngine();
                _encKey = new byte[_policy.EncKeyLength];
                _saltKey = new byte[_policy.SaltKeyLength];
                break;

            case SrtpPolicy.TwofishEncryption:
                _cipher = new TwofishEngine();
                _encKey = new byte[_policy.EncKeyLength];
                _saltKey = new byte[_policy.SaltKeyLength];
                break;
        }

        switch (_policy.AuthType)
        {
            case SrtpPolicy.NullAuthentication:
                _authKey = null;
                _tagStore = null;
                break;

            case SrtpPolicy.Hmacsha1Authentication:
                _mac = new HMac(new Sha1Digest());
                _authKey = new byte[_policy.AuthKeyLength];
                _tagStore = new byte[_mac.GetMacSize()];
                break;

            case SrtpPolicy.SkeinAuthentication:
                _authKey = new byte[_policy.AuthKeyLength];
                _tagStore = new byte[_policy.AuthTagLength];
                break;

            default:
                _tagStore = null;
                break;
        }
    }

    /**
         * Transform a SRTCP packet into a RTCP packet.
         * This method is called when a SRTCP packet was received.
         *
         * Operations done by the this operation include:
         * Authentication check, Packet replay check and decryption.
         *
         * Both encryption and authentication functionality can be turned off
         * as long as the SRTPPolicy used in this SRTPCryptoContext requires no
         * encryption and no authentication. Then the packet will be sent out
         * untouched. However this is not encouraged. If no SRTCP feature is enabled,
         * then we shall not use SRTP TransformConnector. We should use the original
         * method (RTPManager managed transportation) instead.
         *
         * @param pkt the received RTCP packet
         * @return true if the packet can be accepted
         *         false if authentication or replay check failed
         */
    public bool ReverseTransformPacket(RawPacket pkt)
    {
        var decrypt = false;
        var tagLength = _policy.AuthTagLength;
        var indexEflag = pkt.GetSrtcpIndex(tagLength);

        if ((indexEflag & 0x80000000) == 0x80000000)
        {
            decrypt = true;
        }

        var index = (int)(indexEflag & ~0x80000000);

        /* Replay control */
        if (!CheckReplay(index))
        {
            return false;
        }

        /* Authenticate the packet */
        if (_policy.AuthType != SrtpPolicy.NullAuthentication)
        {
            // get original authentication data and store in tempStore
            pkt.ReadRegionToBuff(pkt.GetLength() - tagLength, tagLength, _tempStore);

            // Shrink packet to remove the authentication tag and index
            // because this is part of authenticated data
            pkt.Shrink(tagLength + 4);

            // compute, then save authentication in tagStore
            AuthenticatePacket(pkt, indexEflag);

            for (var i = 0; i < tagLength; i++)
            {
                if ((_tempStore[i] & 0xff) == (_tagStore[i] & 0xff))
                {
                    continue;
                }

                return false;
            }
        }

        if (decrypt)
        {
            /* Decrypt the packet using Counter Mode encryption */
            if (_policy.EncType == SrtpPolicy.AescmEncryption || _policy.EncType == SrtpPolicy.TwofishEncryption)
            {
                ProcessPacketAescm(pkt, index);
            }

            /* Decrypt the packet using F8 Mode encryption */
            else if (_policy.EncType == SrtpPolicy.Aesf8Encryption || _policy.EncType == SrtpPolicy.Twofishf8Encryption)
            {
                ProcessPacketAesf8(pkt, index);
            }
        }
        Update(index);
        return true;
    }

    /**
         * Perform Counter Mode AES encryption / decryption
         * @param pkt the RTP packet to be encrypted / decrypted
         */
    private void ProcessPacketAescm(RawPacket pkt, int index)
    {
        long ssrc = pkt.GetRtcpssrc();

        /* Compute the CM IV (refer to chapter 4.1.1 in RFC 3711):
        *
        * k_s   XX XX XX XX XX XX XX XX XX XX XX XX XX XX
        * SSRC              XX XX XX XX
        * index                               XX XX XX XX
        * ------------------------------------------------------XOR
        * IV    XX XX XX XX XX XX XX XX XX XX XX XX XX XX 00 00
        *        0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
        */
        _ivStore[0] = _saltKey[0];
        _ivStore[1] = _saltKey[1];
        _ivStore[2] = _saltKey[2];
        _ivStore[3] = _saltKey[3];

        // The shifts transform the ssrc and index into network order
        _ivStore[4] = (byte)(((ssrc >> 24) & 0xff) ^ _saltKey[4]);
        _ivStore[5] = (byte)(((ssrc >> 16) & 0xff) ^ _saltKey[5]);
        _ivStore[6] = (byte)(((ssrc >> 8) & 0xff) ^ _saltKey[6]);
        _ivStore[7] = (byte)((ssrc & 0xff) ^ _saltKey[7]);

        _ivStore[8] = _saltKey[8];
        _ivStore[9] = _saltKey[9];

        _ivStore[10] = (byte)(((index >> 24) & 0xff) ^ _saltKey[10]);
        _ivStore[11] = (byte)(((index >> 16) & 0xff) ^ _saltKey[11]);
        _ivStore[12] = (byte)(((index >> 8) & 0xff) ^ _saltKey[12]);
        _ivStore[13] = (byte)((index & 0xff) ^ _saltKey[13]);

        _ivStore[14] = _ivStore[15] = 0;

        // Encrypted part excludes fixed header (8 bytes)
        var payloadOffset = 8;
        var payloadLength = pkt.GetLength() - payloadOffset;
        _cipherCtr.Process(_cipher, pkt.GetBuffer(), payloadOffset, payloadLength, _ivStore);
    }

    /**
         * Perform F8 Mode AES encryption / decryption
         *
         * @param pkt the RTP packet to be encrypted / decrypted
         */
    private void ProcessPacketAesf8(RawPacket pkt, int index)
    {
        // byte[] iv = new byte[16];

        // 4 bytes of the iv are zero
        // the first byte of the RTP header is not used.
        _ivStore[0] = 0;
        _ivStore[1] = 0;
        _ivStore[2] = 0;
        _ivStore[3] = 0;

        // Need the encryption flag
        index = (int)(index | 0x80000000);

        // set the index and the encrypt flag in network order into IV
        _ivStore[4] = (byte)(index >> 24);
        _ivStore[5] = (byte)(index >> 16);
        _ivStore[6] = (byte)(index >> 8);
        _ivStore[7] = (byte)index;

        // The fixed header follows and fills the rest of the IV
        var buf = pkt.GetBuffer();
        buf[..8].CopyTo(_ivStore.AsMemory(8, 8));

        // Encrypted part excludes fixed header (8 bytes), index (4 bytes), and
        // authentication tag (variable according to policy)
        var payloadOffset = 8;
        var payloadLength = pkt.GetLength() - (4 + _policy.AuthTagLength);
        SrtpCipherF8.Process(_cipher, pkt.GetBuffer(), payloadOffset, payloadLength, _ivStore, _cipherF8);
    }

    private readonly byte[] _tempBuffer = new byte[RawPacket.RTP_PACKET_MAX_SIZE];

    /**
         * Authenticate a packet.
         *
         * Calculated authentication tag is stored in tagStore area.
         *
         * @param pkt the RTP packet to be authenticated
         */
    private void AuthenticatePacket(RawPacket pkt, int index)
    {
        var buf = pkt.GetBuffer();
        var len = pkt.GetLength();

        buf[..len].CopyTo(_tempBuffer.AsMemory(0, len));

        _mac.BlockUpdate(_tempBuffer, 0, len);
        _rbStore[0] = (byte)(index >> 24);
        _rbStore[1] = (byte)(index >> 16);
        _rbStore[2] = (byte)(index >> 8);
        _rbStore[3] = (byte)index;
        _mac.BlockUpdate(_rbStore, 0, _rbStore.Length);
        _mac.DoFinal(_tagStore, 0);
    }

    /**
         * Checks if a packet is a replayed on based on its sequence number.
         *
         * This method supports a 64 packet history relative to the given
         * sequence number.
         *
         * Sequence Number is guaranteed to be real (not faked) through
         * authentication.
         *
         * @param index index number of the SRTCP packet
         * @return true if this sequence number indicates the packet is not a
         * replayed one, false if not
         */
    private bool CheckReplay(int index)
    {
        // compute the index of previously received packet and its
        // delta to the new received packet
        long delta = index - _receivedIndex;

        if (delta > 0)
        {
            /* Packet not yet received */
            return true;
        }

        if (-delta > REPLAY_WINDOW_SIZE)
        {
            /* Packet too old */
            return false;
        }

        if (((_replayWindow >> (int)-delta) & 0x1) != 0)
        {
            /* Packet already received ! */
            return false;
        }

        /* Packet not yet received */
        return true;
    }

    /**
         * Compute the initialization vector, used later by encryption algorithms,
         * based on the label.
         *
         * @param label label specified for each type of iv
         */
    private void ComputeIv(byte label)
    {
        for (var i = 0; i < 14; i++)
        {
            _ivStore[i] = _masterSalt[i];
        }
        _ivStore[7] ^= label;
        _ivStore[14] = _ivStore[15] = 0;
    }

    /**
         * Derives the srtcp session keys from the master key.
         *
         */
    public void DeriveSrtcpKeys()
    {
        // compute the session encryption key
        byte label = 3;
        ComputeIv(label);

        var encryptionKey = new KeyParameter(_masterKey);
        _cipher.Init(true, encryptionKey);
        Arrays.Fill(_masterKey, 0);

        _cipherCtr.GetCipherStream(_cipher, _encKey, _policy.EncKeyLength, _ivStore);

        if (_authKey != null)
        {
            label = 4;
            ComputeIv(label);
            _cipherCtr.GetCipherStream(_cipher, _authKey, _policy.AuthKeyLength, _ivStore);

            switch (_policy.AuthType)
            {
                case SrtpPolicy.Hmacsha1Authentication:
                    var key = new KeyParameter(_authKey);
                    _mac.Init(key);
                    break;
            }
        }
        Arrays.Fill(_authKey, 0);

        // compute the session salt
        label = 5;
        ComputeIv(label);
        _cipherCtr.GetCipherStream(_cipher, _saltKey, _policy.SaltKeyLength, _ivStore);
        Arrays.Fill(_masterSalt, 0);

        // As last step: initialize cipher with derived encryption key.
        if (_cipherF8 != null)
        {
            SrtpCipherF8.DeriveForIv(_cipherF8, _encKey, _saltKey);
        }
        encryptionKey = new KeyParameter(_encKey);
        _cipher.Init(true, encryptionKey);
        Arrays.Fill(_encKey, 0);
    }


    /**
         * Update the SRTP packet index.
         *
         * This method is called after all checks were successful.
         *
         * @param index index number of the accepted packet
         */
    private void Update(int index)
    {
        var delta = _receivedIndex - index;

        /* update the replay bit mask */
        if (delta > 0)
        {
            _replayWindow = _replayWindow << delta;
            _replayWindow |= 1;
        }
        else
        {
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            _replayWindow |= 1 << delta;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
        }

        _receivedIndex = index;
    }

    /**
         * Derive a new SRTPCryptoContext for use with a new SSRC
         *
         * This method returns a new SRTPCryptoContext initialized with the data of
         * this SRTPCryptoContext. Replacing the SSRC, Roll-over-Counter, and the
         * key derivation rate the application cab use this SRTPCryptoContext to
         * encrypt / decrypt a new stream (Synchronization source) inside one RTP
         * session.
         *
         * Before the application can use this SRTPCryptoContext it must call the
         * deriveSrtpKeys method.
         *
         * @param ssrc
         *            The SSRC for this context
         * @return a new SRTPCryptoContext with all relevant data set.
         */
    public SrtcpCryptoContext DeriveContext()
    {

        var pcc = new SrtcpCryptoContext(_masterKey, _masterSalt, _policy);
        return pcc;
    }
}