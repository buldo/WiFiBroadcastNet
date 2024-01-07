namespace SpaceWizards.Sodium.Interop
{
    public unsafe partial struct randombytes_implementation
    {
        [NativeTypeName("const char *(*)()")]
        public delegate* unmanaged[Cdecl]<sbyte*> implementation_name;

        [NativeTypeName("uint32_t (*)()")]
        public delegate* unmanaged[Cdecl]<uint> random;

        [NativeTypeName("void (*)()")]
        public delegate* unmanaged[Cdecl]<void> stir;

        [NativeTypeName("uint32_t (*)(const uint32_t)")]
        public delegate* unmanaged[Cdecl]<uint, uint> uniform;

        [NativeTypeName("void (*)(void *const, const size_t)")]
        public delegate* unmanaged[Cdecl]<void*, nuint, void> buf;

        [NativeTypeName("int (*)()")]
        public delegate* unmanaged[Cdecl]<int> close;
    }
}
