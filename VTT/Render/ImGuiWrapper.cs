namespace VTT.Render
{
    using ImGuiNET;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using OpenTK.Windowing.Desktop;
    using OpenTK.Windowing.GraphicsLibraryFramework;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
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

        public ImGuiWrapper()
        {
            this._imCtx = ImGui.CreateContext();
            ImGui.SetCurrentContext(this._imCtx);
			ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
			ImGui.GetIO().ConfigWindowsResizeFromEdges = true;
			ImGui.GetIO().ConfigDockingWithShift = true;
			this.SetupGl();
        }

        public void ChangeSkin(ClientSettings.UISkin skin) => this._skinChangeTo = skin;

        private void SetupGl()
        {
            if (!ShaderProgram.TryCompile(out this._shader, ImGuiVertexSource, null, ImGuiFragmentSource))
            {
                throw new Exception("Shader could not compile!");
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
            this._vertexArrayObject.PushElement(new ElementType(4, VertexAttribPointerType.UnsignedByte, sizeof(byte)), true);
        }

        public void Update(double time) => this.UpdateImGuiInput(Client.Instance.Frontend.GameHandle);

        private readonly List<char> PressedChars = new List<char>();
        public void PressChar(char keyChar) => this.PressedChars.Add(keyChar);

        public void MouseScroll(Vector2 offset)
		{
			ImGuiIOPtr io = ImGui.GetIO();
			io.MouseWheel = offset.Y;
			io.MouseWheelH = offset.X;
		}

        private void InitKeyMap()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            foreach (ImGuiKey igk in Enum.GetValues(typeof(ImGuiKey)))
            {
                string name = Enum.GetName(igk);
                if (!Enum.TryParse(name, out Keys k))
                {
					// NOOP
                }
                else
                {
                    io.KeyMap[(int)igk] = (int)k;
                }
            }

            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
            io.KeyMap[(int)ImGuiKey.KeypadEnter] = (int)Keys.KeyPadEnter;
        }

        public void Render(double time)
        {
			ImGui.GetIO().DeltaTime = (float)time;
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

						builder.AddChar((ushort)start++);
						if (start > end)
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
						builder.AddChar((ushort)cp);
					}
                }
			}
		}

        public void Resize(int w, int h)
        {
            ImGui.GetIO().DisplaySize = new System.Numerics.Vector2(w, h);
            ImGui.GetIO().DisplayFramebufferScale = new System.Numerics.Vector2(1, 1);
        }

		private readonly Keys[] allKeys = Enum.GetValues<Keys>();
		private void UpdateImGuiInput(GameWindow wnd)
		{
			ImGuiIOPtr io = ImGui.GetIO();

			MouseState MouseState = wnd.MouseState;
			KeyboardState KeyboardState = wnd.KeyboardState;

			io.MouseDown[0] = MouseState[MouseButton.Left];
			io.MouseDown[1] = MouseState[MouseButton.Right];
			io.MouseDown[2] = MouseState[MouseButton.Middle];

			var screenPoint = new Vector2i((int)MouseState.X, (int)MouseState.Y);
			var point = screenPoint;//wnd.PointToClient(screenPoint);
			io.MousePos = new System.Numerics.Vector2(point.X, point.Y);

			foreach (Keys key in allKeys)
			{
				if (key == Keys.Unknown)
				{
					continue;
				}

				io.KeysDown[(int)key] = KeyboardState.IsKeyDown(key);
			}

			foreach (var c in PressedChars)
			{
				io.AddInputCharacter(c);
			}

			PressedChars.Clear();

			io.KeyCtrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
			io.KeyAlt = KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt);
			io.KeyShift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
			io.KeySuper = KeyboardState.IsKeyDown(Keys.LeftSuper) || KeyboardState.IsKeyDown(Keys.RightSuper);
		}

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
				var cmdList = drawData.CmdListsRange[n];

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
						Console.WriteLine("UserCallback not implemented");
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
	}
}
