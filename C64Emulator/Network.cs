using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using SdlDotNet.Input;

namespace C64Emulator
{
    public class JoyEventArgs : EventArgs
    {
        int joyNum = 0;
        JoystickFunction j = JoystickFunction.Fire;
        bool pressed;

        public JoyEventArgs(int joyNum, JoystickFunction j, bool pressed)
        {
            this.joyNum = joyNum;
            this.j = j;
            this.pressed = pressed;
        }

        public int JoystickNumber
        {
            get
            {
                return joyNum;
            }
        }

        public JoystickFunction Function
        {
            get
            {
                return j;
            }
        }

        public bool Pressed
        {
            get
            {
                return pressed;
            }
        }
    }

    public delegate void ScreenReceived(object sender, EventArgs e);
    public delegate void KeyEventReceived(object sender, KeyboardEventArgs k);
    public delegate void JoyEventReceived(object sender, JoyEventArgs j);
    public delegate void ClientConnected(object sender, EventArgs e);
    public delegate void ClientConnectFailed(object sender, Exception e);
    public delegate void ClientDisconnected(object sender, EventArgs e);
    public delegate void IncomingClient(object sender, EventArgs e);
    public delegate void PartedClient(object sender, EventArgs e);

    class Network
    {
        public const int NETWORK_PORT = 6464;

        public event ScreenReceived screenReceived;
        public event KeyEventReceived keyEventReceived;
        public event JoyEventReceived joyEventReceived;
        public event ClientConnected clientConnected;
        public event ClientConnectFailed clientConnectFailed;
        public event ClientDisconnected clientDisconnected;
        public event IncomingClient incomingClient;
        public event PartedClient partedClient;

        private Socket socket;
        private bool hosting = false;
        private bool client = false;
        private bool imageReady = false;

        public Network()
        {
            socket = new Socket();
            socket.connected += new Connected(socket_Connected);
            socket.connectFailed += new ConnectFailed(socket_ConnectFailed);
            socket.dataSent += new DataSent(socket_DataSent);
            socket.incomingConnection += new IncomingConnection(socket_IncomingConnection);
            socket.receivedCommand += new ReceivedCommand(socket_ReceivedCommand);
            socket.disconnected += new Disconnected(socket_Disconnected);
        }

        public bool IsHosting
        {
            get
            {
                return hosting;
            }
        }

        public bool IsClient
        {
            get
            {
                return client;
            }
        }

        public bool ImageReady
        {
            get
            {
                return imageReady;
            }
        }

        public void HostBegin(int port)
        {
            if (client) return;

            socket.Listen(port);
            hosting = true;
        }

        public void HostEnd()
        {
            if (client) return;

            socket.StopListening();
            socket.SendToAll(new byte[] { (byte)NetCommands.Disconnect });
            socket.DisconnectAll();

            hosting = false;
        }

        public void Send(NetCommands cmd, byte[] data)
        {
            if (socket.Clients.Count == 0) return;

            if (hosting && cmd == NetCommands.Image)
                imageReady = false;

            MemoryStream packet = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(packet);

            bw.Write((byte)cmd);
            bw.Write(data.Length);
            
            if (data.Length > 0)
                bw.Write(data);

            bw.Flush();
            bw.Close();

            packet.Flush();

            socket.SendToAll(packet.ToArray());

            packet.Dispose();
        }

        public int HostNumClients
        {
            get
            {
                return socket.Clients.Count;
            }
        }

        public void ClientConnect(string server, int port)
        {
            if (hosting) return;

            client = true;

            socket.Connect(server, port);
        }

        public void ClientDisconnect()
        {
            if (hosting) return;

            socket.SendToAll(new byte[] { (byte)NetCommands.Disconnect });
            socket.DisconnectAll();

            client = false;
        }

        private void socket_IncomingConnection(object sender, EventArgs e)
        {
            incomingClient(sender, e);
        }

        private void socket_ReceivedCommand(object sender, NetCommands cmd, byte[] data)
        {
            processCommand(cmd, data);
        }

        private void socket_DataSent(object sender, EventArgs e)
        {
        }

        private void socket_Connected(object sender, EventArgs e)
        {
            if (client)
            {
                clientConnected(sender, e);
                socket.SendToAll(new byte[] { (byte)NetCommands.ImageReady, 0, 0, 0, 0 });
            }
        }

        private void socket_ConnectFailed(object sender, Exception e)
        {
            if (client)
            {
                clientConnectFailed(sender, e);
                client = false;
            }
        }

        private void socket_Disconnected(object sender, EventArgs e)
        {
            if (client)
            {
                clientDisconnected(sender, e);
                client = false;
            }
            else
                partedClient(sender, e);
        }

        private void processCommand(NetCommands cmd, byte[] data)
        {
            switch (cmd)
            {
                case NetCommands.Image:
                    int currentX = 0;
                    int currentY = 0;

                    for (int i = 0; i < data.Length; i++)
                    {
                        C64.vic.Screen[currentX, currentY] = C64.palette.MapColorValue((byte)(data[i] >> 4));

                        currentX++;
                        
                        if (currentX == 403)
                        {
                            currentX = 0;
                            currentY++;
                        }

                        C64.vic.Screen[currentX, currentY] = C64.palette.MapColorValue((byte)(data[i] & 0xF));

                        currentX++;

                        if (currentX == 403)
                        {
                            currentX = 0;
                            currentY++;
                        }
                    }

                    screenReceived(this, null);

                    socket.SendToAll(new byte[] { (byte)NetCommands.ImageReady, 0, 0, 0, 0 });

                    break;

                case NetCommands.ImageReady:
                    imageReady = true;
                    break;

                case NetCommands.KeyEvent:
                    KeyboardEventArgs kd =
                        new KeyboardEventArgs(
                            (Key)data[0],
                            (ModifierKeys)(data[1] != 0 ? Key.LeftShift : 0),
                            data[2] == 1
                            );
                    keyEventReceived(null, kd);
                    break;

                case NetCommands.JoyEvent:
                    JoyEventArgs jd =
                        new JoyEventArgs(
                            data[0],
                            (JoystickFunction)data[1],
                            data[2] == 1
                            );
                    joyEventReceived(null, jd);
                    break;
            }
        }
    }
}
