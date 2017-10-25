using System;
using System.Collections.Generic;
using System.IO;

namespace Pre2
{
    public class SqzUnpacker
    {
        // Based on http://ttf.mine.nu/techdocs/compress.txt
        // The optional '-altlzw' parameter enables alternate LZW decompression,
        // as used for SQZ executable by CDRUN.COM on the '10 Great Games' CDROM by 'Telstar Fun and Games'.

        public static byte[] Unpack(string filename)
        {
            return Unpack(filename, false);
        }

        public static byte[] Unpack(string filename, bool altLzw)
        {
            if (!File.Exists(filename))
            {
                throw new IOException("No such input file!");
            }

            Boolean isDiet;
            using (BinaryReader br = new BinaryReader(File.OpenRead(filename)))
            {
                UInt16 dietSignature = br.ReadUInt16();
                isDiet = dietSignature == 0x4CB4;
            }

            return isDiet ? UnpackDiet(filename) : UnpackTtf(filename, altLzw);
        }

        private static byte[] UnpackDiet(string filename)
        {
            byte[] dietSignature = { 0xB4, 0x4C, 0xCD, 0x21, 0x9D, 0x89, 0x64, 0x6C, 0x7A };
            using (BinaryReader br = new BinaryReader(File.OpenRead(filename)))
            {
                for (var i = 0; i < dietSignature.Length; i++)
                {
                    byte b = br.ReadByte();
                    if (b != dietSignature[i])
                    {
                        throw new FormatException("Not a DIET file!");
                    }
                }
                throw new NotImplementedException();
            }
        }

        private static byte[] UnpackTtf(string filename, bool altLzw)
        {
            using (BinaryReader br = new BinaryReader(File.OpenRead(filename)))
            {
                int payloadSizeHi = br.ReadByte() & 0x0F; // bits 4..7 unused
                int type = br.ReadByte();
                int payloadSizeLo = br.ReadUInt16();
                int payloadSize = (payloadSizeHi << 16) | payloadSizeLo;
                byte[] payload = new byte[payloadSize];
                using (Stream output = new MemoryStream(payload))
                {
                    if (type == 0x10)
                    {
                        DecodeLzw(br, output, altLzw);
                    }
                    else
                    {
                        DecodeHuffmanRle(br, output);
                    }
                    if (output.Position != payloadSize)
                    {
                        throw new InvalidDataException("Invalid decoded data length (maybe try altlzw?)");
                    }
                }
                return payload;
            }
        }

        private class LzwCodeWordReader
        {
            private readonly BinaryReader br;
            private int buf24;
            private int missingBits;

            public LzwCodeWordReader(BinaryReader br)
            {
                this.br = br;
                buf24 = (br.ReadByte() << 16) + (br.ReadByte() << 8) + br.ReadByte();
            }

            public int ReadCodeWord(int nbit)
            {
                if (nbit > 12) { throw new ArgumentOutOfRangeException(); }
                if (nbit > 24 - missingBits) { throw new InvalidDataException(); }
                int cw = buf24 >> (24 - nbit);
                buf24 = (buf24 << nbit) & 0xFFFFFF;
                missingBits += nbit;
                while (missingBits >= 8 & br.BaseStream.Position < br.BaseStream.Length)
                {
                    missingBits -= 8;
                    int rawByte = br.ReadByte();
                    if (rawByte != -1)
                    {
                        buf24 = buf24 | (rawByte << missingBits);
                    }
                }
                return cw;
            }
        }

        private static void DecodeLzw(BinaryReader br, Stream output, bool altLzw)
        {
            int codeClear = altLzw ? 0x101 : 0x100;
            int codeEnd   = altLzw ? 0x100 : 0x101;
            const int dictSizeInitial = 0x102;
            const int dictLimit = 0x1000;

            int nbit = 9;	// Current word size
            int dictsize = dictSizeInitial;
            Dictionary<int, byte[]> dictNew = new Dictionary<int, byte[]>(dictLimit);
            for (int i = 0; i < 256; i++)
            {
                dictNew[i] = new[] {(byte) i};
            }

            Dictionary<int, byte[]> dict = new Dictionary<int, byte[]>(dictNew);

            var cwReader = new LzwCodeWordReader(br);

            int prev = codeClear;
            while (prev != codeEnd)
            {
                if (prev == codeClear)
                {
                    nbit = 9;
                    dictsize = dictSizeInitial;
                    dict = new Dictionary<int, byte[]>(dictNew);
                }
                int cw = cwReader.ReadCodeWord(nbit);
                if (cw != codeEnd && cw != codeClear)
                {
                    byte newbyte;
                    if (cw < dictsize)
                    {
                        newbyte = dict[cw][0];
                    }
                    else
                    {
                        if (prev == codeClear)       { throw new InvalidDataException(); }
                        if (!(dictsize < dictLimit)) { throw new InvalidDataException(); }
                        if (cw != dictsize)          { throw new InvalidDataException(); }
                        newbyte = dict[prev][0];
                    }
                    if ((prev != codeClear) && (dictsize < dictLimit))
                    {
                        byte[] prevArray = dict[prev];
                        byte[] newArray = new byte[prevArray.Length + 1];
                        Array.Copy(prevArray, newArray, prevArray.Length);
                        newArray[newArray.Length - 1] = newbyte;
                        dict.Add(dictsize, newArray);
                        dictsize++;
                        int maxDictSize = 1 << nbit;
                        if (dictsize == maxDictSize && nbit < 12)
                        {
                            nbit++;
                        }
                    }
                    byte[] outData = dict[cw];
                    output.Write(outData, 0, outData.Length);
                }
                prev = cw;
            }
        }

        private class BitReader
        {
            private int bit = 8;
            private int currentByte;
            private readonly BinaryReader br;

            public BitReader(BinaryReader br)
            {
                this.br = br;
            }

            public bool IsEndOfStream()
            {
                return (bit == 8) && !(br.BaseStream.Position < br.BaseStream.Length);
            }

            public bool ReadBit(bool bigEndian = false)
            {
                if (bit == 8)
                {
                    currentByte = br.ReadByte();
                    bit = 0;
                }
                int shift = bigEndian ? (7 - bit) : bit;
                int mask = 1 << shift;
                bool value = (currentByte & mask) > 0;
                bit++;
                return value;
            }
        }

        private class TtfHuffmanReader
        {
            private readonly ushort[] huffmanTree;
            private readonly BitReader bitReader;

            public TtfHuffmanReader(BinaryReader br)
            {
                int huffmanTreeSize = br.ReadUInt16() >> 1;
                huffmanTree = new ushort[huffmanTreeSize];
                for (var i = 0; i < huffmanTreeSize; i++)
                {
                    int node = br.ReadUInt16();
                    // convert offset to index for parent nodes
                    huffmanTree[i] = (ushort) (IsParentNode(node) ? (node >> 1) : node);
                }
                bitReader = new BitReader(br); // init bitreader after huffmanTree init!!
            }

            private bool IsParentNode(int node)
            {
                return (node & (1 << 15)) == 0;  // bit 15 is not set
            }

            public int ReadCodeWord()
            {
                int nodeIdx = 0;
                while (!bitReader.IsEndOfStream())
                {
                    bool chooseFirstChild = !bitReader.ReadBit(true);
                    ushort currentNode = huffmanTree[chooseFirstChild ? nodeIdx : nodeIdx + 1]; // second child is next to the first one
                    if (IsParentNode(currentNode))
                    {
                        nodeIdx = currentNode;
                    }
                    else
                    {
                        return currentNode & 0x7FFF;
                    }
                }
                return -1;
            }
        }

        private static void DecodeHuffmanRle(BinaryReader br, Stream output)
        {
            TtfHuffmanReader huffmanReader = new TtfHuffmanReader(br);
            int cw;
            byte last = 0;
            while ((cw = huffmanReader.ReadCodeWord()) != -1)
            {
                int lo = cw & 0x00FF;
                int hi = cw & 0xFF00;
                if (hi == 0)
                {
                    last = (byte) lo;
                    output.WriteByte(last);
                }
                else
                {
                    switch (lo)
                    {
                        case 0:
                            cw = huffmanReader.ReadCodeWord();
                            if (cw == -1) { return; }
                            for (var i = 0; i < cw; i++)
                            {
                                output.WriteByte(last);
                            }
                            break;

                        case 1:
                            cw = huffmanReader.ReadCodeWord();
                            if (cw == -1) { return; }
                            int countHi = (byte) cw; // lo

                            cw = huffmanReader.ReadCodeWord();
                            if (cw == -1) { return; }
                            int countLo = (byte) cw; // lo

                            int count = (countHi << 8) | countLo;
                            for (var i = 0; i < count; i++)
                            {
                                output.WriteByte(last);
                            }
                            break;

                        default:
                            for (var i = 0; i < lo; i++)
                            {
                                output.WriteByte(last);
                            }
                            break;
                    }
                }
            }
        }
    }
}
