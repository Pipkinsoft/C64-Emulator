using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SdlDotNet;
using SdlDotNet.Core;
using SdlDotNet.Graphics;
using SdlDotNet.Graphics.Primitives;
using SdlDotNet.Graphics.Sprites;
using SdlDotNet.Input;
using SdlDotNet.Windows;
using System.Drawing;
using System.Timers;
using C64Emulator.Properties;

namespace C64Emulator
{
    class RendererSDL
    {
        private Surface screen;
        private bool isDrawing = false;
        private bool isSwitching = false;
        private short scale = 1;
        private short xPos = 0;
        private short yPos = 0;
        private Color[,] lastRender;
        private bool fullscreen = false;
        private System.Timers.Timer statusTimer;
		private System.Timers.Timer fpsTimer;
        private bool statusEnabled = false;
        private TextSprite statusLight;
        private TextSprite statusDark;
		private TextSprite fpsLight;
		private TextSprite fpsDark;
        private SdlDotNet.Graphics.Font statusFont;
		private SdlDotNet.Graphics.Font fpsFont;
        private SdlDotNet.Graphics.Font menuFont;
        private Color invalidateColor;
		private int screenWidth;
		private int screenHeight;
		private bool showFPS = false;
        private string errorMessage = string.Empty;

		private const int C64WIDTH = 403;
		private const int C64HEIGHT = 284;
		private const int C64BORDER = 42;
		
        public RendererSDL()
        {          
            statusTimer = new System.Timers.Timer(3000);
            statusTimer.Elapsed += new ElapsedEventHandler(statusTimer_Elapsed);
			
			fpsTimer = new System.Timers.Timer(1000);
			fpsTimer.Elapsed += new ElapsedEventHandler(fpsTimer_Elapsed);

            statusFont = new SdlDotNet.Graphics.Font(Resources.c64font, 30);
            fpsFont = new SdlDotNet.Graphics.Font(Resources.c64font, 16);
            menuFont = new SdlDotNet.Graphics.Font(Resources.c64font, 24);

            statusLight = getTextSprite(statusFont, Color.LightGreen);
            statusDark = getTextSprite(statusFont, Color.DarkGreen);
			fpsLight = getTextSprite(fpsFont, Color.White);
			fpsDark = getTextSprite(fpsFont, Color.Black);

            lastRender = new Color[C64WIDTH, C64HEIGHT];

            invalidateColor = Color.FromArgb(1, 1, 1);

            DisplayFPS = Settings.Default.ShowSpeed;

            Reset(
                Settings.Default.Fullscreen, 
                Settings.Default.ResolutionWidth, 
                Settings.Default.ResolutionHeight
                );
        }

		private TextSprite getTextSprite(SdlDotNet.Graphics.Font font, Color color)
		{
            TextSprite ret = new TextSprite(
                " ",
                font,
                color);
			ret.AntiAlias = false;
			
			return ret;
		}
		
        private void statusTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            statusTimer.Stop();

            statusEnabled = false;
            resetLastBuffer();
        }

        private void fpsTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
            if (C64.cpu != null)
            {
                fpsLight.Text = "CPU Speed: " + C64.cpu.SpeedPercentage.ToString() + "%";
                fpsDark.Text = "CPU Speed: " + C64.cpu.SpeedPercentage.ToString() + "%";
                resetLastBuffer();
            }
		}

        public void DisplayStatus(string message)
        {
            statusLight.Text = statusDark.Text = message;

            if (statusEnabled)
            {
                statusTimer.Stop();
                resetLastBuffer();
                statusTimer.Start();
            }
            else
            {
                statusEnabled = true;
                statusTimer.Start();
            }
        }

        public void ResetBuffer()
        {
            resetLastBuffer();
        }

        private void resetLastBuffer()
        {
            for (int y = 0; y < lastRender.GetLength(1); y++)
                for (int x = 0; x < lastRender.GetLength(0); x++)
                    lastRender[x, y] = invalidateColor;
        }

        public bool Fullscreen
        {
            get
            {
                return fullscreen;
            }
        }

        public void Reset(bool fullscreen, int width, int height)
        {
            isSwitching = true;

            this.fullscreen = fullscreen;

            resetLastBuffer();

			screenWidth = width;
			screenHeight = height;
            Mouse.ShowCursor = !fullscreen;
			
			screen = 
					Video.SetVideoMode(
					                   screenWidth,
					                   screenHeight, 
					                   false, 
					                   false, 
					                   fullscreen, 
					                   true, 
					                   !fullscreen
					                   );

            Settings.Default.Fullscreen = fullscreen;
            Settings.Default.ResolutionWidth = width;
            Settings.Default.ResolutionHeight = height;

            scale = (short)(Math.Ceiling((float)screenWidth / (float)C64WIDTH));

			if (
			    C64WIDTH * scale - screenWidth > C64BORDER * scale * 2 ||
			    C64HEIGHT * scale - screenHeight > C64BORDER * scale * 2
			    )
				scale--;
			
            xPos = (short)((screenWidth - C64WIDTH * scale) / 2);
            yPos = (short)((screenHeight - C64HEIGHT * scale) / 2);

            statusFont.Dispose();
            statusFont = new SdlDotNet.Graphics.Font(Resources.c64font, 15 * scale);
			
			fpsFont.Dispose();
            fpsFont = new SdlDotNet.Graphics.Font(Resources.c64font, 8 * scale);

            menuFont.Dispose();
            menuFont = new SdlDotNet.Graphics.Font(Resources.c64font, 12 * scale);

            statusLight.Font = statusFont;
            statusDark.Font = statusFont;
			
			fpsLight.Font = fpsFont;
			fpsDark.Font = fpsFont;

            isSwitching = false;
        }

        public void Kill()
        {
            isDrawing = true;
			fpsTimer.Stop();
			statusTimer.Stop();

            statusLight.Dispose();
            statusDark.Dispose();
            statusFont.Dispose();
			
			fpsLight.Dispose();
			fpsDark.Dispose();
			fpsFont.Dispose();
        }

        public void ResetDrawState()
        {
            isDrawing = false;
        }

		public bool DisplayFPS
		{
            get
            {
                return showFPS;
            }
            set
            {
                if (value == showFPS) return;

                Settings.Default.ShowSpeed = value;

                resetLastBuffer();
                fpsTimer_Elapsed(null, null);
                showFPS = value;
                fpsTimer.Enabled = showFPS;
            }
		}

        public void DisplayError(string error)
        {
            DrawError(error);
        }
		
        public void Draw()
        {
            if (isDrawing || isSwitching)
                return;

            isDrawing = true;
            
            //try
            //{
                for (short y = 0; y < C64HEIGHT; y++)
                    for (short x = 0; x < C64WIDTH; x++)
                    {
                        if (
                            (xPos < 0 &&
                                (x * scale < Math.Abs(xPos) ||
                                x * scale > C64WIDTH * scale + xPos)) ||
                            (yPos < 0 &&
                                (y * scale < Math.Abs(yPos) ||
                                y * scale > C64HEIGHT * scale + yPos))
                            )
                            continue;

                        if (lastRender[x, y] != C64.vic.Screen[x, y])
                        {
                            lastRender[x, y] = C64.vic.Screen[x, y];

                            screen.Draw(
                                new Box(
                                    (short)(xPos + x * scale),
                                    (short)(yPos + y * scale),
                                    (short)(xPos + x * scale + scale - 1),
                                    (short)(yPos + y * scale + scale - 1)
                                    ),
                                C64.vic.Screen[x, y],
                                false,
                                true
                                );
                        }
                    }
               
				if (showFPS)
				{
					int fontX = xPos + C64WIDTH * scale - 100 * scale;
					int fontY = yPos + C64HEIGHT * scale - fpsFont.Height - 10;

                    if (xPos < 0) fontX = screenWidth - 100 * scale;
					if (yPos < 0) fontY = screenHeight - fpsFont.Height - 10;

					screen.Blit(fpsLight, new Point(fontX, fontY));
					screen.Blit(fpsDark, new Point(fontX + 1, fontY + 1));
				}

                if (statusEnabled)
                {
                    int fontX = xPos + 10;
                    int fontY = yPos + C64HEIGHT * scale - statusFont.Height - 10;

                    if (xPos < 0) fontX = 10;
                    if (yPos < 0) fontY = screenHeight - statusFont.Height - 10;

                    screen.Blit(statusDark, new Point(fontX, fontY));
                    screen.Blit(statusLight, new Point(fontX + 1, fontY + 1));
                }

                screen.Update();
            //}
            //catch {}

            isDrawing = false;
        }

        public void DrawError(string error)
        {
            try
            {
                Color foreColor = Color.FromArgb(117, 161, 236);
                Color backColor = Color.FromArgb(65, 55, 205);

                screen.Draw(
                    new Box(
                        (short)xPos,
                        (short)yPos,
                        (short)(xPos + C64WIDTH * scale - 1),
                        (short)(yPos + C64BORDER * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );
                screen.Draw(
                    new Box(
                        (short)xPos,
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale),
                        (short)(xPos + C64WIDTH * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );
                screen.Draw(
                    new Box(
                        (short)xPos,
                        (short)(yPos + C64BORDER * scale),
                        (short)(xPos + C64BORDER * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );
                screen.Draw(
                    new Box(
                        (short)(xPos + C64WIDTH * scale - (C64BORDER - 1) * scale),
                        (short)(yPos + C64BORDER * scale),
                        (short)(xPos + C64WIDTH * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );

                screen.Draw(
                    new Box(
                        (short)(xPos + C64BORDER * scale),
                        (short)(yPos + C64BORDER * scale),
                        (short)(xPos + C64WIDTH * scale - (C64BORDER - 1) * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale - 1)
                        ),
                    backColor,
                    false,
                    true
                    );

                string[] lines = new string[error.Length / 33 + 2];

                lines[0] = "Error:";
                for (int i = 1; i < lines.Length - 1; i++)
                {
                    lines[i] = error.Substring(0, 33);
                    error = error.Substring(33);
                }
                lines[lines.Length - 1] = error;

                SpriteCollection errorLines = new SpriteCollection();

                for (int i = 0; i < lines.Length; i++)
                {
                    TextSprite line =
                        new TextSprite(
                            lines[i],
                            menuFont,
                            foreColor,
                            new Point(
                                xPos + C64BORDER * scale + 10 * scale,
                                yPos + C64BORDER * scale + 10 * scale + i * (menuFont.Height + 5 * scale)
                                )
                            );

                    errorLines.Add(line);
                }

                screen.Blit(errorLines);

                screen.Update();
            }
            catch { }
        }

        public void DrawMenu(List<MenuList> menu, int selectedItem, int currentPosition)
        {
            if (isDrawing || isSwitching)
                return;

            isDrawing = true;

            try
            {
                Color foreColor = C64.palette.MapColorValue(14);
                Color backColor = C64.palette.MapColorValue(6);

                screen.Draw(
                    new Box(
                        (short)xPos,
                        (short)yPos,
                        (short)(xPos + C64WIDTH * scale - 1),
                        (short)(yPos + C64BORDER * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );
                screen.Draw(
                    new Box(
                        (short)xPos,
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale),
                        (short)(xPos + C64WIDTH * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );
                screen.Draw(
                    new Box(
                        (short)xPos,
                        (short)(yPos + C64BORDER * scale),
                        (short)(xPos + C64BORDER * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );
                screen.Draw(
                    new Box(
                        (short)(xPos + C64WIDTH * scale - (C64BORDER - 1) * scale),
                        (short)(yPos + C64BORDER * scale),
                        (short)(xPos + C64WIDTH * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale - 1)
                        ),
                    foreColor,
                    false,
                    true
                    );

                screen.Draw(
                    new Box(
                        (short)(xPos + C64BORDER * scale),
                        (short)(yPos + C64BORDER * scale),
                        (short)(xPos + C64WIDTH * scale - (C64BORDER - 1) * scale - 1),
                        (short)(yPos + C64HEIGHT * scale - C64BORDER * scale - 1)
                        ),
                    backColor,
                    false,
                    true
                    );

                SpriteCollection menuItems = new SpriteCollection();

                for (int i = currentPosition; i < currentPosition + Menu.MENUMAXITEMS && i < menu.Count; i++)
                {
                    TextSprite item =
                        new TextSprite(
                            menu[i].Text,
                            menuFont,
                            (selectedItem == i ? backColor : foreColor),
                            new Point(
                                xPos + C64BORDER * scale + 10 * scale + menu[i].Level * 20 * scale,
                                yPos + C64BORDER * scale + 10 * scale + (i - currentPosition) * (menuFont.Height + 10 * scale)
                                )
                            );

                    if (selectedItem == i)
                        item.BackgroundColor = foreColor;

                    menuItems.Add(item);
                }

                screen.Blit(menuItems);

                screen.Update();
            }
            catch { }

            isDrawing = false;
        }
    }
}
