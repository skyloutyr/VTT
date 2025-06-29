﻿namespace VTT.Render.MainMenu
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render.Gui;
    using VTT.Util;
    using static VTT.Network.ClientSettings;

    public class MainMenuRenderer
    {
        public Texture LogoMain { get; set; }

        public Texture BetaLetters { get; set; }
        public Texture BetaLettersOff { get; set; }
        public Texture BetaLight { get; set; }
        public Texture BetaMascot { get; set; }
        public Texture BetaSwitch { get; set; }
        public Texture BetaSwitchOff { get; set; }
        public Texture BetaWires { get; set; }

        public MenuMode CurrentMenuMode { get; set; }

        private string _connectAddress = string.Empty;
        private string _connectPort = string.Empty;
        private string _hostPort = string.Empty;
        private bool _betaSwitchOff;

        public void Create()
        {
            this.LogoMain = OpenGLUtil.LoadUIImage("Logo.logo-main");

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

        public void Render(ref bool showDC, double delta, GuiState state)
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
                if (ImGui.ImageButton("btnBetaSwitch", this._betaSwitchOff ? this.BetaSwitchOff : this.BetaSwitch, new Vector2(14, 19), Vector2.Zero, Vector2.One, Vector4.Zero))
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

                string copyright = "SkyLouTyr MIT © 2024";
                Vector2 cLen = ImGuiHelper.CalcTextSize(copyright);
                ImGui.SetCursorPos(new Vector2(width, height) - new Vector2(8, 8) - cLen);
                ImGui.TextUnformatted(copyright);

                if (Client.Instance.Frontend.UpdaterExitCode == 1)
                {
                    ImGui.SetCursorPos(new Vector2(16, 32));
                    if (ImGui.Button(lang.Translate("ui.button.update")))
                    {
                        if (!Client.Instance.Frontend.GameHandle.IsAnyControlDown())
                        {
                            string updater = Path.Combine(IOVTT.AppDir, "VTTUpdater.exe");
                            if (File.Exists(updater))
                            {
                                Client.Instance.Frontend.GameHandle.Close();
                                Process updaterProcess = new Process();
                                updaterProcess.StartInfo.FileName = updater;
                                updaterProcess.Start();
                            }

                            Environment.Exit(0);
                        }
                        else
                        {
                            OpenUrl("https://github.com/skyloutyr/VTT/releases/latest");
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.update.tt"));
                    }
                }

                ImGui.SetCursorPos(new Vector2((width / 2) - 128, 300));
                if (ImGui.BeginChild("Main Menu Entry", new Vector2(256, 224), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration))
                {
                    if (ImGui.Button(lang.Translate("menu.join") + "###Join", new Vector2(240, 32)))
                    {
                        this.CurrentMenuMode = MenuMode.Join;
                    }

                    if (ImGui.Button(lang.Translate("menu.host") + "###Host", new Vector2(240, 32)))
                    {
                        this.CurrentMenuMode = MenuMode.Host;
                    }

                    if (ImGui.Button(lang.Translate("menu.settings") + "###Settings", new Vector2(240, 32)))
                    {
                        this.CurrentMenuMode = MenuMode.Settings;
                    }

                    if (ImGui.Button(lang.Translate("menu.credits") + "###Credits", new Vector2(240, 32)))
                    {
                        this.CurrentMenuMode = MenuMode.Credits;
                    }

                    if (ImGui.Button(lang.Translate("menu.changelog") + "###Changelog", new Vector2(240, 32)))
                    {
                        this.CurrentMenuMode = MenuMode.Changelog;
                    }

                    if (ImGui.Button(lang.Translate("menu.quit") + "###Quit", new Vector2(240, 32)))
                    {
                        Client.Instance.Frontend.GameHandle.Close();
                    }
                }

                ImGui.EndChild();

                switch (this.CurrentMenuMode)
                {
                    case MenuMode.Join:
                    {
                        ImGui.SetCursorPos(new Vector2((width / 2) - 128, 532));
                        if (ImGui.BeginChild("Main Menu Connect", new Vector2(256, 158), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration))
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
                        }

                        ImGui.EndChild();
                        break;
                    }

                    case MenuMode.Settings:
                    {
                        ImGui.SetCursorPos(new Vector2((width / 2) - 200, 532));
                        DrawSettings(lang, state);
                        break;
                    }

                    case MenuMode.Host:
                    {
                        ImGui.SetCursorPos(new Vector2((width / 2) - 128, 532));
                        if (ImGui.BeginChild("Main Menu Host", new Vector2(256, 158), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration))
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
                        }

                        ImGui.EndChild();
                        break;
                    }

                    case MenuMode.Changelog:
                    {
                        if (Client.Instance.ClientVersion != null)
                        {
                            ImGui.SetCursorPos(new Vector2((width / 2) - 256, 532));
                            if (ImGui.BeginChild("Main Menu Changelog", new Vector2(512, 300), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | (ImGuiWindowFlags.NoDecoration & ~ImGuiWindowFlags.NoScrollbar)))
                            {
                                foreach ((Version, string) kv in Client.Instance.ClientVersion.EnumerateChangelogData())
                                {
                                    ImGui.TextUnformatted(kv.Item1.ToString());
                                    ImGui.Spacing();
                                    foreach (string s in kv.Item2.Split('\n'))
                                    {
                                        ImGui.Bullet();
                                        ImGui.PushTextWrapPos();
                                        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                                        ImGui.TextUnformatted(s);
                                        ImGui.PopStyleColor();
                                        ImGui.PopTextWrapPos();
                                    }

                                    ImGui.Spacing();
                                    ImGui.Spacing();
                                }
                            }

                            ImGui.EndChild();
                        }

                        break;
                    }

                    case MenuMode.Credits:
                    {
                        ImGui.SetCursorPos(new Vector2((width / 2) - 256, 532));
                        if (ImGui.BeginChild("Main Menu Credits", new Vector2(512, 300), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | (ImGuiWindowFlags.NoDecoration & ~ImGuiWindowFlags.NoScrollbar)))
                        {
                            string sp = "    ";
                            ImGui.Text(lang.Translate("credits.dependencies"));
                            ImGui.Text(sp + lang.Translate("credits.ncalc"));
                            ImGui.Text(sp + lang.Translate("credits.ffmpeg"));
                            ImGui.Text(sp + lang.Translate("credits.gltf"));
                            ImGui.Text(sp + lang.Translate("credits.imgui"));
                            ImGui.Text(sp + lang.Translate("credits.imgui.c"));
                            ImGui.Text(sp + lang.Translate("credits.netcoreserver"));
                            ImGui.Text(sp + lang.Translate("credits.json"));
                            ImGui.Text(sp + lang.Translate("credits.opentk"));
                            ImGui.Text(sp + lang.Translate("credits.imagesharp"));
                            ImGui.Text(sp + lang.Translate("credits.net"));
                            ImLink(lang.Translate("credits.imgui_markdown"), "https://github.com/juliettef/imgui_markdown");
                            ImGui.NewLine();
                            ImLink(lang.Translate("credits.icons8"), "https://icons8.com/");
                            ImGui.Text("    3d");
                            ImGui.Text("    abscissa");
                            ImGui.Text("    accuracy");
                            ImGui.Text("    block");
                            ImGui.Text("    box-important");
                            ImGui.Text("    brackets");
                            ImGui.Text("    closed-eye");
                            ImGui.Text("    color-swatch");
                            ImGui.Text("    cube");
                            ImGui.Text("    cursor");
                            ImGui.Text("    curved-arrow");
                            ImGui.Text("    day-camera");
                            ImGui.Text("    dice");
                            ImGui.Text("    door");
                            ImGui.Text("    double-down");
                            ImGui.Text("    double-right");
                            ImGui.Text("    drag");
                            ImGui.Text("    edit");
                            ImGui.Text("    erase");
                            ImGui.Text("    error");
                            ImGui.Text("    eye");
                            ImGui.Text("    folder");
                            ImGui.Text("    help");
                            ImGui.Text("    incoming-data");
                            ImGui.Text("    length");
                            ImGui.Text("    light");
                            ImGui.Text("    link-picture");
                            ImGui.Text("    lips");
                            ImGui.Text("    loading-circle");
                            ImGui.Text("    magic");
                            ImGui.Text("    money-bag");
                            ImGui.Text("    move-all-arrow");
                            ImGui.Text("    move-separate");
                            ImGui.Text("    musical-notes");
                            ImGui.Text("    music-library");
                            ImGui.Text("    no-image");
                            ImGui.Text("    outgoing-data");
                            ImGui.Text("    paint");
                            ImGui.Text("    paper-plane");
                            ImGui.Text("    particle");
                            ImGui.Text("    pause");
                            ImGui.Text("    pentagram");
                            ImGui.Text("    picture");
                            ImGui.Text("    pipeline");
                            ImGui.Text("    pipette");
                            ImGui.Text("    play");
                            ImGui.Text("    plus-math");
                            ImGui.Text("    polyline");
                            ImGui.Text("    process");
                            ImGui.Text("    radar-plot");
                            ImGui.Text("    radius");
                            ImGui.Text("    rectangle");
                            ImGui.Text("    resize");
                            ImGui.Text("    return");
                            ImGui.Text("    search");
                            ImGui.Text("    security-lock");
                            ImGui.Text("    so-so");
                            ImGui.Text("    sound");
                            ImGui.Text("    sphere");
                            ImGui.Text("    stop");
                            ImGui.Text("    stopwatch");
                            ImGui.Text("    surface");
                            ImGui.Text("    sword");
                            ImGui.Text("    swords");
                            ImGui.Text("    thinking-male");
                            ImGui.Text("    trash-can");
                            ImGui.Text("    vertical-line");
                            ImGui.Text("    visialy-impared");
                            ImGui.Text("    video-camera");
                            ImGui.Text("    wall");
                            ImLink(lang.Translate("credits.ui_skybox"), "https://learnopengl.com/img/textures/skybox.zip");
                            ImGui.NewLine();
                            ImLink(lang.Translate("credits.atlas"), "https://game-icons.net/");
                            ImGui.NewLine();
                            ImGui.Text(lang.Translate("credits.tools"));
                            ImLink(sp + lang.Translate("credits.blender"), "https://www.blender.org/");
                            ImLink(sp + lang.Translate("credits.gimp"), "https://www.gimp.org/");
                            ImLink(sp + lang.Translate("credits.vs"), "https://visualstudio.microsoft.com/");
                            ImLink(sp + lang.Translate("credits.emojidata"), "https://www.unicode.org");
                            ImLink(sp + lang.Translate("credits.nsight"), "https://developer.nvidia.com/nsight-graphics");
                            ImLink(sp + lang.Translate("credits.sfx"), "https://www.storyblocks.com/audio");
                            ImGui.NewLine();
                            ImGui.Text(lang.Translate("credits.special"));
                            ImGui.Text(sp + lang.Translate("credits.stackoverflow"));
                            ImGui.Text(sp + lang.Translate("credits.msspecial"));
                            ImGui.Text(sp + lang.Translate("credits.khronos"));
                            ImGui.Text(sp + lang.Translate("credits.you"));
                        }

                        ImGui.EndChild();
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }

            ImGui.End();

            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(new((ImGui.GetIO().DisplaySize.X * 0.5f) - 200, (ImGui.GetIO().DisplaySize.Y * 0.5f) - 100), ImGuiCond.Appearing);
            if (showDC)
            {
                if (ImGui.Begin(lang.Translate("ui.disconnected") + "###Disconnected", ref showDC))
                {
                    ImGui.Text(lang.Translate("ui.disconnected"));
                    ImGui.Text(lang.Translate("ui.disconnected.reason"));
                    ImGui.NewLine();
                    ImGui.Text("    " + lang.Translate("ui.disconnected.reason." + Enum.GetName(Client.Instance.LastDisconnectReason).ToLower()));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.disconnected.reason." + Enum.GetName(Client.Instance.LastDisconnectReason).ToLower() + ".tt"));

                    }
                }

                ImGui.End();
            }
        }

        private void ImLink(string text, string url)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            ImGui.PushStyleColor(ImGuiCol.Button, 0);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
            if (ImGui.Button(text))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }

            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        public static unsafe void DrawSettings(SimpleLanguage lang, GuiState state)
        {
            if (ImGui.BeginChild("Main Menu Setting", new Vector2(400, 300), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | (ImGuiWindowFlags.NoDecoration & ~ImGuiWindowFlags.NoScrollbar)))
            {
                if (ImGui.TreeNode(lang.Translate("menu.settings.category.display") + "###Display"))
                {
                    string[] wMode = { lang.Translate("menu.settings.screen_mode.normal"), lang.Translate("menu.settings.screen_mode.fullscreen"), lang.Translate("menu.settings.screen_mode.borderless") };
                    int wModeIndex =
                        Client.Instance.Settings.ScreenMode == FullscreenMode.Normal ? 0 :
                        Client.Instance.Settings.ScreenMode == FullscreenMode.Fullscreen ? 1 : 2;

                    ImGui.Text(lang.Translate("menu.settings.screen_mode"));
                    if (ImGui.Combo("##Screen Mode", ref wModeIndex, wMode, 3))
                    { 
                        switch (wModeIndex)
                        {
                            case 0:
                            {
                                Client.Instance.Frontend.SwitchFullscreen(FullscreenMode.Normal);
                                break;
                            }

                            case 1:
                            {
                                Client.Instance.Frontend.SwitchFullscreen(FullscreenMode.Fullscreen);
                                break;
                            }

                            case 2:
                            {
                                Client.Instance.Frontend.SwitchFullscreen(FullscreenMode.Borderless);
                                break;
                            }
                        }

                        Client.Instance.Settings.Save();
                    }

                    string[] vSync = { lang.Translate("menu.settings.vsync.off"), lang.Translate("menu.settings.vsync.on"), lang.Translate("menu.settings.vsync.adaptive") };
                    int vSyncIndex =
                        Client.Instance.Settings.VSync == VSyncMode.On ? 1 :
                        Client.Instance.Settings.VSync == VSyncMode.Off ? 0 : 2;

                    ImGui.Text(lang.Translate("menu.settings.vsync"));
                    if (ImGui.Combo("##VSync", ref vSyncIndex, vSync, 3))
                    {
                        VSyncMode newMode = vSyncIndex == 0 ? VSyncMode.Off : vSyncIndex == 1 ? VSyncMode.On : VSyncMode.Adaptive;
                        Client.Instance.Frontend.GameHandle.VSync.Value = newMode;
                        Client.Instance.Settings.VSync = newMode;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.vsync.tt"));
                    }

                    string[] unfocusedFramerate = { lang.Translate("menu.settings.uframes.none"), lang.Translate("menu.settings.uframes.native"), lang.Translate("menu.settings.uframes.high"), lang.Translate("menu.settings.uframes.medium"), lang.Translate("menu.settings.uframes.low"), lang.Translate("menu.settings.uframes.lowest") };
                    int unfocusedFramerateIndex = (int)Client.Instance.Settings.UnfocusedFramerate;
                    ImGui.Text(lang.Translate("menu.settings.uframes"));
                    if (ImGui.Combo("##UFrames", ref unfocusedFramerateIndex, unfocusedFramerate, unfocusedFramerate.Length))
                    {
                        UnfocusedFramerateCap newMode = (UnfocusedFramerateCap)unfocusedFramerateIndex;
                        Client.Instance.Settings.UnfocusedFramerate = newMode;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.uframes.tt"));
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx(lang.Translate("menu.settings.category.user") + "###User & Multiplayer"))
                {
                    string cName = Client.Instance.Settings.Name;
                    if (ImGui.InputText(lang.Translate("menu.settings.username") + "###Username", ref cName, 255))
                    {
                        if (cName.Length > 0)
                        {
                            Client.Instance.Settings.Name = cName.Replace(" ", "_"); //Please no spacebar in name, spacebar conflicts with the /w command slightly
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
                        color = Extensions.FromVec4(cVec);
                        Client.Instance.Settings.Color = color.Argb();
                        Client.Instance.Settings.Save();
                        if (Client.Instance.NetClient != null && Client.Instance.NetClient.IsConnected)
                        {
                            new PacketClientData() { InfosToUpdate = new List<ClientInfo>() { Client.Instance.CreateSelfInfo() } }.Send();
                        }
                    }

                    if (!Client.Instance.Frontend.FFmpegWrapper.IsInitialized)
                    {
                        ImGui.BeginDisabled();
                    }

                    string[] soundCompressions = { lang.Translate("menu.settings.sound_compression.always"), lang.Translate("menu.settings.sound_compression.large"), lang.Translate("menu.settings.sound_compression.never") };
                    int sSCPolicyIndex = (int)Client.Instance.Settings.SoundCompressionPolicy;
                    ImGui.Text(lang.Translate("menu.settings.sound_compression"));
                    if (ImGui.Combo("##Sound Compression Mode", ref sSCPolicyIndex, soundCompressions, 3))
                    {
                        AudioCompressionPolicy nVal = (AudioCompressionPolicy)sSCPolicyIndex;
                        Client.Instance.Settings.SoundCompressionPolicy = nVal;
                        Client.Instance.Settings.Save();
                    }

                    if (!Client.Instance.Frontend.FFmpegWrapper.IsInitialized)
                    {
                        ImGui.EndDisabled();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.sound_compression.tt"));
                    }

                    bool cAssetLoadIsAsync = Client.Instance.Settings.AsyncAssetLoading;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.async_asset_loading") + "###Async Asset Loading", ref cAssetLoadIsAsync))
                    {
                        Client.Instance.Settings.AsyncAssetLoading = cAssetLoadIsAsync;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.async_asset_loading.tt"));
                    }

                    ImGui.TextUnformatted(lang.Translate("menu.settings.player_image"));
                    if (Client.Instance.Connected)
                    {
                        ImCustomTexturedRect clientImage = Client.Instance.Frontend.Renderer.GuiRenderer.NoImageIcon;
                        if (Client.Instance.Frontend.Renderer.AvatarLibrary.ClientImages.TryGetValue(Client.Instance.ID, out (Texture, bool) cImgData) && cImgData.Item2)
                        {
                            clientImage = ImCustomTexturedRect.WrapCustomTexture(cImgData.Item1);
                        }

                        Vector2 v = ImGui.GetCursorScreenPos();
                        Vector2 m = Client.Instance.Frontend.GameHandle.MousePosition.Value;
                        clientImage.ImImage(new Vector2(64, 64));
                        if (m.X >= v.X && m.Y >= v.Y && m.X <= v.X + 64 && m.Y <= v.Y + 64)
                        {
                            for (int i = state.dropEvents.Count - 1; i >= 0; i--)
                            {
                                string s = state.dropEvents[i];
                                if (s.EndsWith(".png"))
                                {
                                    try
                                    {
                                        Image<Rgba32> img = Image.Load<Rgba32>(s);
                                        if (img.Width != 32 || img.Height != 32)
                                        {
                                            img.Mutate(x => x.Resize(32, 32, KnownResamplers.Bicubic));
                                        }

                                        new PacketClientAvatar() { Image = img }.Send(Client.Instance.NetClient);
                                        state.dropEvents.RemoveAt(i);
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        Client.Instance.Logger.Log(LogLevel.Error, "Could not read image for client's avatar!");
                                        Client.Instance.Logger.Exception(LogLevel.Error, e);
                                        break;
                                    }
                                }
                            }

                            ImGui.SetTooltip(lang.Translate("menu.settings.player_image.tt"));
                        }

                        if (ImGui.Button(lang.Translate("menu.settings.player_image.clear")))
                        {
                            new PacketClientAvatar() { Image = null }.Send(Client.Instance.NetClient);
                        }
                    }
                    else
                    {
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("menu.settings.player_image.needs_connection.tt"));
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
                        //Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches, OpenGLUtil.ShouldUseSPIRV);
                        Client.Instance.Frontend.Renderer.Pipeline.RecompileShaders(sShadowsDir, sShadowsSun);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_sun.tt"));
                    }

                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_points") + "###Enable Light Shadows", ref sShadowsDir))
                    {
                        Client.Instance.Settings.EnableDirectionalShadows = sShadowsDir;
                        Client.Instance.Settings.Save();
                        //Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches, OpenGLUtil.ShouldUseSPIRV);
                        Client.Instance.Frontend.Renderer.Pipeline.RecompileShaders(sShadowsDir, sShadowsSun);
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
                        GraphicsSetting nVal = (GraphicsSetting)sQualIndex;
                        Client.Instance.Settings.PointShadowsQuality = nVal;
                        int r = 128 * (1 << sQualIndex);
                        Client.Instance.Frontend.Renderer.PointLightsRenderer?.ResizeShadowMaps(r);
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.point_shadow_quality.tt"));
                    }

                    bool sParticles = Client.Instance.Settings.ParticlesEnabled;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_particles") + "###Enable Particles", ref sParticles))
                    {
                        Client.Instance.Settings.ParticlesEnabled = sParticles;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_particles.tt"));
                    }

                    bool sttParticles = Client.Instance.Settings.TurnTrackerParticlesEnabled;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_tt_particles") + "###Enable Turn Tracker Particles", ref sttParticles))
                    {
                        Client.Instance.Settings.TurnTrackerParticlesEnabled = sttParticles;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_tt_particles.tt"));
                    }

                    int sPcfQ = Client.Instance.Settings.ShadowsPCF;
                    ImGui.TextUnformatted(lang.Translate("menu.settings.pcf_quality"));
                    ImGui.SameLine();
                    if (ImGui.SliderInt("###PCF Quality", ref sPcfQ, 1, 5))
                    {
                        Client.Instance.Settings.ShadowsPCF = sPcfQ;
                        //Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches, OpenGLUtil.ShouldUseSPIRV);
                        Client.Instance.Frontend.Renderer.Pipeline.RecompileShaders(sShadowsDir, sShadowsSun);
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.pcf_quality.tt"));
                    }

                    float sGamma = Client.Instance.Settings.Gamma;
                    ImGui.TextUnformatted(lang.Translate("menu.settings.gamma"));
                    ImGui.SameLine();
                    if (ImGui.SliderFloat("###Gamma", ref sGamma, 0.96f, 5.0f))
                    {
                        Client.Instance.Settings.Gamma = sGamma;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.gamma.tt"));
                    }

                    bool sCustomShaders = Client.Instance.Settings.EnableCustomShaders;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_custom_shaders") + "###Enable Custom Shaders", ref sCustomShaders))
                    {
                        Client.Instance.Settings.EnableCustomShaders = sCustomShaders;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_custom_shaders.tt"));
                    }

                    bool sHalfPrecision = Client.Instance.Settings.UseHalfPrecision;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.use_half_precision") + "###Use Half Precision", ref sHalfPrecision))
                    {
                        Client.Instance.Settings.UseHalfPrecision = sHalfPrecision;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.use_half_precision.tt"));
                    }

                    string[] msaa = { lang.Translate("menu.settings.msaa.off"), lang.Translate("menu.settings.msaa.low"), lang.Translate("menu.settings.msaa.normal"), lang.Translate("menu.settings.msaa.high") };
                    int msaaIndex = (int)Client.Instance.Settings.MSAA;
                    ImGui.Text(lang.Translate("menu.settings.msaa"));
                    if (ImGui.Combo("##MSAA", ref msaaIndex, msaa, msaa.Length))
                    {
                        MSAAMode newMode = (MSAAMode)msaaIndex;
                        Client.Instance.Settings.MSAA = newMode;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.msaa.tt"));
                    }

                    float sFov = Client.Instance.Settings.FOV;
                    ImGui.TextUnformatted(lang.Translate("menu.settings.fov"));
                    ImGui.SameLine();
                    if (ImGui.SliderFloat("###FOV", ref sFov, 45f, 120f))
                    {
                        Client.Instance.Settings.FOV = sFov;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.fov.tt"));
                    }

                    string[] tcompression = { lang.Translate("menu.settings.compression.off"), lang.Translate("menu.settings.compression.bptc"), lang.Translate("menu.settings.compression.dxt") };
                    int tcompressionIndex = (int)Client.Instance.Settings.CompressionPreference;
                    ImGui.Text(lang.Translate("menu.settings.compression"));
                    if (ImGui.Combo("##TextureCompression", ref tcompressionIndex, tcompression, tcompression.Length))
                    {
                        TextureCompressionPreference newMode = (TextureCompressionPreference)tcompressionIndex;
                        Client.Instance.Settings.CompressionPreference = newMode;
                        Client.Instance.Settings.Save();
                        OpenGLUtil.DetermineCompressedFormats();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.compression.tt"));
                    }

                    bool hwDXTCompression = Client.Instance.Settings.AsyncDXTCompression;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.async_dxt_compression") + "###Compress DXT Async", ref hwDXTCompression))
                    {
                        Client.Instance.Settings.AsyncDXTCompression = hwDXTCompression;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.async_dxt_compression.tt"));
                    }

                    bool asyncTUpload = Client.Instance.Settings.AsyncTextureUploading;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.async_texture_upload") + "###Upload Textures Async", ref asyncTUpload))
                    {
                        Client.Instance.Settings.AsyncTextureUploading = asyncTUpload;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.async_texture_upload.tt"));
                    }

                    ImGui.TextUnformatted(lang.Translate("menu.settings.async_texture_upload.buffers"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.async_texture_upload.buffers.tt"));
                    }

                    int asyncTNumBuffers = Client.Instance.Settings.NumAsyncTextureBuffers - 1;
                    string[] asyncTNumBuffersText = { lang.Translate("menu.settings.async_texture_upload.buffers.single"), lang.Translate("menu.settings.async_texture_upload.buffers.double"), lang.Translate("menu.settings.async_texture_upload.buffers.triple") };
                    if (ImGui.Combo("##NumAsyncTexturePBOs", ref asyncTNumBuffers, asyncTNumBuffersText, asyncTNumBuffersText.Length))
                    {
                        asyncTNumBuffers = Math.Clamp(asyncTNumBuffers, 0, 2);
                        asyncTNumBuffers += 1;
                        Client.Instance.Settings.NumAsyncTextureBuffers = asyncTNumBuffers;
                        Client.Instance.Settings.Save();
                    }

                    string[] drawingsPerformance = { lang.Translate("menu.settings.drawings.none"), lang.Translate("menu.settings.drawings.minimum"), lang.Translate("menu.settings.drawings.limited"), lang.Translate("menu.settings.drawings.standard"), lang.Translate("menu.settings.drawings.extra"), lang.Translate("menu.settings.drawings.unlimited") };
                    int dPerfIndex = (int)Client.Instance.Settings.DrawingsPerformance;

                    ImGui.Text(lang.Translate("menu.settings.drawings.performance"));
                    if (ImGui.Combo("##Drawings Restrictions", ref dPerfIndex, drawingsPerformance, 6))
                    {
                        DrawingsResourceAllocationMode nVal = (DrawingsResourceAllocationMode)dPerfIndex;
                        Client.Instance.Settings.DrawingsPerformance = nVal;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.performance.tt"));
                    }

                    string[] shadow2dPrecisions = { lang.Translate("menu.settings.shadow2dprecision.low"), lang.Translate("menu.settings.shadow2dprecision.medium"), lang.Translate("menu.settings.shadow2dprecision.high"), lang.Translate("menu.settings.shadow2dprecision.full") };
                    int s2dperfIndex = (int)Client.Instance.Settings.Shadow2DPrecision;
                    ImGui.Text(lang.Translate("menu.settings.shadow2dprecision"));
                    if (ImGui.Combo("##Shadow 2D Precision", ref s2dperfIndex, shadow2dPrecisions, 4))
                    {
                        Shadow2DResolution nVal = (Shadow2DResolution)s2dperfIndex;
                        Client.Instance.Settings.Shadow2DPrecision = nVal;
                        Client.Instance.Settings.Save();
                        int w = nVal switch
                        {
                            Shadow2DResolution.Low => 256,
                            Shadow2DResolution.Medium => 512,
                            Shadow2DResolution.High => 1024,
                            Shadow2DResolution.Full => Client.Instance.Frontend.Width,
                            _ => 256
                        };

                        int h = nVal switch
                        {
                            Shadow2DResolution.Low => 256,
                            Shadow2DResolution.Medium => 512,
                            Shadow2DResolution.High => 1024,
                            Shadow2DResolution.Full => Client.Instance.Frontend.Height,
                            _ => 256
                        };

                        Client.Instance.Frontend.Renderer?.ObjectRenderer?.Shadow2DRenderer?.ResizeSimulation(w, h);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.shadow2dprecision.tt"));
                    }

                    bool bChatDiceEnabled = Client.Instance.Settings.ChatDiceEnabled;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.chat_dice_enabled") + "##ChatDiceEnabled", ref bChatDiceEnabled))
                    {
                        Client.Instance.Settings.ChatDiceEnabled = bChatDiceEnabled;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.chat_dice_enabled.tt"));
                    }

                    uint clrD2 = Client.Instance.Settings.ColorD4;
                    ChatDiceColorMode policyD2 = Client.Instance.Settings.ColorModeD2;
                    if (ChatDieColorSetting(lang, ref clrD2, ref policyD2, 2))
                    {
                        Client.Instance.Settings.ColorD2 = clrD2;
                        Client.Instance.Settings.ColorModeD2 = policyD2;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD4 = Client.Instance.Settings.ColorD4;
                    ChatDiceColorMode policyD4 = Client.Instance.Settings.ColorModeD4;
                    if (ChatDieColorSetting(lang, ref clrD4, ref policyD4, 4))
                    {
                        Client.Instance.Settings.ColorD4 = clrD4;
                        Client.Instance.Settings.ColorModeD4 = policyD4;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD6 = Client.Instance.Settings.ColorD6;
                    ChatDiceColorMode policyD6 = Client.Instance.Settings.ColorModeD6;
                    if (ChatDieColorSetting(lang, ref clrD6, ref policyD6, 6))
                    {
                        Client.Instance.Settings.ColorD6 = clrD6;
                        Client.Instance.Settings.ColorModeD6 = policyD6;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD8 = Client.Instance.Settings.ColorD8;
                    ChatDiceColorMode policyD8 = Client.Instance.Settings.ColorModeD8;
                    if (ChatDieColorSetting(lang, ref clrD8, ref policyD8, 8))
                    {
                        Client.Instance.Settings.ColorD8 = clrD8;
                        Client.Instance.Settings.ColorModeD8 = policyD8;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD10 = Client.Instance.Settings.ColorD10;
                    ChatDiceColorMode policyD10 = Client.Instance.Settings.ColorModeD10;
                    if (ChatDieColorSetting(lang, ref clrD10, ref policyD10, 10))
                    {
                        Client.Instance.Settings.ColorD10 = clrD10;
                        Client.Instance.Settings.ColorModeD10 = policyD10;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD12 = Client.Instance.Settings.ColorD12;
                    ChatDiceColorMode policyD12 = Client.Instance.Settings.ColorModeD12;
                    if (ChatDieColorSetting(lang, ref clrD12, ref policyD12, 12))
                    {
                        Client.Instance.Settings.ColorD12 = clrD12;
                        Client.Instance.Settings.ColorModeD12 = policyD12;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD20 = Client.Instance.Settings.ColorD20;
                    ChatDiceColorMode policyD20 = Client.Instance.Settings.ColorModeD20;
                    if (ChatDieColorSetting(lang, ref clrD20, ref policyD20, 20))
                    {
                        Client.Instance.Settings.ColorD20 = clrD20;
                        Client.Instance.Settings.ColorModeD20 = policyD20;
                        Client.Instance.Settings.Save();
                    }

                    uint clrD100 = Client.Instance.Settings.ColorD100;
                    ChatDiceColorMode policyD100 = Client.Instance.Settings.ColorModeD100;
                    if (ChatDieColorSetting(lang, ref clrD100, ref policyD100, 100))
                    {
                        Client.Instance.Settings.ColorD100 = clrD100;
                        Client.Instance.Settings.ColorModeD100 = policyD100;
                        Client.Instance.Settings.Save();
                    }

                    bool bUnifyChatDice = Client.Instance.Settings.UnifyChatDiceRendering;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.unify_chat_dice_rendering") + "###UnifyChatDiceRendering", ref bUnifyChatDice))
                    {
                        Client.Instance.Settings.UnifyChatDiceRendering = bUnifyChatDice;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.unify_chat_dice_rendering.tt"));
                    }

                    bool drawLayerControlsInSidebar = Client.Instance.Settings.DrawSidebarLayerControls;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.draw_sidebar_layer_controls") + "###Draw Sidebar Layer Controls", ref drawLayerControlsInSidebar))
                    {
                        Client.Instance.Settings.DrawSidebarLayerControls = drawLayerControlsInSidebar;
                        Client.Instance.Settings.Save();
                    }

                    float cameraInterpolationRate = Client.Instance.Settings.CameraInterpolationSpeed;
                    ImGui.TextUnformatted(lang.Translate("menu.settings.camera_interpolation_rate"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.camera_interpolation_rate.tt"));
                    }

                    if (ImGui.SliderFloat("##CameraInterpolationRate", ref cameraInterpolationRate, 0.5f, 10f))
                    {
                        Client.Instance.Settings.CameraInterpolationSpeed = cameraInterpolationRate;
                        Client.Instance.Settings.Save();
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode(lang.Translate("menu.settings.category.sound") + "###Sound"))
                {
                    bool bDisableSounds = Client.Instance.Settings.DisableSounds;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.sound.disable_all"), ref bDisableSounds))
                    {
                        Client.Instance.Settings.DisableSounds = bDisableSounds;
                        Client.Instance.Settings.Save();
                    }

                    bool enableChatWindowNotification = Client.Instance.Settings.EnableChatNotification;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.enable_chat_notification"), ref enableChatWindowNotification))
                    {
                        Client.Instance.Settings.EnableChatNotification = enableChatWindowNotification;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.enable_chat_notification.tt"));
                    }

                    ImGui.Text(lang.Translate("menu.settings.sound.master"));
                    float svMaster = Client.Instance.Settings.SoundMasterVolume;
                    if (ImGui.SliderFloat("##MasterVolume", ref svMaster, 0, 1))
                    {
                        Client.Instance.Settings.SoundMasterVolume = svMaster;
                        Client.Instance.Frontend.Sound.NotifyOfVolumeChanges();
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.sound.ui"));
                    float svUI = Client.Instance.Settings.SoundUIVolume;
                    if (ImGui.SliderFloat("##UIVolume", ref svUI, 0, 1))
                    {
                        Client.Instance.Settings.SoundUIVolume = svUI;
                        Client.Instance.Frontend.Sound.NotifyOfVolumeChanges();
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.sound.map_fx"));
                    float svMFX = Client.Instance.Settings.SoundMapFXVolume;
                    if (ImGui.SliderFloat("##MFXVolume", ref svMFX, 0, 1))
                    {
                        Client.Instance.Settings.SoundMapFXVolume = svMFX;
                        Client.Instance.Frontend.Sound.NotifyOfVolumeChanges();
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.sound.assets"));
                    float svA = Client.Instance.Settings.SoundAssetVolume;
                    if (ImGui.SliderFloat("##AssetsVolume", ref svA, 0, 1))
                    {
                        Client.Instance.Settings.SoundAssetVolume = svA;
                        Client.Instance.Frontend.Sound.NotifyOfVolumeChanges();
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.sound.ambiance"));
                    float svAm = Client.Instance.Settings.SoundAmbianceVolume;
                    if (ImGui.SliderFloat("##AmbianceVolume", ref svAm, 0, 1))
                    {
                        Client.Instance.Settings.SoundAmbianceVolume = svAm;
                        Client.Instance.Frontend.Sound.NotifyOfVolumeChanges();
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.sound.music"));
                    float svMs = Client.Instance.Settings.SoundMusicVolume;
                    if (ImGui.SliderFloat("##MusicVolume", ref svMs, 0, 1))
                    {
                        Client.Instance.Settings.SoundMusicVolume = svMs;
                        Client.Instance.Frontend.Sound.NotifyOfVolumeChanges();
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.TreeNode(lang.Translate("menu.settings.category.sound.individual") + "###Individual sound notifications"))
                    {
                        ImGui.TextDisabled(lang.Translate("menu.settings.category.sound.individual.ui"));
                        bool bEnableChatSound = Client.Instance.Settings.EnableSoundChatMessage;
                        if (ImGui.Checkbox(lang.Translate("menu.settings.sound.ui.chat"), ref bEnableChatSound))
                        {
                            Client.Instance.Settings.EnableSoundChatMessage = bEnableChatSound;
                            Client.Instance.Settings.Save();
                        }

                        bool bEnableTurnSound = Client.Instance.Settings.EnableSoundTurnTracker;
                        if (ImGui.Checkbox(lang.Translate("menu.settings.sound.ui.turn"), ref bEnableTurnSound))
                        {
                            Client.Instance.Settings.EnableSoundTurnTracker = bEnableTurnSound;
                            Client.Instance.Settings.Save();
                        }

                        bool bEnablePingSound = Client.Instance.Settings.EnableSoundPing;
                        if (ImGui.Checkbox(lang.Translate("menu.settings.sound.fx.ping"), ref bEnablePingSound))
                        {
                            Client.Instance.Settings.EnableSoundPing = bEnablePingSound;
                            Client.Instance.Settings.Save();
                        }

                        ImGui.TreePop();
                    }

                    ImGui.TreePop();
                }


                if (ImGui.TreeNode(lang.Translate("menu.settings.category.language") + "###Language & Accessibility"))
                {
                    string[] identifiers = new string[Localisation.AllLocales.Count]; 
                    string[] locales = new string[Localisation.AllLocales.Count];
                    for (int i = 0; i < Localisation.AllLocales.Count; i++)
                    {
                        SimpleLanguage val = Localisation.AllLocales[i];
                        identifiers[i] = val.Identifier;
                        locales[i] = val.Locale;
                    }

                    int selected = Math.Max(0, Array.IndexOf(locales, Client.Instance.Settings.Language ?? "en-EN"));
                    if (ImGui.Combo(lang.Translate("menu.settings.language") + "###Language", ref selected, identifiers, identifiers.Length))
                    {
                        Client.Instance.Settings.Language = locales[selected];
                        Client.Instance.Settings.Save();
                        Client.Instance.Lang = Localisation.SwitchLanguage(locales[selected]);
                    }

                    float mSensitivity = Client.Instance.Settings.Sensitivity;
                    ImGui.Text(lang.Translate("menu.settings.sensitivity"));
                    if (ImGui.SliderFloat("##Sensitivity", ref mSensitivity, 0.1f, 10f))
                    {
                        Client.Instance.Settings.Sensitivity = Math.Clamp(mSensitivity, 0.1f, 10f);
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
                        Client.Instance.Settings.ChatBackgroundBrightness = Math.Clamp(mChatBrightness, 0, 1);
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.chat_brightness.tt"));
                    }

                    bool mTextThickShadow = Client.Instance.Settings.TextThickDropShadow;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.thick_drop_shadow"), ref mTextThickShadow))
                    {
                        Client.Instance.Settings.TextThickDropShadow = mTextThickShadow;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.thick_drop_shadow.tt"));
                    }

                    bool enableComprehensiveAuras = Client.Instance.Settings.ComprehensiveAuras;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.comprehensive_auras"), ref enableComprehensiveAuras))
                    {
                        Client.Instance.Settings.ComprehensiveAuras = enableComprehensiveAuras;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.comprehensive_auras.tt"));
                    }

                    if (!enableComprehensiveAuras)
                    {
                        ImGui.BeginDisabled();
                    }

                    ImGui.TextUnformatted(lang.Translate("menu.settings.comprehensive_auras_multiplier"));
                    float cAurasMul = Client.Instance.Settings.ComprehensiveAuraAlphaMultiplier;
                    if (ImGui.SliderFloat("##Aura Opacity", ref cAurasMul, 0, 1))
                    {
                        Client.Instance.Settings.ComprehensiveAuraAlphaMultiplier = cAurasMul;
                        Client.Instance.Settings.Save();
                    }

                    if (!enableComprehensiveAuras)
                    {
                        ImGui.EndDisabled();
                    }

                    int ttAmtMax = Client.Instance.Settings.TurnTrackerSize;
                    ImGui.TextUnformatted(lang.Translate("menu.settings.turn_tracker_size"));
                    if (ImGui.SliderInt("##Turn Tracker Size", ref ttAmtMax, 2, 6))
                    {
                        Client.Instance.Settings.TurnTrackerSize = ttAmtMax;
                        Client.Instance.Settings.Save();
                    }

                    int ttScalingC = (int)Client.Instance.Settings.TurnTrackerScale;
                    string[] ttScalingTexts = { lang.Translate("menu.settings.turn_tracker_scaling.small"), lang.Translate("menu.settings.turn_tracker_scaling.medium"), lang.Translate("menu.settings.turn_tracker_scaling.large") };
                    ImGui.TextUnformatted(lang.Translate("menu.settings.turn_tracker_scaling"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.turn_tracker_scaling.tt"));
                    }

                    if (ImGui.Combo("##Turn Tracker Scaling", ref ttScalingC, ttScalingTexts, ttScalingTexts.Length))
                    {
                        Client.Instance.Settings.TurnTrackerScale = (TurnTrackerScaling)ttScalingC;
                        Client.Instance.Settings.Save();
                    }

                    UISkin dSkin = Client.Instance.Settings.InterfaceSkin;
                    string[] skins = Enum.GetNames(typeof(UISkin)).Select(s => lang.Translate("menu.settings.ui_skin." + s.ToLowerInvariant())).ToArray();
                    int sIdx = (int)dSkin;
                    ImGui.Text(lang.Translate("menu.settings.ui_skin"));
                    if (ImGui.Combo("##UISkinSelector", ref sIdx, skins, skins.Length))
                    {
                        Client.Instance.Settings.InterfaceSkin = dSkin = (UISkin)sIdx;
                        Client.Instance.Settings.Save();
                        Client.Instance.Frontend.GuiWrapper.ChangeSkin(dSkin);
                    }

                    bool bHolidays = Client.Instance.Settings.HolidaySeasons;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.holidays"), ref bHolidays))
                    {
                        Client.Instance.Settings.HolidaySeasons = bHolidays;
                        Client.Instance.Settings.Save();
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
                        Client.Instance.Settings.RaycastMultithreading = (RaycastMultithreadingType)cmtpc;
                        Client.Instance.Settings.Save();
                    }

                    /*
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
                    */

                    bool sNoBranches = Client.Instance.Settings.DisableShaderBranching;
                    bool sShadowsSun = Client.Instance.Settings.EnableSunShadows;
                    bool sShadowsDir = Client.Instance.Settings.EnableDirectionalShadows;
                    bool sUseUBO = Client.Instance.Settings.UseUBO;
                    /*
                    if (ImGui.Checkbox(lang.Translate("menu.settings.disable_branching") + "###Disable Shader Branching", ref sNoBranches))
                    {
                        Client.Instance.Settings.DisableShaderBranching = sNoBranches;
                        Client.Instance.Settings.Save();
                        Client.Instance.Frontend.Renderer.ObjectRenderer.ReloadObjectShader(sShadowsSun, sShadowsDir, sNoBranches, OpenGLUtil.ShouldUseSPIRV);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.disable_branching.tt"));
                    }
                    */

                    if (ImGui.Checkbox(lang.Translate("menu.settings.use_ubo") + "###Use UBO", ref sUseUBO))
                    {
                        Client.Instance.Settings.UseUBO = sUseUBO;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.use_ubo.tt"));
                    }

                    bool multithreadDxtC = Client.Instance.Settings.MultithreadedTextureCompression;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.multithread_texture_compression") + "###MultithreadTextureCompression", ref multithreadDxtC))
                    {
                        Client.Instance.Settings.MultithreadedTextureCompression = multithreadDxtC;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.multithread_texture_compression.tt"));
                    }

                    bool offscreenParticles = Client.Instance.Settings.OffscreenParticleUpdates;
                    if (ImGui.Checkbox(lang.Translate("menu.settings.offscreen_particle_updates") + "###OffscreenParticleUpdates", ref offscreenParticles))
                    {
                        Client.Instance.Settings.OffscreenParticleUpdates = offscreenParticles;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.offscreen_particle_updates.tt"));
                    }

                    ImGui.Text(lang.Translate("menu.settings.gl_context_policy"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.gl_context_policy.tt"));
                    }

                    string[] glps = { lang.Translate("menu.settings.gl_context_policy.implicit"), lang.Translate("menu.settings.gl_context_policy.checked"), lang.Translate("menu.settings.gl_context_policy.explicit") };
                    int glpv = (int)Client.Instance.Settings.ContextHandlingMode;
                    if (ImGui.Combo("##GLContextPolicy", ref glpv, glps, 3))
                    {
                        Client.Instance.Settings.ContextHandlingMode = (GLContextHandlingMode)glpv;
                        Client.Instance.Settings.Save();
                    }

                    ImGui.Text(lang.Translate("menu.settings.al_context_policy"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("menu.settings.al_context_policy.tt"));
                    }

                    // Note that gl_context_policy.xxxxxxx here is not a typo, context mode text matches
                    string[] alps = { lang.Translate("menu.settings.gl_context_policy.implicit"), lang.Translate("menu.settings.gl_context_policy.checked"), lang.Translate("menu.settings.gl_context_policy.explicit") };
                    int alpv = (int)Client.Instance.Settings.AudioContextHandlingMode;
                    if (ImGui.Combo("##ALContextPolicy", ref alpv, alps, 3))
                    {
                        Client.Instance.Settings.AudioContextHandlingMode = (GLContextHandlingMode)alpv;
                        Client.Instance.Settings.Save();
                    }

                    int uiBufCap = Client.Instance.Settings.UIDrawBuffersCapacity;
                    if (ImGui.InputInt($"{lang.Translate("menu.settings.ui_buffers_capacity")}###UIBufferCapacity", ref uiBufCap))
                    {
                        Client.Instance.Settings.UIDrawBuffersCapacity = uiBufCap;
                        Client.Instance.Frontend.GuiWrapper.UIBuffersCapacity = uiBufCap;
                        Client.Instance.Settings.Save();
                    }

                    if (!bDebug)
                    {
                        ImGui.EndDisabled();
                    }

                    ImGui.TreePop();
                }

            }

            ImGui.EndChild();
        }

        private static bool ChatDieColorSetting(SimpleLanguage lang, ref uint clr, ref ChatDiceColorMode mode, int die)
        {
            bool ret = false;
            if (ImGui.TreeNode(lang.Translate($"menu.settings.die.{die}.color_settings") + $"###Die{die}ColorSettingsContextWindow"))
            {
                GuiRenderer uiRoot = Client.Instance.Frontend.Renderer.GuiRenderer;
                (Vector2, Vector2) dieImage = die switch
                {
                    2 => uiRoot.ChatIconD2.BoundsSingularTuple,
                    4 => uiRoot.ChatIconD4.BoundsSingularTuple,
                    6 => uiRoot.ChatIconD6.BoundsSingularTuple,
                    8 => uiRoot.ChatIconD8.BoundsSingularTuple,
                    10 => uiRoot.ChatIconD10.BoundsSingularTuple,
                    12 => uiRoot.ChatIconD12.BoundsSingularTuple,
                    20 => uiRoot.ChatIconD20.BoundsSingularTuple,
                    100 => uiRoot.ChatIconD10.BoundsPrimaryTuple,
                    _ => uiRoot.ChatIconD20.BoundsSingularTuple,
                };

                Vector4 clrVec = Extensions.Vec4FromAbgr(clr);
                ImGui.TextUnformatted(lang.Translate($"menu.settings.die.chat_color_policy"));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate($"menu.settings.die.chat_color_policy.tt"));
                }

                string[] modes = { lang.Translate("menu.settings.chat_die_color.set"), lang.Translate("menu.settings.chat_die_color.own"), lang.Translate("menu.settings.chat_die_color.sender") };
                int mI = (int)mode;
                if (ImGui.Combo($"##Die{die}ColorPolicy", ref mI, modes, modes.Length))
                {
                    mode = (ChatDiceColorMode)mI;
                    ret = true;
                }

                Vector2 here = ImGui.GetCursorPos();
                ImGui.Image(uiRoot.DiceIconAtlas, new Vector2(24, 24), dieImage.Item1, dieImage.Item2, clrVec);
                if (die == 100)
                {
                    ImGui.SetCursorPos(here); 
                    ImGui.Image(uiRoot.DiceIconAtlas, new Vector2(24, 24), uiRoot.ChatIconD10.BoundsSecondaryStart, uiRoot.ChatIconD10.BoundsSecondaryEnd, clrVec);
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(lang.Translate($"menu.settings.die.chat_color"));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate($"menu.settings.die.chat_color.tt"));
                }

                if (ImGui.ColorPicker4($"##Die{die}ColorValue", ref clrVec, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoOptions | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoTooltip))
                {
                    clr = clrVec.Abgr();
                    ret = true;
                }

                ImGui.TreePop();
            }

            return ret;
        }

        // https://stackoverflow.com/questions/4580263/how-to-open-in-default-browser-in-c-sharp
        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public void Update()
        {
        }

        public enum MenuMode
        {
            None,
            Join,
            Host,
            Settings,
            Credits,
            Changelog
        }
    }
}
