using System;
using System.Collections.Generic;
using System.Text;

namespace C64Emulator
{
    public enum Caller
    {
        CPU,
        VIC
    }

    class Memory
    {
        private const int MEMORY_SIZE = 0x10000; //64 KB

        private byte[] ram = new byte[MEMORY_SIZE];

        private ushort activeVICVMStart;
        private ushort activeVICVMEnd;
        private ushort activeVICPMStart;
        private ushort activeVICPMEnd;
        private bool loram;
        private bool hiram;
        private bool charen;

        public Memory()
        {
            ram[0] = 0xEF;
            ram[1] = 0x07;
            getDirectionRegs();
        }

        public void setActiveVICVM(ushort start, ushort end)
        {
            activeVICVMStart = start;
            activeVICVMEnd = end;
        }

        public void setActiveVICPM(ushort start, ushort end)
        {
            activeVICPMStart = start;
            activeVICPMEnd = end;
        }

        public int Size
        {
            get
            {
                return MEMORY_SIZE;
            }
        }

        private void getDirectionRegs()
        {
            loram = ((ram[0] & ram[1] & 1) != 0);
            hiram = ((ram[0] & ram[1] & 2) != 0);
            charen = ((ram[0] & ram[1] & 4) != 0);
        }

        public void Poke(ushort address, byte value)
        {
            //ram[address] = value;

            if (
                (hiram || loram) &&
                address >= C64.io.Position &&
                address < C64.io.Position + C64.io.Size
                )
                C64.io.Poke((ushort)(address - C64.io.Position), value);
            else
                ram[address] = value;

            if (address < 2)
                getDirectionRegs();
        }

        public byte Peek(ushort address)
        {
            if (hiram && address >= C64.kernal.Position)
            {
                return C64.kernal.Peek((ushort)(address - C64.kernal.Position));
            }
            else if (hiram && loram &&
                address >= C64.basic.Position &&
                address < C64.basic.Position + C64.basic.Size)
            {
                return C64.basic.Peek((ushort)(address - C64.basic.Position));
            }
            else if ((hiram || loram) && !charen &&
                address >= C64.charset.Position1 &&
                address < C64.charset.Position1 + C64.charset.Size)
            {
                return C64.charset.Peek((ushort)(address - C64.charset.Position1));
            }
            else if ((hiram || loram) && charen &&
                address >= C64.io.Position &&
                address < C64.io.Position + C64.io.Size)
            {
                return C64.io.Peek((ushort)(address - C64.io.Position));
            }
            else
                return ram[address];
        }

        public ushort PeekWord(ushort address)
        {
            return
                (ushort)
                (((ushort)(Peek((ushort)(address + 1))) << 8) |
                (ushort)(Peek(address)));
        }

        public byte VICPeek(ushort address)
        {
            return ram[address];
        }

    }
}
