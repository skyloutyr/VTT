namespace VTT.Render
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Util;

    public static class ImGuiHelper
    {
        private static readonly Dictionary<string, ImStringMarshalBuffer> knownBuffers = new Dictionary<string, ImStringMarshalBuffer>();
        private static readonly Dictionary<string, (ImStringMarshalBuffer, ImStringMarshalBuffer)> knownDoubleBuffers = new Dictionary<string, (ImStringMarshalBuffer, ImStringMarshalBuffer)>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ImStringMarshalBuffer GetKnownStringMarshalBuffer(string str, bool expectFixed = false)
        {
            if (!knownBuffers.TryGetValue(str, out ImStringMarshalBuffer buf))
            {
                knownBuffers[str] = buf = new ImStringMarshalBuffer(expectFixed, 255);
            }

            return buf;
        }

        // Double buffer is for fast inputtext getters, as that always has a label+input buffer
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetKnownStringMarshalDoubleBuffer(string str, out ImStringMarshalBuffer labelBuf, out ImStringMarshalBuffer inputBuf)
        {
            if (!knownDoubleBuffers.TryGetValue(str, out (ImStringMarshalBuffer, ImStringMarshalBuffer) data))
            {
                knownDoubleBuffers[str] = data = (new ImStringMarshalBuffer(false, 255), new ImStringMarshalBuffer(false, 255));
            }

            labelBuf = data.Item1;
            inputBuf = data.Item2;
        }

        // These are just a re-implementation of https://github.com/ImGuiNET/ImGui.NET/blob/master/src/ImGui.NET/ImGui.Manual.cs#L179
        // The reason for re-implementation is that ImGui.NET re-allocates the UTF8 buffer every call
        // For marshalling, causing a lot of performance issues (especially of maxLength is a large value)
        // This implementation keeps the underlying buffer, only reallocating whenever necessary, and requires a unique ID string to identify the buffer
        // In most cases using this over ImGui.NET offers a huge performance gain but is unnecessary
        // In some cases with large maxLength buffers (object descriptions) there is too much of a performance boost to have to ignore re-implementing
        // In favour of a cached, not re-allocated memory buffer
        // Note that these still involve a memcpy every frame, they just don't reallocate the buffer
        // The reason for a copy every frame is that by performance metrics simply overriding the memory is faster than a conditional copy on difference
        // Since a comparison is a read->read->comp->jneq pipeline with complications, a basic write is a read->write with complications
        // A comparison first would have a performance boost if a string is changed frequently at the start
        // But most time the string isn't changed at all, and when it is the change happens at the end
        // Overriding heap->heap turns out to be faster than conditional override if strings are not equal
        // In practice, using this call over a ImGui.InputTextMultiline call saves ~0.1ms every frame per call! The things we do for 100 microseconds...
        public static bool InputTextMultilinePreallocated(string bufferID, string label, ref string input, uint maxLength, Vector2 size) => InputTextMultilinePreallocated(bufferID, label, ref input, maxLength, size, ImGuiInputTextFlags.None, null, IntPtr.Zero);
        public static bool InputTextMultilinePreallocated(string bufferID, string label, ref string input, uint maxLength, Vector2 size, ImGuiInputTextFlags flags) => InputTextMultilinePreallocated(bufferID, label, ref input, maxLength, size, flags, null, IntPtr.Zero);
        public static bool InputTextMultilinePreallocated(string bufferID, string label, ref string input, uint maxLength, Vector2 size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback) => InputTextMultilinePreallocated(bufferID, label, ref input, maxLength, size, flags, callback, IntPtr.Zero);
        public static unsafe bool InputTextMultilinePreallocated(string bufferID, string label, ref string input, uint maxLength, Vector2 size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, IntPtr user_data)
        {
            GetKnownStringMarshalDoubleBuffer(bufferID, out ImStringMarshalBuffer labelbuffer, out ImStringMarshalBuffer inputbuffer);
            int labelUtf8CharCountExcludingNullTerminator = labelbuffer.CopyStringAsUTF8(label);
            inputbuffer.EnsureCapacityUTF8((int)maxLength);
            int inputUtf8CharCountExcludingNullTerminator = inputbuffer.CopyStringAsUTF8(input);
            byte b = ImGuiNative.igInputTextMultiline(labelbuffer.Pointer, inputbuffer.Pointer, (uint)Math.Max(maxLength + 1, inputUtf8CharCountExcludingNullTerminator + 1), size, flags, callback, user_data.ToPointer());

            // If heap->heap write is fast, and comparison slow, then why not do a heap->heap here?
            // Because in this case we have a heap->managed write, which does require a realloc, and would case a MANAGED memory allocation every frame without comparisons
            // If there is one thing worse than heap allocations, it is MANAGED memory allocations, since those need to allocate a lot of other stuff too, and register with the GC
            // A comparison here saves MANAGED allocations, so it is all good
            // The reason to use a comparison here vs a b != 0 is because ImGui COULD change the string yet return 0 here
            // If ReturnTrueOnEnter is passed for example (will return 0 on string change, only returns 1 on enter pressed)
            // Yet we need to notify our caller of a string change
            if (!inputbuffer.CompareWithInternalString(input))
            {
                input = inputbuffer.GetStringFromUTF8();
            }

            return b != 0;
        }

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

        public static void ImObjectReferenceFrame(SimpleLanguage lang, Guid oId, Vector2 size, out bool hovered)
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
            hovered = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
            uint bClr = hovered ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(imScreenPos, rectEnd, bClr);
            bool haveObject = false;
            string objectName = lang.Translate("generic.none");
            Map m = Client.Instance.CurrentMap;
            if (m != null)
            {
                if (m.GetObject(oId, out MapObject mo))
                {
                    haveObject = true;
                    objectName = $"{mo.Name} ({mo.ID})";
                    AssetStatus a = Client.Instance.AssetManager.ClientAssetLibrary.Portraits.Get(mo.AssetID, AssetType.Model, out AssetPreview ap);
                    if (a == AssetStatus.Return)
                    {
                        Texture tex = ap.GetGLTexture();
                        if (tex.IsAsyncReady)
                        {
                            if (ap.IsAnimated)
                            {
                                AssetPreview.FrameData frame = ap.GetCurrentFrame((int)Client.Instance.Frontend.UpdatesExisted);
                                float tW = tex.Size.Width;
                                float tH = tex.Size.Height;
                                float sS = frame.X / tW;
                                float sE = sS + (frame.Width / tW);
                                float tS = frame.Y / tH;
                                float tE = tS + (frame.Height / tH);
                                drawList.AddImage(tex, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20), new Vector2(sS, tS), new Vector2(sE, tE));
                            }
                            else
                            {
                                drawList.AddImage(tex, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));

                            }
                        }
                        else
                        {
                            drawList.AddImage(GuiRenderer.Instance.TurnTrackerBackgroundNoObject, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));
                        }
                    }
                }
            }

            if (!haveObject)
            {
                drawList.AddImage(GuiRenderer.Instance.TurnTrackerBackgroundNoObject, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));
            }

            drawList.PushClipRect(imScreenPos, rectEnd);
            drawList.AddText(imScreenPos + new Vector2(20, 4), ImGui.GetColorU32(ImGuiCol.Text), objectName);
            drawList.PopClipRect();
            ImGui.Dummy(size);
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

        private unsafe class ImStringMarshalBuffer
        {
            private byte* _ptr;
            private int _bufferSize;
            private readonly bool _fixedSize;

            public int AllocatedBufferSize => this._bufferSize;
            public byte* Pointer => this._ptr;

            public ImStringMarshalBuffer(bool isFixed, int capacityInBytes)
            {
                this._fixedSize = isFixed;
                this._bufferSize = capacityInBytes + 1;
                this._ptr = MemoryHelper.Allocate<byte>((nuint)capacityInBytes + 1);
            }

            public void EnsureCapacityUTF8(int numCharacters)
            {
                if (this._bufferSize < numCharacters + 1)
                {
                    if (this._fixedSize)
                    {
                        throw new IndexOutOfRangeException("Fixed buffer too small for a string provided!");
                    }

                    this._ptr = MemoryHelper.Reallocate(this._ptr, (nuint)numCharacters + 1);
                    this._bufferSize = numCharacters + 1;
                }
            }

            public int CopyStringAsUTF8(string s)
            {
                fixed (char* cptr = s) // String is internally fixed by the Encoding.GetBytes method, may as well preemptively fix here
                {
                    int bytesNeeded = Encoding.UTF8.GetByteCount(cptr, s.Length);
                    if (this._bufferSize < bytesNeeded + 1)
                    {
                        if (this._fixedSize)
                        {
                            throw new IndexOutOfRangeException("Fixed buffer too small for a string provided!");
                        }

                        this._ptr = MemoryHelper.Reallocate(this._ptr, (nuint)bytesNeeded + 1);
                        this._bufferSize = bytesNeeded + 1;
                    }

                    int r = Encoding.UTF8.GetBytes(cptr, s.Length, this._ptr, bytesNeeded);
                    this._ptr[r] = 0; // Null terminate for ImGui
                    return r;
                }
            }

            public bool CompareWithInternalString(string s)
            {
                fixed (char* cptr = s)
                {
                    int currentCSStringIndex = 0;
                    int currentNativeStringOffset = 0;
                    byte* utf8chars = stackalloc byte[4];
                    while (true)
                    {
                        if (currentCSStringIndex >= s.Length)
                        {
                            return this._ptr[currentNativeStringOffset] == 0; // We reached the end of the string in managed land, and we need to know if the unmanaged matches us with a \0
                        }

                        int nBytes = Encoding.UTF8.GetBytes(cptr + currentCSStringIndex++, 1, utf8chars, 4); // CS never seems to return more than 3 here, but UTF8 contains up to 4 by spec, so being careful
                        for (int i = 0; i < nBytes; ++i)
                        {
                            if (currentNativeStringOffset >= this._bufferSize) // Do not read protected memory
                            {
                                throw new AccessViolationException();
                            }

                            byte native = this._ptr[currentNativeStringOffset++];
                            byte own = utf8chars[i];
                            if (native != own) // Could test for null terminator first but don't need to
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            public string GetStringFromUTF8() => Marshal.PtrToStringUTF8((IntPtr)this._ptr);
        }

        public class UIStreamingBuffer
        {
            private readonly VertexArray _vao;
            private readonly GPUBuffer _vbo;
            private readonly GPUBuffer _ebo;
            private int _vboAllocatedSize;
            private int _eboAllocatedSize;

            private readonly int _drawVertSize;
            private readonly int _drawIdxSize;

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
