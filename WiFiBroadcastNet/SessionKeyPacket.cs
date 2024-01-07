namespace WiFiBroadcastNet;

internal class SessionKeyPacket
{
    private static readonly int _cryptoBoxNonceBytes = Libsodium.crypto_box_NONCEBYTES();
    private static readonly int _cryptoAeadChacha20Poly1305KeyBytes = Libsodium.crypto_aead_chacha20poly1305_KEYBYTES();
    private static readonly int _cryptoBoxMacBytes = Libsodium.crypto_box_MACBYTES();

    private readonly ReadOnlyMemory<byte> _payload;

    public SessionKeyPacket(ReadOnlyMemory<byte> payload)
    {
        _payload = payload;
    }

    public bool IsValid => _payload.Length ==
                           _cryptoBoxNonceBytes + _cryptoAeadChacha20Poly1305KeyBytes + _cryptoBoxMacBytes;

    public ReadOnlySpan<byte> SessionKeyNonce => _payload.Span.Slice(0, _cryptoBoxNonceBytes); // random data

    public ReadOnlySpan<byte> SessionKeyData => _payload.Span.Slice(_cryptoBoxNonceBytes, _cryptoAeadChacha20Poly1305KeyBytes + _cryptoBoxMacBytes); // encrypted session key
};