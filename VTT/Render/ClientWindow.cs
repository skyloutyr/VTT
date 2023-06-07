namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Windowing.Common.Input;
    using OpenTK.Windowing.Desktop;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class ClientWindow
    {
        public ImGuiWrapper GuiWrapper { get; private set; }
        public WindowRenderer Renderer { get; set; }
        public FFmpegWrapper FFmpegWrapper { get; set; }
        public GameWindow GameHandle { get; set; }

        public ConcurrentQueue<Action> ActionsToDo { get; } = new ConcurrentQueue<Action>();

        public int UpdaterExitCode { get; set; } = -2;

        public ulong UpdatesExisted { get; set; }
        public ulong FramesExisted { get; set; }

        public int Width => this.GameHandle.Size.X;
        public int Height => this.GameHandle.Size.Y;
        public float MouseX => this.GameHandle.MousePosition.X;
        public float MouseY => this.GameHandle.MousePosition.Y;

        private Thread _glThread;

        public ClientWindow()
        {
            Configuration.Default.PreferContiguousImageBuffers = true;
            Image<Rgba32> icon = IOVTT.ResourceToImage<Rgba32>("VTT.Embed.icon-alpha.png");
            icon.DangerousTryGetSinglePixelMemory(out var imageSpan);
            byte[] imageBytes = MemoryMarshal.AsBytes(imageSpan.Span).ToArray();
            WindowIcon windowIcon = new WindowIcon(new OpenTK.Windowing.Common.Input.Image(icon.Width, icon.Height, imageBytes));
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
                        ClientSettings.MSAAMode.High => 16,
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

        private void Instance_KeyDown(OpenTK.Windowing.Common.KeyboardKeyEventArgs obj)
        {
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

            this.Renderer.Update(obj.Time);
            this.GuiWrapper.Update(obj.Time);
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

        private int _lastWidth;
        private int _lastHeight;
        private void Instance_Resize(OpenTK.Windowing.Common.ResizeEventArgs obj)
        {
            if (obj.Width > 0 && obj.Height > 0 && obj.Width != this._lastWidth && obj.Height != this._lastHeight)
            {
                this._lastHeight = obj.Height;
                this._lastWidth = obj.Width;
                this.Renderer.Resize(obj.Width, obj.Height);
                this.GuiWrapper.Resize(obj.Width, obj.Height);
                Client.Instance.Settings.Resolution = new Size(obj.Width, obj.Height);
                Client.Instance.Settings.Save();
            }

            this.Renderer.SetWindowState(obj.Width > 0 && obj.Height > 0);
        }
    }
}
