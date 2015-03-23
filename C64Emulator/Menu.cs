using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Timers;
using System.IO;
using System.Reflection;
using SdlDotNet;
using SdlDotNet.Graphics;
using SdlDotNet.Input;
using C64Emulator.Properties;

namespace C64Emulator
{
    public class MenuList
    {
        private string text;
        private string command;
        private string[] args;
        private int level;

        public MenuList(string text, string command, string[] args, int level)
        {
            this.text = text;
            this.command = command;
            this.args = args;
            this.level = level;
        }

        public string Text
        {
            get { return text; }
            set { text = value; }
        }

        public string Command
        {
            get { return command; }
            set { command = value; }
        }

        public string[] Args
        {
            get { return args; }
            set { args = value; }
        }

        public int Level
        {
            get { return level; }
            set { level = value; }
        }
    }

    public delegate void MenuReturn(object sender, EventArgs e);
    public delegate void MenuQuit(object sender, EventArgs e);

    class Menu
    {
        public const int MENUMAXITEMS = 9;

        private List<MenuList> menuList;
        private RendererSDL renderer;
        private Network network;
        private bool active = false;
        private int selectedItem = 0;
        private Timer drawTimer;
        private int currentPosition = 0;
        private string networkAddress = string.Empty;
        private bool settingKeyset = false;
        private string keysetText = string.Empty;

        public event MenuReturn menuReturn;
        public event MenuQuit menuQuit;

        public Menu(RendererSDL renderer, Network network)
        {
            menuList = new List<MenuList>();
            
            this.renderer = renderer;
            this.network = network;

            networkAddress = Settings.Default.LastHost;

            drawTimer = new Timer(20);
            drawTimer.Elapsed += new ElapsedEventHandler(drawTimer_Elapsed);
        }

        void drawTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            renderer.DrawMenu(menuList, selectedItem, currentPosition);
        }

        private void loadMainMenu()
        {
            menuList.Clear();
            selectedItem = 0;
            currentPosition = 0;

            menuList.Add(new MenuList("Display", "display", null, 0));
            menuList.Add(new MenuList("Input", "input", null, 0));
            menuList.Add(new MenuList("Network", "network", null, 0));
            menuList.Add(new MenuList("Load Program/Disk", "load", null, 0));
            menuList.Add(new MenuList("Soft Reset", "softreset", null, 0));
            menuList.Add(new MenuList("Hard Reset", "hardreset", null, 0));
            menuList.Add(new MenuList("Exit Menu", "return", null, 0));
            menuList.Add(new MenuList("Quit", "quit", null, 0));
        }

        private void loadDisplayMenu()
        {
            menuList.Clear();
            selectedItem = 0;
            currentPosition = 0;

            menuList.Add(new MenuList("Back to Main Menu", "main", null, 0));
            menuList.Add(new MenuList("Display FPS: " + (renderer.DisplayFPS ? "Yes" : "No"), "fps", null, 0));
            menuList.Add(new MenuList("Display Mode:", string.Empty, null, 0));

            menuList.Add(
                new MenuList(
                    "Windowed (403 x 284)",
                    "resolution",
                    new string[] { "window", "403", "284" },
                    1)
                );
            menuList.Add(
                new MenuList(
                    "Windowed (806 x 568)",
                    "resolution",
                    new string[] { "window", "806", "568" },
                    1)
                );

            Size[] modes = Video.ListModes();
            for (int i = 0; i < modes.Length; i++)
                menuList.Add(
                    new MenuList(
                        "Fullscreen (" + 
                        modes[i].Width.ToString() + " x " +
                        modes[i].Height.ToString() + ")", 
                        "resolution",
                        new string[] { "fullscreen", modes[i].Width.ToString(), modes[i].Height.ToString() },
                        1)
                    );
        }

        private void loadInputMenu()
        {
            menuList.Clear();
            selectedItem = 0;
            currentPosition = 0;

            menuList.Add(new MenuList("Back to Main Menu", "main", null, 0));
            menuList.Add(new MenuList("Joystick 1 Map: " + C64.cia1.Joy1Map.ToString(), "joy1map", null, 0));
            menuList.Add(new MenuList("Joystick 2 Map: " + C64.cia1.Joy2Map.ToString(), "joy2map", null, 0));
            menuList.Add(new MenuList("Edit KeySet1", "editkey1", null, 0));
            menuList.Add(new MenuList("Edit KeySet2", "editkey2", null, 0));
        }

        private void loadNetworkMenu()
        {
            menuList.Clear();
            selectedItem = 0;
            currentPosition = 0;

            menuList.Add(new MenuList("Back to Main Menu", "main", null, 0));
            if (network.IsHosting)
                menuList.Add(new MenuList("Stop Hosting", "stophost", null, 0));
            else
                menuList.Add(new MenuList("Start Hosting", "starthost", null, 0));
            if (network.IsClient)
                menuList.Add(new MenuList("Disconnect from Host", "disconnect", null, 0));
            else
                menuList.Add(new MenuList("Connect to Host: " + networkAddress, "connect", new string[] { networkAddress }, 0));
        }

        private void loadFileMenu(string directory)
        {
            menuList.Clear();
            selectedItem = 0;
            currentPosition = 0;

            menuList.Add(new MenuList("Back to Main Menu", "main", null, 0));
            
            DirectoryInfo di = new DirectoryInfo(directory);
            
            if (di.Parent != null)
                menuList.Add(
                    new MenuList(
                        "..", 
                        "directory", 
                        new string[] { di.Parent.FullName }, 
                        0)
                    );

            foreach (string d in Directory.GetDirectories(directory))
            {
                menuList.Add(
                    new MenuList(
                        d.Substring(d.LastIndexOf(Path.DirectorySeparatorChar) + 1) + 
                        Path.DirectorySeparatorChar.ToString(), 
                        "directory", 
                        new string[] { d },
                        0
                        )
                    );
            }

            foreach (string f in Directory.GetFiles(directory))
            {
                string ext = Path.GetExtension(f).ToLower();

                if (ext == ".prg")
                {
                    menuList.Add(
                        new MenuList(
                            Path.GetFileName(f),
                            "file",
                            new string[] { "prg", f },
                            0
                            )
                        );
                }
            }
        }

        private void loadKeySetMenu(int keyset)
        {
            menuList.Clear();
            selectedItem = 0;
            currentPosition = 0;

            menuList.Add(new MenuList("Back to Input Menu", "input", null, 0));
            menuList.Add(
                new MenuList(
                    "KeySet" + keyset.ToString() + " Up: " + 
                    (keyset == 1 ? C64.cia1.KeySet1.Up.ToString() : C64.cia1.KeySet2.Up.ToString()), 
                    "keyset" + keyset.ToString(), 
                    new string[] { "up" }, 
                    0
                    )
                );
            menuList.Add(
                new MenuList(
                    "KeySet" + keyset.ToString() + " Right: " +
                    (keyset == 1 ? C64.cia1.KeySet1.Right.ToString() : C64.cia1.KeySet2.Right.ToString()),
                    "keyset" + keyset.ToString(),
                    new string[] { "right" },
                    0
                    )
                );
            menuList.Add(
                new MenuList(
                    "KeySet" + keyset.ToString() + " Down: " +
                    (keyset == 1 ? C64.cia1.KeySet1.Down.ToString() : C64.cia1.KeySet2.Down.ToString()),
                    "keyset" + keyset.ToString(),
                    new string[] { "down" },
                    0
                    )
                );
            menuList.Add(
                new MenuList(
                    "KeySet" + keyset.ToString() + " Left: " +
                    (keyset == 1 ? C64.cia1.KeySet1.Left.ToString() : C64.cia1.KeySet2.Left.ToString()),
                    "keyset" + keyset.ToString(),
                    new string[] { "left" },
                    0
                    )
                );
            menuList.Add(
                new MenuList(
                    "KeySet" + keyset.ToString() + " Fire: " +
                    (keyset == 1 ? C64.cia1.KeySet1.Fire.ToString() : C64.cia1.KeySet2.Fire.ToString()),
                    "keyset" + keyset.ToString(),
                    new string[] { "fire" },
                    0
                    )
                );
        }

        public bool Active
        {
            get
            {
                return active;
            }
            set
            {
                if (active == value) return;

                active = value;

                if (active)
                {
                    keysetText = string.Empty;
                    settingKeyset = false;
                    loadMainMenu();
                    drawTimer.Start();
                }
                else
                {
                    drawTimer.Stop();
                }
            }
        }

        private void processCommand(string command, string[] args)
        {
            switch (command)
            {
                case "display":
                    loadDisplayMenu();
                    break;

                case "input":
                    loadInputMenu();
                    break;

                case "network":
                    loadNetworkMenu();
                    break;

                case "load":
                    string appDir =
                        Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
                    loadFileMenu(appDir);
                    break;

                case "softreset":
                    menuReturn(this, null);
                    C64.SoftReset();
                    break;

                case "hardreset":
                    menuReturn(this, null);
                    C64.HardReset();
                    break;

                case "return":
                    menuReturn(this, null);
                    break;

                case "quit":
                    menuQuit(this, null);
                    break;

                case "main":
                    loadMainMenu();
                    break;

                case "fps.left":
                case "fps.right":
                    renderer.DisplayFPS = !renderer.DisplayFPS;
                    menuList[1].Text = "Display FPS: " + (renderer.DisplayFPS ? "Yes" : "No");
                    break;

                case "resolution":
                    renderer.Reset(
                        args[0] == "fullscreen",
                        int.Parse(args[1]),
                        int.Parse(args[2])
                        );
                    break;

                case "starthost":
                    menuReturn(this, null);
                    if (network.IsClient)
                        network.ClientDisconnect();
                    network.HostBegin(Network.NETWORK_PORT);
                    renderer.DisplayStatus("Started hosting");
                    break;

                case "stophost":
                    menuReturn(this, null);
                    network.HostEnd();
                    renderer.DisplayStatus("Stopped hosting");
                    break;

                case "connect":
                    menuReturn(this, null);
                    Settings.Default.LastHost = args[0];
                    if (network.IsHosting)
                        network.HostEnd();
                    network.ClientConnect(args[0], Network.NETWORK_PORT);
                    renderer.DisplayStatus("Trying to connect to host...");
                    break;

                case "disconnect":
                    menuReturn(this, null);
                    network.ClientDisconnect();
                    break;

                case "directory":
                    loadFileMenu(args[0]);
                    break;

                case "file":
                    if (args[0] == "prg")
                    {
                        C64.LoadProgram(args[1]);
                        menuReturn(this, null);
                        renderer.DisplayStatus("Program loaded into memory");
                    }
                    break;

                case "joy1map.left":
                    int newMap = (int)C64.cia1.Joy1Map - 1;
                    if (newMap == -1) newMap = 4;
                    C64.cia1.Joy1Map = (MapType)newMap;
                    menuList[1].Text = "Joystick 1 Map: " + C64.cia1.Joy1Map.ToString();
                    break;

                case "joy1map.right":
                    newMap = (int)C64.cia1.Joy1Map + 1;
                    if (newMap == 5) newMap = 0;
                    C64.cia1.Joy1Map = (MapType)newMap;
                    menuList[1].Text = "Joystick 1 Map: " + C64.cia1.Joy1Map.ToString();
                    break;

                case "joy2map.left":
                    newMap = (int)C64.cia1.Joy2Map - 1;
                    if (newMap == -1) newMap = 4;
                    C64.cia1.Joy2Map = (MapType)newMap;
                    menuList[2].Text = "Joystick 2 Map: " + C64.cia1.Joy2Map.ToString();
                    break;

                case "joy2map.right":
                    newMap = (int)C64.cia1.Joy2Map + 1;
                    if (newMap == 5) newMap = 0;
                    C64.cia1.Joy2Map = (MapType)newMap;
                    menuList[2].Text = "Joystick 2 Map: " + C64.cia1.Joy2Map.ToString();
                    break;

                case "editkey1":
                    loadKeySetMenu(1);
                    break;

                case "editkey2":
                    loadKeySetMenu(2);
                    break;
            }
        }

        public void KeyEvent(KeyboardEventArgs k)
        {
            if (!k.Down)
            {
                if (settingKeyset)
                {
                    if (k.Key == Key.Escape)
                    {
                        settingKeyset = false;
                        menuList[selectedItem].Text = keysetText;
                    }
                    else if (
                        k.Key != Key.CapsLock && k.Key != Key.NumLock &&
                        k.Key != Key.LeftWindows && k.Key != Key.RightWindows &&
                        k.Key != Key.LeftMeta && k.Key != Key.RightMeta
                        )
                    {
                        C64.cia1.SetKeySet(
                            (menuList[selectedItem].Command == "keyset1" ? 1 : 2),
                            menuList[selectedItem].Args[0],
                            k.Key
                            );

                        string txt = menuList[selectedItem].Text;
                        settingKeyset = false;
                        menuList[selectedItem].Text =
                            txt.Substring(0, txt.IndexOf(':') + 2) +
                            k.Key.ToString();
                    }
                }
                else
                {
                    switch (k.Key)
                    {
                        case Key.DownArrow:
                            if (selectedItem < menuList.Count - 1)
                                selectedItem++;
                            if (selectedItem == currentPosition + MENUMAXITEMS)
                                currentPosition++;
                            break;

                        case Key.UpArrow:
                            if (selectedItem > 0)
                                selectedItem--;
                            if (selectedItem < currentPosition)
                                currentPosition--;
                            break;

                        case Key.PageDown:
                            if (selectedItem + MENUMAXITEMS > menuList.Count - 1)
                                selectedItem = menuList.Count - 1;
                            else
                                selectedItem = selectedItem + MENUMAXITEMS;
                            if (selectedItem >= currentPosition + MENUMAXITEMS)
                                currentPosition += MENUMAXITEMS;
                            if (currentPosition > menuList.Count - MENUMAXITEMS)
                                currentPosition = menuList.Count - MENUMAXITEMS;
                            break;

                        case Key.PageUp:
                            if (selectedItem - MENUMAXITEMS < 0)
                                selectedItem = 0;
                            else
                                selectedItem = selectedItem - MENUMAXITEMS;
                            if (selectedItem < currentPosition)
                                currentPosition -= MENUMAXITEMS;
                            if (currentPosition < 0)
                                currentPosition = 0;
                            break;

                        case Key.Return:
                            if (menuList[selectedItem].Command.StartsWith("keyset"))
                            {
                                string txt = menuList[selectedItem].Text;
                                keysetText = txt;
                                menuList[selectedItem].Text =
                                    txt.Substring(0, txt.IndexOf(':') + 2) +
                                    "Press a key";
                                settingKeyset = true;
                            }
                            else
                                processCommand(
                                    menuList[selectedItem].Command,
                                    menuList[selectedItem].Args
                                    );
                            break;

                        case Key.LeftArrow:
                            processCommand(
                                menuList[selectedItem].Command + ".left",
                                menuList[selectedItem].Args
                                );
                            break;

                        case Key.RightArrow:
                            processCommand(
                                menuList[selectedItem].Command + ".right",
                                menuList[selectedItem].Args
                                );
                            break;

                        case Key.Escape:
                            menuReturn(this, null);
                            break;

                        case Key.Backspace:
                            if (menuList[selectedItem].Command == "connect" &&
                                menuList[selectedItem].Args[0].Length > 0)
                            {
                                menuList[selectedItem].Args[0] =
                                    menuList[selectedItem].Args[0].Substring(
                                        0, menuList[selectedItem].Args[0].Length - 1
                                        );

                                menuList[selectedItem].Text =
                                    "Connect to Host: " +
                                    menuList[selectedItem].Args[0];
                            }
                            break;

                        default:
                            if (menuList[selectedItem].Command == "connect" &&
                                k.KeyboardCharacter.Length == 1)
                            {
                                char chr = char.Parse(k.KeyboardCharacter);

                                if (chr >= '\x21' && chr <= '\x7A')
                                {
                                    menuList[selectedItem].Args[0] += chr.ToString();

                                    menuList[selectedItem].Text =
                                        "Connect to Host: " +
                                        menuList[selectedItem].Args[0];
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}
