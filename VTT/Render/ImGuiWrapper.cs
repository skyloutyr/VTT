namespace VTT.Render
{
    using ImGuiNET;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using OpenTK.Windowing.GraphicsLibraryFramework;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using System.Text;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public sealed class ImGuiWrapper : IDisposable
    {
        private static string ImGuiVertexSource => @"#version 330 core

uniform mat4 projection_matrix;
layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
layout(location = 2) in vec4 in_color;
out vec4 color;
out vec2 texCoord;
void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
	color = in_color;

    texCoord = in_texCoord;
}";

        private static string ImGuiFragmentSource => @"#version 330 core
uniform sampler2D in_fontTexture;
in vec4 color;
in vec2 texCoord;
out vec4 outputColor;
void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
}";

        private readonly IntPtr _imCtx;

        private ShaderProgram _shader;
        private GPUBuffer _vboHandle;
        private GPUBuffer _elementsHandle;
        private VertexArray _vertexArrayObject;
        private Texture _fontTexture;

        private ClientSettings.UISkin? _skinChangeTo;

        public Stopwatch CPUTimer { get; set; }

        public ImGuiWrapper()
        {
            this._imCtx = ImGui.CreateContext();
            ImGui.SetCurrentContext(this._imCtx);
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGui.GetIO().ConfigWindowsResizeFromEdges = true;
            ImGui.GetIO().ConfigDockingWithShift = true;
            ImGui.GetIO().DisplayFramebufferScale = new System.Numerics.Vector2(1, 1);
            this.SetupGl();
        }

        public void ChangeSkin(ClientSettings.UISkin skin) => this._skinChangeTo = skin;

        private void SetupGl()
        {
            if (!ShaderProgram.TryCompile(out this._shader, ImGuiVertexSource, null, ImGuiFragmentSource, out string err))
            {
                throw new Exception("Shader could not compile! Shader error was " + err);
            }

            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            this._skinChangeTo = Client.Instance.Settings.InterfaceSkin;
            UISkins.DefaultStyleData = new ImGuiStyle(ImGui.GetStyle());


            this.RebuildFontAtlas();
            this.InitKeyMap();

            this._vboHandle = new GPUBuffer(BufferTarget.ArrayBuffer, BufferUsageHint.StreamDraw);
            this._elementsHandle = new GPUBuffer(BufferTarget.ElementArrayBuffer, BufferUsageHint.StreamDraw);
            this._vertexArrayObject = new VertexArray();
            this._vertexArrayObject.Bind();
            this._vboHandle.Bind();
            this._elementsHandle.Bind();
            this._vertexArrayObject.SetVertexSize<float>(5);
            this._vertexArrayObject.PushElement(ElementType.Vec2);
            this._vertexArrayObject.PushElement(ElementType.Vec2);
            this._vertexArrayObject.PushElement(new ElementType(4, 4, VertexAttribPointerType.UnsignedByte, sizeof(byte)), true);
            this.CPUTimer = new Stopwatch();
        }

        public void Update(double time)
        {
            // NOOP
        }

        public void PressChar(uint keyChar) => ImGui.GetIO().AddInputCharacter(keyChar);

        private readonly Dictionary<Keys, ImGuiKey> _keyMappings = new Dictionary<Keys, ImGuiKey>();

        public void KeyEvent(Keys key, int scan, KeyModifiers mods, bool isRepeat, bool release)
        {
            this.UpdateModifiers(mods);
            if (this._keyMappings.TryGetValue(key, out ImGuiKey val))
            {
                ImGui.GetIO().AddKeyEvent(val, !release);
                ImGui.GetIO().SetKeyEventNativeData(val, (int)key, scan);
            }
        }

        private void UpdateModifiers(KeyModifiers mods)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddKeyEvent(ImGuiKey.ModCtrl, mods.HasFlag(KeyModifiers.Control));
            io.AddKeyEvent(ImGuiKey.ModShift, mods.HasFlag(KeyModifiers.Shift));
            io.AddKeyEvent(ImGuiKey.ModAlt, mods.HasFlag(KeyModifiers.Alt));
            io.AddKeyEvent(ImGuiKey.ModSuper, mods.HasFlag(KeyModifiers.Super));
        }

        public void MouseScroll(Vector2 offset) => ImGui.GetIO().AddMouseWheelEvent(offset.X, offset.Y);
        public void MouseMove(Vector2 pos) => ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y);

        public void MouseKey(MouseButton btn, KeyModifiers mods, bool release)
        {
            int mb = (int)btn;
            if (mb is >= 0 and < ((int)ImGuiMouseButton.COUNT))
            {
                ImGui.GetIO().AddMouseButtonEvent((int)btn, !release);
            }

            this.UpdateModifiers(mods);
        }

        public void Focus(bool focused) => ImGui.GetIO().AddFocusEvent(focused);

        private void InitKeyMap()
        {
            _ = ImGui.GetIO();
            foreach (ImGuiKey igk in Enum.GetValues(typeof(ImGuiKey)))
            {
                string name = Enum.GetName(igk);
                if (Enum.TryParse(name, out Keys k))
                {
                    this._keyMappings[k] = igk;
                }
            }

            this._keyMappings[Keys.Left] = ImGuiKey.LeftArrow;
            this._keyMappings[Keys.Right] = ImGuiKey.RightArrow;
            this._keyMappings[Keys.Up] = ImGuiKey.UpArrow;
            this._keyMappings[Keys.Down] = ImGuiKey.DownArrow;
            this._keyMappings[Keys.KeyPad0] = ImGuiKey.Keypad0;
            this._keyMappings[Keys.KeyPad1] = ImGuiKey.Keypad1;
            this._keyMappings[Keys.KeyPad2] = ImGuiKey.Keypad2;
            this._keyMappings[Keys.KeyPad3] = ImGuiKey.Keypad3;
            this._keyMappings[Keys.KeyPad4] = ImGuiKey.Keypad4;
            this._keyMappings[Keys.KeyPad5] = ImGuiKey.Keypad5;
            this._keyMappings[Keys.KeyPad6] = ImGuiKey.Keypad6;
            this._keyMappings[Keys.KeyPad7] = ImGuiKey.Keypad7;
            this._keyMappings[Keys.KeyPad8] = ImGuiKey.Keypad8;
            this._keyMappings[Keys.KeyPad9] = ImGuiKey.Keypad9;
            this._keyMappings[Keys.D0] = ImGuiKey._0;
            this._keyMappings[Keys.D1] = ImGuiKey._1;
            this._keyMappings[Keys.D2] = ImGuiKey._2;
            this._keyMappings[Keys.D3] = ImGuiKey._3;
            this._keyMappings[Keys.D4] = ImGuiKey._4;
            this._keyMappings[Keys.D5] = ImGuiKey._5;
            this._keyMappings[Keys.D6] = ImGuiKey._6;
            this._keyMappings[Keys.D7] = ImGuiKey._7;
            this._keyMappings[Keys.D8] = ImGuiKey._8;
            this._keyMappings[Keys.D9] = ImGuiKey._9;
            this._keyMappings[Keys.KeyPadDivide] = ImGuiKey.KeypadDivide;
            this._keyMappings[Keys.KeyPadMultiply] = ImGuiKey.KeypadMultiply;
            this._keyMappings[Keys.KeyPadSubtract] = ImGuiKey.KeypadSubtract;
            this._keyMappings[Keys.KeyPadAdd] = ImGuiKey.KeypadAdd;
            this._keyMappings[Keys.KeyPadEnter] = ImGuiKey.KeypadEnter;
            this._keyMappings[Keys.KeyPadEqual] = ImGuiKey.KeypadEqual;
            this._keyMappings[Keys.KeyPadDecimal] = ImGuiKey.KeypadDecimal;
            this._keyMappings[Keys.Unknown] = ImGuiKey.None;
            this._keyMappings[Keys.LeftControl] = ImGuiKey.LeftCtrl;
            this._keyMappings[Keys.RightControl] = ImGuiKey.RightCtrl;
        }

        public void Render(double time)
        {
            ImGui.Render();
            RenderDrawData();
        }

        public void BeforeFrame()
        {
            if (this._skinChangeTo != null)
            {
                UISkins.Reset();
                switch (this._skinChangeTo)
                {
                    case ClientSettings.UISkin.Dark:
                    {
                        ImGui.StyleColorsDark();
                        break;
                    }

                    case ClientSettings.UISkin.Light:
                    {
                        ImGui.StyleColorsLight();
                        break;
                    }

                    case ClientSettings.UISkin.Classic:
                    {
                        ImGui.StyleColorsClassic();
                        break;
                    }

                    case ClientSettings.UISkin.SharpGray:
                    {
                        UISkins.SkinSharpGray();
                        break;
                    }

                    case ClientSettings.UISkin.DarkRounded:
                    {
                        UISkins.DarkRounded();
                        break;
                    }

                    case ClientSettings.UISkin.Source:
                    {
                        UISkins.Source();
                        break;
                    }

                    case ClientSettings.UISkin.HumanRevolution:
                    {
                        UISkins.HumanRevolution();
                        break;
                    }

                    case ClientSettings.UISkin.DeepHell:
                    {
                        UISkins.DeepHell();
                        break;
                    }

                    case ClientSettings.UISkin.VisualStudio:
                    {
                        UISkins.VisualStudio();
                        break;
                    }

                    case ClientSettings.UISkin.UnityDark:
                    {
                        UISkins.UnityDark();
                        break;
                    }

                    case ClientSettings.UISkin.MSLight:
                    {
                        UISkins.MSLight();
                        break;
                    }

                    case ClientSettings.UISkin.Cherry:
                    {
                        UISkins.Cherry();
                        break;
                    }

                    case ClientSettings.UISkin.Photoshop:
                    {
                        UISkins.Photoshop();
                        break;
                    }
                }

                this._skinChangeTo = null;
            }
        }

        private unsafe void RebuildFontAtlas()
        {
            try
            {
                var fonts = ImGui.GetIO().Fonts;

                string temp = Path.Combine(IOVTT.ClientDir, "unifont.ttf");
                if (!File.Exists(temp))
                {
                    using Stream s = IOVTT.ResourceToStream("VTT.Embed.unifont-14.0.04.zip");
                    using ZipArchive za = new ZipArchive(s);
                    za.Entries[0].ExtractToFile(temp);
                }

                ImFontGlyphRangesBuilderPtr builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
                builder.AddRanges(fonts.GetGlyphRangesDefault());
                builder.AddRanges(fonts.GetGlyphRangesCyrillic());
                builder.AddRanges(fonts.GetGlyphRangesJapanese());
                builder.AddRanges(fonts.GetGlyphRangesChineseSimplifiedCommon());
                LoadEmoji(builder);
                builder.BuildRanges(out ImVector ranges);
                fonts.AddFontFromFileTTF(temp, 16, null, ranges.Data);
                fonts.Build();

                _fontTexture = new Texture(TextureTarget.Texture2D);
                _fontTexture.Bind();
                _fontTexture.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
                _fontTexture.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                ImGui.GetIO().Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int w, out int h);
                _fontTexture.Size = new Size(w, h);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                fonts.TexID = _fontTexture;
                fonts.ClearTexData();
            }
            catch (Exception e)
            {
                Client.Instance.Logger.Log(LogLevel.Fatal, "A fatal exception loading fonts had occured!");
                Client.Instance.Logger.Exception(LogLevel.Fatal, e);
                throw;
            }
        }

        private void LoadEmoji(ImFontGlyphRangesBuilderPtr builder)
        {
            string[] file = IOVTT.ResourceToLines("VTT.Embed.emoji-data.txt");
            foreach (string line in file)
            {
                if (string.IsNullOrEmpty(line) || line[0] == '#')
                {
                    continue;
                }

                int idx = line.IndexOf(';');
                if (idx == -1)
                {
                    continue;
                }

                string codepoints = line[..idx].Trim();
                if (codepoints.IndexOf("..") != -1) // Have ranges
                {
                    string[] cps = codepoints.Split("..");
                    uint start = Convert.ToUInt32(cps[0], 16);
                    uint end = Convert.ToUInt32(cps[1], 16);
                    while (true)
                    {
                        if (start > ushort.MaxValue)
                        {
                            break;
                        }

                        if (Rune.TryCreate(start, out Rune r))
                        {
                            builder.AddText(r.ToString());
                        }

                        //builder.AddChar((ushort)start++);
                        if (++start > end)
                        {
                            break;
                        }
                    }
                }
                else // Single char
                {
                    uint cp = Convert.ToUInt32(codepoints, 16);
                    if (cp <= ushort.MaxValue)
                    {
                        if (Rune.TryCreate(cp, out Rune r))
                        {
                            builder.AddText(r.ToString());
                        }
                    }
                }
            }
        }

        public void NewFrame(double delta)
        {
            ClientWindow cw = Client.Instance.Frontend;
            int ww = cw.GlfwWidth;
            int wh = cw.GlfwHeight;
            ImGui.GetIO().DisplaySize = new System.Numerics.Vector2(cw.Width, cw.Height);
            if (ww != 0 && wh != 0)
            {
                ImGui.GetIO().DisplayFramebufferScale = new System.Numerics.Vector2((float)cw.Width / ww, (float)cw.Height / wh);
            }

            ImGui.GetIO().DeltaTime = (float)delta;
            ImGui.NewFrame();
        }

        public void Resize(int w, int h) => ImGui.GetIO().DisplaySize = new System.Numerics.Vector2(w, h);

        void SetupRenderState(ImDrawDataPtr drawData, int fbWidth, int fbHeight)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.ScissorTest);

            _shader.Bind();

            var left = drawData.DisplayPos.X;
            var right = drawData.DisplayPos.X + drawData.DisplaySize.X;
            var top = drawData.DisplayPos.Y;
            var bottom = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

            _shader["projection_matrix"].Set(Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, -1, 1));
        }

        unsafe void RenderDrawData()
        {
            ImDrawDataPtr drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0)
            {
                return;
            }

            // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
            var fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
            var fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
            if (fbWidth <= 0 || fbHeight <= 0)
                return;

            this.CPUTimer.Restart();
            SetupRenderState(drawData, fbWidth, fbHeight);

            var clipOffset = drawData.DisplayPos;
            var clipScale = drawData.FramebufferScale;

            drawData.ScaleClipRects(clipScale);

            var lastTexId = ImGui.GetIO().Fonts.TexID;
            GL.BindTexture(TextureTarget.Texture2D, (uint)lastTexId);

            var drawVertSize = Marshal.SizeOf<ImDrawVert>();
            var drawIdxSize = sizeof(ushort);

            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];

                _vertexArrayObject.Bind();
                // Upload vertex/index buffers
                _vboHandle.Bind();
                _vboHandle.SetData(cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * drawVertSize);
                _elementsHandle.Bind();
                _elementsHandle.SetData(cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * drawIdxSize);

                for (var cmd_i = 0; cmd_i < cmdList.CmdBuffer.Size; cmd_i++)
                {
                    var pcmd = cmdList.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        Marshal.GetDelegateForFunctionPointer<ImDrawCallback>(pcmd.UserCallback)(pcmd, drawIdxSize, fbWidth, fbHeight);
                    }
                    else
                    {
                        // Project scissor/clipping rectangles into framebuffer space
                        var clip_rect = pcmd.ClipRect;

                        clip_rect.X = pcmd.ClipRect.X - clipOffset.X;
                        clip_rect.Y = pcmd.ClipRect.Y - clipOffset.Y;
                        clip_rect.Z = pcmd.ClipRect.Z - clipOffset.X;
                        clip_rect.W = pcmd.ClipRect.W - clipOffset.Y;

                        GL.Scissor((int)clip_rect.X, (int)(fbHeight - clip_rect.W), (int)(clip_rect.Z - clip_rect.X), (int)(clip_rect.W - clip_rect.Y));

                        // Bind texture, Draw
                        if (pcmd.TextureId != IntPtr.Zero)
                        {
                            GL.BindTexture(TextureTarget.Texture2D, (uint)pcmd.TextureId);
                        }

                        GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, drawIdxSize == 2 ? DrawElementsType.UnsignedShort : DrawElementsType.UnsignedInt, (IntPtr)(pcmd.IdxOffset * drawIdxSize), (int)pcmd.VtxOffset);
                    }
                }
            }

            ClearRenderState();
            this.CPUTimer.Stop();
        }

        private void ClearRenderState()
        {
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.ScissorTest);
        }

        public void Dispose()
        {
            if (_shader != null)
            {
                _shader.Dispose();
                _shader = null;
                _vboHandle.Dispose();
                _elementsHandle.Dispose();
                _vertexArrayObject.Dispose();
                _fontTexture.Dispose();
            }
        }

        public delegate void ImDrawCallback(ImDrawCmdPtr command, int drawIdxSize, int fbWidth, int fbHeight);
    }

    public static class ImGuiHelper
    {
        public static System.Numerics.Vector2 CalcTextSize(string tIn) => string.IsNullOrEmpty(tIn) ? System.Numerics.Vector2.Zero : ImGui.CalcTextSize(tIn);

        public static string TextOrEmpty(string text) => string.IsNullOrEmpty(text) ? " " : text;
    }
}
