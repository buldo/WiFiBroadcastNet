namespace OpenHd.Fec.Tests;

public class MemUtilsTests
{

    [Test]
    public unsafe void memcpyTest1()
    {
        byte[] first = new byte[5];
        byte[] second = { 0x01, 0x02, 0x03, 0x04 };

        fixed (byte* firstPtr = first)
        fixed (byte* secondPtr = second)
        {
            MemUtils.memcpy(firstPtr, secondPtr, 4);
        }

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x00 }, first);

        Assert.Pass();
    }

    [Test]
    public unsafe void memcpyTest2()
    {
        byte[] first = new byte[5];
        byte[] second = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        fixed (byte* firstPtr = first)
        fixed (byte* secondPtr = second)
        {
            MemUtils.memcpy(firstPtr, secondPtr, 5);
        }

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, first);

        Assert.Pass();
    }

    [Test]
    public unsafe void memsetTest1()
    {
        byte[] first = new byte[5];

        fixed (byte* firstPtr = first)
        {
            MemUtils.memset(firstPtr, 0x03, 4);
        }

        CollectionAssert.AreEqual(new byte[] { 0x03, 0x03, 0x03, 0x03, 0x00 }, first);

        Assert.Pass();
    }

    [Test]
    public unsafe void memcmpTest1()
    {
        byte[] first = { 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] second = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        int result = -10;

        fixed (byte* firstPtr = first)
        fixed (byte* secondPtr = second)
        {
            result = MemUtils.memcmp(firstPtr, secondPtr, 5);
        }

        Assert.AreEqual(0, result);
    }

    [Test]
    public unsafe void memcmpTest2()
    {
        byte[] first = { 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] second = { 0x01, 0x03, 0x03, 0x04, 0x05 };

        int result = -10;

        fixed (byte* firstPtr = first)
        fixed (byte* secondPtr = second)
        {
            result = MemUtils.memcmp(firstPtr, secondPtr, 5);
        }

        Assert.AreNotEqual(0, result);
    }
}