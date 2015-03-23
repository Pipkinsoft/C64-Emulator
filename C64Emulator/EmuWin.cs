using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using SdlDotNet;
using SdlDotNet.Core;
using SdlDotNet.Graphics;
using SdlDotNet.Audio;
using SdlDotNet.Input;
using C64Emulator.Properties;

namespace C64Emulator
{
    public class EmuWin : IDisposable
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// [STAThread]
        static void Main(string[] args)
        {
            EmuWin emu = new EmuWin();
            emu.Start();
        }

        private RendererSDL renderer;
        private Network network;
        private Menu menu;
        private Joystick joy1;
        private Joystick joy2;
        private bool enteringChat = false;

        public EmuWin()
        {
        }

        public void Start()
        {
            network = new Network();
            network.screenReceived += new ScreenReceived(network_screenReceived);
            network.keyEventReceived += new KeyEventReceived(network_keyEventReceived);
            network.joyEventReceived += new JoyEventReceived(network_joyEventReceived);
            network.clientConnected += new ClientConnected(network_clientConnected);
            network.clientConnectFailed += new ClientConnectFailed(network_clientConnectFailed);
            network.clientDisconnected += new ClientDisconnected(network_clientDisconnected);
            network.incomingClient += new IncomingClient(network_incomingClient);
            network.partedClient += new PartedClient(network_partedClient);

            Video.WindowIcon(Resources.c64icon);
            Video.WindowCaption = "Pipkinsoft C64 Emulator";
            
            renderer = new RendererSDL();

            menu = new Menu(renderer, network);
            menu.menuReturn += new MenuReturn(menu_menuReturn);
            menu.menuQuit += new MenuQuit(menu_menuQuit);

            try
            {
                C64.Start(renderer, network);
            }
            catch (Exception ex)
            {
                renderer.DrawError(ex.Message);
                Events.Quit += new EventHandler<QuitEventArgs>(Quit);
                Events.Run();
                return;
            }
            
            Joysticks.Initialize();

            if (Joysticks.NumberOfJoysticks > 0)
                joy1 = Joysticks.OpenJoystick(0);
            if (Joysticks.NumberOfJoysticks > 1)
                joy2 = Joysticks.OpenJoystick(1);

            Events.Quit += new EventHandler<QuitEventArgs>(Quit);
            Events.KeyboardDown += new EventHandler<KeyboardEventArgs>(KeyboardEvent);
            Events.KeyboardUp += new EventHandler<KeyboardEventArgs>(KeyboardEvent);
            Events.JoystickAxisMotion += new EventHandler<JoystickAxisEventArgs>(JoystickAxisMotion);
            Events.JoystickButtonDown += new EventHandler<JoystickButtonEventArgs>(JoystickButtonEvent);
            Events.JoystickButtonUp += new EventHandler<JoystickButtonEventArgs>(JoystickButtonEvent);
            
            Events.Run();
        }

        private void JoystickButtonEvent(object sender, JoystickButtonEventArgs e)
        {
            if (
                (joy1 != null && C64.cia1.Joy1Map == MapType.Joystick1) ||
                (joy2 != null && C64.cia1.Joy1Map == MapType.Joystick2)
                )
                C64.cia1.JoyEvent(1, JoystickFunction.Fire, e.ButtonPressed);
            if (
                (joy1 != null && C64.cia1.Joy2Map == MapType.Joystick1) ||
                (joy2 != null && C64.cia1.Joy2Map == MapType.Joystick2)
                )
                C64.cia1.JoyEvent(2, JoystickFunction.Fire, e.ButtonPressed);
        }

        private void JoystickAxisMotion(object sender, JoystickAxisEventArgs e)
        {
            bool joy1Mapped = false;
            bool joy2Mapped = false;

            if (
                (joy1 != null && C64.cia1.Joy1Map == MapType.Joystick1) ||
                (joy2 != null && C64.cia1.Joy1Map == MapType.Joystick2)
                )
                joy1Mapped = true;
            if (
                (joy1 != null && C64.cia1.Joy2Map == MapType.Joystick1) ||
                (joy2 != null && C64.cia1.Joy2Map == MapType.Joystick2)
                )
                joy2Mapped = true;

            if (e.AxisIndex == 0)
            {
                switch ((int)e.AxisValue)
                {
                    case 1:
                        if (joy1Mapped) 
                            C64.cia1.JoyEvent(1, JoystickFunction.Right, true);
                        if (joy2Mapped)
                            C64.cia1.JoyEvent(2, JoystickFunction.Right, true);
                        break;
                    case -1:
                        if (joy1Mapped)
                            C64.cia1.JoyEvent(1, JoystickFunction.Left, true);
                        if (joy2Mapped)
                            C64.cia1.JoyEvent(2, JoystickFunction.Left, true);
                        break;
                    default:
                        if (joy1Mapped)
                            C64.cia1.JoyEvent(1, JoystickFunction.HorizontalCenter, true);
                        if (joy2Mapped)
                            C64.cia1.JoyEvent(2, JoystickFunction.HorizontalCenter, true);
                        break;
                }
            }
            else
            {
                switch ((int)e.AxisValue)
                {
                    case 1:
                        if (joy1Mapped)
                            C64.cia1.JoyEvent(1, JoystickFunction.Down, true);
                        if (joy2Mapped)
                            C64.cia1.JoyEvent(2, JoystickFunction.Down, true);
                        break;
                    case -1:
                        if (joy1Mapped)
                            C64.cia1.JoyEvent(1, JoystickFunction.Up, true);
                        if (joy2Mapped)
                            C64.cia1.JoyEvent(2, JoystickFunction.Up, true);
                        break;
                    default:
                        if (joy1Mapped)
                            C64.cia1.JoyEvent(1, JoystickFunction.VerticalCenter, true);
                        if (joy2Mapped)
                            C64.cia1.JoyEvent(2, JoystickFunction.VerticalCenter, true);
                        break;
                }
            }
        }

        private void menu_menuQuit(object sender, EventArgs e)
        {
            closeApp();
        }

        private void menu_menuReturn(object sender, EventArgs e)
        {
            menu.Active = false;
            renderer.ResetDrawState();
            renderer.ResetBuffer();
            renderer.Draw();
            if (!network.IsClient)
                C64.Unpause();
        }

        private void KeyboardEvent(object sender, KeyboardEventArgs e)
        {
            processKey(e);
        }

        private void network_joyEventReceived(object sender, JoyEventArgs j)
        {
            C64.cia1.JoyEvent(j.JoystickNumber, j.Function, j.Pressed);
        }

        private void network_partedClient(object sender, EventArgs e)
        {
            renderer.DisplayStatus("Client disconnected");
        }

        private void network_incomingClient(object sender, EventArgs e)
        {
            renderer.DisplayStatus("Client connected");
        }

        private void network_keyEventReceived(object sender, KeyboardEventArgs k)
        {
            C64.cia1.KeyEvent(k, true);
        }

        private void network_clientConnectFailed(object sender, Exception e)
        {
            renderer.DisplayStatus("Connection failed");
        }

        private void network_clientDisconnected(object sender, EventArgs e)
        {
            C64.HardReset();
            renderer.DisplayStatus("Disconnected");
        }

        private void network_clientConnected(object sender, EventArgs e)
        {
            C64.Stop();
            renderer.ResetBuffer();
            renderer.DisplayStatus("Connected to host");
        }

        private void network_screenReceived(object sender, EventArgs e)
        {
            if (!menu.Active)
                renderer.Draw();
        }

        private void processKey(KeyboardEventArgs k)
        {
            if (k.Key == Key.F9)
            {
                if (k.Down)
                    C64.FastForward();
                else
                    C64.RegularSpeed();
            }
            else if (k.Key == Key.F10)
            {
            }
            else if (k.Key == Key.F11)
            {
                if (!k.Down)
                {
                    C64.cia1.SwitchJoysticks();
                    renderer.DisplayStatus("Joysticks Switched");
                }
            }
            else if (k.Key == Key.F12)
            {
                if (!k.Down)
                {
                    if (menu.Active)
                        menu_menuReturn(null, null);
                    else
                    {
                        if (!network.IsClient)
                            C64.Pause();
                        renderer.ResetDrawState();
                        menu.Active = true;
                    }
                }
            }
            else if (menu.Active)
                menu.KeyEvent(k);
            else
                C64.cia1.KeyEvent(k, false);
        }

        private void Quit(object sender, QuitEventArgs e)
        {
            closeApp();
        }

        private void closeApp()
        {
            Settings.Default.Save();
            Joysticks.Close();
            Dispose();
            Events.QuitApplication();
        }

       #region IDisposable Members

       private bool disposed;

       /// <summary>
       /// Destroy object
       /// </summary>
       public void Dispose()
       {
           this.Dispose(true);
           GC.SuppressFinalize(this);
       }

       /// <summary>
       /// Destroy object
       /// </summary>
       public void Close()
       {
           Dispose();
       }

       /// <summary>
       /// Destroy object
       /// </summary>
       ~EmuWin()
       {
           Dispose(false);
       }
       /// <summary>
       ///
       /// </summary>
       /// <param name="disposing"></param>
       protected virtual void Dispose(bool disposing)
       {
           if (!this.disposed)
           {
               C64.Dispose();
               if (network.IsHosting) network.HostEnd();
               if (network.IsClient) network.ClientDisconnect();
               renderer.Kill();

               this.disposed = true;
           }
       }

       #endregion
    }
}
