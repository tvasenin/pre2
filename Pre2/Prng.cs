namespace Pre2
{
    public class Prng
    {
        private byte tmp1 = 0x5;
        private byte tmp2 = 0x22;
        private byte tmp3 = 0x86;
        private ushort tmp4 = 0xE58D;

        public byte GetNext()
        {
            tmp4 += tmp1;

            tmp1 += 3;
            tmp1 += (byte) ((tmp4 & 0xFF00) >> 8);

            tmp2 += tmp3;
            tmp2 += tmp2;
            tmp2 += tmp1;

            tmp3 ^= tmp1;
            tmp3 ^= tmp2;

            return tmp2;
        }
    }
}
