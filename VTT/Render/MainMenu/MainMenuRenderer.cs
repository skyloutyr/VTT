namespace VTT.Render.MainMenu
{
    using ImGuiNET;
    using OpenTK.Windowing.Common;
    using OpenTK.Windowing.GraphicsLibraryFramework;
    using SixLabors.ImageSharp;
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Net;
    using System.Numerics;
    using VTT.Network;
    using VTT.Util;
    using VTT.Network.Packet;
    using System.IO;
    using VTT.GL;
    using MathHelper = OpenTK.Mathematics.MathHelper;

    public class MainMenuRenderer
    {
        public Texture AlphaHardhat { get; set; }
        public Texture AlphaLetters { get; set; }
        public Texture AlphaMascot { get; set; }
        public Texture AlphaSparks { get; set; }
        public Texture LogoMain { get; set; }
        public Texture LogoNulEng { get; set; }
        public Texture LogoAlpha { get; set; }
        public Texture AlphaHardhatBroken { get; set; }
        public Texture AlphaThunk { get; set; }

        public Texture BetaLetters { get; set; }
        public Texture BetaLettersOff { get; set; }
        public Texture BetaLight { get; set; }
        public Texture BetaMascot { get; set; }
        public Texture BetaSwitch { get; set; }
        public Texture BetaSwitchOff { get; set; }
        public Texture BetaWires { get; set; }

        public int MenuMode { get; set; }

        private string _connectAddress = string.Empty;
        private string _connectPort = string.Empty;
        private string _hostPort = string.Empty;
        private bool _betaSwitchOff;

        public void Create()
        {
            this.AlphaHardhat = OpenGLUtil.LoadUIImage("Logo.alpha-hardhat");
            this.AlphaLetters = OpenGLUtil.LoadUIImage("Logo.alpha-letters");
            this.AlphaMascot = OpenGLUtil.LoadUIImage("Logo.alpha-mascot");
            this.AlphaSparks = OpenGLUtil.LoadUIImage("Logo.alpha-sparks");
            this.LogoMain = OpenGLUtil.LoadUIImage("Logo.logo-main");
            this.LogoNulEng = OpenGLUtil.LoadUIImage("Logo.logo-nuleng-anim");
            this.LogoAlpha = OpenGLUtil.LoadUIImage("Logo.logo-alpha");
            this.AlphaHardhatBroken = OpenGLUtil.LoadUIImage("Logo.alpha-hardhat-broken");
            this.AlphaThunk = OpenGLUtil.LoadUIImage("Logo.alpha-thunk");

            this.BetaLetters = OpenGLUtil.LoadUIImage("Logo.beta-letters");
            this.BetaLettersOff = OpenGLUtil.LoadUIImage("Logo.beta-letters-off");
            this.BetaLight = OpenGLUtil.LoadUIImage("Logo.beta-light");
            this.BetaMascot = OpenGLUtil.LoadUIImage("Logo.beta-mascot");
            this.BetaSwitch = OpenGLUtil.LoadUIImage("Logo.beta-switch");
            this.BetaSwitchOff = OpenGLUtil.LoadUIImage("Logo.beta-switch-off");
            this.BetaWires = OpenGLUtil.LoadUIImage("Logo.beta-wires");

            this._connectAddress = Client.Instance.Settings.LastConnectIPAddress;
            this._connectPort = Client.Instance.Settings.LastConnectPort;
        }

        public void Render(ref bool showDC, double delta)
        {
            SimpleLanguage lang = Client.Instance.Lang;
            ImGui.SetNextWindowSize(new(ImGui.GetIO().DisplaySize.X, ImGui.GetIO().DisplaySize.Y));
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            if (ImGui.Begin("Main Menu", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar))
            {
                float width = ImGui.GetIO().DisplaySize.X;
                float height = ImGui.GetIO().DisplaySize.Y;

                ImGui.SetCursorPosX(8);
                ImGui.SetCursorPosY(height - 131 - 8);
                int frame = this._currentNulEngAnimFrame;
                int numFrames = 10;
                float vStart = (float)frame / numFrames;
                float vEnd = vStart + 0.1f;
                ImGui.Image(this.LogoNulEng, new Vector2(198, 131), new Vector2(0, vStart), new Vector2(1, vEnd));

                ImGui.SetCursorPos(new Vector2((width / 2) - 320, 0));
                ImGui.Image(this.LogoMain, new Vector2(640, 240));

                #region Beta
                ImGui.SetCursorPos(new Vector2((width / 2) - 320 + 376, 123));
                ImGui.Image(this.BetaWires, new Vector2(264, 55));

                ImGui.SetCursorPos(new Vector2((width / 2) - 320 + 307, 0));
                ImGui.Image(this._betaSwitchOff ? this.BetaLettersOff : this.BetaLetters, new Vector2(263, 165));

                ImGui.SetCursorPos(new Vector2(width - 49, 72));
                ImGui.Image(this.BetaMascot, new Vector2(49, 59));

                ImGui.SetCursorPos(new Vector2((width / 2) - 320 + 624, 159));
                if (ImGui.ImageButton(this._betaSwitchOff ? this.BetaSwitchOff : this.BetaSwitch, new Vector2(14, 19), Vector2.Zero, Vector2.One, 0, Vector4.Zero))
                {
                    this._betaSwitchOff = !this._betaSwitchOff;
                }

                if (!this._betaSwitchOff)
                {
                    ImGui.SetCursorPos(new Vector2((width / 2) - 320, 0));
                    ImGui.Image(this.BetaLight, new Vector2(276, 168));
                    ImGui.SetCursorPos(new Vector2((width / 2) - 320 + 320, 0));
                    ImGui.Image(this.BetaLight, new Vector2(276, 168));
                }

                #endregion

                #region Alpha
                /*
                ImGui.SetCursorPos(new System.Numerics.Vector2((width / 2) - 320, 0));
                ImGui.Image(this.LogoAlpha, new System.Numerics.Vector2(640, 240));

                if (!this._hardhatBroken)
                {
                    ImGui.SetCursorPos(new System.Numerics.Vector2((width / 2) - 320 + 354, 87));
                    ImGui.Image(this.AlphaHardhat, new System.Numerics.Vector2(144, 144));
                }

                float vMod = this._hardhatBroken ? 0.66666666f : this._mascotThunk % 90 < 20 ? 0.33333333f : 0;
                vStart = 0 + vMod;
                vEnd = 0.33333333f + vMod;

                ImGui.SetCursorPos(new System.Numerics.Vector2((width / 2) - 320 + 490, 175));
                ImGui.Image(this.AlphaMascot, new System.Numerics.Vector2(49, 64), new System.Numerics.Vector2(0, vStart), new System.Numerics.Vector2(1, vEnd));

                this._hardhatParticle?.Draw();
                foreach (SparkParticle spark in this._sparks)
                {
                    spark.Draw();
                }

                foreach (ThunkParticle thunk in this._thunks)
                {
                    thunk.Draw();
                }
                */
                #endregion


                string copyright = "SkyLouTyr MIT © 2022";
                Vector2 cLen = ImGui.CalcTextSize(copyright);
                ImGui.SetCursorPos(new Vector2(width, height) - new Vector2(8, 8) - cLen);
                ImGui.Text(copyright);

                if (Client.Instance.Frontend.UpdaterExitCode == 1)
                {
                    ImGui.SetCursorPos(new Vector2(16, 32));
                    if (ImGui.Button(lang.Translate("ui.button.update")))
                    {
                        string updater = Path.Combine(IOVTT.AppDir, "VTTUpdater.exe");
                        if (File.Exists(updater))
                        {
                            Client.Instance.Frontend.GameHandle.Close();
                            System.Diagnostics.Process updaterProcess = new System.Diagnostics.Process();
                            updaterProcess.StartInfo.FileName = updater;
                            updaterProcess.Start();
                        }

                        Environment.Exit(0);
                    }
                }

                ImGui.SetCursorPos(new Vector2((width / 2) - 128, 300));
                ImGui.BeginChild("Main Menu Entry", new Vector2(256, 158), true, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration);
                if (ImGui.Button(lang.Translate("menu.join") + "###Join", new Vector2(240, 32)))
                {
                    this.MenuMode = 1;
                }

                if (ImGui.Button(lang.Translate("menu.host") + "###Host", new Vector2(240, 32)))
                {
                    this.MenuMode = 2;
                }

                if (ImGui.Button(lang.Translate("menu.settings") + "###Settings", new Vector2(240, 32)))
                {
                    this.MenuMode = 3;
                }

                if (ImGui.Button(lang.Translate("menu.quit") + "###Quit", new Vector2(240, 32)))
                {
                    Client.Instance.Frontend.GameHandle.Close();
                }

                ImGui.EndChild();

                if (this.MenuMode == 1)
                {
                    ImGui.SetCursorPos(new Vector2((width / 2) - 128, 500));
                    if (ImGui.BeginChild("Main Menu Connect", new Vector2(256, 158), true, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration))
                    {
                        ImGui.InputText(lang.Translate("menu.connect.address") + "###Address", ref this._connectAddress, 15);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("menu.connect.address.tt"));
                        }

                        ImGui.InputText(lang.Translate("menu.connect.port") + "###Port", ref this._connectPort, 5);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("menu.connect.port.tt"));
                        }

                        bool allCorrect = Client.Instance.NetClient == null || !Client.Instance.NetClient.IsConnecting;

                        if (!IPAddress.TryParse(this._connectAddress, out IPAddress address))
                        {
                            allCorrect = false;
                            ImGui.TextColored(((Vector4)Color.Red), lang.Translate("menu.connect.error.address"));
                        }

                        if (!ushort.TryParse(this._connectPort, out ushort cPort))
                        {
                            allCorrect = false;
                            ImGui.TextColored(((Vector4)Color.Red), lang.Translate("menu.connect.error.port"));
                        }

                        ImGui.SetCursorPosY(150 - 32);
                        if (!allCorrect)
                        {
                            ImGui.BeginDisabled();
                        }

                        if (ImGui.Button(lang.Translate("menu.connect.connect") + "###Connect", new Vector2(240, 32)))
                        {
                            Client.Instance.Settings.LastConnectIPAddress = this._connectAddress;
                            Client.Instance.Settings.LastConnectPort = this._connectPort;
                            Client.Instance.Settings.Save();
                            Client.Instance.Connect(new IPEndPoint(address, cPort));
                        }

                        if (!allCorrect)
                        {
                            ImGui.EndDisabled();
                        }

                        ImGui.EndChild();
                    }
                }
                if (this.MenuMode == 3)
                {
                    ImGui.SetCursorPos(new Vector2((width / 2) - 200, 500));
                    DrawSettings(lang);

                }
                if (this.MenuMode == 2)
                {
                    ImGui.SetCursorPos(new Vector2((width / 2) - 128, 500));
                    if (ImGui.BeginChild("Main Menu Host", new Vector2(256, 158), true, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration))
                    {
                        ImGui.InputText(lang.Translate("menu.host.port") + "###Server Port", ref this._hostPort, 5);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("menu.host.port.tt"));
                        }

                        bool allCorrect = Client.Instance.NetClient == null || !Client.Instance.NetClient.IsConnecting;
                        allCorrect &= (Server.Instance == null || !Server.Instance.running);

                        if (!ushort.TryParse(this._hostPort, out ushort cPort))
                        {
                            allCorrect = false;
                            ImGui.TextColored(((Vector4)Color.Red), lang.Translate("menu.connect.error.port"));
                        }

                        ImGui.SetCursorPosY(150 - 32);
                        if (!allCorrect)
                        {
                            ImGui.BeginDisabled();
                        }

                        if (ImGui.Button(lang.Translate("menu.host") + "###Host", new Vector2(240, 32)))
                        {
                            Server.Instance = new Server(IPAddress.Loopback, cPort);
                            Server.Instance.LocalAdminID = Client.Instance.ID;
                            Server.Instance.Create();
                            Client.Instance.Connect(new IPEndPoint(IPAddress.Loopback, cPort));
                        }

                        if (!allCorrect)
                        {
                            ImGui.EndDisabled();
                        }

                        ImGui.EndChild();
                    }
                }

                ImGui.End();
            }

            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(new(ImGui.GetIO().DisplaySize.X * 0.5f - 200, ImGui.GetIO().DisplaySize.Y * 0.5f - 100), ImGuiCond.Appearing);
            if (showDC && ImGui.Begin(lang.Translate("ui.disconnected") + "###Disconnected", ref showDC))
            {
                ImGui.Text(lang.Translate("ui.disconnected"));
                ImGui.Text(lang.Translate("ui.disconnected.reason"));
                ImGui.NewLine();
                ImGui.Text("    " + lang.Translate("ui.disconnected.reason." + Enum.GetName(Client.Instance.LastDisconnectReason).ToLower()));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.disconnected.reason." + Enum.GetName(Client.Instance.LastDisconnectReason).ToLower() + ".tt"));

                }

                ImGui.End();
            }
        }

        private static Size? oldScreenSize;
        private static int? oldPosY;

        public static unsafe void DrawSettings(SimpleLanguage lang)
        {
            if (ImGui.BeginChild("Main Menu Setting", new Vector2(400, 300), true, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | (ImGuiWindowFlags.NoDecoration & ~ImGuiWindowFlags.NoScrollbar)))
            {
                if (ImGui.TreeNode(lang.Translate("menu.settings.category.display") + "###Display"))
                {
                    string[] wMode = { lang.Translate("menu.settings.screen_mode.normal"), lang.Translate("menu.settings.screen_mode.fullscreen"), lang.Translate("menu.settings.screen_mode.borderless") };
                    int wModeIndex =
                        Client.Instance.Settings.ScreenMode == ClientSettings.FullscreenMode.Normal ? 0 :
                        Client.Instance.Settings.ScreenMode == ClientSettings.FullscreenMode.Fullscreen ? 1 : 2;

                    ImGui.Text(lang.Translate("menu.settings.screen_mode"));
                    if (ImGui.Combo("##Screen Mode", ref wModeIndex, wMode, 3))
                    {
                        VideoMode* vModePtr = GLFW.GetVideoMode((Monitor*)Client.Instance.Frontend.GameHandle.CurrentMonitor.Pointer);
                        int w = vModePtr->Width;
                        int h = vModePtr->Height;

                        switch (wModeIndex)
                        {
                            case 0:
                            {
                                Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Normal;
                                Client.Instance.Frontend.GameHandle.WindowState = WindowState.Normal;
                                Client.Instance.Frontend.GameHandle.WindowBorder = WindowBorder.Resizable;
                                int ow = oldScreenSize?.Width ?? 1366;
                                int oh = oldScreenSize?.Height ?? 768;
                                Client.Instance.Frontend.GameHandle.Size = new OpenTK.Mathematics.Vector2i(ow, oh);
                                Client.Instance.Frontend.GameHandle.WindowState = WindowState.Minimized;
                                if (oldPosY.HasValue)
                                {
                                    Client.Instance.Frontend.GameHandle.Location = new OpenTK.Mathematics.Vector2i(Client.Instance.Frontend.GameHandle.Location.X, oldPosY.Value);
                                }
                                else
                                {
                                    Client.Instance.Frontend.GameHandle.CenterWindow();
                                }

                                Client.Instance.Settings.Resolution = new Size(ow, oh);
                                Client.Instance.Frontend.GameHandle.WindowState = WindowState.Normal;
                                break;
                            }

                            case 1:
                            {
                                Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Fullscreen;
                                oldScreenSize = new Size(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                                oldPosY = Client.Instance.Frontend.GameHandle.Location.Y;
                                Client.Instance.Frontend.GameHandle.Size = new OpenTK.Mathematics.Vector2i(w, h);
                                Client.Instance.Settings.Resolution = new Size(w, h);
                                Client.Instance.Frontend.GameHandle.WindowState = WindowState.Fullscreen;
                                Client.Instance.Frontend.GameHandle.WindowBorder = WindowBorder.Hidden;
                                break;
                            }

                            case 2:
                            {
                                Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Borderless;
                                Client.Instance.Frontend.GameHandle.Size = new OpenTK.Mathematics.Vector2i(w, h);
                                Client.Instance.Settings.Resolution = new Size(w, h);
                                Client.Instance.Frontend.GameHandle.WindowBorder = WindowBorder.Hidden;
                                Client.Instance.Frontend.GameHandle.WindowState = WindowState.Minimized;
                                Client.Instance.Frontend.GameHandle.CenterWindow();
                                Client.Instance.Frontend.GameHandle.WindowState = WindowState.Maximized;
                                break;
                            }
                        }

                        Client.Instance.Settings.Save();
                    }

                    string[] vSync = { lang.Translate("menu.settings.vsync.off"), lang.Translate("menu.settings.vsync.on"), lang.Translate("menu.settings.vsync.adaptive") };
                    int vSyncIndex =
                        Client.Instance.Settings.VSync == OpenTK.Windowing.Common.VSyncMode.On ? 1 :
                        Client.Instance.Settings.VSync == OpenTK.Windowing.Common.VSyncMode.Off ? 0 : 2;

                    ImGui.Text(lang.Translate("menu.settings.vsync"));
                    if (ImGui.Combo("##VSync", ref vSyncIndex, vSync, 3))
                    {
                        VSyncMode newMode = vSyncIndex == 0 ? VSyncMode.Off : vSyncIndex == 1 ? VSyncMode.On : VSyncMode.Adaptive;
                        Client.Instance.Frontend.GameHandle.VSync = newMode;
                        Client.Instance.Settings.VSync = newMode;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.vsync.tt"));
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx(lang.Translate("menu.settings.category.user") + "###User & Multiplayer"))
                {
                    string cName = Client.Instance.Settings.Name;
                    if (ImGui.InputText(lang.Translate("menu.settings.username") + "###Username", ref cName, ushort.MaxValue))
                    {
                        if (cName.Length > 0)
                        {
                            Client.Instance.Settings.Name = cName;
                            Client.Instance.Settings.Save();
                            if (Client.Instance.NetClient != null && Client.Instance.NetClient.IsConnected)
                            {
                                new PacketClientData() { InfosToUpdate = new List<ClientInfo>() { Client.Instance.CreateSelfInfo() } }.Send();
                            }
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.username.tt"));
                    }

                    Color color = Extensions.FromArgb(Client.Instance.Settings.Color);
                    Vector4 cVec = ((Vector4)color);
                    if (ImGui.ColorPicker4(lang.Translate("menu.settings.color"), ref cVec, ImGuiColorEditFlags.DisplayHSV))
                    {
                        color = Extensions.FromVec4(cVec.GLVector());
                        Client.Instance.Settings.Color = color.Argb();
                        Client.Instance.Settings.Save();
                        if (Client.Instance.NetClient != null && Client.Instance.NetClient.IsConnected)
                        {
                            new PacketClientData() { InfosToUpdate = new List<ClientInfo>() { Client.Instance.CreateSelfInfo() } }.Send();
                        }
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode(lang.Translate("menu.settings.category.graphics") + "###Graphics & Performance"))
                {
                    bool sShadowsSun = Client.Instance.Settings.EnableSunShadows;
                    bool sShadowsDir = Client.Instance.Settings.EnableDirectionalShadows;
                    bool sNoBranches = Client.Instance.Settings.DisableShaderBranching;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_sun") + "###Enable Sun Shadows", ref sShadowsSun))
                    {
                        Client.Instance.Settings.EnableSunShadows = sShadowsSun;
                        Client.Instance.Settings.Save();
                        Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_sun.tt"));
                    }

                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_points") + "###Enable Light Shadows", ref sShadowsDir))
                    {
                        Client.Instance.Settings.EnableDirectionalShadows = sShadowsDir;
                        Client.Instance.Settings.Save();
                        Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_points.tt"));
                    }

                    string[] shadowQuality = { lang.Translate("menu.settings.point_shadow_quality.low"), lang.Translate("menu.settings.point_shadow_quality.medium"), lang.Translate("menu.settings.point_shadow_quality.high"), lang.Translate("menu.settings.point_shadow_quality.ultra") };
                    int sQualIndex = (int)Client.Instance.Settings.PointShadowsQuality;

                    ImGui.Text(lang.Translate("menu.settings.point_shadow_quality"));
                    if (ImGui.Combo("##Point Shadows Quality", ref sQualIndex, shadowQuality, 4))
                    {
                        ClientSettings.GraphicsSetting nVal = (ClientSettings.GraphicsSetting)sQualIndex;
                        Client.Instance.Settings.PointShadowsQuality = nVal;
                        int r = 128 * (1 << sQualIndex);
                        Client.Instance.Frontend.Renderer.PointLightsRenderer?.ResizeShadowMaps(r);
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.point_shadow_quality.tt"));
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode(lang.Translate("menu.settings.category.language") + "###Language & Accessibility"))
                {
                    string[] langs = { lang.Translate("menu.settings.language.en-EN"), lang.Translate("menu.settings.language.ru-RU") };
                    string[] actualLangs = { "en-EN", "ru-RU" };
                    int selected = Array.IndexOf(actualLangs, Client.Instance.Settings.Language ?? "en-EN");
                    if (ImGui.Combo(lang.Translate("menu.settings.language") + "###Language", ref selected, langs, langs.Length))
                    {
                        Client.Instance.Settings.Language = actualLangs[selected];
                        Client.Instance.Settings.Save();
                        lang.LoadFile(actualLangs[selected]);
                    }

                    float mSensitivity = Client.Instance.Settings.Sensitivity;
                    ImGui.Text(lang.Translate("menu.settings.sensitivity"));
                    if (ImGui.SliderFloat("##Sensitivity", ref mSensitivity, 0.1f, 10f))
                    {
                        Client.Instance.Settings.Sensitivity = MathHelper.Clamp(mSensitivity, 0.1f, 10f);
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.sensitivity.tt"));
                    }

                    float mChatBrightness = Client.Instance.Settings.ChatBackgroundBrightness;
                    ImGui.Text(lang.Translate("menu.settings.chat_brightness"));
                    if (ImGui.SliderFloat("##ChatBrightness", ref mChatBrightness, 0f, 1f))
                    {
                        Client.Instance.Settings.ChatBackgroundBrightness = MathHelper.Clamp(mChatBrightness, 0, 1);
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.chat_brightness.tt"));
                    }

                    ClientSettings.UISkin dSkin = Client.Instance.Settings.InterfaceSkin;
                    string[] skins = Enum.GetNames(typeof(ClientSettings.UISkin)).Select(s => lang.Translate("menu.settings.ui_skin." + s.ToLowerInvariant())).ToArray();
                    int sIdx = (int)dSkin;
                    ImGui.Text(lang.Translate("menu.settings.ui_skin"));
                    if (ImGui.Combo("##UISkinSelector", ref sIdx, skins, skins.Length))
                    {
                        Client.Instance.Settings.InterfaceSkin = dSkin = (ClientSettings.UISkin)sIdx;
                        Client.Instance.Settings.Save();
                        Client.Instance.Frontend.GuiWrapper.ChangeSkin(dSkin);
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode(lang.Translate("menu.settings.category.advanced") + "###Advanced & Debug"))
                {
                    bool bDebug = Client.Instance.Settings.DebugSettingsEnabled;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.advanced_mode_enabled"), ref bDebug))
                    {
                        Client.Instance.Settings.DebugSettingsEnabled = bDebug;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.advanced_mode_enabled.tt"));
                    }

                    if (!bDebug)
                    {
                        ImGui.BeginDisabled();
                    }

                    ImGui.Text(lang.Translate("menu.settings.raycast_multithreading"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.raycast_multithreading.tt"));
                    }

                    string[] mtpcs = { lang.Translate("menu.settings.raycast_multithreading.always"), lang.Translate("menu.settings.raycast_multithreading.eager"), lang.Translate("menu.settings.raycast_multithreading.cautious"), lang.Translate("menu.settings.raycast_multithreading.never") };
                    int cmtpc = (int)Client.Instance.Settings.RaycastMultithreading;
                    if (ImGui.Combo("##RaycastMultithreading", ref cmtpc, mtpcs, 4))
                    {
                        Client.Instance.Settings.RaycastMultithreading = (ClientSettings.RaycastMultithreadingType)cmtpc;
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.pipeline"));
                    string[] pps = { lang.Translate("menu.settings.pipeline.forward"), lang.Translate("menu.settings.pipeline.deferred") };
                    int cpp = Client.Instance.Settings.Pipeline == ClientSettings.PipelineType.Forward ? 0 : 1;
                    if (ImGui.Combo("##Pipeline", ref cpp, pps, pps.Length))
                    {
                        Client.Instance.Settings.Pipeline = cpp == 0 ? ClientSettings.PipelineType.Forward : ClientSettings.PipelineType.Deferred;
                        Client.Instance.Settings.Save();
                        if (cpp == 1)
                        {
                            Client.Instance.Frontend.Renderer.ObjectRenderer.DeferredPipeline.Create();
                            Client.Instance.Frontend.Renderer.ObjectRenderer.DeferredPipeline.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                        }
                        else
                        {
                            Client.Instance.Frontend.Renderer.ObjectRenderer.DeferredPipeline.Dispose();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.pipeline.tt"));
                    }

                    bool sNoBranches = Client.Instance.Settings.DisableShaderBranching;
                    bool sShadowsSun = Client.Instance.Settings.EnableSunShadows;
                    bool sShadowsDir = Client.Instance.Settings.EnableDirectionalShadows;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.disable_branching") + "###Disable Shader Branching", ref sNoBranches))
                    {
                        Client.Instance.Settings.DisableShaderBranching = sNoBranches;
                        Client.Instance.Settings.Save();
                        Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.disable_branching.tt"));
                    }

                    if (!bDebug)
                    {
                        ImGui.EndDisabled();
                    }

                    ImGui.TreePop();
                }

                ImGui.EndChild();
            }
        }

        private List<SparkParticle> _sparks = new List<SparkParticle>();
        private List<ThunkParticle> _thunks = new List<ThunkParticle>();

        private Random _rand = new Random();
        private int _fCount = 0;
        private int _mascotThunk = 0;
        private int _currentNulEngAnimFrame = 0;

        private int _lastThunkTick = 0;
        private int _consecutiveThunks = 0;
        private bool _hardhatBroken = false;

        private HardhatParticle _hardhatParticle;

        private int[,] _frameData = new int[,] { 
            { 0, 1 }, 
            { 30, 2 }, 
            { 60, 1 }, 
            { 90, 2 }, 
            { 120, 1 }, 
            { 150, 2 }, 
            { 180, 1 }, 
            { 210, 2 }, 
            { 240, 1 }, 
            { 270, 2 }, 
            { 300, 1 }, 
            { 315, 3 }, 
            { 330, 4 }, 
            { 345, 5 }, 
            { 360, 6 }, 
            { 375, 7 }, 
            { 390, 8 }, 
            { 415, 9 }, 
            { 430, 0 } 
        };

        private bool _lmbDown;
        private bool _hardhatInHand;

        private Vector2 _mouseLastUpdate;
        private Vector2 _hardhatCursor2CenterWhenBroken;

        public void Update(double delta)
        {
            if (Client.Instance.NetClient != null && Client.Instance.NetClient.IsConnected)
            {
                return;
            }

            float width = ImGui.GetIO().DisplaySize.X;
            float height = ImGui.GetIO().DisplaySize.Y;

            this._fCount += 1;
            int idx = 0;
            while (true)
            {
                int c = this._frameData[idx, 0];
                if (c >= this._fCount)
                {
                    break;
                }

                ++idx;
                if (idx == this._frameData.GetLength(0))
                {
                    --idx;
                    break;
                }
            }

            this._currentNulEngAnimFrame = this._frameData[idx, 1];
            if (this._currentNulEngAnimFrame == 0)
            {
                this._currentNulEngAnimFrame = this._fCount % 60 >= 30 ? 9 : 0;
            }

            #region Alpha
            /*

            if (!this._hardhatBroken && this._consecutiveThunks <= 0)
            {
                if ((++this._mascotThunk) % 90 == 3)
                {
                    for (int i = 0; i < this._rand.Next(4, 9); ++i)
                    {
                        System.Numerics.Vector2 vel = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(
                            56 + this._rand.Next(0, 23) - this._rand.Next(0, 23),
                            -32 + this._rand.Next(0, 12) - this._rand.Next(0, 12))) * (float)(1.0f + (this._rand.NextDouble() * 2.0f));

                        System.Numerics.Vector2 pos = new System.Numerics.Vector2((width / 2) - 320 + 486, 182);
                        SparkParticle sp = new SparkParticle() { Gravity = true, Size = 16, DrawTexture = this.AlphaSparks, Position = pos, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                        this._sparks.Add(sp);
                    }
                }
            }

            if (this._rand.NextDouble() < 0.02)
            {
                while (true)
                {
                    System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                            (float)((this._rand.NextDouble() * 4) - (this._rand.NextDouble() * 4)),
                            -this._rand.Next(0, 12) / 5f);

                    System.Numerics.Vector2 pos = new System.Numerics.Vector2((width / 2) - 320 + 604, 217);
                    SparkParticle sp = new SparkParticle() { Gravity = true, Size = 12, DrawTexture = this.AlphaSparks, Position = pos, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                    this._sparks.Add(sp);
                    if (this._rand.NextDouble() < 0.5)
                    {
                        break;
                    }
                }
            }

            Vector2 cablePos1 = new Vector2((width / 2) - 320 + 377, 114);
            Vector2 cablePos2 = new Vector2((width / 2) - 320 + 419, 84);
            Vector2 cablePos3 = new Vector2((width / 2) - 320 + 452, 92);

            if (this._hardhatBroken)
            {
                if (this._rand.NextDouble() < 0.02)
                {
                    while (true)
                    {
                        System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                                (float)((this._rand.NextDouble() * 4) - (this._rand.NextDouble() * 4)),
                                -this._rand.Next(0, 12) / 5f);

                        System.Numerics.Vector2 pos = new System.Numerics.Vector2((width / 2) - 320 + 604, 217);
                        SparkParticle sp = new SparkParticle() { Gravity = true, Size = 12, DrawTexture = this.AlphaSparks, Position = cablePos1, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                        this._sparks.Add(sp);
                        if (this._rand.NextDouble() < 0.5)
                        {
                            break;
                        }
                    }
                }

                if (this._rand.NextDouble() < 0.02)
                {
                    while (true)
                    {
                        System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                                (float)((this._rand.NextDouble() * 4) - (this._rand.NextDouble() * 4)),
                                -this._rand.Next(0, 12) / 5f);

                        System.Numerics.Vector2 pos = new System.Numerics.Vector2((width / 2) - 320 + 604, 217);
                        SparkParticle sp = new SparkParticle() { Gravity = true, Size = 12, DrawTexture = this.AlphaSparks, Position = cablePos2, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                        this._sparks.Add(sp);
                        if (this._rand.NextDouble() < 0.5)
                        {
                            break;
                        }
                    }
                }

                if (this._rand.NextDouble() < 0.02)
                {
                    while (true)
                    {
                        System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                                (float)((this._rand.NextDouble() * 4) - (this._rand.NextDouble() * 4)),
                                -this._rand.Next(0, 12) / 5f);

                        System.Numerics.Vector2 pos = new System.Numerics.Vector2((width / 2) - 320 + 604, 217);
                        SparkParticle sp = new SparkParticle() { Gravity = true, Size = 12, DrawTexture = this.AlphaSparks, Position = cablePos3, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                        this._sparks.Add(sp);
                        if (this._rand.NextDouble() < 0.5)
                        {
                            break;
                        }
                    }
                }
            }

            if (!this._lmbDown && Game.Instance.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left))
            {
                this._lmbDown = true;
                if (!this._hardhatBroken)
                {
                    Vector2 center = new System.Numerics.Vector2((width / 2) - 320 + 354 + 74, 87 + 74);
                    Vector2 mouse = ImGui.GetIO().MousePos;
                    float dist = (center - mouse).Length();
                    if (dist <= 77f) // have thunk!
                    {
                        ThunkParticle tp = new ThunkParticle() { Angle = (float)this._rand.NextDouble() * MathF.PI * 2f, DrawTexture = this.AlphaThunk, Lifetime = 90, Position = mouse, Size = 32 };
                        this._thunks.Add(tp);
                        this._mascotThunk = 40;
                        this._consecutiveThunks += 1;
                        this._lastThunkTick = 180;
                    }

                    if (this._consecutiveThunks >= 10)
                    {
                        this._hardhatBroken = true;
                        this._hardhatInHand = true;
                        this._hardhatCursor2CenterWhenBroken = center - mouse;
                        this._hardhatParticle = new HardhatParticle() { Rotation = 0, DrawTexture = this.AlphaHardhatBroken, Position = center, Size = 144, Velocity = Vector2.Zero };
                        this._thunks.Clear();

                        for (int i = 0; i < 100 + this._rand.Next(100); ++i)
                        {
                            System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                                (float)((this._rand.NextDouble() * 6) - (this._rand.NextDouble() * 6)),
                                -this._rand.Next(0, 12));
                            SparkParticle sp = new SparkParticle() { Gravity = true, Size = 16 + this._rand.Next(12), DrawTexture = this.AlphaSparks, Position = cablePos1, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                            this._sparks.Add(sp);
                        }

                        for (int i = 0; i < 100 + this._rand.Next(100); ++i)
                        {
                            System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                                (float)((this._rand.NextDouble() * 6) - (this._rand.NextDouble() * 6)),
                                -this._rand.Next(0, 12));
                            SparkParticle sp = new SparkParticle() { Gravity = true, Size = 16 + this._rand.Next(12), DrawTexture = this.AlphaSparks, Position = cablePos2, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                            this._sparks.Add(sp);
                        }

                        for (int i = 0; i < 100 + this._rand.Next(100); ++i)
                        {
                            System.Numerics.Vector2 vel = new System.Numerics.Vector2(
                                (float)((this._rand.NextDouble() * 6) - (this._rand.NextDouble() * 6)),
                                -this._rand.Next(0, 12));
                            SparkParticle sp = new SparkParticle() { Gravity = true, Size = 16 + this._rand.Next(12), DrawTexture = this.AlphaSparks, Position = cablePos3, Rotation = (float)this._rand.NextDouble(), TextureIndex = this._rand.Next(4), Velocity = vel };
                            this._sparks.Add(sp);
                        }
                    }
                }
            }

            if (this._lmbDown && this._hardhatInHand)
            {
                this._hardhatParticle.Position = ImGui.GetIO().MousePos + this._hardhatCursor2CenterWhenBroken;
                this._hardhatParticle.Velocity = Vector2.Zero;
            }

            if (this._lmbDown && !Game.Instance.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left))
            {
                this._lmbDown = false;
                if (this._hardhatInHand)
                {
                    this._hardhatInHand = false;
                    this._hardhatParticle.Velocity = ImGui.GetIO().MousePos - this._mouseLastUpdate;
                    this._hardhatParticle.Rotation = MathF.PI * (float)this._rand.NextDouble();
                }
            }

            if (--this._lastThunkTick <= 0)
            {
                this._consecutiveThunks = 0;
            }

            for (int i = this._sparks.Count - 1; i >= 0; i--)
            {
                SparkParticle spark = this._sparks[i];
                spark.Update(delta);
                if (spark.Size < 1.0f)
                {
                    this._sparks.RemoveAt(i);
                }
            }

            for (int i = this._thunks.Count - 1; i >= 0; i--)
            {
                ThunkParticle tp = this._thunks[i];
                if (--tp.Lifetime <= 0)
                {
                    this._thunks.Remove(tp);
                }
            }

            this._hardhatParticle?.Update(delta);
            this._mouseLastUpdate = ImGui.GetIO().MousePos;
            */
            #endregion
        }
    }

    public class SparkParticle
    {
        public Vector2 Position { get; set; }
        public int TextureIndex { get; set; }
        public float Rotation { get; set; }
        public Vector2 Velocity { get; set; }
        public Texture DrawTexture { get; set; }
        public float Size { get; set; }
        public bool Gravity { get; set; }

        public float CurrentRotation { get; set; }

        public void Draw()
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2[] offsets = {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };

            float cos = MathF.Cos(this.CurrentRotation);
            float sin = MathF.Sin(this.CurrentRotation);
            for (int i = 0; i < 4; ++i)
            {
                Vector2 v = offsets[i];
                float dX = (v.X * cos) - (v.Y * sin);
                float dY = (v.X * sin) + (v.Y * cos);
                offsets[i] = this.Position + (new Vector2(dX, dY) * this.Size);
            }

            Vector2 uv0 = new Vector2((this.TextureIndex % 2) * 0.5f, (this.TextureIndex / 2) * 0.5f);
            Vector2 uv1 = uv0 + new Vector2(0.5f, 0);
            Vector2 uv2 = uv0 + new Vector2(0.5f, 0.5f);
            Vector2 uv3 = uv0 + new Vector2(0f, 0.5f);

            drawList.AddImageQuad(this.DrawTexture, offsets[0], offsets[1], offsets[2], offsets[3], uv0, uv1, uv2, uv3);
        }

        public void Update(double delta)
        {
            this.Position += this.Velocity;
            this.Size *= 0.95f;
            this.Rotation *= 0.95f;
            this.CurrentRotation += this.Rotation;
            if (this.Gravity)
            {
                this.Velocity += new Vector2(0, 9.8f) * (float)delta;
            }
        }
    }

    public class ThunkParticle
    {
        public Vector2 Position { get; set; }
        public int Lifetime { get; set; }
        public float Angle { get; set; }
        public float Size { get; set; }
        public Texture DrawTexture { get; set; }

        public void Draw()
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2[] offsets = {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };

            float cos = MathF.Cos(this.Angle);
            float sin = MathF.Sin(this.Angle);

            float x = 1.0f - (this.Lifetime / 90.0f);
            float d = MathF.Max(0, MathF.Ceiling(0.41f - x));
            float m = MathF.Sin((x + 5.1f) * 5) * 1.5f;
            float aScale = (d * m) + ((-x + 1.4f) * (1.0f - d));
            aScale *= this.Size;

            for (int i = 0; i < 4; ++i)
            {
                Vector2 v = offsets[i];
                float dX = (v.X * cos) - (v.Y * sin);
                float dY = (v.X * sin) + (v.Y * cos);
                offsets[i] = this.Position + (new Vector2(dX, dY) * aScale);
            }

            Vector2 uv0 = new Vector2(0, 0);
            Vector2 uv1 = uv0 + new Vector2(1, 0);
            Vector2 uv2 = uv0 + new Vector2(1, 1);
            Vector2 uv3 = uv0 + new Vector2(0, 1);

            drawList.AddImageQuad(this.DrawTexture, offsets[0], offsets[1], offsets[2], offsets[3], uv0, uv1, uv2, uv3);
        }
    }

    public class HardhatParticle
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Angle { get; set; }
        public float Rotation { get; set; }
        public Texture DrawTexture { get; set; }
        public float Size { get; set; }

        public void Draw()
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2[] offsets = {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };

            float cos = MathF.Cos(this.Angle);
            float sin = MathF.Sin(this.Angle);

            for (int i = 0; i < 4; ++i)
            {
                Vector2 v = offsets[i];
                float dX = (v.X * cos) - (v.Y * sin);
                float dY = (v.X * sin) + (v.Y * cos);
                offsets[i] = this.Position + (new Vector2(dX, dY) * this.Size);
            }

            Vector2 uv0 = new Vector2(0, 0);
            Vector2 uv1 = uv0 + new Vector2(1, 0);
            Vector2 uv2 = uv0 + new Vector2(1, 1);
            Vector2 uv3 = uv0 + new Vector2(0, 1);

            drawList.AddImageQuad(this.DrawTexture, offsets[0], offsets[1], offsets[2], offsets[3], uv0, uv1, uv2, uv3);
        }

        public void Update(double delta)
        {
            this.Position += this.Velocity;
            this.Rotation *= 0.975f;
            this.Angle += this.Rotation;
            this.Velocity += new Vector2(0, 9.8f) * (float)delta;
        }
    }
}
