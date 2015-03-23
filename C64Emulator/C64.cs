using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Reflection;

namespace C64Emulator
{
    static class C64
    {
        private const string KERNALROM = "KERNAL.ROM";
        private const string BASICROM = "BASIC.ROM";
        private const string CHARROM = "CHAR.ROM";
        private const string DRIVEROM = "C1541.ROM";

        public static Kernal kernal;
        public static BASIC basic;
        public static IO io;
        public static Char charset;
        public static Memory ram;
        public static CPU6510 cpu;
        public static VIC vic;
        public static SID sid;
        public static CIA1 cia1;
        public static CIA2 cia2;
        public static Palette palette;
        public static RendererSDL renderer;
        public static Network network;

        public static void Start(RendererSDL r, Network n)
        {
            string appDir = 
                Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) + 
					Path.DirectorySeparatorChar.ToString();

            renderer = r;
            renderer.ResetDrawState();
            network = n;
            kernal = new Kernal(appDir + KERNALROM);
            basic = new BASIC(appDir + BASICROM);
            charset = new Char(appDir + CHARROM);
            palette = new Palette();
            ram = new Memory();
            vic = new VIC();
            sid = new SID();
            cia1 = new CIA1();
            cia2 = new CIA2();
            io = new IO();
            cpu = new CPU6510();

            cpu.Start();
        }

        public static void Stop()
        {
            cpu.Stop();
            renderer.ResetDrawState();
        }

        public static void Dispose()
        {
            if (cpu != null)
            {
                cpu.Stop();

                while (!cpu.ThreadExited) Thread.Sleep(20);
            }

            kernal = null;
            basic = null;
            charset = null;
            palette = null;
            ram = null;
            vic = null;
            sid = null;
            cia1 = null;
            cia2 = null;
            io = null;
            cpu = null;
        }

        public static void HardReset()
        {
            Dispose();
            Start(renderer, network);
        }

        public static void SoftReset()
        {
            cpu.Reset();
        }

        public static void Pause()
        {
            cpu.Paused = true;
        }

        public static void Unpause()
        {
            cpu.Paused = false;
        }

        public static void FastForward()
        {
            cpu.FastForward = true;
        }

        public static void RegularSpeed()
        {
            cpu.FastForward = false;
        }

        public static void LoadProgram(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            BinaryReader br = new BinaryReader(fs);

            ushort address = br.ReadUInt16();

            while (fs.Position < fs.Length)
                C64.ram.Poke(address++, br.ReadByte());

            fs.Close();
            fs.Dispose();

            C64.ram.Poke(0x2D, (byte)(address & 0xFF));
            C64.ram.Poke(0x2E, (byte)((address >> 8) & 0xFF));

            C64.cpu.SetPC(0xA52A);
        }

        public static void SaveProgram(string path, ushort fromAddress, ushort toAddress)
        {
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            BinaryWriter bw = new BinaryWriter(fs);

            bw.Write(fromAddress);

            for (ushort i = fromAddress; i <= toAddress; i++)
                bw.Write(C64.ram.Peek(i));

            fs.Close();
            fs.Dispose();
        }
    }
}
