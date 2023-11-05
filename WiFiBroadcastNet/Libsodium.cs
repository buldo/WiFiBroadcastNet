namespace WiFiBroadcastNet;
internal static class Libsodium
{
    static Libsodium()
    {
        //ASodium.SodiumInit.Init();

    }

    public static int crypto_box_NONCEBYTES()
    {
        return (int)SpaceWizards.Sodium.Interop.Libsodium.crypto_box_NONCEBYTES;
    }

    public static int crypto_aead_chacha20poly1305_KEYBYTES()
    {
        return (int)SpaceWizards.Sodium.Interop.Libsodium.crypto_aead_chacha20poly1305_KEYBYTES;
    }

    public static int crypto_box_MACBYTES()
    {
        return (int)SpaceWizards.Sodium.Interop.Libsodium.crypto_box_MACBYTES;
    }

    public static int crypto_box_SECRETKEYBYTES()
    {
        return (int)SpaceWizards.Sodium.Interop.Libsodium.crypto_box_SECRETKEYBYTES;
    }

    public static int crypto_box_PUBLICKEYBYTES()
    {
        return (int)SpaceWizards.Sodium.Interop.Libsodium.crypto_box_PUBLICKEYBYTES;
    }

    public static int crypto_box_SEEDBYTES()
    {
        return (int)SpaceWizards.Sodium.Interop.Libsodium.crypto_box_SEEDBYTES;
    }

    public static ulong crypto_pwhash_OPSLIMIT_INTERACTIVE()
    {
        return SpaceWizards.Sodium.Interop.Libsodium.crypto_pwhash_OPSLIMIT_INTERACTIVE;
    }

    public static UIntPtr crypto_pwhash_MEMLIMIT_INTERACTIVE()
    {
        return SpaceWizards.Sodium.Interop.Libsodium.crypto_pwhash_MEMLIMIT_INTERACTIVE;
    }

    public static int crypto_pwhash_ALG_DEFAULT()
    {
        return SpaceWizards.Sodium.Interop.Libsodium.crypto_pwhash_ALG_DEFAULT;
    }
}
