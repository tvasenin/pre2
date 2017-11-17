using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;
using System;
using System.IO;
using System.Xml;
using Tilengine;

namespace Pre2
{
    public static class AssetConverter
    {
        private const string SqzDir = "sqz";
        private const string CacheDir = "cache";
        private const string ResDir = "res";
        private const string SoundDir = CacheDir + "/audio";

        private const int TileSide = 16;
        private const int LevelTilesPerRow = 256;

        private const int NumSprites = 460;
        private const int NumFrontTiles = 163;
        private const int NumUnionTiles = 544;

        private static readonly byte[][] LevelPalettes = ReadLevelPalettes(Path.Combine(ResDir, "levels.pals"));

        private static readonly char[] LevelSuffixes = {  '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G'};
        private static readonly byte[] LevelNumRows  = {  49, 104,  49,  45, 128, 128, 128,  86, 110,  12,  24,  51,  51,  38, 173,  84 };
        private static readonly byte[] LevelPals     = {   8,  10,   7,   6,   3,   5,   1,   4,   2,   2,  11,  11,  11,  12,   2,   1 }; // no pal #0 and #9!
        private static readonly char[] BackSuffixes  = {  '0', '0', '0', '1', '1', '1', '2', '3', '3', '0', '4', '4', '4', '5', '0', '2'};

        private static readonly byte[][] FrontTiles = ReadTiles(SqzUnpacker.Unpack(Path.Combine(SqzDir, "FRONT.SQZ")), NumFrontTiles, TileSide, TileSide);
        private static readonly byte[][] UnionTiles = ReadTiles(SqzUnpacker.Unpack(Path.Combine(SqzDir, "UNION.SQZ")), NumUnionTiles, TileSide, TileSide);

        private static readonly SpriteInfo FontYearDevsInfo = new SpriteInfo { W =  8, H = 12 };
        private static readonly SpriteInfo PanelSpritesInfo = new SpriteInfo { W = 16, H = 12 };
        private static readonly SpriteInfo FontUnknownInfo  = new SpriteInfo { W = 16, H = 11 };
        private static readonly byte[][] FontYearDevs;
        private static readonly byte[][] PanelSprites;
        private static readonly byte[][] FontUnknown;

        private static readonly SpriteInfo PanelImageInfo = new SpriteInfo { W = 320, H = 23 };
        private static readonly byte[] PanelImage;

        private static readonly byte[] UnknownAllFontsData = new byte[2368];

        static AssetConverter()
        {
            // Read AllFonts
            using (Stream input = new MemoryStream(SqzUnpacker.Unpack(Path.Combine(SqzDir, "ALLFONTS.SQZ"))))
            {
                FontYearDevs = ReadTiles(input, 41, FontYearDevsInfo.W, FontYearDevsInfo.H);
                byte[] panelImageRaw = new byte[3680];
                input.Read(panelImageRaw, 0, panelImageRaw.Length);
                PanelImage = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(panelImageRaw));
                PanelSprites = ReadTiles(input, 17, PanelSpritesInfo.W, PanelSpritesInfo.H);
                FontUnknown = ReadTiles(input, 10, FontUnknownInfo.W, FontUnknownInfo.H);
                input.Read(UnknownAllFontsData, 0, UnknownAllFontsData.Length);
            }
        }

        public static void PrepareAllAssets()
        {
            Directory.CreateDirectory(CacheDir);
            ConvertAllFonts();

            ConvertIndex8WithPalette("CASTLE");
            ConvertIndex8WithPalette("MENU");
            ConvertIndex8WithPalette("THEEND");
            ConvertIndex8WithPalette("TITUS");

            // Palette for MENU2 is concatenated at the end of the image (using a copy for convenience)!
            ConvertIndex4("GAMEOVER", File.ReadAllBytes(Path.Combine(ResDir, "gameover.pal")), 320, 200);
            ConvertIndex4("MAP",      File.ReadAllBytes(Path.Combine(ResDir, "map.pal")),      640, 200);
            ConvertIndex4("MENU2",    File.ReadAllBytes(Path.Combine(ResDir, "menu2.pal")),    320, 200);
            ConvertIndex4("MOTIF",    File.ReadAllBytes(Path.Combine(ResDir, "motif.pal")),    320, 200);

            ConvertTitle("PRESENT");

            GenerateSpriteSet(LevelPalettes[0]);

            GenerateTileSet(FrontTiles, LevelPalettes[0], NumFrontTiles, TileSide, TileSide, CacheDir, "FRONT");

            Directory.CreateDirectory(SoundDir);
            UnpackTrk("BOULA");
            UnpackTrk("BRAVO");
            UnpackTrk("CARTE");
            UnpackTrk("CODE");
            UnpackTrk("FINAL");
            UnpackTrk("GLACE");
            UnpackTrk("KOOL");
            UnpackTrk("MINES");
            UnpackTrk("MONSTER");
            UnpackTrk("MYSTERY");
            UnpackTrk("PRES");
            UnpackTrk("PRESENTA");

            string rawDir = CacheDir + "/RAW";
            Directory.CreateDirectory(rawDir);
            File.WriteAllBytes(rawDir + "/SAMPLE.BIN",   SqzUnpacker.Unpack(SqzDir + "/SAMPLE.SQZ"));
            File.WriteAllBytes(rawDir + "/KEYB.BIN",     SqzUnpacker.Unpack(SqzDir + "/KEYB.SQZ"));
        }

        public static Palette GetLevelPalette(int levelIdx)
        {
            int numPaletteEntries = 16;
            byte[] vgaPalette = LevelPalettes[LevelPals[levelIdx]];
            Palette palette = new Palette(numPaletteEntries);
            for (var i = 0; i < numPaletteEntries; i++)
            {
                byte r = ConvertVgaToRgb(vgaPalette[i * 3 + 0]);
                byte g = ConvertVgaToRgb(vgaPalette[i * 3 + 1]);
                byte b = ConvertVgaToRgb(vgaPalette[i * 3 + 2]);
                palette.SetColor(i, new Color(r, g, b));
            }
            return palette;
        }

        public static Bitmap GetLevelBackground(int levelIdx)
        {
            int width = 320;
            int height = 200;
            int numBytesInput = width * height / 2; // 4 bpp
            byte[] rawData = SqzUnpacker.Unpack(Path.Combine(SqzDir, "BACK" + BackSuffixes[levelIdx] + ".SQZ"));
            if (rawData.Length != numBytesInput)
            {
                Array.Resize(ref rawData, numBytesInput);
            }
            byte[] indexBytes = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(rawData));
            Bitmap bitmap = new Bitmap(width, height, 8)
            {
                PixelData = indexBytes,
                Palette = GetLevelPalette(levelIdx)
            };
            return bitmap;
        }

        public static Tilemap GetLevelTilemap(int levelIdx)
        {
            byte[] rawData = SqzUnpacker.Unpack(Path.Combine(SqzDir, "LEVEL" + LevelSuffixes[levelIdx] + ".SQZ"));
            int numRows = LevelNumRows[levelIdx];
            Palette palette = GetLevelPalette(levelIdx);
            return GenerateLevelTilemap(rawData, numRows, palette);
        }

        private static void UnpackTrk(string resource)
        {
            string destFilename = Path.Combine(SoundDir, resource + ".mod");
            string sqzFilename = Path.Combine(SqzDir, resource + ".TRK");
            byte[] data = SqzUnpacker.Unpack(sqzFilename);
            File.WriteAllBytes(destFilename, data);
        }

        private struct SpriteSetEntry
        {
            public int PosX { get; }
            public int PosY { get; }
            public int Width { get; }
            public int Height { get; }

            public SpriteSetEntry(int posX, int posY, int width, int height)
            {
                PosX = posX;
                PosY = posY;
                Width = width;
                Height = height;
            }
        }

        private static void GenerateSpriteSet(byte[] pal)
        {

            string resPath = Path.Combine(ResDir, "sprites.txt");
            SpriteSetEntry[] entries = ReadSpriteSetEntries(resPath);

            byte[] rawSprites = SqzUnpacker.Unpack(Path.Combine(SqzDir, "SPRITES.SQZ"));
            byte[][] sprites = ReadSprites(rawSprites, entries);

            int spritesheetW = 0;
            int spritesheetH = 0;
            foreach (SpriteSetEntry entry in entries)
            {
                int localW = entry.PosX + entry.Width;
                int localH = entry.PosY + entry.Height;
                if (spritesheetW < localW) { spritesheetW = localW; }
                if (spritesheetH < localH) { spritesheetH = localH; }
            }

            byte[][] image = new byte[spritesheetH][];
            for (var i = 0; i < image.Length; i++) { image[i] = new byte[spritesheetW]; }

            for (var i = 0; i < entries.Length; i++)
            {
                SpriteSetEntry entry = entries[i];
                byte[] sprite = sprites[i];
                for (var spriteLine = 0; spriteLine < entry.Height; spriteLine++)
                {
                    int targetLine = entry.PosY + spriteLine;
                    Array.Copy(sprite, spriteLine * entry.Width, image[targetLine], entry.PosX, entry.Width);
                }
            }

            using (FileStream outPng = File.Create(Path.Combine(CacheDir, "sprites.png")))
            {
                int numPaletteEntries = pal.Length / 3;
                ImageInfo imiPng = new ImageInfo(spritesheetW, spritesheetH, 8, false, false, true);
                PngWriter pngw = new PngWriter(outPng, imiPng);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, pal);
                PngChunkTRNS trns = pngw.GetMetadata().CreateTRNSChunk();
                trns.setIndexEntryAsTransparent(0);
                pngw.WriteRowsByte(image);
                pngw.End();
            }

            string spriteset = Path.Combine(CacheDir, Path.GetFileName(resPath));
            File.Copy(resPath, spriteset, true);
        }

        private static SpriteSetEntry[] ReadSpriteSetEntries(string txtFilename)
        {
            string[] lines = File.ReadAllLines(txtFilename);
            SpriteSetEntry[] entries = new SpriteSetEntry[NumSprites];
            for (var i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (String.IsNullOrWhiteSpace(line)) { continue; }
                string[] data = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (data.Length != 6 || !data[1].Equals("=")) { throw  new InvalidDataException("Spritesheet error at line " + i); }
                entries[i] = new SpriteSetEntry(int.Parse(data[2]), int.Parse(data[3]), int.Parse(data[4]), int.Parse(data[5]));
            }
            return entries;
        }

        private static byte[][] ReadSprites(byte[] rawSprites, SpriteSetEntry[] entries)
        {
            byte[][] sprites = new byte[entries.Length][];
            using (Stream input = new MemoryStream(rawSprites, false))
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    SpriteSetEntry entry = entries[i];
                    ImageInfo imi = new ImageInfo(entry.Width, entry.Height, 4, false, false, true);
                    int inputLength = imi.BytesPerRow * imi.Rows;
                    byte[] buffer = new byte[inputLength];
                    input.Read(buffer, 0, inputLength);
                    sprites[i] = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(buffer));
                }
            }
            return sprites;
        }

        private static void ConvertIndex8WithPalette(string resource)
        {
            string destFilename = Path.Combine(CacheDir, resource + ".png");
            string sqzFilename = Path.Combine(SqzDir, resource + ".SQZ");
            byte[] data = SqzUnpacker.Unpack(sqzFilename);
            ImageInfo imi = new ImageInfo(320, 200, 8, false, false, true);
            const int numPaletteEntries = 256;
            byte[] pal = new byte[numPaletteEntries * 3];
            byte[] indexBytes = new byte[imi.BytesPerRow * imi.Rows];
            using (Stream input = new MemoryStream(data))
            {
                input.Read(pal, 0, pal.Length);
                input.Read(indexBytes, 0, indexBytes.Length);
            }
            WritePng8(destFilename, indexBytes, pal, 320, 200);
        }

        private static void ConvertIndex4(string resource, byte[] pal, int width, int height)
        {
            string destFilename = Path.Combine(CacheDir, resource + ".png");
            string sqzFilename = Path.Combine(SqzDir, resource + ".SQZ");
            ConvertIndex4(sqzFilename, destFilename, pal, width, height);
        }

        private static void ConvertIndex4(string sqzFilename, string destFilename, byte[] pal, int width, int height)
        {
            byte[] rawData = SqzUnpacker.Unpack(sqzFilename);
            using (Stream input = new MemoryStream(rawData))
            {
                ConvertIndex4(input, destFilename, pal, width, height);
            }
        }

        private static void ConvertIndex4(Stream input, string destFilename, byte[] pal, int width, int height)
        {
            int numBytesInput = width * height / 2; // 4bpp!
            byte[] rawData = new byte[numBytesInput];
            input.Read(rawData, 0, rawData.Length);
            ConvertIndex4(rawData, destFilename, pal, width, height);
        }

        private static void ConvertIndex4(byte[] rawData, string destFilename, byte[] pal, int width, int height)
        {
            byte[] indexBytes = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(rawData));
            WritePng8(destFilename, indexBytes,pal, width, height);
        }

        private static void ConvertTitle(string resource)
        {
            byte[] data = SqzUnpacker.Unpack(Path.Combine(SqzDir, resource + ".SQZ"));
            ImageInfo imi = new ImageInfo(320, 200, 8, false, false, true);
            const int numPaletteEntries = 256;
            byte[] pal = new byte[numPaletteEntries * 3];
            int imageLength = imi.BytesPerRow * imi.Rows;
            byte[] imageBackground = new byte[imageLength];
            byte[] imageForeground = new byte[imageLength];
            using (Stream input = new MemoryStream(data))
            {
                input.Read(pal, 0, pal.Length);
                input.Read(imageBackground, 0, imageLength);
                input.Seek(0x600, SeekOrigin.Current);
                int numRead = input.Read(imageForeground, 0, imageLength);
                if (numRead < imageLength)
                {
                    Array.Copy(imageBackground, numRead, imageForeground, numRead, imageLength - numRead);
                }
            }
            string destBackground = Path.Combine(CacheDir, resource + "_B" + ".png");
            string destForeground = Path.Combine(CacheDir, resource + "_F" + ".png");
            WritePng8(destBackground, imageBackground, pal, 320, 200);
            WritePng8(destForeground, imageForeground, pal, 320, 200);
        }

        public static Bitmap GetDevPhoto()
        {
            byte[] planes01 = SqzUnpacker.Unpack(Path.Combine(SqzDir, "LEVELH.SQZ"));
            byte[] planes02 = SqzUnpacker.Unpack(Path.Combine(SqzDir, "LEVELI.SQZ"));

            byte[] planes = new byte[planes01.Length + planes02.Length];
            Array.Copy(planes01, 0, planes, 0, planes01.Length);
            Array.Copy(planes02, 0, planes, planes01.Length, planes02.Length);

            byte[] indexBytes = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(planes));

            // input height is 415, need to pad the remaining lines with zeroes
            int imageLength = 640 * 480; // assuming 8bpp!
            if (indexBytes.Length < imageLength)
            {
                Array.Resize(ref indexBytes, imageLength);
            }

            // generate greyscale palette (naive way)
            const int numPaletteEntries = 16;
            Palette palette = new Palette(numPaletteEntries);
            for (var i = 0; i < numPaletteEntries; i++)
            {
                byte c = ConvertVgaToRgb((byte) (i * 4));
                palette.SetColor(i, new Color(c, c, c));
            }

            Bitmap devBitmap = new Bitmap(640, 480, 8)
            {
                PixelData = indexBytes,
                Palette = palette
            };
            return devBitmap;
        }

        private static Tilemap GenerateLevelTilemap(byte[] rawData, int numRows, Palette palette)
        {
            int tilemapLength = numRows * LevelTilesPerRow;
            byte[] tilemapBytes = new byte[tilemapLength];
            ushort[] lut = new ushort[256];
            byte[][] localTiles;
            using (BinaryReader br = new BinaryReader(new MemoryStream(rawData, false)))
            {
                br.Read(tilemapBytes, 0, tilemapBytes.Length);
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
                localTiles = ReadTiles(br.BaseStream, maxLocalIdx + 1, TileSide, TileSide);
            }

            Tileset tileset = new Tileset(lut.Length, TileSide, TileSide, palette, new SequencePack(), null);
            // Fetch 256 tiles according to LUT
            int bytesPerTile = UnionTiles[0].Length;
            int pitch = TileSide; // assume 8bpp!
            for (var i = 0; i < lut.Length; i++)
            {
                byte[] tilePixels;
                ushort idx = lut[i];
                if (idx < 256)
                {
                    tilePixels = localTiles[idx];
                }
                else if (idx < 256 + NumUnionTiles)
                {
                    tilePixels = UnionTiles[idx - 256];
                }
                else
                {
                    tilePixels = new byte[bytesPerTile];
                }
                tileset.SetPixels(i + 1, tilePixels, pitch); // first tile index is 1!
            }

            Tile[] tiles = new Tile[tilemapLength];
            for (var i = 0; i < tilemapBytes.Length; i++)
            {
                byte t = tilemapBytes[i];
                ushort tileIdx = lut[t];
                if (tileIdx > 256 + NumUnionTiles) { throw new InvalidDataException(); }
                // replace first union tile with an empty-by-convention tile
                ushort tileMapIdx = (ushort) ((tileIdx == 256) ? 0 : (t + 1)); // first tile index is 1!
                tiles[i] = new Tile { index = tileMapIdx };
            }

            Tilemap tilemap = new Tilemap(numRows, LevelTilesPerRow, tiles, new Color(0, 0, 0), tileset);
            return tilemap;
        }

        private static void ConvertAllFonts()
        {
            byte[] palYearDevs = File.ReadAllBytes(Path.Combine(ResDir, "year_devs.pal"));
            byte[] palDefault = LevelPalettes[0];

            GenerateTileSet(FontYearDevs, palYearDevs, FontYearDevs.Length, FontYearDevsInfo.W, FontYearDevsInfo.H, CacheDir, "FontYearDevs");
            ConvertIndex4(PanelImage, Path.Combine(CacheDir, "panel.png"), palDefault, PanelImageInfo.W, PanelImageInfo.H);
            GenerateTileSet(PanelSprites, palDefault, PanelSprites.Length, PanelSpritesInfo.W, PanelSpritesInfo.H, CacheDir, "PanelSprites");
            GenerateTileSet(FontUnknown, palDefault, FontUnknown.Length, FontUnknownInfo.W, FontUnknownInfo.H, CacheDir, "FontUnknown");

            string rawDir = CacheDir + "/RAW";
            Directory.CreateDirectory(rawDir);
            File.WriteAllBytes(Path.Combine(rawDir, "ALLFONTS_UNKNOWN_PART.bin"), UnknownAllFontsData);
        }

        private static int DivideWithRoundUp(int x, int y)
        {
            return x / y + (x % y > 0 ? 1 : 0);
        }

        private static void GenerateTileSet(byte[][] tiles, byte[] pal, int tilesPerRow, int tileWidth, int tileHeight, string outPath, string baseFileName)
        {
            int numTiles = tiles.Length;
            int tilesPerColumn = DivideWithRoundUp(numTiles,tilesPerRow);

            int outWidth = tileWidth * tilesPerRow;
            int outHeight = tileHeight * tilesPerColumn;

            int bytesPerTileRow = tileWidth; // assume 8bpp
            
            const int numPaletteEntries = 16;

            using (FileStream outPng = File.Create(Path.Combine(outPath, baseFileName + ".png")))
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
                    for (var line = 0; line < tileHeight; line++)
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
                        pngw.WriteRowByte(row, i * tileHeight + line);
                    }
                }
                pngw.End();
            }
            WriteTsx(baseFileName, outPath, tileWidth, tileHeight, outWidth, outHeight);
        }

        private static void WriteTsx(string baseFilename, string outPath, int tileWidth, int tileHeight, int outWidth, int outHeight)
        {
            string outFilename = Path.Combine(outPath, baseFilename + ".tsx");

            XmlDocument doc = new XmlDocument();

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            XmlElement tileset = doc.CreateElement("tileset");
            tileset.SetAttribute("name", baseFilename); // set tileset name to the base filename
            tileset.SetAttribute("tilewidth", tileWidth.ToString());
            tileset.SetAttribute("tileheight", tileHeight.ToString());
            doc.AppendChild(tileset);

            XmlElement image = doc.CreateElement("image");
            image.SetAttribute("source", baseFilename + ".png");
            image.SetAttribute("width", outWidth.ToString());
            image.SetAttribute("height", outHeight.ToString());
            tileset.AppendChild(image);

            doc.Save(outFilename);
        }

        private static byte[][] ReadTiles(byte[] rawTiles, int numTiles, int tileWidth, int tileHeight)
        {
            using (Stream input = new MemoryStream(rawTiles, false))
            {
                return ReadTiles(input, numTiles, tileWidth, tileHeight);
            }
        }

        private static byte[][] ReadTiles(Stream input, int numTiles, int tileWidth, int tileHeight)
        {
            ImageInfo tileImageInfoInput = new ImageInfo(tileWidth, tileHeight, 4, false, false, true);
            int tileLength = tileImageInfoInput.BytesPerRow * tileImageInfoInput.Rows;
            byte[][] tiles = new byte[numTiles][];
            byte[] buffer = new byte[tileLength];
            for (var i = 0; i < numTiles; i++)
            {
                input.Read(buffer, 0, tileLength);
                tiles[i] = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(buffer));
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

        private static byte ConvertVgaToRgb(byte sixBitValue)
        {
            sixBitValue &= 0x3F; // make sure it's really 6-bit value
            // 6-bit VGA to 8-bit RGB, approximation to Round(sixBitValue * 255 / 63)
            //byte eightBitValue = (byte) ((sixBitValue * 255) / 63); // quite bad
            byte eightBitValue = (byte) ((sixBitValue << 2) | (sixBitValue >> 4)); // almost correct
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

        private static void WritePng8(string filename, byte[] indexBytes, byte[] palVga, int width, int height)
        {
            ImageInfo imiPng = new ImageInfo(width, height, 8, false, false, true);
            int numBytesRowPng = imiPng.BytesPerRow;
            byte[] row = new byte[numBytesRowPng];
            int numPaletteEntries = palVga.Length / 3;
            using (FileStream output = File.Create(filename))
            {
                PngWriter pngw = new PngWriter(output, imiPng);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, palVga);
                for (var i = 0; i < imiPng.Rows; i++)
                {
                    Array.Copy(indexBytes, i * numBytesRowPng, row, 0, row.Length);
                    pngw.WriteRowByte(row, i);
                }
                pngw.End();
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

        private static byte[] ConvertPlanarIndex4Bytes(byte[] data)
        {
            int targetLength = data.Length;
            if ((targetLength % 4) != 0) { throw new ArgumentException("Image length should be a multiple of 4!"); }
            byte[] indexBytes = new byte[targetLength];
            int planeLength = targetLength / 4;
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
