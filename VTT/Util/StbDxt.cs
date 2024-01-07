// stb_dxt.h - v1.12 - DXT1/DXT5 compressor - public domain
// original by fabian "ryg" giesen - ported to C by stb
// use '#define STB_DXT_IMPLEMENTATION' before including to create the implementation
//
// USAGE:
//   call stb_compress_dxt_block() for every block (you must pad)
//     source should be a 4x4 block of RGBA data in row-major order;
//     Alpha channel is not stored if you specify alpha=0 (but you
//     must supply some constant alpha in the alpha channel).
//     You can turn on dithering and "high quality" using mode.
//
// version history:
//   v1.12  - (ryg) fix bug in single-color table generator
//   v1.11  - (ryg) avoid racy global init, better single-color tables, remove dither
//   v1.10  - (i.c) various small quality improvements
//   v1.09  - (stb) update documentation re: surprising alpha channel requirement
//   v1.08  - (stb) fix bug in dxt-with-alpha block
//   v1.07  - (stb) bc4; allow not using libc; add STB_DXT_STATIC
//   v1.06  - (stb) fix to known-broken 1.05
//   v1.05  - (stb) support bc5/3dc (Arvids Kokins), use extern "C" in C++ (Pavel Krajcevski)
//   v1.04  - (ryg) default to no rounding bias for lerped colors (as per S3TC/DX10 spec);
//            single color match fix (allow for inexact color interpolation);
//            optimal DXT5 index finder; "high quality" mode that runs multiple refinement steps.
//   v1.03  - (stb) endianness support
//   v1.02  - (stb) fix alpha encoding bug
//   v1.01  - (stb) fix bug converting to RGB that messed up quality, thanks ryg & cbloom
//   v1.00  - (stb) first release
//
// contributors:
//   Rich Geldreich (more accurate index selection)
//   Kevin Schmidt (#defines for "freestanding" compilation)
//   github:ppiastucki (BC4 support)
//   Ignacio Castano - improve DXT endpoint quantization
//   Alan Hickman - static table initialization
//
// LICENSE
//
//   See end of file for license information.

// compression mode (bitflags)
#define STB_DXT_USE_ROUNDING_BIAS

namespace VTT.Util;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Runtime.InteropServices;

[Flags]
public enum CompressionMode
{
    Normal = 0,
    Quality = 2
}

public static unsafe class StbDxt
{
    private static readonly byte[][] OMatch5 = {
        new byte[]{ 0,  0 },  new byte[]{ 0,  0 },  new byte[]{ 0,  1 },  new byte[]{ 0,  1 },  new byte[]{ 1,  0 },  new byte[]{ 1,  0 },  new byte[]{ 1,  0 },  new byte[]{ 1,  1 },
        new byte[]{ 1,  1 },  new byte[]{ 1,  1 },  new byte[]{ 1,  2 },  new byte[]{ 0,  4 },  new byte[]{ 2,  1 },  new byte[]{ 2,  1 },  new byte[]{ 2,  1 },  new byte[]{ 2,  2 },
        new byte[]{ 2,  2 },  new byte[]{ 2,  2 },  new byte[]{ 2,  3 },  new byte[]{ 1,  5 },  new byte[]{ 3,  2 },  new byte[]{ 3,  2 },  new byte[]{ 4,  0 },  new byte[]{ 3,  3 },
        new byte[]{ 3,  3 },  new byte[]{ 3,  3 },  new byte[]{ 3,  4 },  new byte[]{ 3,  4 },  new byte[]{ 3,  4 },  new byte[]{ 3,  5 },  new byte[]{ 4,  3 },  new byte[]{ 4,  3 },
        new byte[]{ 5,  2 },  new byte[]{ 4,  4 },  new byte[]{ 4,  4 },  new byte[]{ 4,  5 },  new byte[]{ 4,  5 },  new byte[]{ 5,  4 },  new byte[]{ 5,  4 },  new byte[]{ 5,  4 },
        new byte[]{ 6,  3 },  new byte[]{ 5,  5 },  new byte[]{ 5,  5 },  new byte[]{ 5,  6 },  new byte[]{ 4,  8 },  new byte[]{ 6,  5 },  new byte[]{ 6,  5 },  new byte[]{ 6,  5 },
        new byte[]{ 6,  6 },  new byte[]{ 6,  6 },  new byte[]{ 6,  6 },  new byte[]{ 6,  7 },  new byte[]{ 5,  9 },  new byte[]{ 7,  6 },  new byte[]{ 7,  6 },  new byte[]{ 8,  4 },
        new byte[]{ 7,  7 },  new byte[]{ 7,  7 },  new byte[]{ 7,  7 },  new byte[]{ 7,  8 },  new byte[]{ 7,  8 },  new byte[]{ 7,  8 },  new byte[]{ 7,  9 },  new byte[]{ 8,  7 },
        new byte[]{ 8,  7 },  new byte[]{ 9,  6 },  new byte[]{ 8,  8 },  new byte[]{ 8,  8 },  new byte[]{ 8,  9 },  new byte[]{ 8,  9 },  new byte[]{ 9,  8 },  new byte[]{ 9,  8 },
        new byte[]{ 9,  8 },  new byte[]{ 10,  7 }, new byte[]{ 9,  9 },  new byte[]{ 9,  9 },  new byte[]{ 9, 10 },  new byte[]{ 8, 12 },  new byte[]{ 10,  9 }, new byte[]{ 10,  9 },
        new byte[]{ 10,  9 }, new byte[]{ 10, 10 }, new byte[]{ 10, 10 }, new byte[]{ 10, 10 }, new byte[]{ 10, 11 }, new byte[]{ 9, 13 },  new byte[]{ 11, 10 }, new byte[]{ 11, 10 },
        new byte[]{ 12,  8 }, new byte[]{ 11, 11 }, new byte[]{ 11, 11 }, new byte[]{ 11, 11 }, new byte[]{ 11, 12 }, new byte[]{ 11, 12 }, new byte[]{ 11, 12 }, new byte[]{ 11, 13 },
        new byte[]{ 12, 11 }, new byte[]{ 12, 11 }, new byte[]{ 13, 10 }, new byte[]{ 12, 12 }, new byte[]{ 12, 12 }, new byte[]{ 12, 13 }, new byte[]{ 12, 13 }, new byte[]{ 13, 12 },
        new byte[]{ 13, 12 }, new byte[]{ 13, 12 }, new byte[]{ 14, 11 }, new byte[]{ 13, 13 }, new byte[]{ 13, 13 }, new byte[]{ 13, 14 }, new byte[]{ 12, 16 }, new byte[]{ 14, 13 },
        new byte[]{ 14, 13 }, new byte[]{ 14, 13 }, new byte[]{ 14, 14 }, new byte[]{ 14, 14 }, new byte[]{ 14, 14 }, new byte[]{ 14, 15 }, new byte[]{ 13, 17 }, new byte[]{ 15, 14 },
        new byte[]{ 15, 14 }, new byte[]{ 16, 12 }, new byte[]{ 15, 15 }, new byte[]{ 15, 15 }, new byte[]{ 15, 15 }, new byte[]{ 15, 16 }, new byte[]{ 15, 16 }, new byte[]{ 15, 16 },
        new byte[]{ 15, 17 }, new byte[]{ 16, 15 }, new byte[]{ 16, 15 }, new byte[]{ 17, 14 }, new byte[]{ 16, 16 }, new byte[]{ 16, 16 }, new byte[]{ 16, 17 }, new byte[]{ 16, 17 },
        new byte[]{ 17, 16 }, new byte[]{ 17, 16 }, new byte[]{ 17, 16 }, new byte[]{ 18, 15 }, new byte[]{ 17, 17 }, new byte[]{ 17, 17 }, new byte[]{ 17, 18 }, new byte[]{ 16, 20 },
        new byte[]{ 18, 17 }, new byte[]{ 18, 17 }, new byte[]{ 18, 17 }, new byte[]{ 18, 18 }, new byte[]{ 18, 18 }, new byte[]{ 18, 18 }, new byte[]{ 18, 19 }, new byte[]{ 17, 21 },
        new byte[]{ 19, 18 }, new byte[]{ 19, 18 }, new byte[]{ 20, 16 }, new byte[]{ 19, 19 }, new byte[]{ 19, 19 }, new byte[]{ 19, 19 }, new byte[]{ 19, 20 }, new byte[]{ 19, 20 },
        new byte[]{ 19, 20 }, new byte[]{ 19, 21 }, new byte[]{ 20, 19 }, new byte[]{ 20, 19 }, new byte[]{ 21, 18 }, new byte[]{ 20, 20 }, new byte[]{ 20, 20 }, new byte[]{ 20, 21 },
        new byte[]{ 20, 21 }, new byte[]{ 21, 20 }, new byte[]{ 21, 20 }, new byte[]{ 21, 20 }, new byte[]{ 22, 19 }, new byte[]{ 21, 21 }, new byte[]{ 21, 21 }, new byte[]{ 21, 22 },
        new byte[]{ 20, 24 }, new byte[]{ 22, 21 }, new byte[]{ 22, 21 }, new byte[]{ 22, 21 }, new byte[]{ 22, 22 }, new byte[]{ 22, 22 }, new byte[]{ 22, 22 }, new byte[]{ 22, 23 },
        new byte[]{ 21, 25 }, new byte[]{ 23, 22 }, new byte[]{ 23, 22 }, new byte[]{ 24, 20 }, new byte[]{ 23, 23 }, new byte[]{ 23, 23 }, new byte[]{ 23, 23 }, new byte[]{ 23, 24 },
        new byte[]{ 23, 24 }, new byte[]{ 23, 24 }, new byte[]{ 23, 25 }, new byte[]{ 24, 23 }, new byte[]{ 24, 23 }, new byte[]{ 25, 22 }, new byte[]{ 24, 24 }, new byte[]{ 24, 24 },
        new byte[]{ 24, 25 }, new byte[]{ 24, 25 }, new byte[]{ 25, 24 }, new byte[]{ 25, 24 }, new byte[]{ 25, 24 }, new byte[]{ 26, 23 }, new byte[]{ 25, 25 }, new byte[]{ 25, 25 },
        new byte[]{ 25, 26 }, new byte[]{ 24, 28 }, new byte[]{ 26, 25 }, new byte[]{ 26, 25 }, new byte[]{ 26, 25 }, new byte[]{ 26, 26 }, new byte[]{ 26, 26 }, new byte[]{ 26, 26 },
        new byte[]{ 26, 27 }, new byte[]{ 25, 29 }, new byte[]{ 27, 26 }, new byte[]{ 27, 26 }, new byte[]{ 28, 24 }, new byte[]{ 27, 27 }, new byte[]{ 27, 27 }, new byte[]{ 27, 27 },
        new byte[]{ 27, 28 }, new byte[]{ 27, 28 }, new byte[]{ 27, 28 }, new byte[]{ 27, 29 }, new byte[]{ 28, 27 }, new byte[]{ 28, 27 }, new byte[]{ 29, 26 }, new byte[]{ 28, 28 },
        new byte[]{ 28, 28 }, new byte[]{ 28, 29 }, new byte[]{ 28, 29 }, new byte[]{ 29, 28 }, new byte[]{ 29, 28 }, new byte[]{ 29, 28 }, new byte[]{ 30, 27 }, new byte[]{ 29, 29 },
        new byte[]{ 29, 29 }, new byte[]{ 29, 30 }, new byte[]{ 29, 30 }, new byte[]{ 30, 29 }, new byte[]{ 30, 29 }, new byte[]{ 30, 29 }, new byte[]{ 30, 30 }, new byte[]{ 30, 30 },
        new byte[]{ 30, 30 }, new byte[]{ 30, 31 }, new byte[]{ 30, 31 }, new byte[]{ 31, 30 }, new byte[]{ 31, 30 }, new byte[]{ 31, 30 }, new byte[]{ 31, 31 }, new byte[]{ 31, 31 },
    };

    private static readonly byte[][] OMatch6 = {
        new byte[]{ 0,  0 },  new byte[]{ 0,  1 },  new byte[]{ 1,  0 },  new byte[]{ 1,  1 },  new byte[]{ 1,  1 },  new byte[]{ 1,  2 },  new byte[]{ 2,  1 },  new byte[]{ 2,  2 },
        new byte[]{ 2,  2 },  new byte[]{ 2,  3 },  new byte[]{ 3,  2 },  new byte[]{ 3,  3 },  new byte[]{ 3,  3 },  new byte[]{ 3,  4 },  new byte[]{ 4,  3 },  new byte[]{ 4,  4 },
        new byte[]{ 4,  4 },  new byte[]{ 4,  5 },  new byte[]{ 5,  4 },  new byte[]{ 5,  5 },  new byte[]{ 5,  5 },  new byte[]{ 5,  6 },  new byte[]{ 6,  5 },  new byte[]{ 6,  6 },
        new byte[]{ 6,  6 },  new byte[]{ 6,  7 },  new byte[]{ 7,  6 },  new byte[]{ 7,  7 },  new byte[]{ 7,  7 },  new byte[]{ 7,  8 },  new byte[]{ 8,  7 },  new byte[]{ 8,  8 },
        new byte[]{ 8,  8 },  new byte[]{ 8,  9 },  new byte[]{ 9,  8 },  new byte[]{ 9,  9 },  new byte[]{ 9,  9 },  new byte[]{ 9, 10 },  new byte[]{ 10,  9 }, new byte[]{ 10, 10 },
        new byte[]{ 10, 10 }, new byte[]{ 10, 11 }, new byte[]{ 11, 10 }, new byte[]{ 8, 16 },  new byte[]{ 11, 11 }, new byte[]{ 11, 12 }, new byte[]{ 12, 11 }, new byte[]{ 9, 17 },
        new byte[]{ 12, 12 }, new byte[]{ 12, 13 }, new byte[]{ 13, 12 }, new byte[]{ 11, 16 }, new byte[]{ 13, 13 }, new byte[]{ 13, 14 }, new byte[]{ 14, 13 }, new byte[]{ 12, 17 },
        new byte[]{ 14, 14 }, new byte[]{ 14, 15 }, new byte[]{ 15, 14 }, new byte[]{ 14, 16 }, new byte[]{ 15, 15 }, new byte[]{ 15, 16 }, new byte[]{ 16, 14 }, new byte[]{ 16, 15 },
        new byte[]{ 17, 14 }, new byte[]{ 16, 16 }, new byte[]{ 16, 17 }, new byte[]{ 17, 16 }, new byte[]{ 18, 15 }, new byte[]{ 17, 17 }, new byte[]{ 17, 18 }, new byte[]{ 18, 17 },
        new byte[]{ 20, 14 }, new byte[]{ 18, 18 }, new byte[]{ 18, 19 }, new byte[]{ 19, 18 }, new byte[]{ 21, 15 }, new byte[]{ 19, 19 }, new byte[]{ 19, 20 }, new byte[]{ 20, 19 },
        new byte[]{ 20, 20 }, new byte[]{ 20, 20 }, new byte[]{ 20, 21 }, new byte[]{ 21, 20 }, new byte[]{ 21, 21 }, new byte[]{ 21, 21 }, new byte[]{ 21, 22 }, new byte[]{ 22, 21 },
        new byte[]{ 22, 22 }, new byte[]{ 22, 22 }, new byte[]{ 22, 23 }, new byte[]{ 23, 22 }, new byte[]{ 23, 23 }, new byte[]{ 23, 23 }, new byte[]{ 23, 24 }, new byte[]{ 24, 23 },
        new byte[]{ 24, 24 }, new byte[]{ 24, 24 }, new byte[]{ 24, 25 }, new byte[]{ 25, 24 }, new byte[]{ 25, 25 }, new byte[]{ 25, 25 }, new byte[]{ 25, 26 }, new byte[]{ 26, 25 },
        new byte[]{ 26, 26 }, new byte[]{ 26, 26 }, new byte[]{ 26, 27 }, new byte[]{ 27, 26 }, new byte[]{ 24, 32 }, new byte[]{ 27, 27 }, new byte[]{ 27, 28 }, new byte[]{ 28, 27 },
        new byte[]{ 25, 33 }, new byte[]{ 28, 28 }, new byte[]{ 28, 29 }, new byte[]{ 29, 28 }, new byte[]{ 27, 32 }, new byte[]{ 29, 29 }, new byte[]{ 29, 30 }, new byte[]{ 30, 29 },
        new byte[]{ 28, 33 }, new byte[]{ 30, 30 }, new byte[]{ 30, 31 }, new byte[]{ 31, 30 }, new byte[]{ 30, 32 }, new byte[]{ 31, 31 }, new byte[]{ 31, 32 }, new byte[]{ 32, 30 },
        new byte[]{ 32, 31 }, new byte[]{ 33, 30 }, new byte[]{ 32, 32 }, new byte[]{ 32, 33 }, new byte[]{ 33, 32 }, new byte[]{ 34, 31 }, new byte[]{ 33, 33 }, new byte[]{ 33, 34 },
        new byte[]{ 34, 33 }, new byte[]{ 36, 30 }, new byte[]{ 34, 34 }, new byte[]{ 34, 35 }, new byte[]{ 35, 34 }, new byte[]{ 37, 31 }, new byte[]{ 35, 35 }, new byte[]{ 35, 36 },
        new byte[]{ 36, 35 }, new byte[]{ 36, 36 }, new byte[]{ 36, 36 }, new byte[]{ 36, 37 }, new byte[]{ 37, 36 }, new byte[]{ 37, 37 }, new byte[]{ 37, 37 }, new byte[]{ 37, 38 },
        new byte[]{ 38, 37 }, new byte[]{ 38, 38 }, new byte[]{ 38, 38 }, new byte[]{ 38, 39 }, new byte[]{ 39, 38 }, new byte[]{ 39, 39 }, new byte[]{ 39, 39 }, new byte[]{ 39, 40 },
        new byte[]{ 40, 39 }, new byte[]{ 40, 40 }, new byte[]{ 40, 40 }, new byte[]{ 40, 41 }, new byte[]{ 41, 40 }, new byte[]{ 41, 41 }, new byte[]{ 41, 41 }, new byte[]{ 41, 42 },
        new byte[]{ 42, 41 }, new byte[]{ 42, 42 }, new byte[]{ 42, 42 }, new byte[]{ 42, 43 }, new byte[]{ 43, 42 }, new byte[]{ 40, 48 }, new byte[]{ 43, 43 }, new byte[]{ 43, 44 },
        new byte[]{ 44, 43 }, new byte[]{ 41, 49 }, new byte[]{ 44, 44 }, new byte[]{ 44, 45 }, new byte[]{ 45, 44 }, new byte[]{ 43, 48 }, new byte[]{ 45, 45 }, new byte[]{ 45, 46 },
        new byte[]{ 46, 45 }, new byte[]{ 44, 49 }, new byte[]{ 46, 46 }, new byte[]{ 46, 47 }, new byte[]{ 47, 46 }, new byte[]{ 46, 48 }, new byte[]{ 47, 47 }, new byte[]{ 47, 48 },
        new byte[]{ 48, 46 }, new byte[]{ 48, 47 }, new byte[]{ 49, 46 }, new byte[]{ 48, 48 }, new byte[]{ 48, 49 }, new byte[]{ 49, 48 }, new byte[]{ 50, 47 }, new byte[]{ 49, 49 },
        new byte[]{ 49, 50 }, new byte[]{ 50, 49 }, new byte[]{ 52, 46 }, new byte[]{ 50, 50 }, new byte[]{ 50, 51 }, new byte[]{ 51, 50 }, new byte[]{ 53, 47 }, new byte[]{ 51, 51 },
        new byte[]{ 51, 52 }, new byte[]{ 52, 51 }, new byte[]{ 52, 52 }, new byte[]{ 52, 52 }, new byte[]{ 52, 53 }, new byte[]{ 53, 52 }, new byte[]{ 53, 53 }, new byte[]{ 53, 53 },
        new byte[]{ 53, 54 }, new byte[]{ 54, 53 }, new byte[]{ 54, 54 }, new byte[]{ 54, 54 }, new byte[]{ 54, 55 }, new byte[]{ 55, 54 }, new byte[]{ 55, 55 }, new byte[]{ 55, 55 },
        new byte[]{ 55, 56 }, new byte[]{ 56, 55 }, new byte[]{ 56, 56 }, new byte[]{ 56, 56 }, new byte[]{ 56, 57 }, new byte[]{ 57, 56 }, new byte[]{ 57, 57 }, new byte[]{ 57, 57 },
        new byte[]{ 57, 58 }, new byte[]{ 58, 57 }, new byte[]{ 58, 58 }, new byte[]{ 58, 58 }, new byte[]{ 58, 59 }, new byte[]{ 59, 58 }, new byte[]{ 59, 59 }, new byte[]{ 59, 59 },
        new byte[]{ 59, 60 }, new byte[]{ 60, 59 }, new byte[]{ 60, 60 }, new byte[]{ 60, 60 }, new byte[]{ 60, 61 }, new byte[]{ 61, 60 }, new byte[]{ 61, 61 }, new byte[]{ 61, 61 },
        new byte[]{ 61, 62 }, new byte[]{ 62, 61 }, new byte[]{ 62, 62 }, new byte[]{ 62, 62 }, new byte[]{ 62, 63 }, new byte[]{ 63, 62 }, new byte[]{ 63, 63 }, new byte[]{ 63, 63 },
    };

    static int Mul8Bit(int a, int b)
    {
        int t = (a * b) + 128;
        return (t + (t >> 8)) >> 8;
    }

    static void From16Bit(byte* bOut, ushort v)
    {
        int rv = (v & 0xf800) >> 11;
        int gv = (v & 0x07e0) >> 5;
        int bv = (v & 0x001f) >> 0;

        // expand to 8 bits via bit replication
        bOut[0] = unchecked((byte)((rv * 33) >> 2));
        bOut[1] = unchecked((byte)((gv * 65) >> 4));
        bOut[2] = unchecked((byte)((bv * 33) >> 2));
        bOut[3] = 0;
    }

    static ushort As16Bit(int r, int g, int b) => (ushort)((Mul8Bit(r, 31) << 11) + (Mul8Bit(g, 63) << 5) + Mul8Bit(b, 31));

    // linear interpolation at 1/3 point between a and b, using desired rounding type
    static int Lerp13(int a, int b) =>
#if STB_DXT_USE_ROUNDING_BIAS
        // with rounding bias
        a + Mul8Bit(b - a, 0x55);
#else
        // without rounding bias
        // replace "/ 3" by "* 0xaaab) >> 17" if your compiler sucks or you really need every ounce of speed.
        return (2 * a + b) / 3;
#endif


    // lerp RGB color
    static void Lerp13RGB(byte* bOut, byte* p1, byte* p2)
    {
        bOut[0] = unchecked((byte)Lerp13(p1[0], p2[0]));
        bOut[1] = unchecked((byte)Lerp13(p1[1], p2[1]));
        bOut[2] = unchecked((byte)Lerp13(p1[2], p2[2]));
    }

    /****************************************************************************/

    static void EvalColors(byte* color, ushort c0, ushort c1)
    {
        From16Bit(color + 0, c0);
        From16Bit(color + 4, c1);
        Lerp13RGB(color + 8, color + 0, color + 4);
        Lerp13RGB(color + 12, color + 4, color + 0);
    }

    // The color matching function
    static uint MatchColorsBlock(byte* block, byte* color)
    {
        uint mask = 0;
        int dirr = color[(0 * 4) + 0] - color[(1 * 4) + 0];
        int dirg = color[(0 * 4) + 1] - color[(1 * 4) + 1];
        int dirb = color[(0 * 4) + 2] - color[(1 * 4) + 2];
        int* dots = stackalloc int[16];
        int* stops = stackalloc int[4];
        int i;
        int c0Point, halfPoint, c3Point;

        for (i = 0; i < 16; i++)
        {
            dots[i] = (block[(i * 4) + 0] * dirr) + (block[(i * 4) + 1] * dirg) + (block[(i * 4) + 2] * dirb);
        }

        for (i = 0; i < 4; i++)
        {
            stops[i] = (color[(i * 4) + 0] * dirr) + (color[(i * 4) + 1] * dirg) + (color[(i * 4) + 2] * dirb);
        }

        // think of the colors as arranged on a line; project point onto that line, then choose
        // next color out of available ones. we compute the crossover points for "best color in top
        // half"/"best in bottom half" and then the same inside that subinterval.
        //
        // relying on this 1d approximation isn't always optimal in terms of euclidean distance,
        // but it's very close and a lot faster.
        // http://cbloomrants.blogspot.com/2008/12/12-08-08-dxtc-summary.html

        c0Point = (stops[1] + stops[3]);
        halfPoint = (stops[3] + stops[2]);
        c3Point = (stops[2] + stops[0]);

        for (i = 15; i >= 0; i--)
        {
            int dot = dots[i] * 2;
            mask <<= 2;

            if (dot < halfPoint)
            {
                mask |= unchecked((uint)((dot < c0Point) ? 1 : 3));
            }
            else
            {
                mask |= unchecked((uint)((dot < c3Point) ? 2 : 0));
            }
        }

        return mask;
    }

    // The color optimization function. (Clever code, part 1)
    static void OptimizeColorsBlock(byte* block, ushort* pmax16, ushort* pmin16)
    {
        int mind, maxd;
        byte* minp, maxp;
        double magn;
        int v_r, v_g, v_b;
        const int nIterPower = 4;
        float* covf = stackalloc float[6];
        float vfr, vfg, vfb;

        // determine color distribution
        int* cov = stackalloc int[6];
        int* mu = stackalloc int[3], min = stackalloc int[3], max = stackalloc int[3];
        int ch, i, iter;

        for (ch = 0; ch < 3; ch++)
        {
            byte* bp = block + ch;
            int muv, minv, maxv;

            muv = minv = maxv = bp[0];
            for (i = 4; i < 64; i += 4)
            {
                muv += bp[i];
                if (bp[i] < minv)
                {
                    minv = bp[i];
                }
                else
                {
                    if (bp[i] > maxv)
                    {
                        maxv = bp[i];
                    }
                }
            }

            mu[ch] = (muv + 8) >> 4;
            min[ch] = minv;
            max[ch] = maxv;
        }

        // determine covariance matrix
        for (i = 0; i < 6; i++)
        {
            cov[i] = 0;
        }

        for (i = 0; i < 16; i++)
        {
            int r = block[(i * 4) + 0] - mu[0];
            int g = block[(i * 4) + 1] - mu[1];
            int b = block[(i * 4) + 2] - mu[2];

            cov[0] += r * r;
            cov[1] += r * g;
            cov[2] += r * b;
            cov[3] += g * g;
            cov[4] += g * b;
            cov[5] += b * b;
        }

        // convert covariance matrix to float, find principal axis via power iter
        for (i = 0; i < 6; i++)
        {
            covf[i] = cov[i] / 255.0f;
        }

        vfr = max[0] - min[0];
        vfg = max[1] - min[1];
        vfb = max[2] - min[2];

        for (iter = 0; iter < nIterPower; iter++)
        {
            float r = (vfr * covf[0]) + (vfg * covf[1]) + (vfb * covf[2]);
            float g = (vfr * covf[1]) + (vfg * covf[3]) + (vfb * covf[4]);
            float b = (vfr * covf[2]) + (vfg * covf[4]) + (vfb * covf[5]);

            vfr = r;
            vfg = g;
            vfb = b;
        }

        magn = MathF.Abs(vfr);
        if (MathF.Abs(vfg) > magn)
        {
            magn = MathF.Abs(vfg);
        }

        if (MathF.Abs(vfb) > magn)
        {
            magn = MathF.Abs(vfb);
        }

        if (magn < 4.0f)
        { // too small, default to luminance
            v_r = 299; // JPEG YCbCr luma coefs, scaled by 1000.
            v_g = 587;
            v_b = 114;
        }
        else
        {
            magn = 512.0 / magn;
            v_r = (int)(vfr * magn);
            v_g = (int)(vfg * magn);
            v_b = (int)(vfb * magn);
        }

        minp = maxp = block;
        mind = maxd = (block[0] * v_r) + (block[1] * v_g) + (block[2] * v_b);
        // Pick colors at extreme points
        for (i = 1; i < 16; i++)
        {
            int dot = (block[(i * 4) + 0] * v_r) + (block[(i * 4) + 1] * v_g) + (block[(i * 4) + 2] * v_b);

            if (dot < mind)
            {
                mind = dot;
                minp = block + (i * 4);
            }

            if (dot > maxd)
            {
                maxd = dot;
                maxp = block + (i * 4);
            }
        }

        *pmax16 = As16Bit(maxp[0], maxp[1], maxp[2]);
        *pmin16 = As16Bit(minp[0], minp[1], minp[2]);
    }

    private static readonly float[] midpoints5 = {
       0.015686f, 0.047059f, 0.078431f, 0.111765f, 0.145098f, 0.176471f, 0.207843f, 0.241176f, 0.274510f, 0.305882f, 0.337255f, 0.370588f, 0.403922f, 0.435294f, 0.466667f, 0.5f,
       0.533333f, 0.564706f, 0.596078f, 0.629412f, 0.662745f, 0.694118f, 0.725490f, 0.758824f, 0.792157f, 0.823529f, 0.854902f, 0.888235f, 0.921569f, 0.952941f, 0.984314f, 1.0f
    };

    private static readonly float[] midpoints6 = {
       0.007843f, 0.023529f, 0.039216f, 0.054902f, 0.070588f, 0.086275f, 0.101961f, 0.117647f, 0.133333f, 0.149020f, 0.164706f, 0.180392f, 0.196078f, 0.211765f, 0.227451f, 0.245098f,
       0.262745f, 0.278431f, 0.294118f, 0.309804f, 0.325490f, 0.341176f, 0.356863f, 0.372549f, 0.388235f, 0.403922f, 0.419608f, 0.435294f, 0.450980f, 0.466667f, 0.482353f, 0.500000f,
       0.517647f, 0.533333f, 0.549020f, 0.564706f, 0.580392f, 0.596078f, 0.611765f, 0.627451f, 0.643137f, 0.658824f, 0.674510f, 0.690196f, 0.705882f, 0.721569f, 0.737255f, 0.754902f,
       0.772549f, 0.788235f, 0.803922f, 0.819608f, 0.835294f, 0.850980f, 0.866667f, 0.882353f, 0.898039f, 0.913725f, 0.929412f, 0.945098f, 0.960784f, 0.976471f, 0.992157f, 1.0f
    };

    static ushort Quantize5(float x)
    {
        ushort q;
        x = x < 0 ? 0 : x > 1 ? 1 : x;  // saturate
        q = (ushort)(x * 31);
        q += (ushort)(x > midpoints5[q] ? 1 : 0);
        return q;
    }

    static ushort Quantize6(float x)
    {
        ushort q;
        x = x < 0 ? 0 : x > 1 ? 1 : x;  // saturate
        q = (ushort)(x * 63);
        q += (ushort)(x > midpoints6[q] ? 1 : 0);
        return q;
    }

    // The refinement function. (Clever code, part 2)
    // Tries to optimize colors to suit block contents better.
    // (By solving a least squares system via normal equations+Cramer's rule)
    static bool RefineBlock(byte* block, ushort* pmax16, ushort* pmin16, uint mask)
    {
        int[] w1Tab = { 3, 0, 2, 1 };
        int[] prods = { 0x090000, 0x000900, 0x040102, 0x010402 };
        // ^some magic to save a lot of multiplies in the accumulating loop...
        // (precomputed products of weights for least squares system, accumulated inside one 32-bit register)

        float f;
        ushort oldMin, oldMax, min16, max16;
        int i, akku = 0, xx, xy, yy;
        int At1_r, At1_g, At1_b;
        int At2_r, At2_g, At2_b;
        uint cm = mask;

        oldMin = *pmin16;
        oldMax = *pmax16;

        if ((mask ^ (mask << 2)) < 4) // all pixels have the same index?
        {
            // yes, linear system would be singular; solve using optimal
            // single-color match on average color
            int r = 8, g = 8, b = 8;
            for (i = 0; i < 16; ++i)
            {
                r += block[(i * 4) + 0];
                g += block[(i * 4) + 1];
                b += block[(i * 4) + 2];
            }

            r >>= 4; g >>= 4; b >>= 4;

            max16 = unchecked((ushort)((OMatch5[r][0] << 11) | (OMatch6[g][0] << 5) | OMatch5[b][0]));
            min16 = unchecked((ushort)((OMatch5[r][1] << 11) | (OMatch6[g][1] << 5) | OMatch5[b][1]));
        }
        else
        {
            At1_r = At1_g = At1_b = 0;
            At2_r = At2_g = At2_b = 0;
            for (i = 0; i < 16; ++i, cm >>= 2)
            {
                int step = (int)(cm & 3);
                int w1 = w1Tab[step];
                int r = block[(i * 4) + 0];
                int g = block[(i * 4) + 1];
                int b = block[(i * 4) + 2];

                akku += prods[step];
                At1_r += w1 * r;
                At1_g += w1 * g;
                At1_b += w1 * b;
                At2_r += r;
                At2_g += g;
                At2_b += b;
            }

            At2_r = (3 * At2_r) - At1_r;
            At2_g = (3 * At2_g) - At1_g;
            At2_b = (3 * At2_b) - At1_b;

            // extract solutions and decide solvability
            xx = akku >> 16;
            yy = (akku >> 8) & 0xff;
            xy = (akku >> 0) & 0xff;

            f = 3.0f / 255.0f / ((xx * yy) - (xy * xy));

            max16 = unchecked((ushort)(Quantize5(((At1_r * yy) - (At2_r * xy)) * f) << 11));
            max16 |= unchecked((ushort)(Quantize6(((At1_g * yy) - (At2_g * xy)) * f) << 5));
            max16 |= unchecked((ushort)(Quantize5(((At1_b * yy) - (At2_b * xy)) * f) << 0));

            min16 = unchecked((ushort)(Quantize5(((At2_r * xx) - (At1_r * xy)) * f) << 11));
            min16 |= unchecked((ushort)(Quantize6(((At2_g * xx) - (At1_g * xy)) * f) << 5));
            min16 |= unchecked((ushort)(Quantize5(((At2_b * xx) - (At1_b * xy)) * f) << 0));
        }

        *pmin16 = min16;
        *pmax16 = max16;
        return oldMin != min16 || oldMax != max16;
    }

    static void CompressColorBlock(byte* dest, byte* block, CompressionMode mode)
    {
        uint mask;
        int i;
        int refinecount;
        ushort max16, min16;
        byte* color = stackalloc byte[4 * 4];

        refinecount = mode.HasFlag(CompressionMode.Quality) ? 2 : 1;

        // check if block is constant
        for (i = 1; i < 16; i++)
        {
            if (((uint*)block)[i] != ((uint*)block)[0])
            {
                break;
            }
        }

        if (i == 16)
        { // constant color
            int r = block[0], g = block[1], b = block[2];
            mask = 0xaaaaaaaa;
            max16 = unchecked((ushort)((OMatch5[r][0] << 11) | (OMatch6[g][0] << 5) | OMatch5[b][0]));
            min16 = unchecked((ushort)((OMatch5[r][1] << 11) | (OMatch6[g][1] << 5) | OMatch5[b][1]));
        }
        else
        {
            // first step: PCA+map along principal axis
            OptimizeColorsBlock(block, &max16, &min16);
            if (max16 != min16)
            {
                EvalColors(color, max16, min16);
                mask = MatchColorsBlock(block, color);
            }
            else
            {
                mask = 0;
            }

            // third step: refine (multiple times if requested)
            for (i = 0; i < refinecount; i++)
            {
                uint lastmask = mask;

                if (RefineBlock(block, &max16, &min16, mask))
                {
                    if (max16 != min16)
                    {
                        EvalColors(color, max16, min16);
                        mask = MatchColorsBlock(block, color);
                    }
                    else
                    {
                        mask = 0;
                        break;
                    }
                }

                if (mask == lastmask)
                {
                    break;
                }
            }
        }

        // write the color block
        if (max16 < min16)
        {
            (max16, min16) = (min16, max16);
            mask ^= 0x55555555;
        }

        dest[0] = (byte)(max16);
        dest[1] = (byte)(max16 >> 8);
        dest[2] = (byte)(min16);
        dest[3] = (byte)(min16 >> 8);
        dest[4] = (byte)(mask);
        dest[5] = (byte)(mask >> 8);
        dest[6] = (byte)(mask >> 16);
        dest[7] = (byte)(mask >> 24);
    }

    static void CompressAlphaBlock(byte* dest, byte* src, int stride)
    {
        int i, dist, bias, dist4, dist2, bits, mask;

        // find min/max color
        int mn, mx;
        mn = mx = src[0];

        for (i = 1; i < 16; i++)
        {
            if (src[i * stride] < mn) mn = src[i * stride];
            else if (src[i * stride] > mx) mx = src[i * stride];
        }

        // encode them
        dest[0] = (byte)mx;
        dest[1] = (byte)mn;
        dest += 2;

        // determine bias and emit color indices
        // given the choice of mx/mn, these indices are optimal:
        // http://fgiesen.wordpress.com/2009/12/15/dxt5-alpha-block-index-determination/
        dist = mx - mn;
        dist4 = dist * 4;
        dist2 = dist * 2;
        bias = (dist < 8) ? (dist - 1) : ((dist / 2) + 2);
        bias -= mn * 7;
        bits = 0;
        mask = 0;

        for (i = 0; i < 16; i++)
        {
            int a = (src[i * stride] * 7) + bias;
            int ind, t;

            // select index. this is a "linear scale" lerp factor between 0 (val=min) and 7 (val=max).
            t = (a >= dist4) ? -1 : 0; ind = t & 4; a -= dist4 & t;
            t = (a >= dist2) ? -1 : 0; ind += t & 2; a -= dist2 & t;
            ind += (a >= dist ? 1 : 0);

            // turn linear scale into DXT index (0/1 are extremal pts)
            ind = -ind & 7;
            ind ^= (2 > ind ? 1 : 0);

            // write index
            mask |= ind << bits;
            if ((bits += 3) >= 8)
            {
                *dest++ = (byte)mask;
                mask >>= 8;
                bits -= 8;
            }
        }
    }

    public static void CompressDXTBlock(byte* dest, byte* src, bool alpha, CompressionMode mode)
    {
        byte* data = stackalloc byte[16 * 4];
        if (alpha)
        {
            int i;
            CompressAlphaBlock(dest, src + 3, 4);
            dest += 8;
            // make a new copy of the data in which alpha is opaque,
            // because code uses a fast test for color constancy
            Buffer.MemoryCopy(src, data, 4 * 16, 4 * 16);
            for (i = 0; i < 16; ++i)
            {
                data[(i * 4) + 3] = 255;
            }

            src = data;
        }

        CompressColorBlock(dest, src, mode);
    }

    public static CompressedMipmapData CompressImageWithMipmaps(Image<Rgba32> img, bool actuallyMip)
    {
        int minWH = Math.Min(img.Width, img.Height);
        int numMipmaps = 1;
        if (actuallyMip)
        {
            while (true)
            {
                minWH >>= 1;
                if (minWH < 4) // Normally would do a <0 comparison here but it doesn't make sense to compress less than block size anyway
                {
                    break;
                }

                numMipmaps += 1;
            }
        }

        IntPtr[] ret = new IntPtr[numMipmaps];
        int[] sizes = new int[numMipmaps];
        Size[] szs = new Size[numMipmaps];
        ret[0] = (IntPtr)CompressImage(img, out int nBytesMip);
        sizes[0] = nBytesMip;
        int mW = img.Width;
        int mH = img.Height;
        szs[0] = new Size(mW, mH);
        for (int i = 1; i < numMipmaps; ++i)
        {
            mW >>= 1;
            mH >>= 1;
            Image<Rgba32> mipped = img.Clone();
            mipped.Mutate(x => x.Resize(mW, mH, KnownResamplers.Box));
            ret[i] = (IntPtr)CompressImage(mipped, out nBytesMip);
            sizes[i] = nBytesMip;
            szs[i] = new Size(mW, mH);
            mipped.Dispose();
        }

        return new CompressedMipmapData(ret, szs, sizes);
    }

    public static byte* CompressImage(Image<Rgba32> img, out int nBytes)
    {
        ImageMemoryView imv = CommitToMemory(img);
        int nBlocksX = ((img.Width + 3) & ~3) >> 2;
        int nBlocksY = ((img.Height + 3) & ~3) >> 2;
        nBytes = nBlocksX * nBlocksY * 16;
        byte* ret = (byte*)Marshal.AllocHGlobal(nBytes);
        byte* blockData = stackalloc byte[16];
        for (int y = 0; y < nBlocksY; ++y)
        {
            for (int x = 0; x < nBlocksX; ++x)
            {
                int blockX = x << 2;
                int blockY = y << 2;
                Rgba32* mem = imv.GetBlock(blockX, blockY);
                CompressDXTBlock(blockData, (byte*)mem, true, CompressionMode.Normal);
                Marshal.FreeHGlobal((IntPtr)mem);
                Buffer.MemoryCopy(blockData, ret + (x * 16) + (y * nBlocksX * 16), 16, 16);
            }
        }

        imv.Free();
        return ret;
    }

    private static unsafe ImageMemoryView CommitToMemory(Image<Rgba32> img)
    {
        int adjWidth = ((img.Width + 3) & ~3);
        int adjHeight = ((img.Height + 3) & ~3);
        int nBytes = sizeof(Rgba32) * adjWidth * adjHeight;
        Rgba32* mem = (Rgba32*)Marshal.AllocHGlobal(nBytes);
        int bL = img.Width * sizeof(Rgba32);
        img.ProcessPixelRows(x =>
        {
            for (int y = 0; y < img.Height; ++y)
            {
                Span<Rgba32> s = x.GetRowSpan(y);
                fixed (Rgba32* m = &MemoryMarshal.GetReference(s))
                {
                    Buffer.MemoryCopy(m, (void*)(mem + (y * adjWidth)), bL, bL);
                }
            }
        });

        return new ImageMemoryView(mem, adjWidth);
    }

    public unsafe class CompressedMipmapData
    {
        public IntPtr[] data;
        public Size[] sizes;
        public int[] dataLength;
        public int numMips => data.Length;

        public CompressedMipmapData(IntPtr[] data, Size[] sizes, int[] dataLength)
        {
            this.data = data;
            this.dataLength = dataLength;
        }

        public void Free()
        {
            foreach (IntPtr ptr in data)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    private unsafe class ImageMemoryView
    {
        private readonly Rgba32* mem;
        private readonly int w;

        public ImageMemoryView(Rgba32* mem, int w)
        {
            this.mem = mem;
            this.w = w;
        }

        public Rgba32* GetBlock(int x, int y)
        {
            int s = 4 * 4 * sizeof(Rgba32);
            Rgba32* ret = (Rgba32*)Marshal.AllocHGlobal(s);
            for (int j = 0; j < 4; ++j)
            {
                int dy = y + j;
                Rgba32* m = this.mem + (dy * w) + x;
                Buffer.MemoryCopy(m, ret + (4 * j), 16, 16);
            }

            return ret;
        }

        public void Free() => Marshal.FreeHGlobal((IntPtr)this.mem);
    }
}

/*
------------------------------------------------------------------------------
This software is available under 2 licenses -- choose whichever you prefer.
------------------------------------------------------------------------------
ALTERNATIVE A - MIT License
Copyright (c) 2017 Sean Barrett
Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
------------------------------------------------------------------------------
ALTERNATIVE B - Public Domain (www.unlicense.org)
This is free and unencumbered software released into the public domain.
Anyone is free to copy, modify, publish, use, compile, sell, or distribute this
software, either in source code form or as a compiled binary, for any purpose,
commercial or non-commercial, and by any means.
In jurisdictions that recognize copyright laws, the author or authors of this
software dedicate any and all copyright interest in the software to the public
domain. We make this dedication for the benefit of the public at large and to
the detriment of our heirs and successors. We intend this dedication to be an
overt act of relinquishment in perpetuity of all present and future rights to
this software under copyright law.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
------------------------------------------------------------------------------
*/
