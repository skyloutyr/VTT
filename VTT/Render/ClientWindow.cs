namespace VTT.Render
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.GLFW;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render.Gui;
    using VTT.Sound;
    using VTT.Util;

    public class ClientWindow
    {
        public ImGuiWrapper GuiWrapper { get; private set; }
        public WindowRenderer Renderer { get; set; }
        public SoundManager Sound { get; set; }
        public FFmpegWrapper FFmpegWrapper { get; set; }
        public GLFWWindowHandler GameHandle { get; set; }
        public AsyncTextureUploader TextureUploader { get; private set; }
        public AsyncAssetLoader AssetLoader { get; private set; }

        private readonly ConcurrentQueue<RepeatableActionContainer> _actionsToDo = new ConcurrentQueue<RepeatableActionContainer>();
        private readonly ConcurrentQueue<RepeatableActionContainer> _actionsToDoNextFrame = new ConcurrentQueue<RepeatableActionContainer>();

        public int UpdaterExitCode { get; set; } = -2;

        public ULongCounter UpdatesExisted { get; set; } = new ULongCounter();
        public ULongCounter FramesExisted { get; set; } = new ULongCounter();

        public int Width => this._lastFramebufferWidth > 0 ? this._lastFramebufferWidth : this.GameHandle.FramebufferSize.Value.Width;
        public int Height => this._lastFramebufferHeight > 0 ? this._lastFramebufferHeight : this.GameHandle.FramebufferSize.Value.Height;
        public int GlfwWidth => this._lastWindowWidth > 0 ? this._lastWindowWidth : this.GameHandle.Size.Value.Width;
        public int GlfwHeight => this._lastWindowHeight > 0 ? this._lastWindowHeight : this.GameHandle.Size.Value.Height;

        public float MouseX => this.GameHandle.MousePosition.Value.X;
        public float MouseY => this.GameHandle.MousePosition.Value.Y;
        public bool GLDebugEnabled { get; private set; }

        private Thread _glThread;

        public ClientWindow()
        {
            Configuration.Default.PreferContiguousImageBuffers = true;
            string postfix = "";
            DateTime dt = DateTime.Now;
            if (Client.Instance.Settings.HolidaySeasons)
            {
                if ((dt.Month == 12 && dt.Day >= 25) || (dt.Month == 1 && dt.Day <= 7))
                {
                    postfix = "-ny";
                }

                if ((dt.Month == 10 && dt.Day == 31))
                {
                    postfix = "-hw";
                }
            }

            GLFWimage[] windowIcon = this.LoadIcon(postfix);
            bool dbg = ArgsManager.TryGetValue<string>(LaunchArgumentKey.GLDebugMode, out _);
            if (Client.Instance.Settings.Resolution.Width <= 0 || Client.Instance.Settings.Resolution.Height <= 0)
            {
                Client.Instance.Settings.Resolution = new Size(1366, 768);
            }

            this.FFmpegWrapper = new FFmpegWrapper();
            this.FFmpegWrapper.Init();
            this.GameHandle = new GLFWWindowHandler(
                new GameWindowSettings()
                {
                    RenderFrequency = -1,
                    UpdateFrequency = 60
                },
                new NativeWindowSettings()
                {
                    API = ClientAPI.OpenGL,
                    APIVersion = new Version(3, 3),
                    ForwardCompatible = true,
                    DebugContext = dbg,
                    Fullscreen = Client.Instance.Settings.ScreenMode != ClientSettings.FullscreenMode.Normal,
                    NumberOfSamples = Client.Instance.Settings.MSAA switch
                    {
                        ClientSettings.MSAAMode.Disabled => 0,
                        ClientSettings.MSAAMode.Low => 2,
                        ClientSettings.MSAAMode.Standard => 4,
                        ClientSettings.MSAAMode.High => 8,
                        _ => 0
                    },

                    Size = new Size(Client.Instance.Settings.Resolution.Width, Client.Instance.Settings.Resolution.Height),
                    Icon = windowIcon,
                    DepthBits = 24,
                    StencilBits = 8,
                }
            );

            this.GameHandle.Title.Value = "VTT" + Program.Version.ToString();
            this.GameHandle.VSync.Value = Client.Instance.Settings.VSync;
            this.Renderer = new WindowRenderer(this);
            this.GameHandle.RenderFrame += this.Instance_RenderFrame;
            this.GameHandle.UpdateFrame += this.Instance_UpdateFrame;
            this.GameHandle.Load += this.Instance_SetupHander;
            this.GameHandle.TextInput += this.Instance_TextInput;
            this.GameHandle.MouseWheel += this.Instance_MouseWheel;
            this.GameHandle.MouseMove += this.Instance_MouseMove;
            this.GameHandle.FileDrop += this.Instance_FileDrop;
            this.GameHandle.KeyDown += this.Instance_KeyDown;
            this.GameHandle.KeyRepeat += this.Instance_KeyRepeat;
            this.GameHandle.KeyUp += this.Instance_KeyUp;
            this.GameHandle.MouseDown += this.Instance_MouseDown;
            this.GameHandle.MouseUp += this.Instance_MouseUp;
            this.GameHandle.FocusedChanged += this.Instance_Focus;
        }

        private void Instance_KeyRepeat(KeyEventData obj) =>
            //ImGui creates key repeats by its own, no need to pass key repeat there
            this.Renderer?.MapRenderer?.HandleKeys(obj);

        private unsafe GLFWimage[] LoadIcon(string postfix)
        {
            using Image<Rgba32> normal = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.icon-beta{postfix}.png");
            using Image<Rgba32> small = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.icon-beta_small{postfix}.png");
            using Image<Rgba32> smaller = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.icon-beta_smaller{postfix}.png");
            using Image<Rgba32> smallest = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.icon-beta_smallest{postfix}.png");
            Span<byte> normalBytes = new Span<byte>(new byte[sizeof(Rgba32) * 256 * 256]);
            Span<byte> smallBytes = new Span<byte>(new byte[sizeof(Rgba32) * 48 * 48]);
            Span<byte> smallerBytes = new Span<byte>(new byte[sizeof(Rgba32) * 32 * 32]);
            Span<byte> smallestBytes = new Span<byte>(new byte[sizeof(Rgba32) * 16 * 16]);
            normal.CopyPixelDataTo(normalBytes);
            small.CopyPixelDataTo(smallBytes);
            smaller.CopyPixelDataTo(smallerBytes);
            smallest.CopyPixelDataTo(smallestBytes);
            GLFWimage[] ret = new GLFWimage[4];
            ret[0] = new GLFWimage(256, 256, normalBytes.ToArray());
            ret[1] = new GLFWimage(48, 48, smallBytes.ToArray());
            ret[2] = new GLFWimage(32, 32, smallerBytes.ToArray());
            ret[3] = new GLFWimage(16, 16, smallestBytes.ToArray());
            return ret;
        }

        private void Instance_Focus(bool obj) => this.GuiWrapper.Focus(obj);
        private void Instance_MouseDown(MouseEventData obj) => this.GuiWrapper.MouseKey(obj.Button, obj.Mods, false);
        private void Instance_MouseUp(MouseEventData obj) => this.GuiWrapper.MouseKey(obj.Button, obj.Mods, true);
        private void Instance_MouseMove(Vector2 pos) => this.GuiWrapper.MouseMove(pos);
        private void Instance_KeyUp(KeyEventData obj) => this.GuiWrapper.KeyEvent(obj.Key, obj.Scancode, obj.Mods, false, true);

        private readonly ConcurrentQueue<Action> _gpuReqs = new ConcurrentQueue<Action>();
        public void EnqueueOrExecuteTask(Action a)
        {
            if (this.CheckThread())
            {
                a();
            }
            else
            {
                this._gpuReqs.Enqueue(a);
            }
        }

        public void PushNotification()
        {
            this.EnqueueTask(() =>
            {
                if (!this.GameHandle.IsFocused)
                {
                    unsafe
                    {
                        this.GameHandle.RequestWindowAttention();
                    }
                }
            });
        }

        public void EnqueueTask(Action a) => this._actionsToDo.Enqueue((RepeatableActionContainer)a);
        public void EnqueueSpecializedTask(RepeatableActionContainer a) => this._actionsToDo.Enqueue(a);
        public void EnqueueTaskNextUpdate(Action a) => this._actionsToDoNextFrame.Enqueue((RepeatableActionContainer)a);
        public void EnqueueSpecializedTaskNextUpdate(RepeatableActionContainer a) => this._actionsToDoNextFrame.Enqueue(a);

        private ClientSettings.FullscreenMode? _lastFsMode;
        private Size? oldScreenSize;
        private Point? oldPos;

        public void SwitchFullscreen(ClientSettings.FullscreenMode? fsMode)
        {
            bool toggled = !fsMode.HasValue;
            ClientSettings.FullscreenMode switchTo = fsMode ?? this._lastFsMode ?? (!this.GameHandle.IsFullscreen ? ClientSettings.FullscreenMode.Fullscreen : ClientSettings.FullscreenMode.Normal);
            this._lastFsMode = toggled ? this.GameHandle.IsFullscreen ? ClientSettings.FullscreenMode.Fullscreen : Client.Instance.Settings.ScreenMode : null;

            unsafe
            {
                IntPtr win = Client.Instance.Frontend.GameHandle.GLFWWindow;
                IntPtr m = Glfw.GetWindowMonitor(win);
                GLFWvidmode* vm = Glfw.GetVideoMode(m == IntPtr.Zero ? Glfw.GetPrimaryMonitor() : m);
                bool wasDecorated = Client.Instance.Frontend.GameHandle.Decorated;
                int w = vm->width;
                int h = vm->height;

                switch (switchTo)
                {
                    case ClientSettings.FullscreenMode.Normal:
                    {
                        Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Normal;
                        int ow = this.oldScreenSize?.Width ?? 1366;
                        int oh = this.oldScreenSize?.Height ?? 768;
                        int ox = this.oldPos?.X ?? 32;
                        int oy = this.oldPos?.Y ?? 32;
                        Glfw.HideWindow(win);
                        Glfw.SetWindowMonitor(win, IntPtr.Zero, ox, oy, ow, oh, Glfw.DontCare);
                        Glfw.SetWindowAttrib(win, WindowProperty.Resizable, true);
                        Glfw.SetWindowAttrib(win, WindowProperty.Decorated, true);
                        Glfw.SetWindowAttrib(win, WindowProperty.Floating, false);
                        Glfw.ShowWindow(win);
                        if (!wasDecorated)
                        {
                            Client.Instance.Frontend.GameHandle.Decorated.Value = true;
                        }

                        break;
                    }

                    case ClientSettings.FullscreenMode.Fullscreen:
                    {
                        Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Fullscreen;
                        Glfw.HideWindow(win);
                        Glfw.SetWindowMonitor(win, Glfw.GetPrimaryMonitor(), 0, 0, w, h, Glfw.DontCare);
                        Glfw.SetWindowAttrib(win, WindowProperty.Decorated, false);
                        Glfw.SetWindowAttrib(win, WindowProperty.Floating, false);
                        Glfw.ShowWindow(win);
                        Glfw.GetWindowPos(win, out int wx, out int wy);
                        oldPos = new Point(wx, wy);
                        Client.Instance.Frontend.GameHandle.Decorated.Value = false;
                        break;
                    }

                    case ClientSettings.FullscreenMode.Borderless:
                    {
                        Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Borderless;
                        Glfw.HideWindow(win);
                        Glfw.SetWindowAttrib(win, WindowProperty.Decorated, false);
                        Glfw.SetWindowAttrib(win, WindowProperty.Floating, true);
                        Client.Instance.Frontend.GameHandle.Decorated.Value = false;
                        Glfw.SetWindowMonitor(win, Glfw.GetPrimaryMonitor(), 0, 0, w, h, Glfw.DontCare);
                        Glfw.ShowWindow(win);
                        break;
                    }
                }
            }
        }

        private void Instance_KeyDown(KeyEventData obj)
        {
            if (obj.Key == Keys.F11 && this.GameHandle.IsFocused)
            {
                this.SwitchFullscreen(null);
            }

            this.GuiWrapper.KeyEvent(obj.Key, obj.Scancode, obj.Mods, false, false);
            this.Renderer?.MapRenderer?.HandleKeys(obj);
        }

        private void Instance_FileDrop(string[] obj) => this.Renderer.GuiRenderer.HandleFileDrop(obj);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0022:Use expression body for methods", Justification = "False positive - only applicable in DEBUG builds due to preprocessor directives")]
        public void Run()
        {
#if DEBUG
            this.GameHandle.Run();
#else
            try
            {
                this.GameHandle.Run();
            }
            catch (Exception e)
            {
                Logger l = Client.Instance.Logger;
                if (l != null)
                {
                    l.Log(LogLevel.Fatal, "A fatal exception has occured:");
                    l.Exception(LogLevel.Fatal, e);
                }

                throw;
            }
#endif
        }

        private void Instance_MouseWheel(double offsetX, double offsetY)
        {
            this.GuiWrapper.MouseScroll(new Vector2((float)offsetX, (float)offsetY));
            this.Renderer.ScrollWheel((float)offsetX, (float)offsetY);
        }

        private void Instance_TextInput(TextTypeEventData obj) => this.GuiWrapper.PressChar(obj.Unicode);

        private void GL_DebugCallback(DebugMessageSource source, DebugMessageType type, uint id, DebugMessageSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            if (severity != DebugMessageSeverity.Notification)
            {
                string msg = Marshal.PtrToStringAnsi(message, length);
                Client.Instance.Logger.Log(severity switch
                { 
                    DebugMessageSeverity.High => LogLevel.Error,
                    DebugMessageSeverity.Medium => LogLevel.Warn,
                    DebugMessageSeverity.Low => LogLevel.Info,
                    _ => LogLevel.Debug
                }, "GLMessage: " + msg);
            }
        }

        private int _kaTimer;
        private void Instance_UpdateFrame()
        {
            this.UpdatesExisted.Increment();
            NetClient nc = Client.Instance.NetClient;
            if (nc != null)
            {
                if (nc.IsConnected)
                {
                    if (++this._kaTimer >= 300)
                    {
                        this._kaTimer = 0;
                        try
                        {
                            new PacketKeepalivePing() { Side = false }.Send();
                        }
                        catch
                        {
                            // NOOP
                        }
                    }

                    long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    if (now - nc.LastPingResponseTime > Client.Instance.TimeoutInterval)
                    {
                        Client.Instance.Disconnect(DisconnectReason.Timeout);
                    }
                }
            }

            this.Sound.Update();
            this.Renderer.Update();
            this.GuiWrapper.Update();
            GuiRenderer.Instance.Update();
            Client.Instance.AssetManager.ClientAssetLibrary?.Pulse();
            while (!this._actionsToDo.IsEmpty)
            {
                if (this._actionsToDo.TryDequeue(out RepeatableActionContainer a))
                {
                    if (!a.Invoke())
                    {
                        this._actionsToDoNextFrame.Enqueue(a);
                    }
                }
            }

            while (!this._actionsToDoNextFrame.IsEmpty)
            {
                if (this._actionsToDoNextFrame.TryDequeue(out RepeatableActionContainer a))
                {
                    this._actionsToDo.Enqueue(a);
                }
            }

            Client.Instance.NetworkIn.TickTimeframe();
            Client.Instance.NetworkOut.TickTimeframe();
            Server.Instance?.NetworkIn.TickTimeframe();
            Server.Instance?.NetworkOut.TickTimeframe();
        }

        private GL.DebugProcCallback _debugProc;
        private void Instance_SetupHander()
        {
            this._glThread = Thread.CurrentThread;
            if (ArgsManager.TryGetValue(LaunchArgumentKey.GLDebugMode, out string val))
            {
                this.GLDebugEnabled = true;
                this._debugProc = this.GL_DebugCallback;
                GL.DebugMessageCallback(Marshal.GetFunctionPointerForDelegate(this._debugProc), IntPtr.Zero);
            }

            StbDxt.Init();
            this.TextureUploader = new AsyncTextureUploader();
            this.AssetLoader = new AsyncAssetLoader();
            this.Sound = new SoundManager();
            this.Sound.Init();
            this.GuiWrapper = new ImGuiWrapper();
            this.Renderer.Create();
            this.GuiWrapper.RebuildFontAtlas();
            string updater = Path.Combine(IOVTT.AppDir, "VTTUpdater.exe");
            if (File.Exists(updater))
            {
                new Thread(() =>
                {
                    try
                    {
                        System.Diagnostics.Process updaterProcess = new System.Diagnostics.Process();
                        updaterProcess.StartInfo.FileName = updater;
                        updaterProcess.StartInfo.UseShellExecute = false;
                        updaterProcess.StartInfo.Arguments = "--background";
                        updaterProcess.Start();
                        updaterProcess.WaitForExit();
                        this.UpdaterExitCode = updaterProcess.ExitCode;
                    }
                    catch
                    {
                        // NOOP
                    }
                })
                { IsBackground = true }.Start();
            }
        }

        public bool CheckThread() => Thread.CurrentThread == this._glThread;

        private bool _lastFocusState;
        private void Instance_RenderFrame(double dt)
        {
            this.CheckResize();

            this.FramesExisted.Increment();

            if (this._lastFocusState != this.GameHandle.IsFocused)
            {
                this._lastFocusState = this.GameHandle.IsFocused;
                if (this._lastFocusState)
                {
                    Glfw.SwapInterval(this.GameHandle.MinSwapInterval);
                }
                else
                {
                    switch (Client.Instance.Settings.UnfocusedFramerate)
                    {
                        case ClientSettings.UnfocusedFramerateCap.Native:
                        {
                            unsafe
                            {
                                Glfw.SwapInterval(1);
                            }

                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.High:
                        {
                            Glfw.SwapInterval(1);
                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.Medium:
                        {
                            Glfw.SwapInterval(2);
                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.Low:
                        {
                            Glfw.SwapInterval(6);
                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.Lowest:
                        {
                            Glfw.SwapInterval(60);
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }
            }

            this.TextureUploader.ProcessPrimary();
            this.Renderer.Render(dt);
            while (!this._gpuReqs.IsEmpty)
            {
                if (this._gpuReqs.TryDequeue(out Action a))
                {
                    a();
                }
            }
        }


        private int _lastWindowWidth;
        private int _lastWindowHeight;
        private int _lastFramebufferWidth;
        private int _lastFramebufferHeight;
        private unsafe void CheckResize()
        {
            Glfw.GetWindowSize(this.GameHandle.GLFWWindow, out int ww, out int wh);
            Glfw.GetFramebufferSize(this.GameHandle.GLFWWindow, out int fw, out int fh);

            if (ww != this._lastWindowWidth || wh != this._lastWindowHeight)
            {
                this._lastWindowWidth = ww;
                this._lastWindowHeight = wh;
                if (ww != 0 && wh != 0) // Don't save zero-size
                {
                    Client.Instance.Frontend.GameHandle.Size.ChangeWithoutNotify(new Size(ww, wh));
                    Client.Instance.Settings.Resolution = new Size(ww, wh); // Actually window size
                    Client.Instance.Settings.Save();
                }
            }

            if (fw != this._lastFramebufferWidth || fh != this._lastFramebufferHeight)
            {
                this._lastFramebufferWidth = fw;
                this._lastFramebufferHeight = fh;
                if (fw != 0 && fh != 0)
                {
                    Client.Instance.Frontend.GameHandle.FramebufferSize.ChangeWithoutNotify(new Size(fw, fh));
                    this.Renderer.Resize(fw, fh);
                    this.GuiWrapper.Resize(fw, fh);
                }

                this.Renderer.SetWindowState(fw > 0 && fh > 0);
            }

        }
    }
}
