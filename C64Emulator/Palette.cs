using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace C64Emulator
{
    class Palette
    {
        private const int PALETTE_SIZE = 0x400;
        private const ushort PALETTE_POS = 0xD800;

        private byte[] palette;
        private Color[] colormap;

        public Palette()
        {
            palette = new byte[PALETTE_SIZE];
            colormap = new Color[16]
                {
                    Color.Black,
                    Color.White,
                    Color.FromArgb(190, 53, 53),
                    Color.FromArgb(131, 240, 220),
                    Color.FromArgb(204, 89, 198),
                    Color.FromArgb(89, 205, 54),
                    Color.FromArgb(65, 55, 205),
                    Color.FromArgb(247, 238, 89),
                    Color.FromArgb(209, 127, 48),
                    Color.FromArgb(145, 95, 51),
                    Color.FromArgb(249, 155, 151),
                    Color.FromArgb(91, 91, 91),
                    Color.FromArgb(142, 142, 142),
                    Color.FromArgb(172, 234, 136),
                    Color.FromArgb(117, 161, 236),
                    Color.FromArgb(202, 202, 202)
                };
        }

        public int Size
        {
            get
            {
                return PALETTE_SIZE;
            }
        }

        public ushort Position
        {
            get
            {
                return PALETTE_POS;
            }
        }

        public void Poke(ushort address, byte value)
        {
            palette[address] = value;
            //C64.vic.Refresh();
        }

        public byte Peek(ushort address)
        {
            return palette[address];
        }

        public Color MapColorPosition(ushort position)
        {
            return colormap[palette[position] & 0xF];
        }

        public Color MapColorValue(byte color)
        {
            return colormap[color & 0xF];
        }

        public byte MapColorToValue(Color color)
        {
            for (int i = 0; i < colormap.Length; i++)
                if (colormap[i] == color) return (byte)i;

            return 0;
        }
    }
}
