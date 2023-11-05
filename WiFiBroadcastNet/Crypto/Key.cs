namespace WiFiBroadcastNet.Crypto;

internal class Key
{
    public byte[] public_key = new byte[Libsodium.crypto_box_PUBLICKEYBYTES()];
    public byte[] secret_key = new byte[Libsodium.crypto_box_SECRETKEYBYTES()];
};