using System.Text;

namespace WiFiBroadcastNet.Crypto;

internal static class CryptoHelpers
{
    private static byte[] OHD_SALT_AIR =  { 192,189,216,102,56,153,154,92,228,26,49,209,157,7,128,207};
    private static byte[] OHD_SALT_GND = { 179,30,150,20,17,200,225,82,48,64,18,130,89,62,83,234};

    public const string DEFAULT_BIND_PHRASE = "openhd";

    public static KeyPairTxRx generate_keypair_from_bind_phrase(string bind_phrase = DEFAULT_BIND_PHRASE)
    {
        var seed_air = create_seed_from_password_openhd_salt(bind_phrase, true);
        var seed_gnd = create_seed_from_password_openhd_salt(bind_phrase, false);
        KeyPairTxRx ret = new();
        unsafe
        {
            fixed (byte* public_keyPtr = ret.key_1.public_key)
            fixed(byte* secret_keyPtr =  ret.key_1.secret_key)
            fixed(byte* seed_airPtr = seed_air)
            {
                SpaceWizards.Sodium.Interop.Libsodium.crypto_box_seed_keypair(public_keyPtr, secret_keyPtr, seed_airPtr);
            }

            fixed (byte* public_keyPtr = ret.key_2.public_key)
            fixed (byte* secret_keyPtr = ret.key_2.secret_key)
            fixed (byte* seed_gndPtr = seed_gnd)
            {

                SpaceWizards.Sodium.Interop.Libsodium.crypto_box_seed_keypair(public_keyPtr, secret_keyPtr, seed_gndPtr);
            }
        }

        return ret;
    }

    private static byte[] create_seed_from_password_openhd_salt(string pw, bool use_salt_air)
    {
        var salt = use_salt_air ? OHD_SALT_AIR : OHD_SALT_GND;
        byte[] key = new byte[Libsodium.crypto_box_SEEDBYTES()];

        var pwAnsi = Encoding.ASCII.GetBytes(pw);

        unsafe
        {
            fixed(byte* keyPtr = key)
            fixed(byte* pwAnsiPtr = pwAnsi)
            fixed(byte* saltPtr = salt)
            {
                if (SpaceWizards.Sodium.Interop.Libsodium.crypto_pwhash(
                        keyPtr, (ulong)key.LongLength,
                        (sbyte*)pwAnsiPtr, (ulong)pwAnsi.Length,
                        saltPtr,
                        Libsodium.crypto_pwhash_OPSLIMIT_INTERACTIVE(),
                        Libsodium.crypto_pwhash_MEMLIMIT_INTERACTIVE(),
                        Libsodium.crypto_pwhash_ALG_DEFAULT()) != 0)
                {
                    Console.WriteLine("ERROR: cannot create_seed_from_password_openhd_salt");
                }
            }

        }

        return key;
    }
}