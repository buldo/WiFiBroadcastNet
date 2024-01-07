namespace SpaceWizards.Sodium.Interop
{
    public unsafe partial struct crypto_aead_aes256gcm_state
    {
        [NativeTypeName("unsigned char [512]")]
        public fixed byte opaque[512];
    }
}
