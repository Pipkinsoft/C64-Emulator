using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace C64Emulator
{
    class Char
    {
        private const ushort CHAR_POS1 = 0xD000;
        private const int CHAR_SIZE = 0x1000;

        private readonly byte[] charset;

        public Char(string file)
        {
            charset = new byte[CHAR_SIZE];

            try
            {
                FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                fs.Read(charset, 0, charset.Length);
                fs.Close();
                fs.Dispose();
            }
            catch
            {
                throw new ApplicationException("CHAR.ROM not found");
            }
        }

        public ushort Position1
        {
            get
            {
                return CHAR_POS1;
            }
        }

        public int Size
        {
            get
            {
                return CHAR_SIZE;
            }
        }

        public byte Peek(ushort address)
        {
            return charset[address];
        }
    }
}
