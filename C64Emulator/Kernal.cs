using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace C64Emulator
{
    class Kernal
    {
        private const ushort KERNAL_POS = 0xE000;
        private const int KERNAL_SIZE = 0x2000;

        private readonly byte[] kernal;

        public Kernal(string file)
        {
            kernal = new byte[KERNAL_SIZE];

            try
            {
                FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                fs.Read(kernal, 0, kernal.Length);
                fs.Close();
                fs.Dispose();
            }
            catch
            {
                throw new ApplicationException("KERNAL.ROM not found");
            }
        }

        public ushort Position
        {
            get
            {
                return KERNAL_POS;
            }
        }

        public int Size
        {
            get
            {
                return KERNAL_SIZE;
            }
        }

        public byte Peek(ushort address)
        {
            return kernal[address];
        }
    }
}
