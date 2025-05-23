﻿namespace VTT.Render
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using VTT.GLFW;
    using VTT.Network;
    using VTT.Util;
    using static VTT.Network.ClientSettings;

    public class GLFWWindowHandler
    {
        public ObservableValue<Vector2> MousePosition { get; set; } = new ObservableValue<Vector2>(Vector2.Zero);
        public ObservableValue<Size> Size { get; set; } = new ObservableValue<Size>(SixLabors.ImageSharp.Size.Empty);
        public ObservableValue<Size> FramebufferSize { get; set; } = new ObservableValue<Size>(SixLabors.ImageSharp.Size.Empty);
        public ObservableValue<string> Title { get; set; } = new ObservableValue<string>("VTT");
        public ObservableValue<VSyncMode> VSync { get; set; } = new ObservableValue<VSyncMode>(VSyncMode.Off);
        public ObservableValue<bool> Decorated { get; set; } = new ObservableValue<bool>(true);

        private readonly GameWindowSettings _gameWindowSettings;
        private readonly NativeWindowSettings _nativeWindowSettings;
        private readonly IntPtr _nativeWindow;
        private IntPtr _offscreenCtxWindow;
        private readonly bool _haveVTearExt = false;
        private bool _needsFboResize = false;
        private bool _needsWinResize = false;
        private bool _focused = true;
        private readonly Version _glApiVersion;
        private readonly bool _ctxForwardCompatible;

        public bool IsFocused => this._focused;
        public bool IsFullscreen => Glfw.GetWindowMonitor(this._nativeWindow) != IntPtr.Zero;
        public IntPtr GLFWWindow => this._nativeWindow;

        public event Action<double> RenderFrame;
        public event Action UpdateFrame;
        public event Action Load;
        public event Action<TextTypeEventData> TextInput;
        public event Action<double, double> MouseWheel;
        public event Action<Vector2> MouseMove;
        public event Action<string[]> FileDrop;
        public event Action<KeyEventData> KeyDown;
        public event Action<KeyEventData> KeyUp;
        public event Action<KeyEventData> KeyRepeat;
        public event Action<KeyEventData> KeyEvent;
        public event Action<bool> FocusedChanged;
        public event Action<MouseEventData> MouseUp;
        public event Action<MouseEventData> MouseDown;
        public event Action<MouseEventData> MouseRepeat;
        public event Action<MouseEventData> MouseEvent;

        public int MinSwapInterval { get; private set; }

        public PerformanceMetrics MetricsFramerate { get; } = new PerformanceMetrics();

        public GLFWWindowHandler(GameWindowSettings gws, NativeWindowSettings nws)
        {
            this._gameWindowSettings = gws;
            this._nativeWindowSettings = nws;

            if (!Glfw.Init())
            {
                throw new Exception("GLFW failed to initialize!");
            }

            this._glApiVersion = nws.APIVersion;
            this._ctxForwardCompatible = nws.ForwardCompatible;
            IntPtr w = IntPtr.Zero;
            Glfw.WindowHint(WindowCreationHint.ClientAPI, nws.API);
            Glfw.WindowHint(WindowCreationHint.ContextCreationAPI, ContextCreationAPI.Native);
            Glfw.WindowHint(WindowCreationHint.ContextVersionMajor, nws.APIVersion.Major);
            Glfw.WindowHint(WindowCreationHint.ContextVersionMinor, nws.APIVersion.Minor);
            Glfw.WindowHint(WindowCreationHint.OpenGLForwardCompatible, nws.ForwardCompatible);
            Glfw.WindowHint(WindowCreationHint.OpenGLProfile, OpenGLProfile.Core);
            Glfw.WindowHint(WindowCreationHint.ContextDebug, nws.DebugContext);
            Glfw.WindowHint(WindowCreationHint.Samples, nws.NumberOfSamples);
            Glfw.WindowHint(WindowCreationHint.DepthBits, nws.DepthBits);
            Glfw.WindowHint(WindowCreationHint.StencilBits, nws.StencilBits);
            Glfw.WindowHint(WindowCreationHint.DoubleBuffer, true);
            IntPtr m = Glfw.GetPrimaryMonitor();
            if (nws.Fullscreen)
            {
                unsafe
                {
                    GLFWvidmode* vm = Glfw.GetVideoMode(m);
                    Glfw.WindowHint(WindowCreationHint.RedBits, vm->redBits);
                    Glfw.WindowHint(WindowCreationHint.GreenBits, vm->greenBits);
                    Glfw.WindowHint(WindowCreationHint.BlueBits, vm->blueBits);
                    Glfw.WindowHint(WindowCreationHint.RefreshRate, vm->refreshRate);
                    Glfw.WindowHint(WindowCreationHint.Decorated, false);
                    this.Decorated.ChangeWithoutNotify(false);
                }
            }
            else
            {
                Glfw.WindowHint(WindowCreationHint.RedBits, 8);
                Glfw.WindowHint(WindowCreationHint.GreenBits, 8);
                Glfw.WindowHint(WindowCreationHint.BlueBits, 8);
                Glfw.WindowHint(WindowCreationHint.RefreshRate, gws.RenderFrequency);
                Glfw.WindowHint(WindowCreationHint.Decorated, true);
                this.Decorated.ChangeWithoutNotify(true);
            }

            w = this._nativeWindow = Glfw.CreateWindow(nws.Size.Width, nws.Size.Height, "VTT", nws.Fullscreen ? m : IntPtr.Zero, IntPtr.Zero);
            if (w == IntPtr.Zero)
            {
                Glfw.Terminate();
            }

            Glfw.SetCursorPosCallback(w, this.CursorPosCallback);
            Glfw.SetFramebufferSizeCallback(w, this.WindowFBOSizeCallback);
            Glfw.SetWindowSizeCallback(w, this.WindowSizeCallback);
            Glfw.SetCharCallback(w, this.CharCallback);
            Glfw.SetKeyCallback(w, this.KeyCallback);
            Glfw.SetScrollCallback(w, this.ScrollCallback);
            unsafe
            {
                Glfw.SetDropCallback(w, this.DropCallback);
            }

            Glfw.SetWindowFocusCallback(w, this.FocusedCallback);
            Glfw.SetMouseButtonCallback(w, this.MouseButtonCallback);
            //this.CreateOffscreenContext(); Experimented for offscreen texture loading but unfortunately cross-platform portability becomes broken due to poor implementation of object sharing between GL context by various drivers.
            Glfw.MakeContextCurrent(w);
            Glfw.SetWindowIcon(w, nws.Icon);
            GL.Bindings.MiniGLLoader.Load(GLFWLoader.glfwGetProcAddress);
            this._haveVTearExt = Glfw.ExtensionSupported("WGL_EXT_swap_control_tear") || Glfw.ExtensionSupported("GLX_EXT_swap_control_tear");
            this.MinSwapInterval = 0;
            Glfw.SwapInterval(this.MinSwapInterval);
        }

        private void CursorPosCallback(IntPtr w, double x, double y)
        {
        }

        private void WindowFBOSizeCallback(IntPtr win, int w, int h) => this._needsFboResize = true;

        private void WindowSizeCallback(IntPtr win, int w, int h) => this._needsWinResize = true;

        private void CharCallback(IntPtr w, uint codepoint)
        {
            string s = char.ConvertFromUtf32((int)codepoint);
            char c = string.IsNullOrEmpty(s) ? (char)0 : s[0];
            this.TextInput?.Invoke(new TextTypeEventData(s, c, codepoint));
        }

        private void ScrollCallback(IntPtr w, double x, double y) => this.MouseWheel?.Invoke(x, y);

        private unsafe void DropCallback(IntPtr w, int count, byte** paths)
        {
            string[] ret = new string[count];
            for (int i = 0; i < count; ++i)
            {
                ret[i] = Marshal.PtrToStringUTF8((IntPtr)paths[i]);
            }

            this.FileDrop?.Invoke(ret);
        }

        private void FocusedCallback(IntPtr w, int focus)
        {
            this._focused = focus != 0;
            this.FocusedChanged?.Invoke(focus != 0);
        }

        private void KeyCallback(IntPtr w, Keys key, int scancode, InputState action, ModifierKeys mods)
        {
            KeyEventData evtData = new KeyEventData(key, scancode, (int)key, mods, action);
            this.KeyEvent?.Invoke(evtData);
            switch (action)
            {
                case InputState.Release:
                {
                    this.KeyUp?.Invoke(evtData);
                    break;
                }

                case InputState.Press:
                {
                    this.KeyDown?.Invoke(evtData); 
                    break;
                }

                case InputState.Repeat:
                default:
                {
                    this.KeyRepeat?.Invoke(evtData); 
                    break;
                }
            }
        }

        private void MouseButtonCallback(IntPtr w, MouseButton button, InputState action, ModifierKeys mods)
        {
            MouseEventData evtData = new MouseEventData(button, action, mods);
            this.MouseEvent?.Invoke(evtData);
            switch (action)
            {
                case InputState.Release:
                {
                    this.MouseUp?.Invoke(evtData);
                    break;
                }

                case InputState.Press:
                {
                    this.MouseDown?.Invoke(evtData);
                    break;
                }

                case InputState.Repeat:
                default:
                {
                    this.MouseRepeat?.Invoke(evtData);
                    break;
                }
            }
        }

        public const int MaxFrameskip = 5;

        public void Update() => this.UpdateFrame.Invoke();

        public void Render(double dt) => this.RenderFrame?.Invoke(dt);

        public void RequestWindowAttention() => Glfw.RequestWindowAttention(this._nativeWindow);
        public void SwapBuffers() => Glfw.SwapBuffers(this._nativeWindow);
        public bool IsKeyDown(Keys key) => Glfw.GetKey(this._nativeWindow, key) != InputState.Release;
        public bool IsMouseButtonDown(MouseButton key) => Glfw.GetMouseButton(this._nativeWindow, key) != InputState.Release;
        public bool IsAnyShiftDown() => this.IsKeyDown(Keys.LeftShift) || this.IsKeyDown(Keys.RightShift);
        public bool IsAnyAltDown() => this.IsKeyDown(Keys.LeftAlt) || this.IsKeyDown(Keys.RightAlt);
        public bool IsAnyControlDown() => this.IsKeyDown(Keys.LeftCtrl) || this.IsKeyDown(Keys.RightCtrl);
        public void Close() => Glfw.SetWindowShouldClose(this._nativeWindow, true);

        public double RenderDt { get; set; }

        private readonly BlockingCollection<Action> _offscreenCtxActions = new BlockingCollection<Action>();
        private void CreateOffscreenContext()
        {
            if (this._offscreenCtxWindow == IntPtr.Zero)
            {
                Glfw.WindowHint(WindowCreationHint.ClientAPI, ClientAPI.OpenGL);
                Glfw.WindowHint(WindowCreationHint.ContextCreationAPI, ContextCreationAPI.Native);
                Glfw.WindowHint(WindowCreationHint.ContextVersionMajor, this._glApiVersion.Major);
                Glfw.WindowHint(WindowCreationHint.ContextVersionMinor, this._glApiVersion.Minor);
                Glfw.WindowHint(WindowCreationHint.OpenGLForwardCompatible, this._ctxForwardCompatible);
                Glfw.WindowHint(WindowCreationHint.OpenGLProfile, OpenGLProfile.Core);
                Glfw.WindowHint(WindowCreationHint.ContextDebug, false);
                Glfw.WindowHint(WindowCreationHint.Samples, 0);
                Glfw.WindowHint(WindowCreationHint.DoubleBuffer, false);
                Glfw.WindowHint(WindowCreationHint.Visible, false);
                Glfw.WindowHint(WindowCreationHint.Decorated, false);
                Glfw.WindowHint(WindowCreationHint.RefreshRate, 0);
                IntPtr win = this._offscreenCtxWindow = Glfw.CreateWindow(1, 1, string.Empty, IntPtr.Zero, this._nativeWindow);
                if (win == IntPtr.Zero)
                {
                    throw new Exception("Could not create offscreen glfw context!");
                }

                new Thread(this.RunOffscreen) { IsBackground = true, CurrentCulture = Thread.CurrentThread.CurrentCulture, CurrentUICulture = Thread.CurrentThread.CurrentUICulture }.Start();
            }
        }

        private void RunOffscreen()
        {
            while (!Glfw.WindowShouldClose(this._offscreenCtxWindow))
            {
                Action a = this._offscreenCtxActions.Take();
                if (a == null)
                {
                    break; // Empty action passed means we die
                }

                Glfw.MakeContextCurrent(this._offscreenCtxWindow);
                a();
                Glfw.MakeContextCurrent(IntPtr.Zero);
            }
        }

        public bool HasOffscreenContext => this._offscreenCtxWindow != IntPtr.Zero;
        public bool DoOffscreenAction(Action a) => this._offscreenCtxActions.TryAdd(a);

        public void Run()
        {
            Glfw.MakeContextCurrent(this._nativeWindow);
            this.Load?.Invoke();
            long nextTick = DateTime.Now.Ticks;
            long skipTicks = (TimeSpan.TicksPerSecond / this._gameWindowSettings.UpdateFrequency);
            while (!Glfw.WindowShouldClose(this._nativeWindow))
            {
                ulong now = (ulong)DateTime.Now.Ticks;
                switch (Client.Instance.Settings.ContextHandlingMode)
                {
                    case GLContextHandlingMode.Checked:
                    {
                        IntPtr ctx = Glfw.GetCurrentContext();
                        if (ctx != this._nativeWindow)
                        {
                            Glfw.MakeContextCurrent(this._nativeWindow);
                        }

                        break;
                    }

                    case GLContextHandlingMode.Explicit:
                    {
                        Glfw.MakeContextCurrent(this._nativeWindow);
                        break;
                    }

                    case GLContextHandlingMode.Implicit:
                    default:
                    {
                        break;
                    }
                }
                

                if (this.MousePosition.ValueChanged)
                {
                    Glfw.SetCursorPos(this._nativeWindow, this.MousePosition.Value.X, this.MousePosition.Value.Y);
                    this.MousePosition.ValueChanged = false;
                }

                Glfw.GetCursorPos(this._nativeWindow, out double cx, out double cy);
                float mx = (float)cx;
                float my = (float)cy;
                if (mx != this.MousePosition.Value.X || my != this.MousePosition.Value.Y)
                {
                    this.MousePosition.Value = new Vector2((float)mx, (float)my);
                    this.MouseMove?.Invoke(this.MousePosition.Value);
                    this.MousePosition.ValueChanged = false;
                }

                if (this.Title.ValueChanged)
                {
                    Glfw.SetWindowTitle(this._nativeWindow, Title.Value);
                    this.Title.ValueChanged = false;
                }

                if (this.Size.ValueChanged)
                {
                    Glfw.SetWindowSize(this._nativeWindow, this.Size.Value.Width, this.Size.Value.Height);
                    this.Size.ValueChanged = false;
                }

                if (this.VSync.ValueChanged)
                {
                    switch (this.VSync.Value)
                    {
                        case VSyncMode.Off:
                        {
                            Glfw.SwapInterval(0);
                            this.MinSwapInterval = 0;
                            break;
                        }

                        default:
                        {
                            Glfw.SwapInterval(1);
                            this.MinSwapInterval = 1;
                            break;
                        }
                    }

                    this.VSync.ValueChanged = false;
                }

                if (this.Decorated.ValueChanged)
                {
                    Glfw.SetWindowAttrib(this._nativeWindow, WindowProperty.Decorated, this.Decorated.Value);
                    this.Decorated.ValueChanged = false;
                }

                if (this._needsFboResize)
                {
                    this._needsFboResize = false;
                    Glfw.GetFramebufferSize(this._nativeWindow, out int fw, out int fh);
                    this.FramebufferSize.Value = new Size(fw, fh);
                }

                if (this._needsWinResize)
                {
                    this._needsWinResize = false;
                    Glfw.GetWindowSize(this._nativeWindow, out int ww, out int wh);
                    this.Size.Value = new Size(ww, wh);
                    this.Size.ValueChanged = false;
                }

                int loops = 0;
                while (DateTime.Now.Ticks > nextTick && loops < MaxFrameskip)
                {
                    this.Update();
                    nextTick += skipTicks;
                    ++loops;
                }

                double dt = (double)(DateTime.Now.Ticks + skipTicks - nextTick) / skipTicks;
                this.Render(Math.Clamp(dt, 0, 1));
                Glfw.PollEvents();

                ulong delta = (ulong)DateTime.Now.Ticks - now;
                this.RenderDt = (double)delta / TimeSpan.TicksPerSecond;
                this.MetricsFramerate.AddTick(delta);
                if (this.MetricsFramerate.CheckCumulative(TimeSpan.TicksPerSecond))
                {
                    this.MetricsFramerate.SwapBuffers(TimeSpan.TicksPerSecond);
                }
            }

            Glfw.MakeContextCurrent(this._nativeWindow);
            Glfw.DestroyWindow(this._nativeWindow);
            if (this._offscreenCtxWindow != IntPtr.Zero)
            {
                this._offscreenCtxActions.TryAdd(null);
                Glfw.DestroyWindow(this._offscreenCtxWindow);
            }

            Glfw.Terminate();
        }
    }

    public readonly struct KeyEventData
    {
        public Keys Key { get; }
        public int Scancode { get; }
        public int Keycode { get; }
        public ModifierKeys Mods { get; }
        public InputState Action { get; }

        public KeyEventData(Keys key, int scancode, int keycode, ModifierKeys mods, InputState action)
        {
            this.Key = key;
            this.Scancode = scancode;
            this.Keycode = keycode;
            this.Mods = mods;
            this.Action = action;
        }
    }

    public readonly struct MouseEventData
    {
        public MouseButton Button { get; }
        public InputState Action { get; }
        public ModifierKeys Mods { get; }

        public MouseEventData(MouseButton button, InputState action, ModifierKeys mods)
        {
            this.Button = button;
            this.Action = action;
            this.Mods = mods;
        }
    }

    public readonly struct TextTypeEventData
    {
        public string UnicodeString { get; }
        public char SingleChar { get; }
        public uint Unicode { get; }

        public TextTypeEventData(string unicodeString, char singleChar, uint unicode)
        {
            this.UnicodeString = unicodeString;
            this.SingleChar = singleChar;
            this.Unicode = unicode;
        }
    }

    public struct GameWindowSettings
    {
        public int RenderFrequency { get; set; }
        public int UpdateFrequency { get; set; }
    }

    public struct NativeWindowSettings
    {
        public ClientAPI API { get; set; }
        public Version APIVersion { get; set; }
        public bool ForwardCompatible { get; set; }
        public bool DebugContext { get; set; }
        public bool Fullscreen { get; set; }
        public int NumberOfSamples { get; set; }
        public Size Size { get; set; }
        public int DepthBits { get; set; }
        public int StencilBits { get; set; }
        public GLFWimage[] Icon { get; set; }
    }

    public class PerformanceMetrics
    {
        private readonly UnsafeResizeableArray<ulong> _ticks;

        private ulong _tickMax = 0;
        private ulong _tickMin = ulong.MaxValue;
        private ulong _ticksCumulativeNow;

        private ulong _lastTickMax;
        private ulong _lastTickMin;
        private ulong _lastTicksAvg;
        private ulong _lastNumTicks;

        public ulong LastTickMax => this._lastTickMax;
        public ulong LastTickMin => this._lastTickMin;
        public ulong LastTickAvg => this._lastTicksAvg;
        public ulong LastNumFrames => this._lastNumTicks;

        public PerformanceMetrics() => this._ticks = new UnsafeResizeableArray<ulong>(1000);

        public bool CheckCumulative(ulong t) => this._ticksCumulativeNow >= t;

        public void AddTick(ulong tick)
        {
            this._ticks.Add(tick);
            this._tickMax = Math.Max(tick, this._tickMax);
            this._tickMin = Math.Min(tick, this._tickMin);
            this._ticksCumulativeNow += tick;
        }

        public void SwapBuffers(ulong delta)
        {
            this._lastTickMax = this._tickMax;
            this._lastTickMin = this._tickMin;
            this._tickMax = 0;
            this._tickMin = ulong.MaxValue;
            this._lastTicksAvg = 0;
            for (int i = 0; i < this._ticks.Length; ++i)
            {
                this._lastTicksAvg += this._ticks[i];
            }

            this._lastTicksAvg /= (ulong)this._ticks.Length;
            this._lastNumTicks = (ulong)this._ticks.Length;
            this._ticks.Reset();
            this._ticksCumulativeNow -= delta;
            if (this._ticksCumulativeNow > delta)
            {
                // If we are still above delta then we skipped ticks and don't care to corretly carry over values into the next second anymore.
                this._ticksCumulativeNow = 0;
            }
        }
    }
}
