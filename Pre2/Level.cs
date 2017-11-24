using System.IO;

namespace Pre2
{
    public struct Position
    {
        public ushort X;
        public ushort Y;

        public Position(BinaryReader br)
        {
            X = br.ReadUInt16();
            Y = br.ReadUInt16();
        }
    }

    public struct TilePosition
    {
        public byte Col;
        public byte Row;
    }

    public struct Gate
    {
        public TilePosition InPosition;
        public TilePosition OutScreenPosition;
        public TilePosition OutPosition;
        public byte ScrollProps;

        public Gate(BinaryReader br)
        {
            InPosition = new TilePosition { Col = br.ReadByte(), Row = br.ReadByte() };
            OutScreenPosition = new TilePosition { Col = br.ReadByte(), Row = br.ReadByte() };
            OutPosition = new TilePosition { Col = br.ReadByte(), Row = br.ReadByte() };
            ScrollProps = br.ReadByte();
        }
    }

    public struct ShiftingTileBlock
    {
        public byte X;
        public byte Y;
        public byte W;
        public byte H;
        public byte ActX;
        public byte ActY;
        public ushort Unknown1; // TODO: research
        public byte Dist;
        public byte Unknown2; // TODO: research

        public ShiftingTileBlock(BinaryReader br)
        {
            X = br.ReadByte(); Y = br.ReadByte();
            W = br.ReadByte(); H = br.ReadByte();
            ActX = br.ReadByte(); ActY = br.ReadByte();
            Unknown1 = br.ReadUInt16();
            Dist = br.ReadByte();
            Unknown2 = br.ReadByte();
        }
    }

    public struct Secret
    {
        public byte FromTile;
        public byte ToTile;
        public byte Bonus;
        public byte X;
        public byte Y;

        public Secret(BinaryReader br)
        {
            FromTile = br.ReadByte(); ToTile= br.ReadByte();
            Bonus = br.ReadByte();
            X = br.ReadByte(); Y = br.ReadByte();
        }
    }

    public struct Item
    {
        public Position Position;
        public short SpriteIdx;
        public byte Unknown;

        public Item(BinaryReader br)
        {
            Position = new Position(br);
            SpriteIdx = br.ReadInt16();
            Unknown = br.ReadByte();
        }
    }

    public struct Platform
    {
        public Position Position;
        public short SpriteIdx;
        public byte Behaviour;
        public byte Speed;
        public byte Unknown1;
        public byte DropDelay;
        public ushort Distance;
        public byte Unknown2;
        public byte MaybeDrop1;
        public byte MaybeDrop2;

        public Platform(BinaryReader br)
        {
            Position = new Position(br);
            SpriteIdx = br.ReadInt16();
            Behaviour = br.ReadByte();
            Speed = br.ReadByte();
            Unknown1 = br.ReadByte();
            DropDelay = br.ReadByte();
            Distance = br.ReadUInt16();
            Unknown2 = br.ReadByte();
            MaybeDrop1 = br.ReadByte();
            MaybeDrop2 = br.ReadByte();
        }
    }

    public struct Kong
    {
        public ushort LeftBorder;
        public ushort RightBorder;
        public byte Unknown1;
        public ushort Health;
        public byte Unknown2;
        public Position Position;

        public Kong(BinaryReader br)
        {
            LeftBorder = br.ReadUInt16(); RightBorder = br.ReadUInt16();
            Unknown1 = br.ReadByte();
            Health = br.ReadUInt16();
            Unknown2 = br.ReadByte();
            Position = new Position(br);
        }
    }

    public class Level
    {
        private byte[] tileProps1;
        private byte[] tileProps2;
        private byte[] tileProps3;
        private ushort unknownMaybeRevision; // TODO: research
        private Position startPosition;
        private byte maxTileX;
        private byte unknown775;
        private byte scrollingProps;
        private readonly ushort[] idxFrontTiles = new ushort[256];
        private readonly Gate[] gates = new Gate[20];
        private readonly ShiftingTileBlock[] shiftingTileBlocks = new ShiftingTileBlock[15];
        private Enemy[] enemies;
        private ushort spriteOffsetItemPlatform;
        private ushort spriteOffsetEnemy;
        private readonly Secret[] secrets = new Secret[80];
        private byte[] tileProps4;
        private readonly Item[] items = new Item[70];
        private readonly Platform[] platforms = new Platform[16];
        private Kong kong;

        public Level(byte[] data)
        {
            using (BinaryReader br = new BinaryReader(new MemoryStream(data, false)))
            {
                tileProps1 = br.ReadBytes(256);
                tileProps2 = br.ReadBytes(256);
                tileProps3 = br.ReadBytes(256);
                unknownMaybeRevision = br.ReadUInt16();
                startPosition = new Position(br);
                maxTileX = br.ReadByte();
                unknown775 = br.ReadByte();
                scrollingProps = br.ReadByte();
                for (var i = 0; i < idxFrontTiles.Length; i++)
                {
                    idxFrontTiles[i] = br.ReadUInt16();
                }
                for (var i = 0; i < gates.Length; i++)
                {
                    gates[i] = new Gate(br);
                }
                for (var i = 0; i < shiftingTileBlocks.Length; i++)
                {
                    shiftingTileBlocks[i] = new ShiftingTileBlock(br);
                }
                byte[] enemydata = br.ReadBytes(2048);
                enemies = Enemy.InitEnemies(enemydata);
                spriteOffsetItemPlatform = br.ReadUInt16();
                spriteOffsetEnemy = br.ReadUInt16();
                for (var i = 0; i < secrets.Length; i++)
                {
                    secrets[i] = new Secret(br);
                }
                tileProps4 = br.ReadBytes(256);
                for (var i = 0; i < items.Length; i++)
                {
                    items[i] = new Item(br);
                }
                for (var i = 0; i < platforms.Length; i++)
                {
                    platforms[i] = new Platform(br);
                }
                kong = new Kong(br);
            }
        }
    }
}
