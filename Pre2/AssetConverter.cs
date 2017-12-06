using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;
using System;
using System.IO;
using System.Text;
using System.Xml;
using Tilengine;

namespace Pre2
{
    public static class AssetConverter
    {
        private const string SqzDir = "sqz";
        public const string CacheDir = "cache";
        private const string ResDir = "res";
        private const string SoundDir = CacheDir + "/audio";

        private const int TileSide = 16;
        private const int LevelTilesPerRow = 256;

        private const int NumSprites = 460;
        private const int NumFrontTiles = 163;
        private const int NumUnionTiles = 544;

        private static readonly SpriteInfo DefaultTileInfo = new SpriteInfo { W = TileSide, H = TileSide };

        private static readonly byte[][] LevelPalettes = ReadLevelPalettes(Path.Combine(ResDir, "levels.pals"));

        private static readonly char[] LevelSuffixes = {  '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G'};
        private static readonly byte[] LevelNumRows  = {  49, 104,  49,  45, 128, 128, 128,  86, 110,  12,  24,  51,  51,  38, 173,  84 };
        private static readonly byte[] LevelPals     = {   8,  10,   7,   6,   3,   5,   1,   4,   2,   2,  11,  11,  11,  12,   2,   1 }; // no pal #0 and #9!
        private static readonly char[] BackSuffixes  = {  '0', '0', '0', '1', '1', '1', '2', '3', '3', '0', '4', '4', '4', '5', '0', '2'};

        private static readonly byte[][] LevelTilemaps = new byte[LevelSuffixes.Length][];
        private static readonly ushort[][] LevelLuts = new ushort[LevelSuffixes.Length][];
        private static readonly byte[][][] LevelLocalTiles = new byte[LevelSuffixes.Length][][];
        public static readonly byte[][] LevelDescriptors = new byte[LevelSuffixes.Length][];

        private static readonly byte[][] FrontTiles = ReadTiles(UnpackSqz("FRONT"), NumFrontTiles, DefaultTileInfo);
        private static readonly byte[][] UnionTiles = ReadTiles(UnpackSqz("UNION"), NumUnionTiles, DefaultTileInfo);

        private static readonly SpriteData[] SpriteSetEntries = ReadSpriteSetEntries(Path.Combine(ResDir, "sprites.txt"), NumSprites);
        private static readonly byte[][] SpriteImages = ReadSprites(UnpackSqz("SPRITES"), SpriteSetEntries);

        private static readonly SpriteInfo BackgroundInfo = new SpriteInfo { W = 320, H = 200 };
        private static readonly SpriteInfo MapInfo = new SpriteInfo { W = 640, H = 200 };

        private static readonly byte[] FontCreditsCharCodes = Encoding.ASCII.GetBytes("0123456789!?.$_ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        private static readonly SpriteInfo FontCreditsInfo  = new SpriteInfo { W =  8, H = 12 };
        private static readonly SpriteInfo PanelSpritesInfo = new SpriteInfo { W = 16, H = 12 };
        private static readonly SpriteInfo FontUnknownInfo  = new SpriteInfo { W = 16, H = 11 };
        private static readonly byte[][] FontCreditsDevs;
        private static readonly byte[][] PanelSprites;
        private static readonly byte[][] FontUnknown;

        private static readonly SpriteInfo PanelImageInfo = new SpriteInfo { W = 320, H = 23 };
        private static readonly byte[] PanelImage;

        private static readonly byte[] UnknownAllFontsData = new byte[2368];

        static AssetConverter()
        {
            // Read AllFonts
            using (Stream input = new MemoryStream(UnpackSqz("ALLFONTS")))
            {
                FontCreditsDevs = ReadTiles(input, 41, FontCreditsInfo);
                byte[] panelImageRaw = new byte[PanelImageInfo.W * PanelImageInfo.H / 2];
                input.Read(panelImageRaw, 0, panelImageRaw.Length);
                PanelImage = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(panelImageRaw));
                PanelSprites = ReadTiles(input, 17, PanelSpritesInfo);
                FontUnknown = ReadTiles(input, 10, FontUnknownInfo);
                input.Read(UnknownAllFontsData, 0, UnknownAllFontsData.Length);
            }

            // Read all Levels
            for (var levelIdx = 0; levelIdx < LevelSuffixes.Length; levelIdx++)
            {
                byte[] rawData = UnpackSqz("LEVEL" + LevelSuffixes[levelIdx]);
                using (BinaryReader br = new BinaryReader(new MemoryStream(rawData, false)))
                {
                    int tilemapLength = LevelNumRows[levelIdx] * LevelTilesPerRow;
                    byte[] tilemapBytes = new byte[tilemapLength];
                    br.Read(tilemapBytes, 0, tilemapBytes.Length);
                    LevelTilemaps[levelIdx] = tilemapBytes;
                    ushort[] lut = new ushort[256];
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
                    LevelLuts[levelIdx] = lut;
                    LevelLocalTiles[levelIdx] = ReadTiles(br.BaseStream, maxLocalIdx + 1, DefaultTileInfo);
                    LevelDescriptors[levelIdx] = br.ReadBytes(5029);
                }
            }
        }

        public static void PrepareAllAssets()
        {
            Directory.CreateDirectory(CacheDir);
            ConvertAllFonts();

            // Palette for MENU2 is concatenated at the end of the image (using a copy for convenience)!
            ConvertIndex4("GAMEOVER", File.ReadAllBytes(Path.Combine(ResDir, "gameover.pal")), BackgroundInfo);
            ConvertIndex4("MAP",      File.ReadAllBytes(Path.Combine(ResDir, "map.pal")),      MapInfo);
            ConvertIndex4("MENU2",    File.ReadAllBytes(Path.Combine(ResDir, "menu2.pal")),    BackgroundInfo);
            ConvertIndex4("MOTIF",    File.ReadAllBytes(Path.Combine(ResDir, "motif.pal")),    BackgroundInfo);

            ConvertTitle("PRESENT");

            GenerateSpriteSet(LevelPalettes[0]);

            GenerateTileSet(FrontTiles, LevelPalettes[0], NumFrontTiles, DefaultTileInfo, CacheDir, "FRONT");

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
            File.WriteAllBytes(rawDir + "/SAMPLE.BIN", UnpackSqz("SAMPLE"));
            File.WriteAllBytes(rawDir + "/KEYB.BIN",   UnpackSqz("KEYB"));
        }

        public static Palette GetLevelPalette(int levelIdx)
        {
            byte[] vgaPalette = LevelPalettes[LevelPals[levelIdx]];
            return GetPalette(vgaPalette);
        }

        public static Bitmap GetLevelBackground(int levelIdx)
        {
            int width = 320;
            int height = 200;
            int numBytesInput = width * height / 2; // 4 bpp
            byte[] rawData = UnpackSqz("BACK" + BackSuffixes[levelIdx]);
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
            return GenerateLevelTilemap(levelIdx);
        }

        public static Bitmap GetCastleBitmap()
        {
            return GetIndex8WithPaletteBackground("CASTLE");
        }

        public static Bitmap GetMenuBitmap()
        {
            return GetIndex8WithPaletteBackground("MENU");
        }

        public static Bitmap GetTheEndBitmap()
        {
            return GetIndex8WithPaletteBackground("THEEND");
        }

        public static Bitmap GetTitusBitmap()
        {
            return GetIndex8WithPaletteBackground("TITUS");
        }

        public static Bitmap GetYearBitmap()
        {
            int width = BackgroundInfo.W;
            int height = BackgroundInfo.H;
            byte[] image = new byte[width * height];
            int year = DateTime.Now.Year;
            if (year >= 1996 && year < 2067) // sic!
            {
                int w = FontCreditsInfo.W;
                int h = FontCreditsInfo.H;
                DrawFontCreditsLine(image, 5 * w,  5 * h, "YEAAA . . .");
                DrawFontCreditsLine(image, 0 * w,  6 * h, "MY GAME IS STILL WORKING IN " + year + " !!");
                DrawFontCreditsLine(image, 1 * w, 12 * h, "PROGRAMMED IN 1992 ON AT .286 12MHZ.");
                DrawFontCreditsLine(image, 3 * w, 13 * h, ". . . ENJOY OLDIES!!");
            }
            Palette palette = GetPalette(File.ReadAllBytes(Path.Combine(ResDir, "credits.pal")));
            Bitmap bitmap = new Bitmap(width, height, 8) { PixelData = image, Palette = palette };
            return bitmap; // TODO: check palette against DOSBox
        }

        public static Bitmap GetCreditsBitmap()
        {
            int width = BackgroundInfo.W;
            int height = BackgroundInfo.H;
            byte[] image = new byte[width * height];
            int w = FontCreditsInfo.W;
            int h = FontCreditsInfo.H ;
            DrawFontCreditsLine(image,  1 * w,  8 +  0 * h, "CODER. DESIGNER AND ARTIST DIRECTOR.");
            DrawFontCreditsLine(image, 14 * w, 10 +  1 * h, "ERIC ZMIRO");
            DrawFontCreditsLine(image,  4 * w,  2 +  4 * h, ".MAIN GRAPHICS AND BACKGROUND.");
            DrawFontCreditsLine(image, 11 * w,  4 +  5 * h, "FRANCIS FOURNIER");
            DrawFontCreditsLine(image,  9 * w,  8 +  7 * h, ".MONSTERS AND HEROS.");
            DrawFontCreditsLine(image, 11 * w, 10 +  8 * h, "LYES  BELAIDOUNI");
            DrawFontCreditsLine(image, 15 * w,  6 + 12 * h, "THANKS TO");
            DrawFontCreditsLine(image,  2 * w,  0 + 14 * h, "CRISTELLE. GIL ESPECHE AND CORINNE.");
            DrawFontCreditsLine(image,  0 * w,  0 + 15 * h, "SEBASTIEN BECHET AND OLIVIER AKA DELTA.");
            Palette palette = GetPalette(File.ReadAllBytes(Path.Combine(ResDir, "credits.pal")));
            Bitmap bitmap = new Bitmap(width, height, 8) { PixelData = image, Palette = palette };
            return bitmap; // TODO: check palette against DOSBox
        }

        private static void DrawFontCreditsLine(byte[] image, int x, int y, string textLine)
        {
            int col = 0;
            byte[] asciiBytes = Encoding.ASCII.GetBytes(textLine);
            foreach (byte c in asciiBytes)
            {
                int idx = Array.IndexOf(FontCreditsCharCodes, c);
                if (idx != -1)
                {
                    int dstX = x + col * FontCreditsInfo.W;
                    int dstY = y;
                    CopyPixels(FontCreditsDevs[idx], FontCreditsInfo, image, BackgroundInfo.W, dstX, dstY);
                }
                col++;
            }
        }

        private static void UnpackTrk(string resource)
        {
            string destFilename = Path.Combine(SoundDir, resource + ".mod");
            byte[] data = UnpackSqz(resource, "TRK");
            File.WriteAllBytes(destFilename, data);
        }

        private static byte[] UnpackSqz(string name, string extension = "SQZ")
        {
            return SqzUnpacker.Unpack(Path.Combine(SqzDir, name + "." + extension));
        }

        private static SpriteInfo GetSpritesheetInfo(SpriteData[] entries)
        {
            SpriteInfo info = new SpriteInfo();
            foreach (SpriteData entry in entries)
            {
                int localW = entry.X + entry.W;
                int localH = entry.Y + entry.H;
                if (info.W < localW) { info.W = localW; }
                if (info.H < localH) { info.H = localH; }
            }
            return info;
        }

        private static byte[] GenerateSpriteSheetImage(byte[][] sprites, SpriteInfo spriteSheetInfo, SpriteData[] entries)
        {
            byte[] image = new byte[spriteSheetInfo.H * spriteSheetInfo.W];
            for (var i = 0; i < SpriteSetEntries.Length; i++)
            {
                SpriteData entry = entries[i];
                byte[] sprite = sprites[i];
                SpriteInfo spriteInfo = new SpriteInfo { W = entry.W, H = entry.H };
                CopyPixels(sprite, spriteInfo, image, spriteSheetInfo.W, entry.X, entry.Y);
            }
            return image;
        }

        private static void GenerateSpriteSet(byte[] pal)
        {
            SpriteInfo spriteSheetInfo = GetSpritesheetInfo(SpriteSetEntries);
            byte[] image = GenerateSpriteSheetImage(SpriteImages, spriteSheetInfo, SpriteSetEntries);

            string filename = Path.Combine(CacheDir, "sprites.png");
            WritePng8(filename, image, pal, spriteSheetInfo, true);

            string resPath = Path.Combine(ResDir, "sprites.txt");
            string spriteset = Path.Combine(CacheDir, Path.GetFileName(resPath));
            File.Copy(resPath, spriteset, true);
        }

        private static SpriteData[] ReadSpriteSetEntries(string txtFilename, int numSprites)
        {
            string[] lines = File.ReadAllLines(txtFilename);
            SpriteData[] entries = new SpriteData[numSprites];
            for (var i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (String.IsNullOrWhiteSpace(line)) { continue; }
                string[] data = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (data.Length != 6 || !data[1].Equals("=")) { throw  new InvalidDataException("Spritesheet error at line " + i); }
                entries[i] = new SpriteData { X = int.Parse(data[2]), Y = int.Parse(data[3]), W = int.Parse(data[4]), H = int.Parse(data[5]) };
            }
            return entries;
        }

        private static byte[][] ReadSprites(byte[] rawSprites, SpriteData[] entries)
        {
            byte[][] sprites = new byte[entries.Length][];
            using (Stream input = new MemoryStream(rawSprites, false))
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    SpriteData entry = entries[i];
                    ImageInfo imi = new ImageInfo(entry.W, entry.H, 4, false, false, true);
                    int inputLength = imi.BytesPerRow * imi.Rows;
                    byte[] buffer = new byte[inputLength];
                    input.Read(buffer, 0, inputLength);
                    sprites[i] = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(buffer));
                }
            }
            return sprites;
        }

        private static Bitmap GetIndex8WithPaletteBackground(string resource)
        {
            byte[] data = UnpackSqz(resource);
            using (BinaryReader br = new BinaryReader(new MemoryStream(data, false)))
            {
                byte[] pal = br.ReadBytes(256 * 3);
                byte[] indexBytes = br.ReadBytes(BackgroundInfo.W * BackgroundInfo.H); // 8 bpp
                Bitmap bitmap = new Bitmap(BackgroundInfo.W, BackgroundInfo.H, 8)
                {
                    PixelData = indexBytes,
                    Palette = GetPalette(pal)
                };
                return bitmap;
            }
        }

        private static void ConvertIndex4(string resource, byte[] pal, SpriteInfo imageInfo)
        {
            string destFilename = Path.Combine(CacheDir, resource + ".png");
            byte[] rawData = UnpackSqz(resource);
            using (Stream input = new MemoryStream(rawData))
            {
                ConvertIndex4(input, destFilename, pal, imageInfo);
            }
        }

        private static void ConvertIndex4(Stream input, string destFilename, byte[] pal, SpriteInfo imageInfo)
        {
            int numBytesInput = imageInfo.W * imageInfo.H / 2; // 4bpp!
            byte[] rawData = new byte[numBytesInput];
            input.Read(rawData, 0, rawData.Length);
            ConvertIndex4(rawData, destFilename, pal, imageInfo);
        }

        private static void ConvertIndex4(byte[] rawData, string destFilename, byte[] pal, SpriteInfo imageInfo)
        {
            byte[] indexBytes = ConvertIndex4ToIndex8Bytes(ConvertPlanarIndex4Bytes(rawData));
            WritePng8(destFilename, indexBytes,pal, imageInfo);
        }

        private static void ConvertTitle(string resource)
        {
            byte[] data = UnpackSqz(resource);
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
            WritePng8(destBackground, imageBackground, pal, BackgroundInfo);
            WritePng8(destForeground, imageForeground, pal, BackgroundInfo);
        }

        public static Bitmap GetDevPhoto()
        {
            byte[] planes01 = UnpackSqz("LEVELH");
            byte[] planes02 = UnpackSqz("LEVELI");

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

        private static Tilemap GenerateLevelTilemap(int levelIdx)
        {
            ushort[] lut = LevelLuts[levelIdx];
            byte[][] localTiles = LevelLocalTiles[levelIdx];
            Palette palette = GetLevelPalette(levelIdx);

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

            int numRows = LevelNumRows[levelIdx];
            int tilemapLength = numRows * LevelTilesPerRow;
            byte[] tilemapBytes = LevelTilemaps[levelIdx];
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
            byte[] palDefault = LevelPalettes[0];

            WritePng8(Path.Combine(CacheDir, "panel.png"), PanelImage, palDefault, PanelImageInfo);
            GenerateTileSet(PanelSprites, palDefault, PanelSprites.Length, PanelSpritesInfo, CacheDir, "PanelSprites");
            GenerateTileSet(FontUnknown, palDefault, FontUnknown.Length, FontUnknownInfo, CacheDir, "FontUnknown");

            string rawDir = CacheDir + "/RAW";
            Directory.CreateDirectory(rawDir);
            File.WriteAllBytes(Path.Combine(rawDir, "ALLFONTS_UNKNOWN_PART.bin"), UnknownAllFontsData);
        }

        private static int DivideWithRoundUp(int x, int y)
        {
            return x / y + (x % y > 0 ? 1 : 0);
        }

        private static void GenerateTileSet(byte[][] tiles, byte[] pal, int tilesPerRow, SpriteInfo tileInfo, string outPath, string baseFileName)
        {
            int numTiles = tiles.Length;
            int tilesPerColumn = DivideWithRoundUp(numTiles,tilesPerRow);

            int outWidth = tileInfo.W * tilesPerRow;
            int outHeight = tileInfo.H * tilesPerColumn;

            SpriteInfo imageInfo = new SpriteInfo { W = outWidth, H = outHeight };

            byte[] imagePixels = new byte[imageInfo.W * imageInfo.H];
            for (var row = 0; row < tilesPerColumn; row++)
            {
                for (var col = 0; col < tilesPerRow; col++)
                {
                    int tileIdx = row * tilesPerRow + col;
                    if (tileIdx < tiles.Length)
                    {
                        CopyPixels(tiles[tileIdx], tileInfo, imagePixels, imageInfo.W, col * tileInfo.W, row * tileInfo.H);
                    }
                }
            }
            string pngFilename = Path.Combine(outPath, baseFileName + ".png");
            WritePng8(pngFilename, imagePixels, pal, imageInfo, true);
            WriteTsx(baseFileName, outPath, tileInfo, imageInfo);
        }

        private static void WriteTsx(string baseFilename, string outPath, SpriteInfo tileInfo, SpriteInfo imageInfo)
        {
            string outFilename = Path.Combine(outPath, baseFilename + ".tsx");

            XmlDocument doc = new XmlDocument();

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            XmlElement tileset = doc.CreateElement("tileset");
            tileset.SetAttribute("name", baseFilename); // set tileset name to the base filename
            tileset.SetAttribute("tilewidth", tileInfo.W.ToString());
            tileset.SetAttribute("tileheight", tileInfo.H.ToString());
            doc.AppendChild(tileset);

            XmlElement image = doc.CreateElement("image");
            image.SetAttribute("source", baseFilename + ".png");
            image.SetAttribute("width", imageInfo.W.ToString());
            image.SetAttribute("height", imageInfo.H.ToString());
            tileset.AppendChild(image);

            doc.Save(outFilename);
        }

        private static byte[][] ReadTiles(byte[] rawTiles, int numTiles, SpriteInfo tileInfo)
        {
            using (Stream input = new MemoryStream(rawTiles, false))
            {
                return ReadTiles(input, numTiles, tileInfo);
            }
        }

        private static byte[][] ReadTiles(Stream input, int numTiles, SpriteInfo tileInfo)
        {
            ImageInfo tileImageInfoInput = new ImageInfo(tileInfo.W, tileInfo.H, 4, false, false, true);
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

        private static Palette GetPalette(byte[] vgaPalette)
        {
            int numEntries = vgaPalette.Length / 3;
            Palette palette = new Palette(numEntries);
            for (var i = 0; i < numEntries; i++)
            {
                byte r = ConvertVgaToRgb(vgaPalette[i * 3 + 0]);
                byte g = ConvertVgaToRgb(vgaPalette[i * 3 + 1]);
                byte b = ConvertVgaToRgb(vgaPalette[i * 3 + 2]);
                palette.SetColor(i, new Color(r, g, b));
            }
            return palette;
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

        private static void WritePng8(string filename, byte[] indexBytes, byte[] palVga, SpriteInfo imageInfo, bool isTransparent = false)
        {
            ImageInfo imiPng = new ImageInfo(imageInfo.W, imageInfo.H, 8, false, false, true);
            int numBytesRowPng = imiPng.BytesPerRow;
            byte[] row = new byte[numBytesRowPng];
            int numPaletteEntries = palVga.Length / 3;
            using (FileStream output = File.Create(filename))
            {
                PngWriter pngw = new PngWriter(output, imiPng);
                PngChunkPLTE palette = pngw.GetMetadata().CreatePLTEChunk();
                FillPalette(palette, numPaletteEntries, palVga);
                if (isTransparent)
                {
                    PngChunkTRNS trns = pngw.GetMetadata().CreateTRNSChunk();
                    trns.setIndexEntryAsTransparent(0);
                }
                for (var i = 0; i < imiPng.Rows; i++)
                {
                    Array.Copy(indexBytes, i * numBytesRowPng, row, 0, row.Length);
                    pngw.WriteRowByte(row, i);
                }
                pngw.End();
            }
        }

        private static void CopyPixels(byte[] srcPixels, SpriteInfo srcInfo, byte[] dstPixels, int dstStride, int dstX, int dstY)
        {
            SpriteData srcData = new SpriteData { X = 0, Y = 0, W = srcInfo.W, H = srcInfo.H };
            CopyPixels(srcPixels, srcInfo.W, srcData, dstPixels, dstStride, dstX, dstY);
        }

        private static void CopyPixels(byte[] srcPixels, int srcStride, SpriteData srcData, byte[] dstPixels, int dstStride, int dstX, int dstY)
        {
            for (var i = 0; i < srcData.H; i++)
            {
                int srcLine = srcData.Y + i;
                int dstLine = dstY + i;
                int srcIdx = (srcLine * srcStride) + srcData.X;
                int dstIdx = (dstLine * dstStride) + dstX;
                Array.Copy(srcPixels, srcIdx, dstPixels, dstIdx, srcData.W);
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
