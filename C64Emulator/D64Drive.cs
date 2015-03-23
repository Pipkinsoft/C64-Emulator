using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace C64Emulator
{
    public enum ChannelMode
    {
	    Free,
	    Command,
	    Directory,
	    File,
	    Direct
    }

    public enum AccessMode
    {
	    Read, 
        Write, 
        Append
    }

    public enum FileType
    {
	    Program,
        Sequential, 
        User, 
        Relative
    }
    /*
    public struct BAM
    {
	    public byte DirectoryTrack;
	    public byte DirectorySector;
	    public byte FormatType;
	    public byte Padding0;
	    public byte[] Sector = new byte[4 * 35];
	    public byte[] DiskName = new byte[18];
	    public byte[] ID = new byte[2];
	    public byte Padding1;
	    public byte[] FormatChars = new byte[2];
	    public byte[] Padding2 = new byte[4];
	    public byte[] Padding3 = new byte[85];
    }

    public struct DirectoryEntry
    {
	    public byte Type;
	    public byte Track;
	    public byte Sector;
	    public byte[] Filename = new byte[16];
	    public byte SideTrack;
	    public byte SideSector;
	    public byte Length;
	    public byte[] Padding0 = new byte[4];
	    public byte OverwriteTrack;
	    public byte OverwriteSector;
	    public ushort BlockCount;
        public byte[] Padding1 = new byte[2];
    }

    public struct C64Directory
    {
	    public byte[] Padding = new byte[2];
	    public byte NextTrack;
	    public byte NextSector;
        public DirectoryEntry[] Entry = new DirectoryEntry[8];
    }
     */

    class D64Drive
    {
        private const int NUM_TRACKS = 35;
        private const int NUM_SECTORS = 683;
        private const int RAM_SIZE = 0x800;

        private string originalFile = string.Empty;
        private MemoryStream fileStream;
        private byte[] ram = new byte[RAM_SIZE];
        //private BAM bam = new BAM();
        //private C64Directory dir = new C64Directory();

        private int[] channelMode = new int[16];
	    private int[] channelBufferNum = new int[16];
	    private byte[] channelBuffer = new byte[16];
	    private byte[] BufferPointer = new byte[16];
	    private int[] bufferLength = new int[16];

	    private bool[] bufferFree = new bool[4];

	    private string command;

	    private int headerLength = 0;

	    private byte[] sectorErrors = new byte[683];

        public D64Drive()
        {
        }
    }
}
