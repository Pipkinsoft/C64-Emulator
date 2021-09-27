using System;
using System.Collections.Generic;
using System.Text;
using SdlDotNet.Input;
using C64Emulator.Properties;

namespace C64Emulator
{
    public enum JoystickFunction : byte
    {
        None = 0,
        Fire = 1,
        Up = 2,
        Down = 3,
        Left = 4,
        Right = 5,
        VerticalCenter = 6,
        HorizontalCenter = 7
    }

    public enum MapType : byte
    {
        None = 0,
        Joystick1 = 1,
        Joystick2 = 2,
        KeySet1 = 3,
        KeySet2 = 4
    }

    public struct KeySet
    {
        public Key Up;
        public Key Right;
        public Key Down;
        public Key Left;
        public Key Fire;
    }

    class CIA1
    {
        private const ushort CIA1_POS = 0xDC00;
        private const int CIA1_SIZE = 0xFF;

        private const int CLOCK_CYCLES = 98500;
        
        private byte timeTenthSecs = 0;
        private byte timeSeconds = 0;
        private byte timeMinutes = 0;
        private byte timeHours = 0;
        private bool timePM = false;

        private byte portAMask = 0;
        private byte portBMask = 0;

        private bool joyPort1Up = false;
        private bool joyPort1Right = false;
        private bool joyPort1Down = false;
        private bool joyPort1Left = false;
        private bool joyPort1Fire = false;
        private bool joyPort2Up = false;
        private bool joyPort2Right = false;
        private bool joyPort2Down = false;
        private bool joyPort2Left = false;
        private bool joyPort2Fire = false;

        private readonly MapType[] joyMap;
        private readonly KeySet[] keyset;

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

        private int selectedPaddle = 1;

        private int clockCycles = 0;

        private byte kbMatrixColumn = 0xFF;
        private byte[] keyboardMatrix;

        public CIA1()
        {
            keyboardMatrix = new byte[8]
            { 
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };

            portAMask = 0xFF;
            portBMask = 0;

            KeySet keyset1 = new KeySet();
            keyset1.Up = Settings.Default.KeySet1Up;
            keyset1.Right = Settings.Default.KeySet1Right;
            keyset1.Down = Settings.Default.KeySet1Down;
            keyset1.Left = Settings.Default.KeySet1Left;
            keyset1.Fire = Settings.Default.KeySet1Fire;

            KeySet keyset2 = new KeySet();
            keyset2.Up = Settings.Default.KeySet2Up;
            keyset2.Right = Settings.Default.KeySet2Right;
            keyset2.Down = Settings.Default.KeySet2Down;
            keyset2.Left = Settings.Default.KeySet2Left;
            keyset2.Fire = Settings.Default.KeySet2Fire;

            keyset = new KeySet[] { keyset1, keyset2 };

            joyMap = new MapType[]
            {
                (MapType)Settings.Default.Joystick1Map,
                (MapType)Settings.Default.Joystick2Map
            };

            timeTenthSecs = (byte)(DateTime.Now.Millisecond / 100);
            timeSeconds = (byte)DateTime.Now.Second;
            timeMinutes = (byte)DateTime.Now.Minute;
            timeHours = (byte)(DateTime.Now.Hour % 12);
            timePM = (DateTime.Now.Hour >= 12);
        }

        public void KeyEvent(KeyboardEventArgs k, bool skipMapCheck)
        {
            processKey(k, skipMapCheck);
        }

        public void SwitchJoysticks()
        {
            MapType jm1 = joyMap[0];
            joyMap[0] = joyMap[1];
            joyMap[1] = jm1;
        }

        public MapType Joy1Map
        {
            get
            {
                return joyMap[0];
            }
            set
            {
                joyMap[0] = value;
                Settings.Default.Joystick1Map = (int)value;
            }
        }

        public MapType Joy2Map
        {
            get
            {
                return joyMap[1];
            }
            set
            {
                joyMap[1] = value;
                Settings.Default.Joystick2Map = (int)value;
            }
        }

        public KeySet KeySet1
        {
            get
            {
                return keyset[0];
            }
        }

        public KeySet KeySet2
        {
            get
            {
                return keyset[1];
            }
        }

        public void SetKeySet(int ksnum, string function, Key key)
        {
            switch (function)
            {
                case "up":
                    keyset[ksnum - 1].Up = key;
                    if (ksnum == 1)
                        Settings.Default.KeySet1Up = key;
                    else
                        Settings.Default.KeySet2Up = key;
                    break;
                case "right":
                    keyset[ksnum - 1].Right = key;
                    if (ksnum == 1)
                        Settings.Default.KeySet1Right = key;
                    else
                        Settings.Default.KeySet2Right = key;
                    break;
                case "down": 
                    keyset[ksnum - 1].Down = key;
                    if (ksnum == 1)
                        Settings.Default.KeySet1Down = key;
                    else
                        Settings.Default.KeySet2Down = key;
                    break;
                case "left": 
                    keyset[ksnum - 1].Left = key;
                    if (ksnum == 1)
                        Settings.Default.KeySet1Left = key;
                    else
                        Settings.Default.KeySet2Left = key;
                    break;
                case "fire": 
                    keyset[ksnum - 1].Fire = key;
                    if (ksnum == 1)
                        Settings.Default.KeySet1Fire = key;
                    else
                        Settings.Default.KeySet2Fire = key;
                    break;
            }
        }

        public void JoyEvent(int joyNum, JoystickFunction j, bool pressed)
        {
            processJoy(joyNum, j, pressed);
        }

        private void processJoy(int joyNum, JoystickFunction j, bool pressed)
        {
            if (C64.network.IsClient)
                sendNetJoy(joyNum, j, pressed);
            else
            {
                switch (j)
                {
                    case JoystickFunction.Fire:
                        if (joyNum == 1)
                            joyPort1Fire = pressed;
                        else
                            joyPort2Fire = pressed;
                        break;

                    case JoystickFunction.Up:
                        if (joyNum == 1)
                            joyPort1Up = pressed;
                        else
                            joyPort2Up = pressed;
                        break;

                    case JoystickFunction.Right:
                        if (joyNum == 1)
                            joyPort1Right = pressed;
                        else
                            joyPort2Right = pressed;
                        break;

                    case JoystickFunction.Down:
                        if (joyNum == 1)
                            joyPort1Down = pressed;
                        else
                            joyPort2Down = pressed;
                        break;

                    case JoystickFunction.Left:
                        if (joyNum == 1)
                            joyPort1Left = pressed;
                        else
                            joyPort2Left = pressed;
                        break;

                    case JoystickFunction.VerticalCenter:
                        if (joyNum == 1)
                        {
                            joyPort1Up = false;
                            joyPort1Down = false;
                        }
                        else
                        {
                            joyPort2Up = false;
                            joyPort2Down = false;
                        }
                        break;

                    case JoystickFunction.HorizontalCenter:
                        if (joyNum == 1)
                        {
                            joyPort1Left = false;
                            joyPort1Right = false;
                        }
                        else
                        {
                            joyPort2Left = false;
                            joyPort2Right = false;
                        }
                        break;

                    case JoystickFunction.None:
                        if (joyNum == 1)
                        {
                            joyPort1Up = false;
                            joyPort1Right = false;
                            joyPort1Down = false;
                            joyPort1Left = false;
                            joyPort1Fire = false;
                        }
                        else
                        {
                            joyPort2Up = false;
                            joyPort2Right = false;
                            joyPort2Down = false;
                            joyPort2Left = false;
                            joyPort2Fire = false;
                        }
                        break;
                }
            }
        }

        private JoystickFunction checkMap(int joyNum, Key k)
        {
            int ksnum = -1;

            if (joyMap[joyNum - 1] == MapType.KeySet1)
                ksnum = 0;
            else if (joyMap[joyNum - 1] == MapType.KeySet2)
                ksnum = 1;

            if (ksnum > -1)
            {
                if (keyset[ksnum].Up == k)
                    return JoystickFunction.Up;
                else if (keyset[ksnum].Right == k)
                    return JoystickFunction.Right;
                else if (keyset[ksnum].Down == k)
                    return JoystickFunction.Down;
                else if (keyset[ksnum].Left == k)
                    return JoystickFunction.Left;
                else if (keyset[ksnum].Fire == k)
                    return JoystickFunction.Fire;
            }

            return JoystickFunction.None;
        }

        private void processKey(KeyboardEventArgs k, bool skipMapCheck)
        {
            if (!skipMapCheck)
            {
                JoystickFunction jf = checkMap(1, k.Key);

                if (jf != JoystickFunction.None)
                {
                    processJoy(1, jf, k.Down);
                    return;
                }

                jf = checkMap(2, k.Key);

                if (jf != JoystickFunction.None)
                {
                    processJoy(2, jf, k.Down);
                    return;
                }
            }

            if (C64.network.IsClient)
                sendNetKey(k);
            else
            {
                C64Key key = KeyboardMatrix.GetC64Key(k);

                if (key.KeyValue > 0)
                {
                    if (k.Down)
                    {
                        keyboardMatrix[key.KeyColumn] &= (byte)(~key.KeyValue);

                        if (key.Shift == ShiftStatus.Enable)
                            keyboardMatrix[1] &= 0x7F;
                        else if (key.Shift == ShiftStatus.Disable)
                        {
                            keyboardMatrix[1] |= 0x80;
                            keyboardMatrix[6] |= 0x10;
                        }
                    }
                    else
                    {
                        keyboardMatrix[key.KeyColumn] |= key.KeyValue;

                        if (key.Shift == ShiftStatus.Enable)
                            keyboardMatrix[1] |= 0x80;
                    }
                }
            }
        }

        private void sendNetKey(KeyboardEventArgs k)
        {
            C64.network.Send(
                NetCommands.KeyEvent,
                new byte[] 
                { 
                    (byte)k.Key,
                    (byte)(KeyboardMatrix.shiftPressed(k) ? 1 : 0),
                    (byte)(k.Down ? 1 : 0)
                }
                );
        }

        private void sendNetJoy(int joyNum, JoystickFunction j, bool pressed)
        {
            C64.network.Send(
                NetCommands.JoyEvent,
                new byte[]
                {
                    (byte)joyNum, 
                    (byte)j,
                    (byte)(pressed ? 1 : 0)
                }
                );
        }

        public void ProcessTimers(int cycles)
        {
            clockCycles += cycles;

            if (clockCycles >= CLOCK_CYCLES)
            {
                clockCycles = clockCycles - CLOCK_CYCLES;

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
                return CIA1_SIZE;
            }
        }

        public ushort Position
        {
            get
            {
                return CIA1_POS;
            }
        }

        public byte Peek(ushort address)
        {
            switch (address)
            {
                case 0x00:
                    return
                        (byte)(
                        (joyPort2Up ? 0 : 0x1) |
                        (joyPort2Down ? 0 : 0x2) |
                        (joyPort2Left ? 0 : 0x4) |
                        (joyPort2Right ? 0 : 0x8) |
                        (joyPort2Fire ? 0 : 0x10) |
                        0xE0
                        );
                case 0x01:
                    byte ret = 0xFF;

                    for (int i = 0; i < 8; i++)
                        if ((kbMatrixColumn & (1 << i)) == 0)
                            ret &= keyboardMatrix[i];
                    
                    if (joyPort1Up) ret = (byte)(ret & 0xFE);
                    if (joyPort1Down) ret = (byte)(ret & 0xFD);
                    if (joyPort1Left) ret = (byte)(ret & 0xFB);
                    if (joyPort1Right) ret = (byte)(ret & 0xF7);
                    if (joyPort1Fire) ret = (byte)(ret & 0xEF);
                    if (invertPortBBit6) ret = (byte)(ret ^ 40);
                    return ret;
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
                case 0x00:
                    if ((portAMask & 0xC0) == 0xC0)
                    {
                        if ((value >> 6) == 1)
                            selectedPaddle = 1;
                        else if ((value >> 6) == 2)
                            selectedPaddle = 2;
                    }
                    
                    kbMatrixColumn = (byte)((value & portAMask) | (value & (~portAMask)));
                    break;
                case 0x02: portAMask = value; break;
                case 0x03: portBMask = value; break;
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

    public enum ShiftStatus
    {
        None,
        Enable,
        Disable
    }

    public struct C64Key
    {
        public int KeyColumn;
        public byte KeyValue;
        public ShiftStatus Shift;
    }

    static class KeyboardMatrix
    {
        public static bool shiftPressed(KeyboardEventArgs k)
        {
            return
                ((k.Mod & ModifierKeys.LeftShift) == ModifierKeys.LeftShift) ||
                ((k.Mod & ModifierKeys.RightShift) == ModifierKeys.RightShift);
        }

        public static C64Key GetC64Key(KeyboardEventArgs k)
        {
            C64Key key = new C64Key();

            key.KeyColumn = 0;
            key.KeyValue = 0;
            key.Shift = (shiftPressed(k) ? ShiftStatus.Enable : ShiftStatus.Disable);

            switch (k.Key)
            {
                case Key.Insert:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x01;
                    key.Shift = ShiftStatus.Enable;
                    break;
                
                case Key.Delete:
                case Key.Backspace:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x01;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.Return:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x02;
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.LeftArrow:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x04;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.RightArrow:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x04;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.F7:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x08;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.F8:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x08;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.F1:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x10;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.F2:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x10;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.F3:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x20;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.F4:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x20;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.F5:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x40;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.F6:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x40;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.UpArrow:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x80;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.DownArrow:
                    key.KeyColumn = 0;
                    key.KeyValue = 0x80;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.Three:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x01;
                    break;

                case Key.W:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x02;
                    break;

                case Key.A:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x04;
                    break;

                case Key.Four:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x08;
                    break;

                case Key.Z:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x10;
                    break;

                case Key.S:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x20;
                    break;

                case Key.E:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x40;
                    break;

                case Key.LeftShift:
                    key.KeyColumn = 1;
                    key.KeyValue = 0x80;
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.Five:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x01;
                    break;

                case Key.R:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x02;
                    break;

                case Key.D:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x04;
                    break;

                case Key.Six:
                    key.KeyColumn = (shiftPressed(k) ? 0 : 2);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0 : 0x08);
                    key.Shift = (shiftPressed(k) ? ShiftStatus.None : ShiftStatus.Disable);
                    break;

                case Key.C:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x10;
                    break;

                case Key.F:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x20;
                    break;

                case Key.T:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x40;
                    break;

                case Key.X:
                    key.KeyColumn = 2;
                    key.KeyValue = 0x80;
                    break;

                case Key.Seven:
                    key.KeyColumn = (shiftPressed(k) ? 2 : 3);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x08 : 0x01);
                    break;

                case Key.Y:
                    key.KeyColumn = 3;
                    key.KeyValue = 0x02;
                    break;

                case Key.G:
                    key.KeyColumn = 3;
                    key.KeyValue = 0x04;
                    break;

                case Key.Eight:
                    key.KeyColumn = (shiftPressed(k) ? 6 : 3);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x02 : 0x08);
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.B:
                    key.KeyColumn = 3;
                    key.KeyValue = 0x10;
                    break;

                case Key.H:
                    key.KeyColumn = 3;
                    key.KeyValue = 0x20;
                    break;

                case Key.U:
                    key.KeyColumn = 3;
                    key.KeyValue = 0x40;
                    break;

                case Key.V:
                    key.KeyColumn = 3;
                    key.KeyValue = 0x80;
                    break;

                case Key.Nine:
                    key.KeyColumn = (shiftPressed(k) ? 3 : 4);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x08 : 0x01);
                    break;

                case Key.I:
                    key.KeyColumn = 4;
                    key.KeyValue = 0x02;
                    break;

                case Key.J:
                    key.KeyColumn = 4;
                    key.KeyValue = 0x04;
                    break;

                case Key.Zero:
                    key.KeyColumn = (shiftPressed(k) ? 4 : 4);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x01 : 0x08);
                    break;

                case Key.M:
                    key.KeyColumn = 4;
                    key.KeyValue = 0x10;
                    break;

                case Key.K:
                    key.KeyColumn = 4;
                    key.KeyValue = 0x20;
                    break;

                case Key.O:
                    key.KeyColumn = 4;
                    key.KeyValue = 0x40;
                    break;

                case Key.N:
                    key.KeyColumn = 4;
                    key.KeyValue = 0x80;
                    break;

                case Key.Equals:
                    key.KeyColumn = (shiftPressed(k) ? 5 : 6);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x01 : 0x20);
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.P:
                    key.KeyColumn = 5;
                    key.KeyValue = 0x02;
                    break;

                case Key.L:
                    key.KeyColumn = 5;
                    key.KeyValue = 0x04;
                    break;

                case Key.Minus:
                    key.KeyColumn = (shiftPressed(k) ? 0 : 5);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x00 : 0x08);
                    key.Shift = (shiftPressed(k) ? ShiftStatus.None : ShiftStatus.Disable);
                    break;

                case Key.Period:
                    key.KeyColumn = 5;
                    key.KeyValue = 0x10;
                    break;

                case Key.Semicolon:
                    key.KeyColumn = (shiftPressed(k) ? 5 : 6);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x20 : 0x04);
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.Two:
                    key.KeyColumn = (shiftPressed(k) ? 5 : 7);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x40 : 0x08);
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.Comma:
                    key.KeyColumn = 5;
                    key.KeyValue = 0x80;
                    break;

                case Key.LeftBracket:
                    key.KeyColumn = (shiftPressed(k) ? 6 : 5);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x01 : 0x20);
                    key.Shift = (shiftPressed(k) ? ShiftStatus.Disable : ShiftStatus.Enable);
                    break;

                case Key.Home:
                    key.KeyColumn = 6;
                    key.KeyValue = 0x08;
                    key.Shift = ShiftStatus.Disable;
                    break;

                case Key.End:
                    key.KeyColumn = 6;
                    key.KeyValue = 0x08;
                    key.Shift = ShiftStatus.Enable;
                    break;

                case Key.RightShift:
                case Key.CapsLock:
                    key.KeyColumn = 6;
                    key.KeyValue = 0x10;
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.Backslash:
                    key.KeyColumn = 6;
                    key.KeyValue = 0x40;
                    break;

                case Key.Slash:
                    key.KeyColumn = 6;
                    key.KeyValue = 0x80;
                    break;

                case Key.One:
                    key.KeyColumn = 7;
                    key.KeyValue = 0x01;
                    break;

                case Key.BackQuote:
                    key.KeyColumn = (shiftPressed(k) ? 0 : 7);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x00 : 0x02);
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.LeftControl:
                case Key.RightControl:
                    key.KeyColumn = 7;
                    key.KeyValue = 0x04;
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.Space:
                    key.KeyColumn = 7;
                    key.KeyValue = 0x10;
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.PageUp:
                case Key.PageDown:
                    key.KeyColumn = 7;
                    key.KeyValue = 0x20;
                    key.Shift = ShiftStatus.None;
                    break;

                case Key.Q:
                    key.KeyColumn = 7;
                    key.KeyValue = 0x40;
                    break;

                case Key.Escape:
                    key.KeyColumn = 7;
                    key.KeyValue = 0x80;
                    break;

                case Key.RightBracket:
                    key.KeyColumn = (shiftPressed(k) ? 0 : 6);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x00 : 0x04);
                    key.Shift = (shiftPressed(k) ? ShiftStatus.Disable : ShiftStatus.Enable);
                    break;

                case Key.Quote:
                    key.KeyColumn = (shiftPressed(k) ? 7 : 3);
                    key.KeyValue = (byte)(shiftPressed(k) ? 0x08 : 0x01);
                    key.Shift = ShiftStatus.Enable;
                    break;
            }

            return key;
        }
    }
}
