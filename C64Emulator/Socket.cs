using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace C64Emulator
{
    public enum NetCommands : byte
    {
        None = 0,
        Image = 1,
        ImageReady = 2,
        Sound = 3,
        Chat = 4,
        KeyEvent = 5,
        JoyEvent = 6,
        Disconnect = 7
    }

    public delegate void IncomingConnection(object sender, EventArgs e);
    public delegate void ReceivedCommand(object sender, NetCommands cmd, byte[] data);
    public delegate void Connected(object sender, EventArgs e);
    public delegate void ConnectFailed(object sender, Exception e);
    public delegate void Disconnected(object sender, EventArgs e);
    public delegate void DataSent(object sender, EventArgs e);

    public class Socket
    {
        public event IncomingConnection incomingConnection;
        public event Connected connected;
        public event ConnectFailed connectFailed;
        public event DataSent dataSent;
        public event ReceivedCommand receivedCommand;
        public event Disconnected disconnected;

        private TcpListener server;
        private List<Client> clients;

        private bool listening = false;

        public Socket()
        {
            clients = new List<Client>();
        }

        public List<Client> Clients
        {
            get
            {
                return clients;
            }
            set
            {
                clients = value;
            }
        }

        public void Listen(int port)
        {
            DisconnectAll();

            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            listening = true;
            server.BeginAcceptTcpClient(new AsyncCallback(AcceptConnection), server);
        }

        public void StopListening()
        {
            listening = false;
            try
            {
                server.Stop();
            }
            catch { }
        }

        private void AcceptConnection(IAsyncResult iar)
        {
            if (!listening) return;
            TcpClient client = server.EndAcceptTcpClient(iar);

            Client c = new Client();
            c.TcpClient = client;

            c.Data.BeginRead(
                c.Buffer, 
                0, 
                c.Buffer.Length, 
                new AsyncCallback(ReceiveData), 
                c
                );

            clients.Add(c);

            incomingConnection(c, null);

            server.BeginAcceptTcpClient(new AsyncCallback(AcceptConnection), server);
        }

        public void DisconnectAll()
        {
            foreach (Client c in clients)
            {
                if (c.TcpClient.Connected)
                    c.TcpClient.Close();
            }
            
            clients.Clear();
        }

        private void ReceiveData(IAsyncResult iar)
        {
            Client c = (Client)iar.AsyncState;
            if (!clients.Contains(c) || !c.TcpClient.Connected) return;

            if (c.Data == null) return;
            int length = c.Data.EndRead(iar);
            c.Memory.Write(c.Buffer, 0, length);
            c.EmptyBuffer();

            if (c.CurrentCommand == NetCommands.Disconnect)
            {
                c.TcpClient.Close();
                clients.Remove(c);
                disconnected(c, null);
                return;
            }
            else
            {
                while (c.CurrentLength == 0 || c.CurrentData.Length > 0)
                {
                    receivedCommand(c, c.CurrentCommand, c.CurrentData);
                    c.EmptyMemory(c.CurrentLength + 5);
                }
            }

            if (c.Data != null)
                c.Data.BeginRead(
                    c.Buffer,
                    0,
                    c.Buffer.Length,
                    new AsyncCallback(ReceiveData),
                    c
                    );
        }

        public void Connect(string server, int port)
        {
            Client c = new Client();
            clients.Add(c);
            c.TcpClient.BeginConnect(server, port, new AsyncCallback(DoConnect), c);
        }

        private void DoConnect(IAsyncResult iar)
        {
            Client c = (Client)iar.AsyncState;

            try
            {
                c.TcpClient.EndConnect(iar);
            }
            catch (Exception ex)
            {
                clients.Remove(c);
                connectFailed(c, ex);
                return;
            }
           
            c.Data.BeginRead(
                c.Buffer,
                0,
                c.Buffer.Length,
                new AsyncCallback(ReceiveData),
                c
                );

            connected(c, null);
        }

        public void Send(Client c, byte[] data)
        {
            if (!clients.Contains(c) || !c.TcpClient.Connected) return;

            c.Data.BeginWrite(
                data,
                0,
                data.Length,
                new AsyncCallback(SendData),
                c
                );
        }

        public void SendToAll(byte[] data)
        {
            foreach (Client c in clients)
            {
                if (c.TcpClient.Connected)
                    c.Data.BeginWrite(
                        data,
                        0,
                        data.Length,
                        new AsyncCallback(SendData),
                        c
                        );
            }
        }

        private void SendData(IAsyncResult iar)
        {
            Client c = (Client)iar.AsyncState;
            if (!clients.Contains(c) || !c.TcpClient.Connected) return;

            if (c.Data != null)
            {
                c.Data.EndWrite(iar);

                dataSent(c, null);
            }
        }
    }

    public class Client
    {
        public const int BUFFER_SIZE = 5120;

        private TcpClient client;
        private byte[] buffer;
        private MemoryStream ms;

        public Client()
        {
            client = new TcpClient();
            buffer = new byte[BUFFER_SIZE];
            ms = new MemoryStream();
        }

        public TcpClient TcpClient
        {
            get
            {
                return client;
            }
            set
            {
                client = value;
            }
        }

        public byte[] Buffer
        {
            get
            {
                return buffer;
            }
            set
            {
                buffer = value;
            }
        }

        public NetworkStream Data
        {
            get
            {
                try
                {
                    return client.GetStream();
                }
                catch
                {
                    return null;
                }
            }
        }

        public MemoryStream Memory
        {
            get
            {
                return ms;
            }
        }

        public NetCommands CurrentCommand
        {
            get
            {
                if (ms.Length > 0)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    BinaryReader br = new BinaryReader(ms);
                    byte ret = br.ReadByte();
                    ms.Seek(0, SeekOrigin.End);

                    return (NetCommands)ret;
                }
                else
                    return NetCommands.None;
            }
        }

        public int CurrentLength
        {
            get
            {
                if (ms.Length > 4)
                {
                    ms.Seek(1, SeekOrigin.Begin);
                    BinaryReader br = new BinaryReader(ms);
                    int ret = br.ReadInt32();
                    ms.Seek(0, SeekOrigin.End);

                    return ret;
                }
                else
                    return -1;
            }
        }

        public byte[] CurrentData
        {
            get
            {

                if (CurrentLength > -1)
                {
                    int length = CurrentLength;

                    if (ms.Length > length + 4)
                    {
                        ms.Seek(5, SeekOrigin.Begin);
                        byte[] ret = new byte[length];
                        ms.Read(ret, 0, ret.Length);
                        ms.Seek(0, SeekOrigin.End);

                        return ret;
                    }
                    else
                        return new byte[0];
                }
                else
                    return new byte[0];
            }
        }

        public void EmptyBuffer()
        {
            buffer = new byte[BUFFER_SIZE];
        }

        public void EmptyMemory(int length)
        {
            MemoryStream newms = new MemoryStream();

            if (length > (int)ms.Length)
                length = (int)ms.Length;
            else if (length < (int)ms.Length)
                newms.Write(ms.ToArray(), length, (int)ms.Length - length);

            ms.Dispose();
            ms = newms;
        }
    }
}
