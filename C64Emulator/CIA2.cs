using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C64Emulator
{
    class CIA2
    {
        private const ushort CIA2_POS = 0xDD00;
        private const int CIA2_SIZE = 0xFF;

        private const int CLOCK_CYCLES = 100000;

        private byte timeTenthSecs = 0;
        private byte timeSeconds = 0;
        private byte timeMinutes = 0;
        private byte timeHours = 0;
        private bool timePM = false;

        private bool paused = false;

        private int timerAValue = 0;
        private int timerAStartValue = 0;
        private bool timerAEnabled = false;
        private bool timerAPortBUnderflow = false;
        private bool timerAPortBInvert = true;
        private bool timerARestart = true;

        private int timerBValue = 0;
        private int timerBStartValue = 0;
        private bool timerBEnabled = false;
        private bool timerBPortBUnderflow = false;
        private bool timerBPortBInvert = true;
        private bool timerBRestart = true;

        private bool timerAUnderflow = false;
        private bool timerBUnderflow = false;

        private bool invertPortBBit6 = false;

        private byte interruptStatus = 0;

        private int clockCycles = 0;

        public CIA2()
        {
            timeTenthSecs = (byte)(DateTime.Now.Millisecond / 100);
            timeSeconds = (byte)DateTime.Now.Second;
            timeMinutes = (byte)DateTime.Now.Minute;
            timeHours = (byte)(DateTime.Now.Hour % 12);
            timePM = (DateTime.Now.Hour >= 12);
        }

        public void ProcessTimers(int cycles)
        {
            clockCycles += cycles;

            if (clockCycles >= CLOCK_CYCLES)
            {
                clockCycles -= CLOCK_CYCLES;

                timeTenthSecs++;

                if (timeTenthSecs == 10)
                {
                    timeTenthSecs = 0;

                    timeSeconds++;

                    if (timeSeconds == 60)
                    {
                        timeSeconds = 0;

                        timeMinutes++;

                        if (timeMinutes == 60)
                        {
                            timeMinutes = 0;

                            timeHours++;

                            if (timeHours == 12)
                            {
                                timeHours = 0;

                                timePM = !timePM;
                            }
                        }
                    }
                }
            }

            if (timerAEnabled)
            {
                timerAValue -= cycles;
                invertPortBBit6 = false;

                if (timerAValue <= 0)
                {
                    interruptStatus = 1;

                    if (timerAPortBUnderflow && timerAPortBInvert)
                        invertPortBBit6 = true;

                    if (timerAUnderflow)
                    {
                        interruptStatus = 0x81;
                        C64.cpu.IRQ();
                    }

                    if (!timerARestart)
                    {
                        timerAValue = timerAStartValue;
                        timerAEnabled = false;
                    }
                    else
                    {
                        if (timerAStartValue == 0)
                            timerAValue = 0;
                        else
                            while (timerAValue < 0)
                                timerAValue = timerAStartValue + timerAValue;
                    }
                }
            }
            if (timerBEnabled)
            {
                timerBValue -= cycles;
                invertPortBBit6 = false;

                if (timerBValue <= 0)
                {
                    interruptStatus = 2;

                    if (timerBPortBUnderflow && timerBPortBInvert)
                        invertPortBBit6 = true;

                    if (timerBUnderflow)
                    {
                        interruptStatus = 0x82;
                        C64.cpu.IRQ();
                    }

                    if (!timerBRestart)
                    {
                        timerBValue = timerBStartValue;
                        timerBEnabled = false;
                    }
                    else
                    {
                        if (timerBStartValue == 0)
                            timerBValue = 0;
                        else
                            while (timerBValue < 0)
                                timerBValue = timerBStartValue + timerBValue;
                    }
                }
            }
        }

        public int Size
        {
            get
            {
                return CIA2_SIZE;
            }
        }

        public ushort Position
        {
            get
            {
                return CIA2_POS;
            }
        }

        public byte Peek(ushort address)
        {
            switch (address)
            {
                case 0x01: return (byte)(invertPortBBit6 ? 0xBF : 0xFF);
                case 0x04: return (byte)(timerAValue & 0xFF);
                case 0x05: return (byte)((timerAValue >> 8) & 0xFF);
                case 0x06: return (byte)(timerBValue & 0xFF);
                case 0x07: return (byte)((timerBValue >> 8) & 0xFF);
                case 0x08: return timeTenthSecs;
                case 0x09: return timeSeconds;
                case 0x0A: return timeMinutes;
                case 0x0B: return (byte)(timeHours | (timePM ? 0x80 : 0));
                case 0x0D:
                    byte status = interruptStatus;
                    interruptStatus = 0;
                    return status;
            }

            return 0xFF;
        }

        public void Poke(ushort address, byte value)
        {
            switch (address)
            {
                case 0x04: timerAStartValue = ((timerAStartValue & 0xFF00) | value); break;
                case 0x05: timerAStartValue = ((timerAStartValue & 0x00FF) | (value << 8)); break;
                case 0x06: timerBStartValue = ((timerBStartValue & 0xFF00) | value); break;
                case 0x07: timerBStartValue = ((timerBStartValue & 0x00FF) | (value << 8)); break;
                case 0x08: timeTenthSecs = value; break;
                case 0x09: timeSeconds = value; break;
                case 0x0A: timeMinutes = value; break;
                case 0x0B:
                    timeHours = (byte)(value & 0x1F);
                    timePM = ((value & 0x80) == 1);
                    break;
                case 0x0D:
                    bool fillBit = ((value & 0x80) != 0);
                    if ((value & 1) != 0) timerAUnderflow = fillBit;
                    if ((value & 2) != 0) timerBUnderflow = fillBit;
                    break;
                case 0x0E:
                    timerAEnabled = ((value & 0x1) != 0);
                    timerAPortBUnderflow = ((value & 0x2) != 0);
                    timerAPortBInvert = ((value & 0x4) == 0);
                    timerARestart = ((value & 0x8) == 0);
                    if ((value & 0x10) != 0) timerAValue = timerAStartValue;
                    break;
                case 0x0F:
                    timerBEnabled = ((value & 0x1) != 0);
                    timerBPortBUnderflow = ((value & 0x2) != 0);
                    timerBPortBInvert = ((value & 0x4) == 0);
                    timerBRestart = ((value & 0x8) == 0);
                    if ((value & 0x10) != 0) timerBValue = timerBStartValue;
                    break;
            }
        }
    }
}
