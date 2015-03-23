using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Drawing;

namespace C64Emulator
{
    // Enumeration of VIC Video Modes
    public enum VideoMode : byte
    {
        SimpleCharacterMode = 0x00,
        MultiColorCharacterMode = 0x10,
        SimpleBitMapMode = 0x20,
        MultiColorBitMapMode = 0x30,
        ExtendedColorCharacterMode = 0x40
    }

    // Structure for a VIC Sprite
    public struct Sprite
    {
        public int X;
        public int Y;
        public bool Enabled;
        public int Width;
        public int Height;
        public bool MulticolorMode;
        public bool TopMost;
        public Color SpriteColor;
    }

    // Structure for a VIC Pixel
    public struct Pixel
    {
        public Color PixelColor;
        public bool Background;
    }

    class VIC
    {
        // Memory Constants
        private const ushort REGISTERS_POS = 0xD000;
        private const int NUM_REGISTERS = 47;
        private const ushort BANKADDRESS_POS = 0xDD00;
        private const ushort CHAR_POS1 = 0x1000;
        private const ushort CHAR_POS2 = 0x9000;
        private const ushort COLORMATRIXADDRESS = 0xD800;
        
        // Screen Constants (PAL)
		private const int FULLSCREENWIDTH = 504;        // Includes HBLANK area
		private const int FULLSCREENHEIGHT = 312;       // Includes VBLANK area
		private const int VISIBLESCREENWIDTH = 403;     // Includes Border area
		private const int VISIBLESCREENHEIGHT = 284;    // Includes Border area
        private const int FIRSTRASTERLINE = 9;
        private const int LASTRASTERLINE = 293;
        private const int FIRSTLINEPIXEL = 0; //76;
        private const int LASTLINEPIXEL = 403; //479;
        private const int FIRSTSCREENLINE = 51;
        private const int LASTSCREENLINE = 250;
        private const int FIRSTSCREENPIXEL = 44; //120;
        private const int LASTSCREENPIXEL = 363; //439;
        private const int SPRITEXCOORD = 24;            // Sprite X-Coordinate for position (0,0)
        private const int SPRITEYCOORD = 50;            // Sprite Y-Coordinate for position (0,0)

        private ushort bankAddress;                     // Selected VIC bank address in memory
        private ushort videoMatrixAddress;              // Screen address in selected bank
        private ushort pixelMatrixAddress;              // Character address in selected bank

        private VideoMode currentVideoMode;
        private bool bitmapMode;
        private int rasterIntLine = 0;
        private bool rasterIntEnabled = false;
        private bool spriteBGCollide = false;
        private bool spriteSpriteCollide = false;
        private byte lightPenX = 0;
        private byte lightPenY = 0;
        private bool lightPenIntEnabled = false;
        private bool screenEnabled = true;
        private byte horizontalScroll = 0;
        private byte verticalScroll = 0;
        private byte textRows = 25;
        private byte textCols = 40;
        private int firstScreenPixel = FIRSTSCREENPIXEL;
        private int lastScreenPixel = LASTSCREENPIXEL;
        private int firstScreenLine = FIRSTSCREENLINE;
        private int lastScreenLine = LASTSCREENLINE;

        private Sprite[] sprites;

        private Pixel emptyPixel;

        private byte[] register;

        private Color[,] activeScreen;
		private byte[] serializedScreen;
        private int currentVLine = 0;
        private int currentHLine = 0;

        public VIC()
        {
            register = new byte[NUM_REGISTERS];
            activeScreen = new Color[VISIBLESCREENWIDTH, VISIBLESCREENHEIGHT];
            serializedScreen = new byte[VISIBLESCREENWIDTH * VISIBLESCREENHEIGHT / 2];
            currentVideoMode = VideoMode.SimpleCharacterMode;
            bitmapMode = false;
            sprites = new Sprite[8];
            
            emptyPixel = new Pixel();
            emptyPixel.Background = true;
            emptyPixel.PixelColor = Color.Black;

            for (int i = 0; i < 8; i++)
            {
                sprites[i].X = 0;
                sprites[i].Y = 0;
                sprites[i].Width = 24;
                sprites[i].Height = 21;
                sprites[i].Enabled = false;
                sprites[i].MulticolorMode = false;
                sprites[i].SpriteColor = Color.Black;
                sprites[i].TopMost = true;
            }
        }

        public int BorderWidth
        {
            get
            {
                return FIRSTSCREENPIXEL - FIRSTLINEPIXEL;
            }
        }

        public int BorderHeight
        {
            get
            {
                return FIRSTSCREENLINE - FIRSTRASTERLINE;
            }
        }

        public Color BorderColor
        {
            get
            {
                return C64.palette.MapColorValue(register[0x20]);
            }
        }

        public Color[,] Screen
        {
            get
            {
                return activeScreen;
            }
        }

        public byte HorizontalScroll
        {
            get
            {
                return horizontalScroll;
            }
        }

        public byte VerticalScroll
        {
            get
            {
                return verticalScroll;
            }
        }

        public void UpdateActiveScreen(int cycles)
        {
            for (int i = 0; i < cycles; i++)
            {
                if (
                    currentHLine >= FIRSTRASTERLINE && 
                    currentHLine < LASTRASTERLINE && 
                    currentVLine > FIRSTLINEPIXEL - 8 &&
                    currentVLine < LASTLINEPIXEL
                    )
                    for (int x = currentVLine; x < currentVLine + 8; x++)
                    {
                        if (x >= FIRSTLINEPIXEL && x < LASTLINEPIXEL)
                        {
                            if (
                                !screenEnabled ||
                                currentHLine < firstScreenLine ||
                                currentHLine > lastScreenLine ||
                                x < firstScreenPixel ||
                                x > lastScreenPixel
                                )
                            {
                                activeScreen[x - FIRSTLINEPIXEL, currentHLine - FIRSTRASTERLINE] =
                                    C64.palette.MapColorValue(register[0x20]);
                            }
                            else
                            {
                                int scrolledX = x - horizontalScroll;
                                int scrolledY = (byte)(currentHLine - (verticalScroll - 3)); ;

                                if (
                                    scrolledX < FIRSTSCREENPIXEL || scrolledX > LASTSCREENPIXEL ||
                                    scrolledY < FIRSTSCREENLINE || scrolledY > LASTSCREENLINE)
                                {
                                    activeScreen[x - FIRSTLINEPIXEL, currentHLine - FIRSTRASTERLINE] = Color.Black;
                                }
                                else
                                {
                                    Pixel pixel = getPixel(scrolledX - FIRSTSCREENPIXEL, scrolledY - FIRSTSCREENLINE);
                                    int pixelSet = -1;
                                    bool pixelDrawn = false;

                                    for (int sprite = 0; sprite < 8; sprite++)
                                        if (sprites[sprite].Enabled)
                                        {
                                            Pixel spritePixel =
                                                getSpritePixel(
                                                    sprite,
                                                    x - FIRSTSCREENPIXEL + SPRITEXCOORD,
                                                    currentHLine - FIRSTSCREENLINE + SPRITEYCOORD
                                                    );

                                            if (!spritePixel.Background)
                                            {
                                                if (pixelSet > -1)
                                                {
                                                    if (spriteSpriteCollide)
                                                    {
                                                        register[0x19] |= 0x84;
                                                        C64.cpu.IRQ();
                                                    }
                                                    register[0x1E] |= (byte)((1 << sprite) | (1 << pixelSet));
                                                }
                                                if (!pixel.Background)
                                                {
                                                    if (spriteBGCollide)
                                                    {
                                                        register[0x19] |= 0x82;
                                                        C64.cpu.IRQ();
                                                    }
                                                    register[0x1F] |= (byte)((1 << sprite));
                                                }

                                                if (!pixelDrawn && (sprites[sprite].TopMost || pixel.Background))
                                                {
                                                    activeScreen[x - FIRSTLINEPIXEL, currentHLine - FIRSTRASTERLINE] =
                                                        spritePixel.PixelColor;

                                                    pixelDrawn = true;
                                                }

                                                pixelSet = sprite;
                                            }
                                        }

                                    if (!pixelDrawn)
                                        activeScreen[x - FIRSTLINEPIXEL, currentHLine - FIRSTRASTERLINE] = pixel.PixelColor;
                                }
                            }

                            if (C64.network.IsHosting && C64.network.HostNumClients > 0)
                            {
                                int currentPos =
                                    (currentHLine - FIRSTRASTERLINE) * VISIBLESCREENWIDTH +
                                    (x - FIRSTLINEPIXEL);

                                serializedScreen[currentPos / 2] =
                                    (byte)(
                                    (
                                        serializedScreen[currentPos / 2] & 
                                        (0xF << ((currentPos % 2) * 4))
                                    ) |
                                    (
                                        (C64.palette.MapColorToValue(
                                            activeScreen[x - FIRSTLINEPIXEL, currentHLine - FIRSTRASTERLINE]
                                        ) << ((1 - (currentPos % 2)) * 4))
                                    ));
                            }
                        }
                    }

                currentVLine += 8;
                if (currentVLine == FULLSCREENWIDTH)
                {
                    currentVLine = 0;
                    currentHLine++;
                    if (currentHLine == FULLSCREENHEIGHT)
                    {
                        currentHLine = 0;
                        
                        C64.renderer.Draw();
                        
                        if (
                            C64.network.IsHosting &&
                            C64.network.HostNumClients > 0 &&
                            C64.network.ImageReady
                            )
                            C64.network.Send(NetCommands.Image, serializedScreen);
                    }

                    if (rasterIntEnabled && currentHLine == rasterIntLine)
                    {
                        register[0x19] |= 0x81;
                        C64.cpu.IRQ();
                    }
                }
            }
        }

        public ushort BankAddress
        {
            get
            {
                return bankAddress;
            }
            set
            {
                bankAddress = (ushort)((~(value & 3)) * 0x4000);
                updateVMAddress();
                updatePMAddress();
            }
        }

        public VideoMode CurrentVideoMode
        {
            get
            {
                return currentVideoMode;
            }
        }

        public bool BitmapMode
        {
            get
            {
                return bitmapMode;
            }
        }

        public ushort BankAddressPosition
        {
            get
            {
                return BANKADDRESS_POS;
            }
        }

        public ushort CharacterPosition1
        {
            get
            {
                return CHAR_POS1;
            }
        }

        public ushort CharacterPosition2
        {
            get
            {
                return CHAR_POS2;
            }
        }

        public int RegistersPosition
        {
            get
            {
                return REGISTERS_POS;
            }
        }

        public byte Peek(ushort address)
        {
            if (address >= CHAR_POS1 && address < CHAR_POS1 + C64.charset.Size)
                return C64.charset.Peek((ushort)((address + 0xC000) - C64.charset.Position1));
            else if (address >= CHAR_POS2 &&  address < CHAR_POS2 + C64.charset.Size)
                return C64.charset.Peek((ushort)((address + 0x4000) - C64.charset.Position1));
            else
                return C64.ram.VICPeek(address);
        }

        public void SetRegister(int regnum, byte value)
        {
            register[regnum] = value;

            switch (regnum)
            {
                case 0x00:
                case 0x02:
                case 0x04:
                case 0x06:
                case 0x08:
                case 0x0A:
                case 0x0C:
                case 0x0E:
                    int xNum = regnum / 2;
                    sprites[xNum].X = (sprites[xNum].X & 0x100) | value;
                    break;

                case 0x01:
                case 0x03:
                case 0x05:
                case 0x07:
                case 0x09:
                case 0x0B:
                case 0x0D:
                case 0x0F:
                    int yNum = regnum / 2;
                    sprites[yNum].Y = value;
                    break;

                case 0x10:
                    for (int i = 0; i < 8; i++)
                        sprites[i].X = (sprites[i].X & 0xFF) | ((value & (1 << i)) << (8 - i));
                    break;

                case 0x11:
                    verticalScroll = (byte)(value & 0x7);
                    textRows = (byte)((value & 8) != 0 ? 25 : 24);
                    if (textRows == 24)
                    {
                        firstScreenLine = FIRSTSCREENLINE + 4;
                        lastScreenLine = LASTSCREENLINE - 4;
                    }
                    else
                    {
                        firstScreenLine = FIRSTSCREENLINE;
                        lastScreenLine = LASTSCREENLINE;
                    }
                    screenEnabled = ((value & 0x10) != 0);
                    rasterIntLine = (rasterIntLine & 0xFF) | ((value & 0x80) << 1);
                    currentVideoMode = (VideoMode)((value & 0x60) | ((byte)currentVideoMode & 0x10));
                    bitmapMode = (((byte)currentVideoMode) & 0x20) != 0;
                    updatePMAddress();
                    break;

                case 0x12:
                    rasterIntLine = (rasterIntLine & 0x100) | value;
                    break;

                case 0x15:
                    for (int i = 0; i < 8; i++)
                        sprites[i].Enabled = ((value & (1 << i)) != 0);
                    break;

                case 0x16:
                    horizontalScroll = (byte)(value & 0x7);
                    textCols = (byte)((value & 8) != 0 ? 40 : 38);
                    if (textCols == 38)
                    {
                        firstScreenPixel = FIRSTSCREENPIXEL + 7;
                        lastScreenPixel = LASTSCREENPIXEL - 9;
                    }
                    else
                    {
                        firstScreenPixel = FIRSTSCREENPIXEL;
                        lastScreenPixel = LASTSCREENPIXEL;
                    }
                    currentVideoMode = (VideoMode)(((byte)currentVideoMode & 0x60) | (value & 0x10));
                    bitmapMode = (((byte)currentVideoMode) & 0x20) != 0;
                    updatePMAddress();
                    break;

                case 0x17:
                    for (int i = 0; i < 8; i++)
                        if ((value & (1 << i)) != 0)
                            sprites[i].Height = 42;
                        else
                            sprites[i].Height = 21;
                    break;

                case 0x18:
                    updateVMAddress();
                    updatePMAddress();
                    break;

                case 0x19:
                    register[regnum] &= (byte)(~value);
                    break;

                case 0x1A:
                    rasterIntEnabled = ((value & 1) != 0);
                    spriteBGCollide = ((value & 2) != 0);
                    spriteSpriteCollide = ((value & 4) != 0);
                    lightPenIntEnabled = ((value & 8) != 0);
                    break;

                case 0x1B:
                    for (int i = 0; i < 8; i++)
                        sprites[i].TopMost = ((value & (1 << i)) == 0);
                    break;

                case 0x1C:
                    for (int i = 0; i < 8; i++)
                        sprites[i].MulticolorMode = ((value & (1 << i)) != 0);
                    break;

                case 0x1D:
                    for (int i = 0; i < 8; i++)
                        if ((value & (1 << i)) != 0)
                            sprites[i].Width = 48;
                        else
                            sprites[i].Width = 24;
                    break;

                case 0x27:
                case 0x28:
                case 0x29:
                case 0x2A:
                case 0x2B:
                case 0x2C:
                case 0x2D:
                case 0x2E:
                    int colorNum = regnum - 0x27;
                    sprites[colorNum].SpriteColor = C64.palette.MapColorValue(value);
                    break;
            }
        }

        private void updateVMAddress()
        {
            videoMatrixAddress = (ushort)(bankAddress + (register[0x18] >> 4) * 0x400);
        }

        private void updatePMAddress()
        {
            if (bitmapMode)
                pixelMatrixAddress = (ushort)(bankAddress + (register[0x18] & 0x08) * 0x400);
            else
                pixelMatrixAddress = (ushort)(bankAddress + (register[0x18] & 0x0E) * 0x400);
        }

        public byte GetRegister(int regnum)
        {
            switch (regnum)
            {
                case 0x11:
                    return (byte)((register[regnum] & 0x7F) | ((currentHLine & 0x100) >> 1));
                case 0x12:
                    return (byte)(currentHLine & 0xFF);
                case 0x13:
                    return (byte)(currentVLine - 96);
                case 0x14:
                    return 0;
                case 0x16:
                    return (byte)(register[regnum] | 0xC0);
                case 0x18:
                    return (byte)(register[regnum] | 1);
                case 0x19:
                    return (byte)(register[regnum] | 0x70);
                case 0x1A:
                    return (byte)(register[regnum] | 0xF0);
                case 0x1E:
                case 0x1F:
                    byte ret = register[regnum];
                    register[regnum] = 0;
                    return ret;
                case 0x20:
                case 0x21:
                case 0x22:
                case 0x23:
                case 0x24:
                case 0x25:
                case 0x26:
                case 0x27:
                case 0x28:
                case 0x29:
                case 0x2A:
                case 0x2B:
                case 0x2C:
                case 0x2D:
                case 0x2E:
                    return (byte)(register[regnum] | 0xF0);
                default: return register[regnum];
            }
        }

        public int NumRegisters
        {
            get
            {
                return NUM_REGISTERS;
            }
        }

        private Pixel getSpritePixel(int sprite, int x, int y)
        {
            if (
                x >= sprites[sprite].X &&
                x < sprites[sprite].X + sprites[sprite].Width &&
                y >= sprites[sprite].Y &&
                y < sprites[sprite].Y + sprites[sprite].Height)
            {
                Pixel pixel = new Pixel();

                int xOffset = x - sprites[sprite].X;
                if (sprites[sprite].Width == 48) xOffset /= 2;
                int yOffset = y - sprites[sprite].Y;
                if (sprites[sprite].Height == 42) yOffset /= 2;

                ushort address = (ushort)(videoMatrixAddress + 0x400 - (8 - sprite));
                ushort spritePixelAddress = (ushort)(bankAddress + Peek(address) * 0x40);
                
                ushort position = (ushort)((yOffset * 24 + xOffset) / 8);
                int bitPos = 7 - (xOffset % 8);
                byte value = 0;

                if (sprites[sprite].MulticolorMode)
                {
                    value =
                        (byte)((Peek((ushort)(spritePixelAddress + position)) >> ((bitPos / 2) * 2)) & 3);
                    
                    switch (value)
                    {
                        case 0: return emptyPixel;
                        case 1:
                            pixel.PixelColor = C64.palette.MapColorValue(register[0x25]);
                            pixel.Background = false;
                            break;
                        case 2:
                            pixel.PixelColor = sprites[sprite].SpriteColor;
                            pixel.Background = false;
                            break;
                        case 3:
                            pixel.PixelColor = C64.palette.MapColorValue(register[0x26]);
                            pixel.Background = false;
                            break;
                    }

                    return pixel;
                }
                else
                {
                    value =
                        (byte)((Peek((ushort)(spritePixelAddress + position)) >> bitPos) & 1);

                    if (value == 0)
                        return emptyPixel;
                    else
                    {
                        pixel.PixelColor = sprites[sprite].SpriteColor;
                        pixel.Background = false;
                        return pixel;
                    }
                }
            }
            else
                return emptyPixel;
        }

        private Pixel getPixel(int x, int y)
        {
            switch (currentVideoMode)
            {
                case VideoMode.SimpleCharacterMode: return getSCM(x, y);
                case VideoMode.MultiColorCharacterMode: return getMCCM(x, y);
                case VideoMode.ExtendedColorCharacterMode: return getECCM(x, y);
                case VideoMode.SimpleBitMapMode: return getSBMM(x, y);
                case VideoMode.MultiColorBitMapMode: return getMCBMM(x, y);
                default: return emptyPixel;
            }
        }

        private Pixel getSCM(int x, int y)
        {
            Pixel pixel = new Pixel();
            ushort position = (ushort)(y / 8 * 40 + x / 8);
            byte value = Peek((ushort)(videoMatrixAddress + position));
            int xRem = x % 8;
            int yRem = y % 8;

            if ((Peek((ushort)(pixelMatrixAddress + value * 8 + yRem)) & (byte)(0x80 >> xRem)) != 0)
            {
                pixel.PixelColor = C64.palette.MapColorPosition(position);
                pixel.Background = false;
                return pixel;
            }
            else
            {
                pixel.PixelColor = C64.palette.MapColorValue(register[0x21]);
                pixel.Background = true;
                return pixel;
            }
        }

        private Pixel getMCCM(int x, int y)
        {
            Pixel pixel = new Pixel();
            ushort position = (ushort)(y / 8 * 40 + x / 8);
            byte value = Peek((ushort)(videoMatrixAddress + position));
            int xRem = x % 8;
            int yRem = y % 8;

            if ((C64.palette.Peek(position) & 0x8) != 0)
            {
                switch ((Peek((ushort)(pixelMatrixAddress + value * 8 + yRem)) >> (2 * ((7 - xRem) / 2))) & 0x3)
                {
                    case 0:
                        pixel.PixelColor = C64.palette.MapColorValue(register[0x21]);
                        pixel.Background = true;
                        break;
                    case 1: 
                        pixel.PixelColor = C64.palette.MapColorValue(register[0x22]);
                        pixel.Background = true;
                        break;
                    case 2: 
                        pixel.PixelColor = C64.palette.MapColorValue(register[0x23]);
                        pixel.Background = false;
                        break;
                    case 3: 
                        pixel.PixelColor = C64.palette.MapColorValue((byte)(C64.palette.Peek(position) & 0x7));
                        pixel.Background = false;
                        break;
                    default: return emptyPixel;
                }

                return pixel;
            }
            else
                return getSCM(x, y);
        }

        private Pixel getECCM(int x, int y)
        {
            Pixel pixel = new Pixel();
            ushort position = (ushort)(y / 8 * 40 + x / 8);
            byte value = Peek((ushort)(videoMatrixAddress + position));
            int xRem = x % 8;
            int yRem = y % 8;

            byte bgColor = (byte)((value & 0xC0) >> 6);
            value = (byte)(value & 0x3F);

            if ((Peek((ushort)(pixelMatrixAddress + value * 8 + yRem)) & (byte)(0x80 >> xRem)) != 0)
            {
                pixel.PixelColor = C64.palette.MapColorPosition(position);
                pixel.Background = false;
                return pixel;
            }
            else
            {
                pixel.PixelColor = C64.palette.MapColorValue(register[0x21 + bgColor]);
                pixel.Background = true;
                return pixel;
            }
        }

        private Pixel getSBMM(int x, int y)
        {
            Pixel pixel = new Pixel();
            ushort position = (ushort)(y / 8 * 40 + x / 8);
            byte value = Peek((ushort)(videoMatrixAddress + position));
            int xRem = x % 8;
            int yRem = y % 8;

            if ((Peek((ushort)(pixelMatrixAddress + position * 8 + yRem)) & (byte)(0x80 >> xRem)) != 0)
            {
                pixel.PixelColor = C64.palette.MapColorValue((byte)((value & 0xF0) >> 4));
                pixel.Background = false;
                return pixel;
            }
            else
            {
                pixel.PixelColor = C64.palette.MapColorValue((byte)(value & 0xF));
                pixel.Background = true;
                return pixel;
            }
        }

        private Pixel getMCBMM(int x, int y)
        {
            Pixel pixel = new Pixel();
            ushort position = (ushort)(y / 8 * 40 + x / 8);
            byte value = Peek((ushort)(videoMatrixAddress + position));
            int xRem = x % 8;
            int yRem = y % 8;

            switch ((Peek((ushort)(pixelMatrixAddress + position * 8 + yRem)) >> (2 * ((7 - xRem) / 2))) & 0x3)
            {
                case 0: 
                    pixel.PixelColor = C64.palette.MapColorValue(register[0x21]);
                    pixel.Background = true;
                    break;
                case 1: 
                    pixel.PixelColor = C64.palette.MapColorValue((byte)((value & 0xF0) >> 4));
                    pixel.Background = true;
                    break;
                case 2: 
                    pixel.PixelColor = C64.palette.MapColorValue((byte)(value & 0xF));
                    pixel.Background = false;
                    break;
                case 3: 
                    pixel.PixelColor = C64.palette.MapColorPosition(position);
                    pixel.Background = false;
                    break;
                default: return emptyPixel;
            }

            return pixel;
        }
    }
}
