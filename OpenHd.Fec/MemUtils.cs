namespace OpenHd.Fec;

public static class MemUtils
{
    public static unsafe void* memcpy(byte* dest, byte* src, nint len)
    {
        byte* d = dest;
        byte* s = src;
        while (len-- != 0)
        {
            *d++ = *s++;
        }
        return dest;
    }

    public static unsafe void* memset(byte* dest, byte val, nint len)
    {
        byte* ptr = dest;
        while (len-- > 0)
        {
            *ptr++ = val;
        }
        return dest;
    }

    public static unsafe int memcmp(byte* str1, byte* str2, nint count)
    {
        byte* s1 = str1;
        byte* s2 = str2;

        while (count-- > 0)
        {
            if (*s1++ != *s2++)
            {
                return s1[-1] < s2[-1]? -1 : 1;
            }
        }
        return 0;
    }
}