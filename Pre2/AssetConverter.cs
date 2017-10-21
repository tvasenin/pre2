using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;
using System;
using System.IO;

namespace Pre2
{
    public static class AssetConverter
    {
        public static void PrepareAllAssets()
        {
            ConvertIndex8WithPalette("./raw/CASTLE.raw", "./out/CASTLE.png");
            ConvertIndex8WithPalette("./raw/MENU.BIN",   "./out/MENU.png");
            ConvertIndex8WithPalette("./raw/THEEND.raw", "./out/THEEND.png");
            ConvertIndex8WithPalette("./raw/TITUS.raw",  "./out/TITUS.png");

            // Palette for MENU2 is concatenated at the end of the image (using a copy for convenience)!
            ConvertIndex4("./raw/GAMEOVER.BIN", "./res/gameover.pal", "./out/GAMEOVER.png", 320, 200);
            ConvertIndex4("./raw/MAP.BIN",      "./res/map.pal",      "./out/MAP.png",      640, 200);
            ConvertIndex4("./raw/MENU2.BIN",    "./res/menu2.pal",    "./out/MENU2.png",    320, 200);
            ConvertIndex4("./raw/MOTIF.BIN",    "./res/motif.pal",    "./out/MOTIF.png",    320, 200);
        }

        private static void ConvertIndex8WithPalette(string srcFilename, string destFilename)
        {
            const int width = 320;
            const int height = 200;
            const int numPaletteEntries = 256;

            using (FileStream input = File.OpenRead(srcFilename),
                              output = File.Create(destFilename))
            {
                ImageInfo imi = new ImageInfo(width, height, 8, false, false, true);
                PngWriter pngw = new PngWriter(output, imi);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, input);
                byte[] row = new byte[width];
                for (var i = 0; i < imi.Rows; i++)
                {
                    input.Read(row, 0, row.Length);
                    pngw.WriteRowByte(row, i);
                }
                pngw.End();
            }
        }

        private static void ConvertIndex4(string srcFilename, string paletteFilename, string destFilename, int width, int height)
        {
            int numBytesRow = (width / 8) * 4; // 4 bpp
            int numBytesOutput = numBytesRow * height;
            byte[] rawData = File.ReadAllBytes(srcFilename);
            byte[] indexBytes = ConvertPlanarIndex4Bytes(rawData, numBytesOutput);

            const int numPaletteEntries = 16;
            using (FileStream pal = File.OpenRead(paletteFilename),
                              output = File.Create(destFilename))
            {
                ImageInfo imi = new ImageInfo(width, height, 4, false, false, true);
                PngWriter pngw = new PngWriter(output, imi);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, pal);
                byte[] row = new byte[numBytesRow];
                for (var i = 0; i < imi.Rows; i++)
                {
                    Array.Copy(indexBytes, i * numBytesRow, row, 0, numBytesRow);
                    pngw.WriteRowByte(row, i);
                }
                pngw.End();
            }
        }

        private static int ConvertVgaToRgb(int sixBitValue)
        {
            sixBitValue &= 0x3F; // make sure it's really 6-bit value
            // 6-bit VGA to 8-bit RGB
            int eightBitValue = (sixBitValue * 255) / 63;
            //int eightBitValue = (sixBitValue << 2) | (sixBitValue >> 4);
            return eightBitValue;
        }

        private static void FillPalette(PngChunkPLTE palette, int numEntries, Stream input)
        {
            palette.SetNentries(numEntries);
            for (var i = 0; i < numEntries; i++)
            {
                int r = ConvertVgaToRgb(input.ReadByte());
                int g = ConvertVgaToRgb(input.ReadByte());
                int b = ConvertVgaToRgb(input.ReadByte());
                palette.SetEntry(i, r, g, b);
            }
        }

        private static byte[] ConvertPlanarIndex4Bytes(byte[] data, int targetLength)
        {
            // target length may be not equal to input data length (less - truncate; greater - pad with zero bytes)
            if ((targetLength % 4) != 0) { throw new ArgumentException("Image length should be a multiple of 4!"); }
            byte[] indexBytes = new byte[targetLength]; // the rows not present in the original picture will be zeroes
            int processLength = Math.Min(data.Length, targetLength);
            int planeLength = processLength / 4;
            for (var i = 0; i < planeLength; i++)
            {
                DecodePlaneBytes(indexBytes, i * 4, data[planeLength * 0 + i], data[planeLength * 1 + i], data[planeLength * 2 + i], data[planeLength * 3 + i]);
            }
            return indexBytes;
        }

        private static void DecodePlaneBytes(byte[] outBytes, int offset, byte b0, byte b1, byte b2, byte b3)
        {
            const int maskHi = 1 << 7;
            const int maskLo = 1 << 6;
            for (var i = 0; i < 4; i++)
            {
                int hi = ((b3 & maskHi) >> 0) | ((b2 & maskHi) >> 1) | ((b1 & maskHi) >> 2) | ((b0 & maskHi) >> 3); // bit 7
                int lo = ((b3 & maskLo) >> 3) | ((b2 & maskLo) >> 4) | ((b1 & maskLo) >> 5) | ((b0 & maskLo) >> 6); // bit 6
                outBytes[offset++] = (byte)(hi | lo);
                b0 <<= 2; b1 <<= 2; b2 <<= 2; b3 <<= 2; // shift all bytes left by 2 bits
            }
        }
    }
}
