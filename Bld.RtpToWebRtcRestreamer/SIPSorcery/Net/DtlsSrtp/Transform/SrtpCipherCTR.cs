using System.Buffers;

using Org.BouncyCastle.Crypto;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class SrtpCipherCtr
{
    private const int BLOCK_LENGTH = 16;
    private const int MAX_BUFFER_LENGTH = 10 * 1024;
    private readonly ArrayPool<byte> _bytesPool = ArrayPool<byte>.Shared;
    private byte[] _streamBuf = new byte[1024];

    public void Process(IBlockCipher cipher, Memory<byte> data, int off, int len, ReadOnlySpan<byte> iv)
    {
        // if data fits in inner buffer - use it. Otherwise allocate bigger
        // buffer store it to use it for later processing - up to a defined
        // maximum size.
        byte[] cipherStream;
        if (len > _streamBuf.Length)
        {
            cipherStream = new byte[len];
            if (cipherStream.Length <= MAX_BUFFER_LENGTH)
            {
                _streamBuf = cipherStream;
            }
        }
        else
        {
            cipherStream = _streamBuf;
        }

        GetCipherStream(cipher, cipherStream, len, iv);
        for (var i = 0; i < len; i++)
        {
            var position = i + off;
            var byteToWrite = data.Span[position];
            data.Span[position] = (byte)(byteToWrite ^ cipherStream[i]);
        }
    }

    /// <summary>
    /// Computes the cipher strea for AES CM mode. See section 4.1.1 in RFC3711 for detailed description.
    /// </summary>
    /// <param name="aesCipher"></param>
    /// <param name="output">byte array holding the output cipher stream</param>
    /// <param name="length">length of the cipher stream to produce, in bytes</param>
    /// <param name="iv">initialization vector used to generate this cipher stream</param>
    public void GetCipherStream(IBlockCipher aesCipher, Span<byte> output, int length, ReadOnlySpan<byte> iv)
    {
        var cipherInBlockArray = _bytesPool.Rent(BLOCK_LENGTH);
        var cipherInBlock = cipherInBlockArray.AsSpan(0, BLOCK_LENGTH);
        var tmpCipherBlockArray = _bytesPool.Rent(BLOCK_LENGTH);
        var tmpCipherBlock = tmpCipherBlockArray.AsSpan(0, BLOCK_LENGTH);
        try
        {
            iv[..14].CopyTo(cipherInBlock[..14]);

            int ctr;
            for (ctr = 0; ctr < length / BLOCK_LENGTH; ctr++)
            {
                // compute the cipher stream
                cipherInBlock[14] = (byte)((ctr & 0xFF00) >> 8);
                cipherInBlock[15] = (byte)(ctr & 0x00FF);

                aesCipher.ProcessBlock(cipherInBlock, output[(ctr * BLOCK_LENGTH)..]);
            }

            // Treat the last bytes:
            cipherInBlock[14] = (byte)((ctr & 0xFF00) >> 8);
            cipherInBlock[15] = (byte)(ctr & 0x00FF);

            aesCipher.ProcessBlock(cipherInBlock, tmpCipherBlock);
            tmpCipherBlock[..(length % BLOCK_LENGTH)].CopyTo(output.Slice(ctr * BLOCK_LENGTH, length % BLOCK_LENGTH));
        }
        finally
        {
            _bytesPool.Return(cipherInBlockArray);
            _bytesPool.Return(tmpCipherBlockArray);
        }
    }
}