using System;
using System.Collections.Generic;
using System.Text;

namespace C64Emulator
{
    class SID
    {
        private const ushort REGISTERS_POS = 0xD400;
        private const int NUM_REGISTERS = 29;

        private byte[] register;

        public SID()
        {
            register = new byte[NUM_REGISTERS];
        }

        public int RegistersPosition
        {
            get
            {
                return REGISTERS_POS;
            }
        }

        public int NumRegisters
        {
            get
            {
                return NUM_REGISTERS;
            }
        }

        public byte GetRegister(int regnum)
        {
            switch (regnum)
            {
                case 0x1B: return (byte)(new Random()).Next(0, 255);
            }

            return register[regnum];
        }

        public void SetRegister(int regnum, byte value)
        {
            register[regnum] = value;
        }
    }
}
