// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006

namespace OpenHd.Fec;

public static class FecDecodeImpl
{
    public const bool FRAGMENT_STATUS_UNAVAILABLE = false;
    public const bool FRAGMENT_STATUS_AVAILABLE = true;

    /// <param name="fragmentSize">size of each fragment</param>
    /// <param name="blockBuffer">blockBuffer (big) data buffer. The nth element is to be treated as the nth fragment of the block, either as primary or secondary fragment.</param>
    /// <param name="nPrimaryFragments">n of primary fragments used during encode step</param>
    /// <param name="fragmentStatusList">
    /// information which (primary or secondary fragments) were received.
    /// values from [0,nPrimaryFragments[ are treated as primary fragments, values from [nPrimaryFragments,size[ are treated as secondary fragments.
    /// </param>
    /// <returns>indices of reconstructed primary fragments</returns>
    public static List<int> fecDecode(int fragmentSize,
        List<byte[]> blockBuffer,
        int nPrimaryFragments,
        List<bool> fragmentStatusList)
    {
        //assert(fragmentSize <= S);
        //assert(fragmentStatusList.size() <= blockBuffer.size());
        //assert(fragmentStatusList.size() == blockBuffer.size());
        List<int> indicesMissingPrimaryFragments = new();
        List<byte[]> primaryFragmentP = new(nPrimaryFragments);
        for (int idx = 0; idx < nPrimaryFragments; idx++)
        {
            if (fragmentStatusList[idx] == FRAGMENT_STATUS_UNAVAILABLE)
            {
                indicesMissingPrimaryFragments.Add(idx);
            }

            primaryFragmentP.Add(blockBuffer[idx]);
        }

        // find enough secondary fragments
        List<byte[]> secondaryFragmentP = new();
        List<int> secondaryFragmentIndices = new();
        for (int i = 0; i < fragmentStatusList.Count - nPrimaryFragments; i++)
        {
            var idx = nPrimaryFragments + i;
            if (fragmentStatusList[idx] == FRAGMENT_STATUS_AVAILABLE)
            {
                secondaryFragmentP.Add(blockBuffer[idx]);
                secondaryFragmentIndices.Add(i);
            }
        }

        // make sure we got enough secondary fragments
        //assert(secondaryFragmentP.size() >= indicesMissingPrimaryFragments.size());
        // assert if fecDecode is called too late (e.g. more secondary fragments than needed for fec
        //assert(indicesMissingPrimaryFragments.size() == secondaryFragmentP.size());
        // do fec step
        fec_decode2(fragmentSize,
            primaryFragmentP,
            indicesMissingPrimaryFragments,
            secondaryFragmentP,
            secondaryFragmentIndices);
        return indicesMissingPrimaryFragments;
    }

    private static void fec_decode2(int fragmentSize,
        List<byte[]> primaryFragments,
        List<int> indicesMissingPrimaryFragments,
        List<byte[]> secondaryFragmentsReceived,
        List<int> indicesOfSecondaryFragmentsReceived)
    {
        foreach (var idx in indicesMissingPrimaryFragments)
        {
            //assert(idx<primaryFragments.size());
        }

        //This assertion is not always true - as an example,you might have gotten FEC secondary packets 0 and 4, but these 2 are enough to perform the fec step.
        //Then packet index 0 is inside @param secondaryFragmentsReceived at position 0, but packet index 4 at position 1
        //for(const auto& idx:indicesOfSecondaryFragmentsReceived){
        //    assert(idx<secondaryFragmentsReceived.size());
        //}
        //assert(indicesMissingPrimaryFragments.size() <= indicesOfSecondaryFragmentsReceived.size());
        //assert(indicesMissingPrimaryFragments.size() == secondaryFragmentsReceived.size());
        //assert(secondaryFragmentsReceived.size() == indicesOfSecondaryFragmentsReceived.size());
        fec_decode(
            fragmentSize,
            primaryFragments,
            primaryFragments.Count,
            secondaryFragmentsReceived,
            indicesOfSecondaryFragmentsReceived,
            indicesMissingPrimaryFragments,
            indicesMissingPrimaryFragments.Count);
    }


    private static void fec_decode(
        int blockSize,
        List<byte[]> data_blocks,
        int nr_data_blocks,
        List<byte[]> fec_blocks,
        List<int> fec_block_nos,
        List<int> erased_blocks,
        int nr_fec_blocks)
    {
        reduce(
            blockSize,
            data_blocks,
            nr_data_blocks,
            fec_blocks,
            fec_block_nos,
            erased_blocks,
            nr_fec_blocks);
        //
        resolve(
            blockSize,
            data_blocks,
            fec_blocks,
            fec_block_nos,
            erased_blocks,
            nr_fec_blocks);
    }

    /**
 * Reduce the system by substracting all received data blocks from FEC blocks
 * This will allow to resolve the system by inverting a much smaller matrix
 * (with size being number of blocks lost, rather than number of data blocks
 * + fec)
 */
    private static unsafe void reduce(
        int blockSize,
        List<byte[]> data_blocks,
        int nr_data_blocks,
        List<byte[]> fec_blocks,
        List<int> fec_block_nos,
        List<int> erased_blocks,
        int nr_fec_blocks)
    {
        int erasedIdx = 0;
        int col;

        /* First we reduce the code vector by substracting all known elements
         * (non-erased data packets) */
        for (col = 0; col < nr_data_blocks; col++)
        {
            if (erasedIdx < nr_fec_blocks && erased_blocks[erasedIdx] == col)
            {
                erasedIdx++;
            }
            else
            {
                var src = data_blocks[col];
                int j;
                for (j = 0; j < nr_fec_blocks; j++)
                {
                    int blno = fec_block_nos[j];
                    fixed (byte* fec_blocksPtr = fec_blocks[j])
                    fixed (byte* srcPtr = src)
                    {
                        Gf256Optimized.gf256_madd_optimized(fec_blocksPtr, srcPtr, Gf256Optimized.gf256_inverse(blno ^ col ^ 128), blockSize);
                    }
                }
            }
        }

        //assert(nr_fec_blocks == erasedIdx);
    }


    /// <summary>
    /// Resolves reduced system. Constructs "mini" encoding matrix, inverts it, and multiply reduced vector by it.
    /// </summary>
    private static unsafe void resolve(
        int blockSize,
        List<byte[]> data_blocks,
        List<byte[]> fec_blocks,
        List<int> fec_block_nos,
        List<int> erased_blocks,
        int nr_fec_blocks)
    {
        /* construct matrix */
        int row;
        var matrix = new byte[nr_fec_blocks * nr_fec_blocks];
        int ptr;
        int r;

        /* we pick the submatrix of code that keeps colums corresponding to
         * the erased data blocks, and rows corresponding to the present FEC
         * blocks. This is the matrix by which we would need to multiply the
         * missing data blocks to obtain the FEC blocks we have */
        for (row = 0, ptr = 0; row < nr_fec_blocks; row++)
        {
            int col;
            int irow = 128 + fec_block_nos[row];
            /*assert(irow < fec_blocks+128);*/
            for (col = 0; col < nr_fec_blocks; col++, ptr++)
            {
                int icol = erased_blocks[col];
                matrix[ptr] = Gf256Optimized.gf256_inverse(irow ^ icol);
            }
        }

        fixed (byte* matrixPtr = matrix)
        {
            r = Matrix.invert_mat(matrixPtr, nr_fec_blocks);
        }

        //if (r)
        //{
        //    int col;
        //    fprintf(stderr, "Pivot not found\n");
        //    fprintf(stderr, "Rows: ");
        //    for (row = 0; row < nr_fec_blocks; row++)
        //        fprintf(stderr, "%d ", 128 + fec_block_nos[row]);
        //    fprintf(stderr, "\n");
        //    fprintf(stderr, "Columns: ");
        //    for (col = 0; col < nr_fec_blocks; col++, ptr++)
        //        fprintf(stderr, "%d ", erased_blocks[col]);
        //    fprintf(stderr, "\n");
        //    assert(0);
        //}

        /* do the multiplication with the reduced code vector */
        for (row = 0, ptr = 0; row < nr_fec_blocks; row++)
        {
            int col;
            var target = data_blocks[erased_blocks[row]];

            fixed (byte* targetPtr = target)
            fixed (byte* fec_blockPtr = fec_blocks[0])
            {
                Gf256Optimized.gf256_mul_optimized(targetPtr, fec_blockPtr, matrix[ptr++], blockSize);
            }

            fixed (byte* targetPtr = target)
            {
                for (col = 1; col < nr_fec_blocks; col++, ptr++)
                {
                    fixed (byte* fec_blockPtr = fec_blocks[col])
                    {
                        Gf256Optimized.gf256_madd_optimized(targetPtr, fec_blockPtr, matrix[ptr], blockSize);
                    }
                }
            }
        }
    }
}