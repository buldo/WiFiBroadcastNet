namespace OpenHd.Fec.Tests;

public class MatrixTests
{

    [Test]
    public unsafe void InvertTest1()
    {
        byte[] original =
        {
            06, 24, 01,
            13, 16, 10,
            20, 17, 15
        };

        fixed (byte* ptr = original)
        {
            Matrix.invert_mat(ptr, 3);
        }

        byte[] expected =
        {
            065, 044, 059,
            109, 063, 067,
            075, 218, 252
        };

        CollectionAssert.AreEqual(expected, original);
    }
}