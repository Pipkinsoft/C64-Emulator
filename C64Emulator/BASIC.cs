using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace C64Emulator
{
    class BASIC
    {
        private const ushort BASIC_POS = 0xA000;
        private const int BASIC_SIZE = 0x2000;

        private byte[] basic;

        public BASIC(string file)
        {
            basic = new byte[BASIC_SIZE];

            try
            {
                FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                fs.Read(basic, 0, basic.Length);
                fs.Close();
                fs.Dispose();
            }
            catch
            {
                throw new ApplicationException("BASIC.ROM not found");
            }
        }

        public ushort Position
        {
            get
            {
                return BASIC_POS;
            }
        }

        public int Size
        {
            get
            {
                return BASIC_SIZE;
            }
        }

        public byte Peek(ushort address)
        {
            return basic[address];
        }
    }
}
