using System;
using System.Collections.Generic;
using System.Text;

namespace C64Emulator
{
    class IO
    {
        private const int IO_SIZE = 0x1000;
        private const ushort IO_POS = 0xD000;
        private const ushort IO_PORT = 0x0001;
        private const ushort KEYBOARD_BUFFER = 0x0277;
        private const ushort KBBUFFER_COUNT = 0x00C6;
        private const ushort KEYCODES_TABLE1 = 0xEB81;
        private const ushort KEYCODES_TABLE2 = 0xEBC2;
        private const ushort KEYCODES_TABLE3 = 0xEC03;
        private const ushort KEYCODES_TABLE4 = 0xEC78;
        private const int KEYTABLE_LENGTH = 0x40;

        private ushort vicRegPos;
        private ushort sidRegPos;
        private ushort palettePos;
        private ushort cia1Pos;
        private ushort cia2Pos;

        public IO()
        {
            vicRegPos = (ushort)(C64.vic.RegistersPosition - IO_POS);
            sidRegPos = (ushort)(C64.sid.RegistersPosition - IO_POS);
            palettePos = (ushort)(C64.palette.Position - IO_POS);
            cia1Pos = (ushort)(C64.cia1.Position - IO_POS);
            cia2Pos = (ushort)(C64.cia2.Position - IO_POS);
        }

        public int Size
        {
            get
            {
                return IO_SIZE;
            }
        }

        public ushort Position
        {
            get
            {
                return IO_POS;
            }
        }

        public ushort IOPort
        {
            get
            {
                return IO_PORT;
            }
        }

        public byte Peek(ushort address)
        {
            if (address >= vicRegPos && address < vicRegPos + 0x03FF)
            {
                int regNum = (address - vicRegPos) % 0x40;

                if (regNum >= C64.vic.NumRegisters)
                    return 0xFF;
                else
                    return C64.vic.GetRegister(regNum);
            }
            else if (address >= sidRegPos && address < sidRegPos + 0x03FF)
            {
                int regNum = (address - sidRegPos) % 0x20;

                if (regNum >= C64.sid.NumRegisters)
                    return 0xFF;
                else
                    return C64.sid.GetRegister(regNum);
            }
            else if (address >= palettePos && address < palettePos + C64.palette.Size)
                return C64.palette.Peek((ushort)(address - palettePos));
            else if (address >= cia1Pos && address < cia1Pos + C64.cia1.Size)
            {
                address = (ushort)((address - cia1Pos) % 0x10);
                return C64.cia1.Peek(address);
            }
            else if (address >= cia2Pos && address < cia2Pos + C64.cia2.Size)
            {
                address = (ushort)((address - cia2Pos) % 0x10);
                return C64.cia2.Peek(address);
            }
            else
                return 0xFF;
        }

        public void Poke(ushort address, byte value)
        {
            if (address >= vicRegPos && address < vicRegPos + C64.vic.NumRegisters)
            {
                int regnum = (address - vicRegPos) % 0x40;

                if (regnum < C64.vic.NumRegisters)
                    C64.vic.SetRegister(regnum, value);
            }
            else if (address >= sidRegPos && address < sidRegPos + C64.sid.NumRegisters)
            {
                int regnum = (address - sidRegPos) % 0x20;

                if (regnum < C64.sid.NumRegisters)
                    C64.sid.SetRegister(regnum, value);
            }
            else if (address >= palettePos && address < palettePos + C64.palette.Size)
                C64.palette.Poke((ushort)(address - palettePos), value);
            else if (address == C64.vic.BankAddressPosition - IO_POS)
                C64.vic.BankAddress = value;
            else if (address >= cia1Pos && address < cia1Pos + C64.cia1.Size)
            {
                address = (ushort)((address - cia1Pos) % 0x10);
                C64.cia1.Poke(address, value);
            }
            else if (address >= cia2Pos && address < cia2Pos + C64.cia2.Size)
            {
                address = (ushort)((address - cia2Pos) % 0x10);
                C64.cia2.Poke(address, value);
            }
        }
    }
}
