using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal static class SrtpCipherF8
{
    /**
         * AES block size, just a short name.
         */
    private const int Blklen = 16;

    /**
         * F8 mode encryption context, see RFC3711 section 4.1.2 for detailed
         * description.
         */
    private class F8Context
    {
        public byte[] S;
        public byte[] IvAccent;
        public long J;
    }

    public static void DeriveForIv(IBlockCipher f8Cipher, byte[] key, byte[] salt)
    {
        /*
         * Get memory for the special key. This is the key to compute the
         * derived IV (IV').
         */
        var saltMask = new byte[key.Length];
        var maskedKey = new byte[key.Length];

        /*
         * First copy the salt into the mask field, then fill with 0x55 to get a
         * full key.
         */
        Array.Copy(salt, 0, saltMask, 0, salt.Length);
        for (var i = salt.Length; i < saltMask.Length; ++i)
        {
            saltMask[i] = 0x55;
        }

        /*
         * XOR the original key with the above created mask to get the special
         * key.
         */
        for (var i = 0; i < key.Length; i++)
        {
            maskedKey[i] = (byte)(key[i] ^ saltMask[i]);
        }

        /*
         * Prepare the f8Cipher with the special key to compute IV'
         */
        var encryptionKey = new KeyParameter(maskedKey);
        f8Cipher.Init(true, encryptionKey);
    }

    public static void Process(IBlockCipher cipher, Memory<byte> data, int off, int len,
        byte[] iv, IBlockCipher f8Cipher)
    {
        var f8Ctx = new F8Context();

        /*
         * Get memory for the derived IV (IV')
         */
        f8Ctx.IvAccent = new byte[Blklen];

        /*
         * Use the derived IV encryption setup to encrypt the original IV to produce IV'.
         */
        f8Cipher.ProcessBlock(iv, 0, f8Ctx.IvAccent, 0);

        f8Ctx.J = 0; // initialize the counter
        f8Ctx.S = new byte[Blklen]; // get the key stream buffer

        Arrays.Fill(f8Ctx.S, 0);

        var inLen = len;

        while (inLen >= Blklen)
        {
            ProcessBlock(cipher, f8Ctx, data, off, Blklen);
            inLen -= Blklen;
            off += Blklen;
        }

        if (inLen > 0)
        {
            ProcessBlock(cipher, f8Ctx, data, off, inLen);
        }
    }

    /**
         * Encrypt / Decrypt a block using F8 Mode AES algorithm, read len bytes
         * data from in at inOff and write the output into out at outOff
         *
         * @param f8ctx
         *            F8 encryption context
         * @param in
         *            byte array holding the data to be processed
         * @param inOff
         *            start offset of the data to be processed inside in array
         * @param out
         *            byte array that will hold the processed data
         * @param outOff
         *            start offset of output data in out
         * @param len
         *            length of the input data
         */
    private static void ProcessBlock(
        IBlockCipher cipher,
        F8Context f8Ctx,
        Memory<byte> @in,
        int inOff,
        int len)
    {
        /*
         * XOR the previous key stream with IV'
         * ( S(-1) xor IV' )
         */
        for (var i = 0; i < Blklen; i++)
        {
            f8Ctx.S[i] ^= f8Ctx.IvAccent[i];
        }

        /*
         * Now XOR (S(n-1) xor IV') with the current counter, then increment
         * the counter
         */
        f8Ctx.S[12] ^= (byte)(f8Ctx.J >> 24);
        f8Ctx.S[13] ^= (byte)(f8Ctx.J >> 16);
        f8Ctx.S[14] ^= (byte)(f8Ctx.J >> 8);
        f8Ctx.S[15] ^= (byte)f8Ctx.J;
        f8Ctx.J++;

        /*
         * Now compute the new key stream using AES encrypt
         */
        cipher.ProcessBlock(f8Ctx.S, 0, f8Ctx.S, 0);

        /*
         * As the last step XOR the plain text with the key stream to produce
         * the cipher text.
         */
        for (var i = 0; i < len; i++)
        {
            var position = inOff + i;
            var inByte = @in.Span[position];
            @in.Span[position] = (byte)(inByte ^ f8Ctx.S[i]);
        }
    }
}