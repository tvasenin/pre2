using System.Collections.Generic;
using System.IO;

namespace Pre2
{
    public enum EnemyType
    {
        T00, T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11, T12
    }

    public class Enemy
    {
        public bool IsExpertOnly;
        public EnemyType Type;
        public short SpriteIdx;
        public byte Unknown1;
        public byte Hitpoints;
        public byte Pause;
        public byte Unknown2;
        public byte Score;

        public byte[] AdditionalDataBytes;

        private Enemy(BinaryReader br, byte recLength)
        {
            if (recLength < 13) { throw new InvalidDataException(); }
            byte prop = br.ReadByte();
            IsExpertOnly = (prop & (1 << 7)) > 0;
            Type = (EnemyType) (prop & 0x7FFF);
            SpriteIdx = br.ReadInt16();
            Unknown1 = br.ReadByte();
            Hitpoints = br.ReadByte();
            Pause = br.ReadByte();
            Unknown2 = br.ReadByte();
            Score = br.ReadByte();
            AdditionalDataBytes = br.ReadBytes(recLength - 9);
        }

        public static Enemy[] InitEnemies(byte[] data)
        {
            List<Enemy> enemies = new List<Enemy>(data.Length / 13);
            using (BinaryReader br = new BinaryReader(new MemoryStream(data, false)))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    byte recLength = br.ReadByte();
                    if (recLength > 50) // including 0xFF
                    {
                        break;
                    }
                    Enemy enemy = new Enemy(br, recLength);
                    enemies.Add(enemy);
                }
            }
            return enemies.ToArray();
        }
    }
}
