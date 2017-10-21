using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;
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
    }
}
