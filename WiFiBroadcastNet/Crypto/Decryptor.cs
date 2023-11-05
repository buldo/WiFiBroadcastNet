﻿using Microsoft.Extensions.Logging;

namespace WiFiBroadcastNet.Crypto;

internal class Decryptor
{
    private readonly ILogger _logger;

    //private byte[] rx_secretkey = new byte[Libsodium.crypto_box_SECRETKEYBYTES()];
    //private byte[] tx_publickey = new byte[Libsodium.crypto_box_PUBLICKEYBYTES()];
    private readonly byte[] rx_secretkey;
    private readonly byte[] tx_publickey;

    private byte[] session_key = new byte[Libsodium.crypto_aead_chacha20poly1305_KEYBYTES()];

    public Decryptor(ILogger logger, Key key)
    {
        rx_secretkey = key.secret_key;
        tx_publickey = key.public_key;
        _logger = logger;
    }

    public DecryptorResult onNewPacketSessionKeyData(Span<byte> sessionKeyNonce, Span<byte> sessionKeyData)
    {
        var new_session_key = new byte[session_key.Length];
        unsafe
        {
            fixed (byte* new_session_keyPtr = new_session_key)
            fixed (byte* sessionKeyDataPtr = sessionKeyData)
            fixed (byte* sessionKeyNoncePtr = sessionKeyNonce)
            fixed (byte* tx_publickeyPtr = tx_publickey)
            fixed (byte* rx_secretkeyPtr = rx_secretkey)
            {
                if (SpaceWizards.Sodium.Interop.Libsodium.crypto_box_open_easy(
                        new_session_keyPtr,
                        sessionKeyDataPtr, (ulong)sessionKeyData.Length,
                        sessionKeyNoncePtr,
                        tx_publickeyPtr, rx_secretkeyPtr) != 0)
                {
                    // this basically should just never happen, and is an error
                    _logger.LogWarning("unable to decrypt session key");
                    return DecryptorResult.SESSION_NOT_VALID;
                }
            }
        }

        if (!session_key.SequenceEqual(new_session_key))
        {
            _logger.LogInformation("Decryptor-New session detected");
            session_key = new_session_key;
            return DecryptorResult.SESSION_VALID_NEW;
        }

        // this is NOT an error, the same session key is sent multiple times !
        return DecryptorResult.SESSION_VALID_NOT_NEW;
    }
}