namespace SpaceWizards.Sodium.Interop;

public partial class Libsodium
{
    public static ulong sodium_base64_ENCODED_LEN(ulong BIN_LEN, ulong VARIANT)
    {
        return (((BIN_LEN) / 3U) * 4U +
                ((((BIN_LEN) - ((BIN_LEN) / 3U) * 3U) | (((BIN_LEN) - ((BIN_LEN) / 3U) * 3U) >> 1)) & 1U) *
                (4U - (~((((VARIANT) & 2U) >> 1) - 1U) & (3U - ((BIN_LEN) - ((BIN_LEN) / 3U) * 3U)))) + 1U);
    }
}
