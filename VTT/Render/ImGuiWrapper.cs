﻿namespace VTT.Render
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text;
    using VTT.Asset;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.GLFW;
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

uniform bool gamma_correct;
uniform float gamma_factor;

void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
    if (gamma_correct)
    {
        outputColor.rgb = pow(outputColor.rgb, vec3(1.0/gamma_factor));
    }
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
#if DEBUG
            unsafe
            {
                ImGui.GetIO().NativePtr->ConfigErrorRecovery = 1;
                ImGui.GetIO().NativePtr->ConfigErrorRecoveryEnableAssert = 1;
                ImGui.GetIO().NativePtr->ConfigErrorRecoveryEnableTooltip = 1;
                ImGui.GetIO().NativePtr->ConfigErrorRecoveryEnableDebugLog = 1;
                ImGui.GetIO().NativePtr->ConfigDebugIsDebuggerPresent = Debugger.IsAttached ? (byte)1 : (byte)0;
            }
#else
            unsafe
            {
                ImGui.GetIO().NativePtr->ConfigErrorRecovery = 1;
                ImGui.GetIO().NativePtr->ConfigErrorRecoveryEnableAssert = 0;
                ImGui.GetIO().NativePtr->ConfigErrorRecoveryEnableDebugLog = 0;
                ImGui.GetIO().NativePtr->ConfigErrorRecoveryEnableTooltip = 1;
            }
#endif
            ImGui.GetIO().ConfigWindowsResizeFromEdges = true;
            ImGui.GetIO().ConfigDockingWithShift = true;
            ImGui.GetIO().DisplayFramebufferScale = new Vector2(1, 1);
            this.SetupGl();
            ImGui.LoadIniSettingsFromDisk(Path.Combine(IOVTT.AppDir, "imgui.ini"));
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

            this._vboHandle = new GPUBuffer(BufferTarget.Array, BufferUsage.StreamDraw);
            this._elementsHandle = new GPUBuffer(BufferTarget.ElementArray, BufferUsage.StreamDraw);
            this._vertexArrayObject = new VertexArray();
            this._vertexArrayObject.Bind();
            this._vboHandle.Bind();
            this._elementsHandle.Bind();
            this._vertexArrayObject.SetVertexSize<float>(5);
            this._vertexArrayObject.PushElement(ElementType.Vec2);
            this._vertexArrayObject.PushElement(ElementType.Vec2);
            this._vertexArrayObject.PushElement(new ElementType(4, 4, VertexAttributeType.Byte, sizeof(byte)), true);
            this.CPUTimer = new Stopwatch();
        }

        public void Update()
        {
            // NOOP
        }

        public void PressChar(uint keyChar) => ImGui.GetIO().AddInputCharacter(keyChar);

        private readonly Dictionary<Keys, ImGuiKey> _keyMappings = new Dictionary<Keys, ImGuiKey>();

        public void KeyEvent(Keys key, int scan, ModifierKeys mods, bool isRepeat, bool release)
        {
            this.UpdateModifiers(mods);
            if (this._keyMappings.TryGetValue(key, out ImGuiKey val))
            {
                ImGui.GetIO().AddKeyEvent(val, !release);
                ImGui.GetIO().SetKeyEventNativeData(val, (int)key, scan);
            }
        }

        private void UpdateModifiers(ModifierKeys mods)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddKeyEvent(ImGuiKey.ModCtrl, mods.HasFlag(ModifierKeys.Control));
            io.AddKeyEvent(ImGuiKey.ModShift, mods.HasFlag(ModifierKeys.Shift));
            io.AddKeyEvent(ImGuiKey.ModAlt, mods.HasFlag(ModifierKeys.Alt));
            io.AddKeyEvent(ImGuiKey.ModSuper, mods.HasFlag(ModifierKeys.Super));
        }

        public void MouseScroll(Vector2 offset) => ImGui.GetIO().AddMouseWheelEvent(offset.X, offset.Y);
        public void MouseMove(Vector2 pos) => ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y);

        public void MouseKey(MouseButton btn, ModifierKeys mods, bool release)
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
                ImFontAtlasPtr fonts = ImGui.GetIO().Fonts;

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
                GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.Rgba8, w, h, PixelDataFormat.Rgba, PixelDataType.Byte, pixels);
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
                if (codepoints.Contains("..", StringComparison.OrdinalIgnoreCase)) // Have ranges
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

        private double _lastDelta;
        public void NewFrame(double delta)
        {
            ClientWindow cw = Client.Instance.Frontend;
            int ww = cw.GlfwWidth;
            int wh = cw.GlfwHeight;
            ImGui.GetIO().DisplaySize = new Vector2(cw.Width, cw.Height);
            if (ww != 0 && wh != 0)
            {
                ImGui.GetIO().DisplayFramebufferScale = new Vector2((float)cw.Width / ww, (float)cw.Height / wh);
            }

            double now = Glfw.GetTime();
            if (now < this._lastDelta)
            {
                now = this._lastDelta + 0.00001f;
            }

            ImGui.GetIO().DeltaTime = this._lastDelta > 0.0 ? (float)(now - this._lastDelta) : (float)(1.0f / 60.0f);
            this._lastDelta = now;
            ImGui.NewFrame();
        }

        public void Resize(int w, int h) => ImGui.GetIO().DisplaySize = new Vector2(w, h);

        void SetupRenderState(ImDrawDataPtr drawData, int fbWidth, int fbHeight)
        {
            GL.Enable(Capability.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(Capability.CullFace);
            GL.Disable(Capability.DepthTest);
            GL.Enable(Capability.ScissorTest);

            this._shader.Bind();

            float left = drawData.DisplayPos.X;
            float right = drawData.DisplayPos.X + drawData.DisplaySize.X;
            float top = drawData.DisplayPos.Y;
            float bottom = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

            this._shader["projection_matrix"].Set(Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, -1, 1));
            this._shader["gamma_correct"].Set(false);
            this._shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
        }

        public void ToggleGamma(bool gamma) => this._shader["gamma_correct"].Set(gamma);

        private int _maxVboSize = 0;
        private int _maxEboSize = 0;
        unsafe void RenderDrawData()
        {
            ImDrawDataPtr drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0)
            {
                return;
            }

            // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
            int fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
            int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
            if (fbWidth <= 0 || fbHeight <= 0)
                return;

            this.CPUTimer.Restart();
            SetupRenderState(drawData, fbWidth, fbHeight);

            Vector2 clipOffset = drawData.DisplayPos;
            Vector2 clipScale = drawData.FramebufferScale;

            drawData.ScaleClipRects(clipScale);

            IntPtr lastTexId = ImGui.GetIO().Fonts.TexID;
            GL.BindTexture(TextureTarget.Texture2D, (uint)lastTexId);

            int drawVertSize = Marshal.SizeOf<ImDrawVert>();
            int drawIdxSize = sizeof(ushort);

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];

                this._vertexArrayObject.Bind();
                // Upload vertex/index buffers
                int vS = cmdList.VtxBuffer.Size * drawVertSize;
                int eS = cmdList.IdxBuffer.Size * drawIdxSize;
                this._maxVboSize = Math.Max(this._maxVboSize, vS);
                this._maxEboSize = Math.Max(this._maxEboSize, eS);

                this._vboHandle.Bind();
                this._vboHandle.SetData(IntPtr.Zero, this._maxVboSize);
                this._vboHandle.SetSubData(cmdList.VtxBuffer.Data, vS, 0);
                this._elementsHandle.Bind();
                this._elementsHandle.SetData(IntPtr.Zero, this._maxEboSize);
                this._elementsHandle.SetSubData(cmdList.IdxBuffer.Data, eS, 0);

                for (int cmd_i = 0; cmd_i < cmdList.CmdBuffer.Size; cmd_i++)
                {
                    ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        Marshal.GetDelegateForFunctionPointer<ImDrawCallback>(pcmd.UserCallback)(pcmd, drawIdxSize, fbWidth, fbHeight, pcmd.UserCallbackData);
                    }
                    else
                    {
                        // Project scissor/clipping rectangles into framebuffer space
                        Vector4 clip_rect = pcmd.ClipRect;

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

                        GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, drawIdxSize == 2 ? ElementsType.UnsignedShort : ElementsType.UnsignedInt, (IntPtr)(pcmd.IdxOffset * drawIdxSize), (int)pcmd.VtxOffset);
                    }
                }
            }

            ClearRenderState();
            this.CPUTimer.Stop();
        }

        private void ClearRenderState()
        {
            GL.Disable(Capability.Blend);
            GL.Enable(Capability.CullFace);
            GL.Enable(Capability.DepthTest);
            GL.Disable(Capability.ScissorTest);
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

        public delegate void ImDrawCallback(ImDrawCmdPtr command, int drawIdxSize, int fbWidth, int fbHeight, IntPtr userData);
    }

    public static class ImGuiHelper
    {
        public static Vector2 CalcTextSize(string tIn) => string.IsNullOrEmpty(tIn) ? Vector2.Zero : ImGui.CalcTextSize(tIn);

        public static string TextOrEmpty(string text) => string.IsNullOrEmpty(text) ? " " : text;

        public static void AddTextWithSingleDropShadow(ImDrawListPtr drawList, Vector2 pos, uint color, string text)
        {
            drawList.AddText(pos + new Vector2(1, 1), 0xff000000, text);
            drawList.AddText(pos, color, text);
        }

        public static bool ImAssetRecepticleCustomText(string text, Texture icon, Vector2 size, Func<AssetRef, bool> assetEvaluator, out bool hovered)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 imScreenPos = ImGui.GetCursorScreenPos();
            Vector2 avail = ImGui.GetContentRegionAvail();
            if (size.X == 0)
            {
                size.X = avail.X;
            }

            if (size.Y == 0)
            {
                size.Y = avail.Y;
            }

            Vector2 rectEnd = imScreenPos + size;
            bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
            AssetRef aRef = Client.Instance.Frontend.Renderer.GuiRenderer.DraggedAssetReference;
            bool result = mouseOver && aRef != null && assetEvaluator(aRef);
            uint bClr = result ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : mouseOver ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(imScreenPos, rectEnd, bClr);
            drawList.AddImage(icon, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));
            string mdlTxt = text;
            drawList.PushClipRect(imScreenPos, rectEnd);
            drawList.AddText(imScreenPos + new Vector2(20, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
            drawList.PopClipRect();
            ImGui.Dummy(size);
            hovered = mouseOver;
            return result;
        }

        public static bool ImAssetRecepticle(SimpleLanguage lang, Guid aId, Texture icon, Vector2 size, Func<AssetRef, bool> assetEvaluator, out bool hovered)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 imScreenPos = ImGui.GetCursorScreenPos();
            Vector2 avail = ImGui.GetContentRegionAvail();
            if (size.X == 0)
            {
                size.X = avail.X;
            }

            if (size.Y == 0)
            {
                size.Y = avail.Y;
            }

            Vector2 rectEnd = imScreenPos + size;
            bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
            AssetRef aRef = Client.Instance.Frontend.Renderer.GuiRenderer.DraggedAssetReference;
            bool result = mouseOver && aRef != null && assetEvaluator(aRef);
            uint bClr = result ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : mouseOver ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(imScreenPos, rectEnd, bClr);
            drawList.AddImage(icon, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));
            string mdlTxt = "";
            int mdlTxtOffset = 0;
            if (Client.Instance.AssetManager.Refs.ContainsKey(aId))
            {
                aRef = Client.Instance.AssetManager.Refs[aId];
                mdlTxt += aRef.Name;
                if (Client.Instance.AssetManager.ClientAssetLibrary.Previews.Get(aId, AssetType.Texture, out AssetPreview ap) == AssetStatus.Return && ap != null)
                {
                    Texture tex = ap.GetGLTexture();
                    if (tex != null)
                    {
                        drawList.AddImage(tex, imScreenPos + new Vector2(20, 4), imScreenPos + new Vector2(36, 20));
                        mdlTxtOffset += 20;
                    }
                }
            }

            if (Guid.Equals(Guid.Empty, aId))
            {
                mdlTxt = lang.Translate("generic.none");
            }
            else
            {
                mdlTxt += " (" + aId.ToString() + ")\0";
            }

            drawList.PushClipRect(imScreenPos, rectEnd);
            drawList.AddText(imScreenPos + new Vector2(20 + mdlTxtOffset, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
            drawList.PopClipRect();
            ImGui.Dummy(size);
            hovered = mouseOver;
            return result;
        }
    }
}
