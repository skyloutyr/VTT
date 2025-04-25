namespace VTT.Render
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Asset;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Util;

    public static class ImGuiHelper
    {
        public static void RenderFrame(Vector2 pMin, Vector2 pMax, uint fillCol, bool borders, float rounding) // imgui.cpp:L3747
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(pMin, pMax, fillCol, rounding);
            float border_size = ImGui.GetStyle().FrameBorderSize;
            if (borders && border_size > 0.0f)
            {
                drawList.AddRect(pMin + Vector2.One, pMax + Vector2.One, ImGui.GetColorU32(ImGuiCol.BorderShadow), rounding, 0, border_size);
                drawList.AddRect(pMin, pMax, ImGui.GetColorU32(ImGuiCol.Border), rounding, 0, border_size);
            }
        }

        public static void AddImage(this ImDrawListPtr drawList, ImCustomTexturedRect texture, Vector2 start, Vector2 end) => texture.AddAsImageToDrawList(drawList, start, end);

        public static Vector2 CalcTextSize(string tIn) => string.IsNullOrEmpty(tIn) ? Vector2.Zero : ImGui.CalcTextSize(tIn);

        public static string TextOrEmpty(string text) => string.IsNullOrEmpty(text) ? " " : text;

        public static void AddTextWithSingleDropShadow(ImDrawListPtr drawList, Vector2 pos, uint color, string text)
        {
            drawList.AddText(pos + new Vector2(1, 1), 0xff000000, text);
            drawList.AddText(pos, color, text);
        }

        public static bool ImAssetRecepticleCustomText(string text, ImCustomTexturedRect icon, Vector2 size, Func<AssetRef, bool> assetEvaluator, out bool hovered)
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

        public static bool ImAssetRecepticle(SimpleLanguage lang, Guid aId, ImCustomTexturedRect icon, Vector2 size, Func<AssetRef, bool> assetEvaluator, out bool hovered)
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

        public class UIStreamingBufferCollection
        {
            private readonly List<UIStreamingBuffer> _buffers = new List<UIStreamingBuffer>();
            private int _currentIndex;

            public int MaximumCapacity { get; set; } = 16;

            public UIStreamingBuffer Next()
            {
                if (this._currentIndex == this._buffers.Count)
                {
                    if (this._currentIndex == this.MaximumCapacity - 1)
                    {
                        this._currentIndex = 0;
                    }
                    else
                    {
                        this._buffers.Add(new UIStreamingBuffer());
                    }
                }

                return this._buffers[this._currentIndex++];
            }

            public void Reset() => this._currentIndex = 0;
            public void Free()
            {
                foreach (UIStreamingBuffer buf in this._buffers)
                {
                    buf.Free();
                }

                this._buffers.Clear();
            }
        }

        public class UIStreamingBuffer
        {
            private readonly VertexArray _vao;
            private readonly GPUBuffer _vbo;
            private readonly GPUBuffer _ebo;
            private int _vboAllocatedSize;
            private int _eboAllocatedSize;

            private int _drawVertSize;
            private int _drawIdxSize;

            public UIStreamingBuffer()
            {
                this._vbo = new GPUBuffer(BufferTarget.Array, BufferUsage.StreamDraw);
                this._ebo = new GPUBuffer(BufferTarget.ElementArray, BufferUsage.StreamDraw);
                this._vao = new VertexArray();
                this._vao.Bind();
                this._vbo.Bind();
                this._ebo.Bind();
                this._vao.SetVertexSize<float>(5);
                this._vao.PushElement(ElementType.Vec2);
                this._vao.PushElement(ElementType.Vec2);
                this._vao.PushElement(new ElementType(4, 4, VertexAttributeType.Byte, sizeof(byte)), true);

                this._drawVertSize = Marshal.SizeOf<ImDrawVert>();
                this._drawIdxSize = sizeof(ushort);
            }

            public void Respecify(ImDrawListPtr cmdList)
            {
                this._vao.Bind();
                // Upload vertex/index buffers
                int vS = cmdList.VtxBuffer.Size * this._drawVertSize;
                int eS = cmdList.IdxBuffer.Size * this._drawIdxSize;
                this._vboAllocatedSize = Math.Max(this._vboAllocatedSize, vS);
                this._eboAllocatedSize = Math.Max(this._eboAllocatedSize, eS);

                this._vbo.Bind();
                this._vbo.SetData(IntPtr.Zero, this._vboAllocatedSize);
                this._vbo.SetSubData(cmdList.VtxBuffer.Data, vS, 0);
                this._ebo.Bind();
                this._ebo.SetData(IntPtr.Zero, this._eboAllocatedSize);
                this._ebo.SetSubData(cmdList.IdxBuffer.Data, eS, 0);
            }

            public void Free()
            {
                this._vao.Dispose();
                this._vbo.Dispose();
                this._ebo.Dispose();
            }
        }
    
        public class UIFontIconLoader
        {
            private readonly ImFontAtlasPtr _fontAtlas;
            private readonly Dictionary<int, (ImCustomTexturedRect, Image<Rgba32>)> _callbackRects = new Dictionary<int, (ImCustomTexturedRect, Image<Rgba32>)>();

            public UIFontIconLoader(ImFontAtlasPtr atlas) => this._fontAtlas = atlas;

            public ImCustomTexturedRect LoadUIIcon(string iconPtr)
            {
                Image<Rgba32> img = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.{iconPtr}.png");
                int idx = this._fontAtlas.AddCustomRectRegular(img.Width, img.Height);
                ImCustomTexturedRect ret = new ImCustomTexturedRect();
                this._callbackRects[idx] = (ret, img);
                return ret;
            }

            public unsafe void BakeIcons(IntPtr pixelsPtr, int width, int height)
            {
                uint* pixels = (uint*)pixelsPtr;
                foreach (KeyValuePair<int, (ImCustomTexturedRect, Image<Rgba32>)> kv in this._callbackRects)
                {
                    ImFontAtlasCustomRectPtr rect = this._fontAtlas.GetCustomRectByIndex(kv.Key);
                    kv.Value.Item2.ProcessPixelRows(x =>
                    {
                        for (int y = 0; y < Math.Min(x.Height, rect.Height); ++y)
                        {
                            Span<Rgba32> span = x.GetRowSpan(y);
                            uint* p = pixels + ((rect.Y + y) * width) + rect.X;
                            // Very very frightening implicit pointer conversion!
                            span.CopyTo(new Span<Rgba32>(p, rect.Width));
                        }
                    });

                    kv.Value.Item2.Dispose();
                    kv.Value.Item1.TexturePixelLocation = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
                    kv.Value.Item1.GLBounds = new Vector4(rect.X / (float)width, rect.Y / (float)height, (rect.X + rect.Width) / (float)width, (rect.Y + rect.Height) / (float)height);
                    kv.Value.Item1.ST = new Vector2(kv.Value.Item1.GLBounds.X, kv.Value.Item1.GLBounds.Y);
                    kv.Value.Item1.UV = new Vector2(kv.Value.Item1.GLBounds.Z, kv.Value.Item1.GLBounds.W);
                    kv.Value.Item1.IsAvailable = true;
                }

                this._callbackRects.Clear();
            }
        }
    }
}
