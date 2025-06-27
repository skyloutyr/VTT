namespace VTT.Render
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.Shaders;
    using VTT.Util;
    using OGL = GL.Bindings.GL;

    public class PortalHighlightRenderer
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;
        private UnsafeResizeableArray<float> _vertices;
        private UnsafeResizeableArray<uint> _indices;
        private Texture _tex;

        private readonly HashSet<MapObject> _portalsToProcess = new HashSet<MapObject>();

        public FastAccessShader<OverlayUniforms> HighlightShader => Client.Instance.Frontend.Renderer.MapRenderer.GridRenderer.InWorldShader;

        public void Create()
        {
            this._vertices = new UnsafeResizeableArray<float>(512 * 5);
            this._indices = new UnsafeResizeableArray<uint>(512);
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(GL.Bindings.BufferTarget.Array, GL.Bindings.BufferUsage.StreamDraw);
            this._ebo = new GPUBuffer(GL.Bindings.BufferTarget.ElementArray, GL.Bindings.BufferUsage.StreamDraw);
            this._vao.Bind();
            this._vbo.Bind();
            this._ebo.Bind();
            this._vao.SetVertexSize<float>(5);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec2);
            this._vbo.SetData(IntPtr.Zero, sizeof(float) * this._vertices.Capacity);
            this._ebo.SetData(IntPtr.Zero, sizeof(uint) * this._indices.Capacity);

            this._tex = new Texture(GL.Bindings.TextureTarget.Texture2D);
            this._tex.Bind();
            this._tex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this._tex.SetWrapParameters(WrapParam.Mirror, WrapParam.Repeat, WrapParam.Repeat);
            this._tex.Size = new Size(1, 2);
            this._tex.AsyncState = AsyncLoadState.NonAsync;
            unsafe
            {
                uint* pixels = stackalloc uint[2];
                pixels[0] = 0xffffffffu;
                pixels[1] = 0x00000000u;
                OGL.TexImage2D(GL.Bindings.TextureTarget.Texture2D, 0, GL.Bindings.SizedInternalFormat.Rgba8, 1, 2, GL.Bindings.PixelDataFormat.Rgba, GL.Bindings.PixelDataType.Byte, (nint)pixels);
            }

            OpenGLUtil.NameObject(GL.Bindings.GLObjectType.VertexArray, this._vao, "Portal highlight vao");
            OpenGLUtil.NameObject(GL.Bindings.GLObjectType.Buffer, this._vbo, "Portal highlight vbo");
            OpenGLUtil.NameObject(GL.Bindings.GLObjectType.Buffer, this._ebo, "Portal highlight ebo");
            OpenGLUtil.NameObject(GL.Bindings.GLObjectType.Texture, this._tex, "Portal highlight texture 1x2");
        }

        public void AddObject(MapObject portal)
        {
            this._portalsToProcess.Add(portal);
        }

        public void Render(Map m, double dt)
        {
            if (m == null)
            {
                this._portalsToProcess.Clear();
                return;
            }

            OpenGLUtil.StartSection("Portals highlight");
            FastAccessShader<OverlayUniforms> shader = this.HighlightShader;
            GLState.Blend.Set(true);
            GLState.BlendFunc.Set((GL.Bindings.BlendingFactor.SrcAlpha, GL.Bindings.BlendingFactor.OneMinusSrcAlpha));
            GLState.DepthTest.Set(false);
            GLState.CullFace.Set(false);
            GLState.DepthMask.Set(false);
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            shader.Bind();
            shader.Uniforms.Transform.View.Set(cam.View);
            shader.Uniforms.Transform.Projection.Set(cam.Projection);
            shader.Uniforms.Transform.Model.Set(Matrix4x4.Identity);
            this._tex.Bind();
            float lineProgress = ((((uint)Client.Instance.Frontend.UpdatesExisted) + (float)dt) / 60.0f);
            bool adminOrObserver = Client.Instance.IsAdmin || Client.Instance.IsObserver;
            while (this._portalsToProcess.Count > 0)
            {
                MapObject p1 = this._portalsToProcess.First();
                if (p1.IsPortal && (adminOrObserver || p1.CanEdit(Client.Instance.ID)))
                {
                    Vector4 portalColor = this.DetermineColor(p1.ID, p1.PairedPortalID);
                    shader.Uniforms.Color.Set(portalColor * new Vector4(1, 1, 1, 0.8f));
                    this.AddContextHighlight(m, p1.Position, p1.PortalSize, lineProgress, 0.025f);
                    if (!p1.PairedPortalID.IsEmpty() && m.GetObject(p1.PairedPortalID, out MapObject pportal) && (adminOrObserver || pportal.CanEdit(Client.Instance.ID)))
                    {
                        this._portalsToProcess.Remove(pportal); // In case it was also added
                        this.AddContextHighlight(m, pportal.Position, pportal.PortalSize, lineProgress, 0.025f);
                        this.AddHighlightLine(pportal.Position, p1.Position, lineProgress, 0.05f, 1.0f);
                    }

                    this.DrawBuffers();
                }

                this._portalsToProcess.Remove(p1);
            }

            GLState.DepthMask.Set(true);
            GLState.DepthTest.Set(true);
            GLState.CullFace.Set(true);
            GLState.Blend.Set(false);
            OpenGLUtil.EndSection();
        }

        private Vector4 DetermineColor(Guid id1, Guid id2)
        {
            int combinedHash = id1.GetHashCode() ^ id2.GetHashCode();
            Random rand = new Random(combinedHash);
            HSVColor clr = new HSVColor(rand.NextSingle() * 360.0f, 0.5f + (rand.NextSingle() * 0.5f), 1f);
            return (Vector4)(Color)clr;
        }

        private void AddContextHighlight(Map m, Vector3 position, Vector3 scale, float offsetY, float lineThickness)
        {
            if (m.Is2D)
            {
                this.AddHighlighSquare(position, scale, offsetY, lineThickness);
            }
            else
            {
                this.AddHighlightCube(position, scale, offsetY, lineThickness);
            }
        }

        private void AddHighlighSquare(Vector3 position, Vector3 scale, float offsetY, float lineThickness)
        {
            Vector3 halfScale = scale * 0.5f;
            Vector3 tl = position + new Vector3(-halfScale.X, -halfScale.Y, 0);
            Vector3 tr = position + new Vector3(halfScale.X, -halfScale.Y, 0);
            Vector3 bl = position + new Vector3(-halfScale.X, halfScale.Y, 0);
            Vector3 br = position + new Vector3(halfScale.X, halfScale.Y, 0);

            AddHighlightLine(tl, tr, offsetY, lineThickness, 2.0f);
            AddHighlightLine(br, bl, offsetY, lineThickness, 2.0f);
            AddHighlightLine(bl, tl, offsetY, lineThickness, 2.0f);
            AddHighlightLine(tr, br, offsetY, lineThickness, 2.0f);
        }

        private void AddHighlightCube(Vector3 position, Vector3 scale, float offsetY, float lineThickness)
        {
            Vector3 halfScale = scale * 0.5f;

            Vector3 blf = position + new Vector3(-halfScale.X, -halfScale.Y, -halfScale.Z);
            Vector3 brf = position + new Vector3(halfScale.X, -halfScale.Y, -halfScale.Z);
            Vector3 blb = position + new Vector3(-halfScale.X, halfScale.Y, -halfScale.Z);
            Vector3 brb = position + new Vector3(halfScale.X, halfScale.Y, -halfScale.Z);
            Vector3 tlf = position + new Vector3(-halfScale.X, -halfScale.Y, halfScale.Z);
            Vector3 trf = position + new Vector3(halfScale.X, -halfScale.Y, halfScale.Z);
            Vector3 tlb = position + new Vector3(-halfScale.X, halfScale.Y, halfScale.Z);
            Vector3 trb = position + new Vector3(halfScale.X, halfScale.Y, halfScale.Z);

            AddHighlightLine(blf, brf, offsetY, lineThickness, 2.0f);
            AddHighlightLine(blb, brb, offsetY, lineThickness, 2.0f);
            AddHighlightLine(blf, blb, offsetY, lineThickness, 2.0f);
            AddHighlightLine(brf, brb, offsetY, lineThickness, 2.0f);

            AddHighlightLine(tlf, trf, offsetY, lineThickness, 2.0f);
            AddHighlightLine(tlb, trb, offsetY, lineThickness, 2.0f);
            AddHighlightLine(tlf, tlb, offsetY, lineThickness, 2.0f);
            AddHighlightLine(trf, trb, offsetY, lineThickness, 2.0f);

            AddHighlightLine(blf, tlf, offsetY, lineThickness, 2.0f);
            AddHighlightLine(brf, trf, offsetY, lineThickness, 2.0f);
            AddHighlightLine(brb, trb, offsetY, lineThickness, 2.0f);
            AddHighlightLine(blb, tlb, offsetY, lineThickness, 2.0f);
        }

        private void AddHighlightLine(Vector3 start, Vector3 end, float offsetY, float thickness, float yScale)
        {
            Vector3 normal = Vector3.Normalize(end - start); 
            Vector3 a = Vector3.Cross(Vector3.UnitX, normal);
            Quaternion qZ = new Quaternion(a, 1).Normalized();

            Vector3 oZ = Vector4.Transform(new Vector4(0, 0, 1, 1), qZ).Xyz().Normalized() * thickness;
            Vector3 oX = Vector3.Cross(normal, oZ).Normalized() * thickness;
            float endY = offsetY + ((end - start).Length() * yScale);

            uint fvIdx = (uint)(this._vertices.Length / 5);
            Vector3 v1 = start + oX + oZ; // 0
            Vector3 v2 = start + oX - oZ; // 1
            Vector3 v3 = start - oX + oZ; // 2
            Vector3 v4 = start - oX - oZ; // 3
            Vector3 v5 = end + oX + oZ;   // 4
            Vector3 v6 = end + oX - oZ;   // 5
            Vector3 v7 = end - oX + oZ;   // 6
            Vector3 v8 = end - oX - oZ;   // 7
            this.AddVertex(v1, new Vector2(0, offsetY));
            this.AddVertex(v2, new Vector2(1, offsetY));
            this.AddVertex(v3, new Vector2(1, offsetY));
            this.AddVertex(v4, new Vector2(0, offsetY));
            this.AddVertex(v5, new Vector2(0, endY));
            this.AddVertex(v6, new Vector2(1, endY));
            this.AddVertex(v7, new Vector2(1, endY));
            this.AddVertex(v8, new Vector2(0, endY));

            this._indices.Add(fvIdx + 0);
            this._indices.Add(fvIdx + 1);
            this._indices.Add(fvIdx + 4);
            this._indices.Add(fvIdx + 1);
            this._indices.Add(fvIdx + 4);
            this._indices.Add(fvIdx + 5);

            this._indices.Add(fvIdx + 2);
            this._indices.Add(fvIdx + 3);
            this._indices.Add(fvIdx + 6);
            this._indices.Add(fvIdx + 3);
            this._indices.Add(fvIdx + 6);
            this._indices.Add(fvIdx + 7);

            this._indices.Add(fvIdx + 0);
            this._indices.Add(fvIdx + 2);
            this._indices.Add(fvIdx + 4);
            this._indices.Add(fvIdx + 2);
            this._indices.Add(fvIdx + 4);
            this._indices.Add(fvIdx + 6);

            this._indices.Add(fvIdx + 1);
            this._indices.Add(fvIdx + 3);
            this._indices.Add(fvIdx + 5);
            this._indices.Add(fvIdx + 3);
            this._indices.Add(fvIdx + 5);
            this._indices.Add(fvIdx + 7);
        }

        private void AddVertex(Vector3 pos, Vector2 uv)
        {
            this._vertices.Add(pos.X);
            this._vertices.Add(pos.Y);
            this._vertices.Add(pos.Z);
            this._vertices.Add(uv.X);
            this._vertices.Add(uv.Y);
        }

        private unsafe void DrawBuffers()
        {
            this._vao.Bind();
            this._vbo.Bind();
            this._ebo.Bind();
            int numIndices = this._indices.Length;
            this._vbo.SetData(IntPtr.Zero, sizeof(float) * this._vertices.Capacity);
            this._vbo.SetSubData((IntPtr)this._vertices.GetPointer(), sizeof(float) * this._vertices.Length, 0);
            this._ebo.SetData(IntPtr.Zero, sizeof(uint) * this._indices.Capacity);
            this._ebo.SetSubData((IntPtr)this._indices.GetPointer(), sizeof(uint) * this._indices.Length, 0);
            GLState.DrawElements(GL.Bindings.PrimitiveType.Triangles, numIndices, GL.Bindings.ElementsType.UnsignedInt, 0);
            this._vertices.Reset();
            this._indices.Reset();
        }
    }
}
