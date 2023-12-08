namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Windowing.Common;
    using OpenTK.Windowing.Common.Input;
    using OpenTK.Windowing.Desktop;
    using OpenTK.Windowing.GraphicsLibraryFramework;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
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
        public GameWindow GameHandle { get; set; }

        public ConcurrentQueue<Action> ActionsToDo { get; } = new ConcurrentQueue<Action>();

        public int UpdaterExitCode { get; set; } = -2;

        public ulong UpdatesExisted { get; set; }
        public ulong FramesExisted { get; set; }

        public int Width => this._lastFramebufferWidth > 0 ? this._lastFramebufferWidth : this.GameHandle.Size.X;
        public int Height => this._lastFramebufferHeight > 0 ? this._lastFramebufferHeight : this.GameHandle.Size.Y;
        public int GlfwWidth => this._lastWindowWidth > 0 ? this._lastWindowWidth : this.GameHandle.Size.X;
        public int GlfwHeight => this._lastWindowHeight > 0 ? this._lastWindowHeight : this.GameHandle.Size.Y;

        public float MouseX => this.GameHandle.MousePosition.X;
        public float MouseY => this.GameHandle.MousePosition.Y;

        private Thread _glThread;

        public ClientWindow()
        {
            Configuration.Default.PreferContiguousImageBuffers = true;
            WindowIcon windowIcon = this.LoadIcon();
            OpenTK.Windowing.Common.ContextFlags winFlags = OpenTK.Windowing.Common.ContextFlags.ForwardCompatible;
            if (ArgsManager.TryGetValue("gldebug", out string val))
            {
                winFlags |= OpenTK.Windowing.Common.ContextFlags.Debug;
            }

            if (Client.Instance.Settings.Resolution.Width <= 0 || Client.Instance.Settings.Resolution.Height <= 0)
            {
                Client.Instance.Settings.Resolution = new Size(1366, 768);
            }

            this.FFmpegWrapper = new FFmpegWrapper();
            this.FFmpegWrapper.Init();
            this.GameHandle = new GameWindow(
                new GameWindowSettings()
                {
                    RenderFrequency = 0,
                    UpdateFrequency = 60
                },
                new NativeWindowSettings()
                {
                    API = OpenTK.Windowing.Common.ContextAPI.OpenGL,
                    APIVersion = new Version(3, 3),
                    AutoLoadBindings = true,
                    Flags = winFlags,
                    IsEventDriven = false,
                    WindowState = Client.Instance.Settings.ScreenMode == ClientSettings.FullscreenMode.Fullscreen ? OpenTK.Windowing.Common.WindowState.Fullscreen : OpenTK.Windowing.Common.WindowState.Normal,
                    NumberOfSamples = Client.Instance.Settings.MSAA switch
                    {
                        ClientSettings.MSAAMode.Disabled => 0,
                        ClientSettings.MSAAMode.Low => 2,
                        ClientSettings.MSAAMode.Standard => 4,
                        ClientSettings.MSAAMode.High => 8,
                        _ => 0
                    },

                    Profile = OpenTK.Windowing.Common.ContextProfile.Core,
                    Size = new OpenTK.Mathematics.Vector2i(Client.Instance.Settings.Resolution.Width, Client.Instance.Settings.Resolution.Height),
                    Icon = windowIcon,
                    DepthBits = 24,
                    StencilBits = 8,
                }
            )
            {
                VSync = Client.Instance.Settings.VSync,
                Title = "VTT " + Program.Version.ToString(),
            };

            this.Renderer = new WindowRenderer(this);
            this.GameHandle.Resize += this.Instance_Resize;
            this.GameHandle.RenderFrame += this.Instance_RenderFrame;
            this.GameHandle.UpdateFrame += this.Instance_UpdateFrame;
            this.GameHandle.Load += this.Instance_SetupHander;
            this.GameHandle.TextInput += this.Instance_TextInput;
            this.GameHandle.MouseWheel += this.Instance_MouseWheel;
            this.GameHandle.MouseMove += this.Instance_MouseMove;
            this.GameHandle.FileDrop += this.Instance_FileDrop;
            this.GameHandle.KeyDown += this.Instance_KeyDown;
            this.GameHandle.KeyUp += this.Instance_KeyUp;
            this.GameHandle.MouseDown += this.Instance_MouseDown;
            this.GameHandle.MouseUp += this.Instance_MouseUp;
            this.GameHandle.FocusedChanged += this.Instance_Focus;
        }

        private unsafe WindowIcon LoadIcon()
        {
            using Image<Rgba32> normal = IOVTT.ResourceToImage<Rgba32>("VTT.Embed.icon-beta.png");
            using Image<Rgba32> small = IOVTT.ResourceToImage<Rgba32>("VTT.Embed.icon-beta_small.png");
            using Image<Rgba32> smaller = IOVTT.ResourceToImage<Rgba32>("VTT.Embed.icon-beta_smaller.png");
            using Image<Rgba32> smallest = IOVTT.ResourceToImage<Rgba32>("VTT.Embed.icon-beta_smallest.png");
            Span<byte> normalBytes = new Span<byte>(new byte[sizeof(Rgba32) * 256 * 256]);
            Span<byte> smallBytes = new Span<byte>(new byte[sizeof(Rgba32) * 48 * 48]);
            Span<byte> smallerBytes = new Span<byte>(new byte[sizeof(Rgba32) * 32 * 32]);
            Span<byte> smallestBytes = new Span<byte>(new byte[sizeof(Rgba32) * 16 * 16]);
            normal.CopyPixelDataTo(normalBytes);
            small.CopyPixelDataTo(smallBytes);
            smaller.CopyPixelDataTo(smallerBytes);
            smallest.CopyPixelDataTo(smallestBytes);
            WindowIcon ret = new WindowIcon(new OpenTK.Windowing.Common.Input.Image[4] {
                new OpenTK.Windowing.Common.Input.Image(256, 256, normalBytes.ToArray()),
                new OpenTK.Windowing.Common.Input.Image(48, 48, smallBytes.ToArray()),
                new OpenTK.Windowing.Common.Input.Image(32, 32, smallerBytes.ToArray()),
                new OpenTK.Windowing.Common.Input.Image(16, 16, smallestBytes.ToArray())
            });

            return ret;
        }

        private void Instance_Focus(OpenTK.Windowing.Common.FocusedChangedEventArgs obj) => this.GuiWrapper.Focus(obj.IsFocused);
        private void Instance_MouseDown(OpenTK.Windowing.Common.MouseButtonEventArgs obj) => this.GuiWrapper.MouseKey(obj.Button, obj.Modifiers, false);
        private void Instance_MouseUp(OpenTK.Windowing.Common.MouseButtonEventArgs obj) => this.GuiWrapper.MouseKey(obj.Button, obj.Modifiers, true);
        private void Instance_MouseMove(OpenTK.Windowing.Common.MouseMoveEventArgs obj) => this.GuiWrapper.MouseMove(obj.Position);
        private void Instance_KeyUp(OpenTK.Windowing.Common.KeyboardKeyEventArgs obj) => this.GuiWrapper.KeyEvent(obj.Key, obj.ScanCode, obj.Modifiers, obj.IsRepeat, true);

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
            this.ActionsToDo.Enqueue(() =>
            {
                if (!this.GameHandle.IsFocused)
                {
                    unsafe
                    {
                        GLFW.RequestWindowAttention(this.GameHandle.WindowPtr);
                    }
                }
            });
        }

        private ClientSettings.FullscreenMode? _lastFsMode;
        private Size? oldScreenSize;
        private Point? oldPos;

        public void SwitchFullscreen(ClientSettings.FullscreenMode? fsMode)
        {
            bool toggled = !fsMode.HasValue;
            ClientSettings.FullscreenMode switchTo = fsMode.HasValue ? fsMode.Value :
                this._lastFsMode.HasValue ? this._lastFsMode.Value :
                    this.GameHandle.WindowState != WindowState.Fullscreen ? ClientSettings.FullscreenMode.Fullscreen : ClientSettings.FullscreenMode.Normal;

            this._lastFsMode = toggled ? this.GameHandle.WindowState == WindowState.Fullscreen ? ClientSettings.FullscreenMode.Fullscreen : Client.Instance.Settings.ScreenMode : null;

            unsafe
            {
                VideoMode* vModePtr = GLFW.GetVideoMode((OpenTK.Windowing.GraphicsLibraryFramework.Monitor*)Client.Instance.Frontend.GameHandle.CurrentMonitor.Pointer);
                Window* win = Client.Instance.Frontend.GameHandle.WindowPtr;
                bool wasDecorated = Client.Instance.Frontend.GameHandle.WindowBorder != WindowBorder.Hidden;
                int w = vModePtr->Width;
                int h = vModePtr->Height;

                switch (switchTo)
                {
                    case ClientSettings.FullscreenMode.Normal:
                    {
                        Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Normal;
                        int ow = this.oldScreenSize?.Width ?? 1366;
                        int oh = this.oldScreenSize?.Height ?? 768;
                        int ox = this.oldPos?.X ?? 32;
                        int oy = this.oldPos?.Y ?? 32;
                        Client.Instance.Frontend.GameHandle.WindowState = WindowState.Normal;
                        Client.Instance.Frontend.GameHandle.Location = new OpenTK.Mathematics.Vector2i(ox, oy);
                        GLFW.HideWindow(win);
                        Client.Instance.Frontend.GameHandle.Location = new OpenTK.Mathematics.Vector2i(ox, oy);
                        GLFW.ShowWindow(win);
                        if (!wasDecorated)
                        {
                            Client.Instance.Frontend.GameHandle.WindowBorder = WindowBorder.Resizable;
                        }

                        break;
                    }

                    case ClientSettings.FullscreenMode.Fullscreen:
                    {
                        Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Fullscreen;
                        oldScreenSize = new Size(vModePtr->Width, vModePtr->Height);
                        GLFW.GetWindowPos(win, out int wx, out int wy);
                        oldPos = new Point(wx, wy);
                        Client.Instance.Frontend.GameHandle.WindowState = WindowState.Fullscreen;
                        Client.Instance.Frontend.GameHandle.WindowBorder = WindowBorder.Hidden;
                        break;
                    }

                    case ClientSettings.FullscreenMode.Borderless:
                    {
                        Client.Instance.Settings.ScreenMode = ClientSettings.FullscreenMode.Borderless;
                        Client.Instance.Frontend.GameHandle.WindowBorder = WindowBorder.Hidden;
                        Client.Instance.Frontend.GameHandle.CenterWindow();
                        Client.Instance.Frontend.GameHandle.Location = new OpenTK.Mathematics.Vector2i(0, 0);
                        break;
                    }
                }
            }
        }

        private void Instance_KeyDown(OpenTK.Windowing.Common.KeyboardKeyEventArgs obj)
        {
            if (!obj.IsRepeat && obj.Key == Keys.F11 && this.GameHandle.IsFocused)
            {
                this.SwitchFullscreen(null);
            }

            this.GuiWrapper.KeyEvent(obj.Key, obj.ScanCode, obj.Modifiers, obj.IsRepeat, false);
            this.Renderer?.MapRenderer?.HandleKeys(obj);
        }

        private void Instance_FileDrop(OpenTK.Windowing.Common.FileDropEventArgs obj) => this.Renderer.GuiRenderer.HandleFileDrop(obj);

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

        private void Instance_MouseWheel(OpenTK.Windowing.Common.MouseWheelEventArgs obj)
        {
            this.GuiWrapper.MouseScroll(obj.Offset);
            this.Renderer.ScrollWheel(obj.OffsetX, obj.OffsetY);
        }

        private void Instance_TextInput(OpenTK.Windowing.Common.TextInputEventArgs obj)
        {
            unsafe
            {
                int uni = obj.Unicode;
                this.GuiWrapper.PressChar(*(uint*)&uni);
            }
        }

        private DebugSeverity _argsGlSeverity;
        private DebugProc _glDebugProc;
        private void GL_DebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            if (type == DebugType.DebugTypeError && severity <= this._argsGlSeverity && severity != DebugSeverity.DontCare)
            {
                string msg = Marshal.PtrToStringAnsi(message, length);
                Client.Instance.Logger.Log(LogLevel.Error, "GLMessage: " + msg);
            }
        }

        private int _kaTimer;
        private void Instance_UpdateFrame(OpenTK.Windowing.Common.FrameEventArgs obj)
        {
            ++this.UpdatesExisted;
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
            this.Renderer.Update(obj.Time);
            this.GuiWrapper.Update(obj.Time);
            GuiRenderer.Instance.Update();
            Client.Instance.AssetManager.ClientAssetLibrary?.PulseRequest();
            Client.Instance.AssetManager.ClientAssetLibrary?.PulsePreview();
            while (!this.ActionsToDo.IsEmpty)
            {
                if (this.ActionsToDo.TryDequeue(out Action a))
                {
                    a.Invoke();
                }
            }

            Client.Instance.NetworkIn.TickTimeframe();
            Client.Instance.NetworkOut.TickTimeframe();
            Server.Instance?.NetworkIn.TickTimeframe();
            Server.Instance?.NetworkOut.TickTimeframe();
        }

        private void Instance_SetupHander()
        {
            this._glThread = Thread.CurrentThread;
            this.Sound = new SoundManager();
            this.Sound.Init();
            this.GuiWrapper = new ImGuiWrapper();
            this.Renderer.Create();
            if (ArgsManager.TryGetValue("gldebug", out string val) && Enum.TryParse(val, out this._argsGlSeverity))
            {
                this._glDebugProc = new DebugProc(this.GL_DebugCallback);
                GL.DebugMessageCallback(this._glDebugProc, IntPtr.Zero);
            }

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
        private void Instance_RenderFrame(OpenTK.Windowing.Common.FrameEventArgs obj)
        {
            this.CheckResize();

            ++this.FramesExisted;

            if (this._lastFocusState != this.GameHandle.IsFocused)
            {
                this._lastFocusState = this.GameHandle.IsFocused;
                if (this._lastFocusState)
                {
                    this.GameHandle.RenderFrequency = 0;
                }
                else
                {
                    switch (Client.Instance.Settings.UnfocusedFramerate)
                    {
                        case ClientSettings.UnfocusedFramerateCap.Native:
                        {
                            unsafe
                            {
                                OpenTK.Windowing.GraphicsLibraryFramework.VideoMode* vmode = OpenTK.Windowing.GraphicsLibraryFramework.GLFW.GetVideoMode(this.GameHandle.CurrentMonitor.ToUnsafePtr<OpenTK.Windowing.GraphicsLibraryFramework.Monitor>());
                                this.GameHandle.RenderFrequency = vmode->RefreshRate;
                            }

                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.High:
                        {
                            this.GameHandle.RenderFrequency = 60;
                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.Medium:
                        {
                            this.GameHandle.RenderFrequency = 30;
                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.Low:
                        {
                            this.GameHandle.RenderFrequency = 10;
                            break;
                        }

                        case ClientSettings.UnfocusedFramerateCap.Lowest:
                        {
                            this.GameHandle.RenderFrequency = 1;
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }
            }

            this.Renderer.Render(obj.Time);
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
            GLFW.GetWindowSize(this.GameHandle.WindowPtr, out int ww, out int wh);
            GLFW.GetFramebufferSize(this.GameHandle.WindowPtr, out int fw, out int fh);

            if (ww != this._lastWindowWidth || wh != this._lastWindowHeight)
            {
                this._lastWindowWidth = ww;
                this._lastWindowHeight = wh;
                if (ww != 0 && wh != 0) // Don't save zero-size
                {
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
                    this.Renderer.Resize(fw, fh);
                    this.GuiWrapper.Resize(fw, fh);
                }

                this.Renderer.SetWindowState(fw > 0 && fh > 0);
            }

        }

        private void Instance_Resize(OpenTK.Windowing.Common.ResizeEventArgs obj)
        {
        }
    }
}
