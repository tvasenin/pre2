using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Pre2
{
    public static class AssetConverter
    {
        private const int TileSide = 16;
        private const int LevelTilesPerRow = 256;

        private static readonly ImageInfo TileImageInfoPng = new ImageInfo(TileSide, TileSide, 8, false, false, true);

        private const int NumUnionTiles = 544;

        private static readonly byte[][] LevelPalettes = ReadLevelPalettes("./res/levels.pals");

        private static readonly char[] LevelSuffixes = {  '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G'};
        private static readonly byte[] LevelNumRows  = {  49, 104,  49,  45, 128, 128, 128,  86, 110,  12,  24,  51,  51,  38, 173,  84 };
        private static readonly byte[] LevelPals     = {   8,  10,   7,   6,   3,   5,   1,   4,   2,   2,  11,  11,  11,  12,   2,   1 }; // no pal #0 and #9!

        private static readonly byte[][] UnionTiles = ReadTiles(SqzUnpacker.Unpack("./sqz/UNION.SQZ"), NumUnionTiles);

        public static void PrepareAllAssets()
        {
            ConvertIndex8WithPalette(SqzUnpacker.Unpack("./sqz/CASTLE.SQZ"), "./out/CASTLE.png");
            ConvertIndex8WithPalette(SqzUnpacker.Unpack("./sqz/MENU.SQZ"),   "./out/MENU.png");
            ConvertIndex8WithPalette(SqzUnpacker.Unpack("./sqz/THEEND.SQZ"), "./out/THEEND.png");
            ConvertIndex8WithPalette(SqzUnpacker.Unpack("./sqz/TITUS.SQZ"),  "./out/TITUS.png");

            // Palette for MENU2 is concatenated at the end of the image (using a copy for convenience)!
            ConvertIndex4(SqzUnpacker.Unpack("./sqz/GAMEOVER.SQZ"), "./res/gameover.pal", "./out/GAMEOVER.png", 320, 200);
            ConvertIndex4(SqzUnpacker.Unpack("./sqz/MAP.SQZ"),      "./res/map.pal",      "./out/MAP.png",      640, 200);
            ConvertIndex4(SqzUnpacker.Unpack("./sqz/MENU2.SQZ"),    "./res/menu2.pal",    "./out/MENU2.png",    320, 200);
            ConvertIndex4(SqzUnpacker.Unpack("./sqz/MOTIF.SQZ"),    "./res/motif.pal",    "./out/MOTIF.png",    320, 200);

            ConvertDevPhoto(SqzUnpacker.Unpack("./sqz/LEVELH.SQZ"), SqzUnpacker.Unpack("./sqz/LEVELI.SQZ"), "./out/LEVELHI.png");


            for (var i = 0; i < 16; i++)
            {
                GenerateLevelTilemap(i, "./sqz", "./out");
            }
        }

        private static void ConvertIndex8WithPalette(byte[] data, string destFilename)
        {
            using (Stream input = new MemoryStream(data),
                          output = File.Create(destFilename))
            {
                ConvertIndex8WithPalette(input, output);
            }
        }

        private static void ConvertIndex8WithPalette(Stream input, Stream output)
        {
            ImageInfo imi = new ImageInfo(320, 200, 8, false, false, true);
            const int numPaletteEntries = 256;
            byte[] pal = new byte[numPaletteEntries * 3];
            input.Read(pal, 0, pal.Length);

            PngWriter pngw = new PngWriter(output, imi);
            PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
            FillPalette(palette, numPaletteEntries, pal);
            byte[] row = new byte[imi.BytesPerRow];
            for (var i = 0; i < imi.Rows; i++)
            {
                input.Read(row, 0, row.Length);
                pngw.WriteRowByte(row, i);
            }
            pngw.End();
        }

        private static void ConvertIndex4(byte[] rawData, string paletteFilename, string destFilename, int width, int height)
        {
            ImageInfo imiInput = new ImageInfo(width, height, 4, false, false, true);
            ImageInfo imiPng = new ImageInfo(width, height, 8, false, false, true);
            int numBytesRow = imiInput.BytesPerRow;
            int numBytesInput = numBytesRow * imiInput.Rows;
            byte[] indexBytes = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(rawData, numBytesInput));

            const int numPaletteEntries = 16;
            byte[] pal = File.ReadAllBytes(paletteFilename);
            using (FileStream output = File.Create(destFilename))
            {
                int numBytesRowPng = imiPng.BytesPerRow;
                PngWriter pngw = new PngWriter(output, imiPng);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, pal);
                byte[] row = new byte[numBytesRowPng];
                for (var i = 0; i < imiPng.Rows; i++)
                {
                    Array.Copy(indexBytes, i * numBytesRowPng, row, 0, row.Length);
                    pngw.WriteRowByte(row, i);
                }
                pngw.End();
            }
        }

        private static void ConvertDevPhoto(byte[] planes01, byte[] planes02, string destFilename)
        {
            //input height is 415, need to pad the remaining lines
            ImageInfo imiInput = new ImageInfo(640, 480, 4, false, true, false);
            ImageInfo imiPng = new ImageInfo(640, 480, 8, false, false, true);

            byte[] planes = new byte[planes01.Length + planes02.Length];
            Array.Copy(planes01, 0, planes, 0, planes01.Length);
            Array.Copy(planes02, 0, planes, planes01.Length, planes02.Length);

            int numBytesInput = imiInput.BytesPerRow * imiInput.Rows;

            // the rows not present in the original picture will be zeroes
            byte[] indexBytes = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(planes, numBytesInput));

            // generate greyscale palette (naive way)
            const int numPaletteEntries = 16;
            byte[] pal = new byte[numPaletteEntries * 3];
            int idx = 0;
            for (var i = 0; i < numPaletteEntries; i++)
            {
                byte c = (byte) (i * 4);
                pal[idx++] = c;
                pal[idx++] = c;
                pal[idx++] = c;
            }

            int numBytesRowPng = imiPng.BytesPerRow;
            byte[] row = new byte[numBytesRowPng];
            using (FileStream output = File.Create(destFilename))
            {
                PngWriter pngw = new PngWriter(output, imiPng);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, pal);
                for (var i = 0; i < imiPng.Rows; i++)
                {
                    Array.Copy(indexBytes, i * numBytesRowPng, row, 0, row.Length);
                    pngw.WriteRowByte(row, i);
                }
                pngw.End();
            }
        }

        private static void GenerateLevelTilemap(int idx, string sqzPath, string outPath)
        {
            char suffix = LevelSuffixes[idx];
            byte[] data = SqzUnpacker.Unpack(Path.Combine(sqzPath, "LEVEL" + suffix + ".SQZ"));
            byte[] pal = LevelPalettes[LevelPals[idx]];
            GenerateLevelTilemapImpl(data, pal, LevelNumRows[idx], outPath, "LEVEL" + suffix);
        }

        private static void GenerateLevelTilemapImpl(byte[] data, byte[] pal, int numTileRows, string outPath, string outName)
        {
            string baseFilename = Path.Combine(outPath, outName);

            byte[][] localTiles;
            byte[] tilemap;
            ushort[] lut = new ushort[256];
            using (BinaryReader br = new BinaryReader(new MemoryStream(data, false)))
            {
                tilemap = br.ReadBytes(numTileRows * LevelTilesPerRow);
                short maxLocalIdx = -1;
                for (var i = 0; i < lut.Length; i++)
                {
                    ushort v = br.ReadUInt16();
                    lut[i] = v;
                    if (v < 256 && v > maxLocalIdx)
                    {
                        maxLocalIdx = (short) v;
                    }
                }
                localTiles = ReadTiles(br.BaseStream, maxLocalIdx + 1);
            }

            // Fetch 256 tiles according to LUT
            int bytesPerTile = TileImageInfoPng.BytesPerRow * TileImageInfoPng.Rows;
            byte[][] totalTiles = new byte[256][];
            for (var i = 0; i < totalTiles.Length; i++)
            {
                ushort idx = lut[i];
                if (idx < 256)
                {
                    totalTiles[i] = localTiles[idx];
                }
                else if (idx < 256 + NumUnionTiles)
                {
                    totalTiles[i] = UnionTiles[idx - 256];
                }
                else
                {
                    totalTiles[i] = new byte[bytesPerTile];
                }
            }

            GenerateTileSet(totalTiles, pal, 20, outPath, outName);

            byte[] gidMap = new byte[tilemap.Length];
            for (var i = 0; i < gidMap.Length; i++)
            {
                byte t = tilemap[i];
                ushort tileIdx = lut[t];
                if (tileIdx > 256 + NumUnionTiles) { throw new InvalidDataException(); }
                // replace first union tile with an empty-by-convention tile
                gidMap[i] = (byte) ((tileIdx == 256) ? 0 : (t + 1));
            }

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false)
            };

            using (XmlWriter writer = XmlWriter.Create(baseFilename + ".tmx", settings))
            {
                writer.WriteStartDocument();

                writer.WriteStartElement("map");
                writer.WriteAttributeString("version", "1.0");
                writer.WriteAttributeString("orientation", "orthogonal");
                writer.WriteAttributeString("renderorder", "right-down");
                writer.WriteAttributeString("width", LevelTilesPerRow.ToString());
                writer.WriteAttributeString("height", numTileRows.ToString());
                writer.WriteAttributeString("tilewidth", TileSide.ToString());
                writer.WriteAttributeString("tileheight", TileSide.ToString());

                writer.WriteStartElement("tileset");
                writer.WriteAttributeString("firstgid", "1");
                writer.WriteAttributeString("source", outName + ".tsx");
                writer.WriteEndElement();

                writer.WriteStartElement("layer");
                writer.WriteAttributeString("name", "Tiles");
                writer.WriteAttributeString("width", LevelTilesPerRow.ToString());
                writer.WriteAttributeString("height", numTileRows.ToString());

                WriteXmlTmxDataAttribute(writer, gidMap, true);

                writer.WriteEndElement();

                writer.WriteEndDocument();

                writer.Flush();
            }
        }

        private static void WriteXmlTmxDataAttribute(XmlWriter writer, byte[] gidMap, bool compress)
        {
            uint Adler32(byte[] bytes)
            {
                const uint a32Mod = 0xFFF1;
                uint s1 = 1, s2 = 0;
                foreach (byte b in bytes)
                {
                    s1 = (s1 + b) % a32Mod;
                    s2 = (s2 + s1) % a32Mod;
                }
                return (s2 << 16) | s1;
            }

            writer.WriteStartElement("data");

            if (compress)
            {
                writer.WriteAttributeString("encoding", "base64");
                writer.WriteAttributeString("compression", "zlib");

                byte[] map = new byte[gidMap.Length * 4];
                using (MemoryStream uncompressedStream = new MemoryStream(map))
                {
                    using (BinaryWriter bw = new BinaryWriter(uncompressedStream, Encoding.UTF8, true))
                    {
                        for (var i = 0; i < gidMap.Length; i++)
                        {
                            bw.Write((UInt32)gidMap[i]); // 4-bytes little endian
                        }
                    }
                }

                var compressedStream = new MemoryStream();
                compressedStream.WriteByte(0x78);
                compressedStream.WriteByte(0x9C);
                using (MemoryStream uncompressedStream = new MemoryStream(map))
                {
                    using (var compressorStream = new DeflateStream(compressedStream, CompressionMode.Compress, true))
                    {
                        uncompressedStream.CopyTo(compressorStream);
                    }
                }
                byte[] checksum = BitConverter.GetBytes(Adler32(map));
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(checksum);
                }
                compressedStream.Write(checksum, 0, checksum.Length);

                byte[] compressed = compressedStream.ToArray();
                writer.WriteBase64(compressed, 0, compressed.Length);
            }
            else
            {
                writer.WriteAttributeString("encoding", "csv");

                int idx = 0;
                StringBuilder sb = new StringBuilder();
                while (idx < gidMap.Length)
                {
                    sb.AppendLine();
                    for (var j = 0; j < LevelTilesPerRow; j++)
                    {
                        sb.Append(gidMap[idx++]);
                        sb.Append(",");
                    }
                }
                sb.Remove(sb.Length - 1, 1); // remove last comma
                sb.AppendLine();
                writer.WriteString(sb.ToString());
            }

            writer.WriteEndElement();
        }

        private static int DivideWithRoundUp(int x, int y)
        {
            return x / y + (x % y > 0 ? 1 : 0);
        }

        private static void GenerateTileSet(byte[][] tiles, byte[] pal, int tilesPerRow, string outPath, string baseFileName)
        {
            const int tileSide = 16;

            int numTiles = tiles.Length;
            int tilesPerColumn = DivideWithRoundUp(numTiles,tilesPerRow);

            int outWidth = tileSide * tilesPerRow;
            int outHeight = tileSide * tilesPerColumn;

            int bytesPerTileRow = TileImageInfoPng.BytesPerRow;
            const int numPaletteEntries = 16;

            using (FileStream outPng = File.Create(Path.Combine(outPath, baseFileName) + ".png"))
            {
                ImageInfo imiPng = new ImageInfo(outWidth, outHeight, 8, false, false, true);
                PngWriter pngw = new PngWriter(outPng, imiPng);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, pal);
                PngChunkTRNS trns = pngw.GetMetadata().CreateTRNSChunk();
                trns.setIndexEntryAsTransparent(0);
                byte[] row = new byte[imiPng.BytesPerRow];
                for (var i = 0; i < tilesPerColumn; i++)
                {
                    for (var line = 0; line < tileSide; line++)
                    {
                        int offsetInsideTile = line * bytesPerTileRow;
                        for (var j = 0; j < tilesPerRow; j++)
                        {
                            int tileIndex = i * tilesPerRow + j;
                            if (tileIndex < numTiles)
                            {
                                Array.Copy(tiles[tileIndex], offsetInsideTile, row, j * bytesPerTileRow, bytesPerTileRow);
                            }
                            else
                            {
                                Array.Clear(row, j * bytesPerTileRow, bytesPerTileRow); // fill with zeroes
                            }
                        }
                        pngw.WriteRowByte(row, i * tileSide + line);
                    }
                }
                pngw.End();
            }
            WriteTsx(baseFileName, outPath, tileSide, outWidth, outHeight);
        }

        private static void WriteTsx(string baseFilename, string outPath, int tileSide, int outWidth, int outHeight)
        {
            string outFilename = Path.Combine(outPath, baseFilename + ".tsx");

            XmlDocument doc = new XmlDocument();

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            XmlElement tileset = doc.CreateElement("tileset");
            tileset.SetAttribute("name", baseFilename); // set tileset name to the base filename
            tileset.SetAttribute("tilewidth", tileSide.ToString());
            tileset.SetAttribute("tileheight", tileSide.ToString());
            doc.AppendChild(tileset);

            XmlElement image = doc.CreateElement("image");
            image.SetAttribute("source", baseFilename + ".png");
            image.SetAttribute("width", outWidth.ToString());
            image.SetAttribute("height", outHeight.ToString());
            tileset.AppendChild(image);

            doc.Save(outFilename);
        }

        private static byte[][] ReadTiles(byte[] rawTiles, int numTiles)
        {
            using (Stream input = new MemoryStream(rawTiles, false))
            {
                return ReadTiles(input, numTiles);
            }
        }

        private static byte[][] ReadTiles(Stream input, int numTiles)
        {
            ImageInfo tileImageInfoInput = new ImageInfo(TileSide, TileSide, 4, false, false, true);
            int tileLength = tileImageInfoInput.BytesPerRow * tileImageInfoInput.Rows;
            byte[][] tiles = new byte[numTiles][];
            byte[] buffer = new byte[tileLength];
            for (var i = 0; i < numTiles; i++)
            {
                input.Read(buffer, 0, tileLength);
                tiles[i] = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(buffer, tileLength));
            }
            return tiles;
        }

        private static byte[][] ReadLevelPalettes(string srcFilename)
        {
            const int numLevelPalettes = 13;
            const int bytesPerPalette = 3 * 16;
            byte[][] palettes = new byte[numLevelPalettes][];
            using (FileStream input = File.OpenRead(srcFilename))
            {
                for (var i = 0; i < numLevelPalettes; i++)
                {
                    byte[] palette = new byte[bytesPerPalette];
                    input.Read(palette, 0, bytesPerPalette);
                    palettes[i] = palette;
                }
            }
            return palettes;
        }

        private static int ConvertVgaToRgb(int sixBitValue)
        {
            sixBitValue &= 0x3F; // make sure it's really 6-bit value
            // 6-bit VGA to 8-bit RGB, approximation to Round(sixBitValue * 255 / 63)
            //int eightBitValue = (sixBitValue * 255) / 63; // quite bad
            int eightBitValue = (sixBitValue << 2) | (sixBitValue >> 4); // almost correct
            return eightBitValue;
        }

        private static void FillPalette(PngChunkPLTE palette, int numEntries, byte[] vgaPalette)
        {
            palette.SetNentries(numEntries);
            for (var i = 0; i < numEntries; i++)
            {
                int r = ConvertVgaToRgb(vgaPalette[i * 3 + 0]);
                int g = ConvertVgaToRgb(vgaPalette[i * 3 + 1]);
                int b = ConvertVgaToRgb(vgaPalette[i * 3 + 2]);
                palette.SetEntry(i, r, g, b);
            }
        }

        private static byte[] ConvertIndex4ToIndex8Bytes(byte[] packedBytes)
        {
            byte[] outBytes = new byte[packedBytes.Length * 2];
            int idx = 0;
            while (idx < outBytes.Length)
            {
                byte b = packedBytes[idx / 2];
                outBytes[idx++] = (byte)((b & 0xF0) >> 4);
                outBytes[idx++] = (byte) (b & 0x0F);
            }
            return outBytes;
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
