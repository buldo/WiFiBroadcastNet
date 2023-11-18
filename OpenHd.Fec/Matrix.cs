namespace OpenHd.Fec;

public static class Matrix
{
    /// <summary>
    /// takes a matrix and produces its inverse
    /// </summary>
    /// <param name="src">matrix</param>
    /// <param name="k">The size of the matrix</param>
    /// <returns>non-zero if singular</returns>
    /// <remarks>Gauss-Jordan, adapted from Numerical Recipes in C</remarks>
    public static int invert_mat(Span<byte> src, int k)
    {
        int error = 1;
        int[] indxc = new int[k];
        int[] indxr = new int[k];
        int[] ipiv = new int[k];
        byte[] id_row = new byte[k];

        //memset(id_row, 0, k * sizeof(byte));
        //DEB(pivloops = 0; pivswaps = 0; /* diagnostic */ )
        /*
         * ipiv marks elements already used as pivots.
         */
        //for (int i = 0; i < k; i++)
        //{
        //    ipiv[i] = 0;
        //}

        for (int col = 0; col < k; col++)
        {
            /*
             * Zeroing column 'col', look for a non-zero element.
             * First try on the diagonal, if it fails, look elsewhere.
             */
            int icol;
            var irow = icol = -1;
            if (ipiv[col] != 1 && src[col * k + col] != 0)
            {
                irow = col;
                icol = col;
                goto found_piv;
            }

            for (int row = 0; row < k; row++)
            {
                if (ipiv[row] != 1)
                {
                    for (var ix = 0; ix < k; ix++)
                    {
                        //DEB(pivloops++;)
                        if (ipiv[ix] == 0)
                        {
                            if (src[row * k + ix] != 0)
                            {
                                irow = row;
                                icol = ix;
                                goto found_piv;
                            }
                        }
                        else if (ipiv[ix] > 1)
                        {
                            //fprintf(stderr, "singular matrix\n");
                            goto fail;
                        }
                    }
                }
            }

            if (icol == -1)
            {
                //fprintf(stderr, "XXX pivot not found!\n");
                goto fail;
            }

        found_piv:
            ++(ipiv[icol]);

            /*
             * swap rows irow and icol, so afterwards the diagonal
             * element will be correct. Rarely done, not worth
             * optimizing.
             */
            if (irow != icol)
            {
                for (var ix = 0; ix < k; ix++)
                {
                    Swap(ref src[irow * k + ix], ref src[icol * k + ix]);
                }
            }

            indxr[col] = irow;
            indxc[col] = icol;
            var pivot_row = src.Slice(icol * k, k);
            var c = pivot_row[icol];
            if (c == 0)
            {
                Console.WriteLine("singular matrix 2");
                goto fail;
            }

            if (c != 1)
            {
                /* otherwhise this is a NOP */
                /*
                 * this is done often , but optimizing is not so
                 * fruitful, at least in the obvious ways (unrolling)
                 */
                //DEB(pivswaps++;)
                c = Gf256Optimized.gf256_inverse(c);
                pivot_row[icol] = 1;
                for (var ix = 0; ix < k; ix++)
                {
                    pivot_row[ix] = Gf256Optimized.gf256_mul(c, pivot_row[ix]);
                }
            }

            /*
             * from all rows, remove multiples of the selected row
             * to zero the relevant entry (in fact, the entry is not zero
             * because we know it must be zero).
             * (Here, if we know that the pivot_row is the identity,
             * we can optimize the addmul).
             */
            id_row[icol] = 1;

            var cmpResult = pivot_row.SequenceEqual(id_row);

            if (!cmpResult)
            {
                for (var ix = 0; ix < k; ix++ )
                {
                    var p  = src.Slice(k * ix, k);
                    if (ix != icol)
                    {
                        c = p[icol];
                        p[icol] = 0;
                        Gf256Optimized.gf256_madd_optimized(p, pivot_row, c);
                    }
                }
            }

            id_row[icol] = 0;
        } /* done all columns */

        for (int col = k - 1; col >= 0; col--)
        {
            if (indxr[col] < 0 || indxr[col] >= k)
            {
                Console.WriteLine($"AARGH, indxr[col] {indxr[col]}");
            }
            else if (indxc[col] < 0 || indxc[col] >= k)
            {
                Console.WriteLine($"AARGH, indxc[col] {indxc[col]}");
            }
            else if (indxr[col] != indxc[col])
            {
                for (int row = 0; row < k; row++)
                {
                    Swap(ref src[row * k + indxr[col]], ref src[row * k + indxc[col]]);
                }
            }
        }

        error = 0;
    fail:
        return error;
    }

    private static void Swap<T>(ref T lhs, ref T rhs)
    {
        T temp;
        temp = lhs;
        lhs = rhs;
        rhs = temp;
    }
}