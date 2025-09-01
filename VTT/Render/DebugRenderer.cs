namespace VTT.Render
{
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Shaders;
    using VTT.Util;

    public class DebugRenderer
    {
        private FastAccessShader<IndividualColorOverlay> Shader => Client.Instance.Frontend.Renderer.ObjectRenderer.Shadow2DRenderer.BoxesOverlay;

        private VertexArray _vao;
        private GPUBuffer _vbo;
        private int _vboCapacity;
        private UnsafeResizeableArray<float> _cpuVBOData;

        public void Create()
        {
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.Array, BufferUsage.StreamDraw);
            this._vbo.Bind();
            this._vao.Bind();
            this._vao.SetVertexSize<float>(4);
            this._vao.PushElement(ElementType.Vec4);
            this._vbo.SetData(IntPtr.Zero, this._vboCapacity = sizeof(float) * 4 * 128);
            this._cpuVBOData = new UnsafeResizeableArray<float>(4 * 128);
            this.Shader.Bind();
            this.Shader.Uniforms.Transform.Model.Set(Matrix4x4.Identity);
        }

        public void Render(Map m, double delta)
        {
            OpenGLUtil.StartSection("Debug Overlay");
            if (this._cpuVBOData.Length != 0)
            {
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                this.Shader.Bind();
                this.Shader.Uniforms.Transform.Projection.Set(cam.Projection);
                this.Shader.Uniforms.Transform.View.Set(cam.View);
                GLState.DepthTest.Set(false);
                GLState.DepthMask.Set(false);

                this._vao.Bind();
                this._vbo.Bind();
                unsafe
                {
                    if (this._vboCapacity < this._cpuVBOData.Length * sizeof(float))
                    {
                        this._vbo.SetData((IntPtr)this._cpuVBOData.GetPointer(), this._vboCapacity = this._cpuVBOData.Length * sizeof(float));
                    }
                    else
                    {
                        this._vbo.SetData(IntPtr.Zero, this._vboCapacity);
                        this._vbo.SetSubData((IntPtr)this._cpuVBOData.GetPointer(), this._cpuVBOData.Length * sizeof(float), 0);
                    }
                }

                GLState.DrawArrays(PrimitiveType.Triangles, 0, this._cpuVBOData.Length / 4);
                this._cpuVBOData.Reset();

                GLState.DepthTest.Set(true);
                GLState.DepthMask.Set(true);
            }

            OpenGLUtil.EndSection();
        }
    }
}
