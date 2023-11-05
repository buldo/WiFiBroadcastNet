namespace WiFiBroadcastNet;

public class SessionKeyPacket
{
    private readonly RxFrame _frame;
    private static int crypto_box_NONCEBYTES = (int)Libsodium.crypto_box_NONCEBYTES();
    private static int crypto_aead_chacha20poly1305_KEYBYTES = (int)Libsodium.crypto_aead_chacha20poly1305_KEYBYTES();
    private static int crypto_box_MACBYTES = (int)Libsodium.crypto_box_MACBYTES();

    public SessionKeyPacket(RxFrame frame)
    {
        _frame = frame;
    }

    public bool IsValid => _frame.Payload.Length ==
                           crypto_box_NONCEBYTES + crypto_aead_chacha20poly1305_KEYBYTES + crypto_box_MACBYTES;

    public Span<byte> sessionKeyNonce => _frame.Payload.Slice(0, crypto_box_NONCEBYTES); // random data

    public Span<byte> sessionKeyData => _frame.Payload.Slice(crypto_box_NONCEBYTES, crypto_aead_chacha20poly1305_KEYBYTES + crypto_box_MACBYTES); // encrypted session key
};