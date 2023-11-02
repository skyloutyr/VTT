namespace VTT.Control
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using VTT.Asset.Obj;
    using VTT.GL;
    using VTT.Render;
    using VTT.Util;
    using OGL = OpenTK.Graphics.OpenGL.GL;

    public class DrawingPointContainer : ISerializable
    {
        public Guid ID { get; private set; }
        public Guid OwnerID { get; private set; }
        public List<DrawingPoint> Points { get; } = new List<DrawingPoint>();
        public AABox TotalBounds { get; set; }

        public float Radius { get; private set; }
        public Vector4 Color { get; private set; }

        private bool _haveBuffers;
        private GPUBuffer _vbo;
        private GPUBuffer _instancedVBO;
        private GPUBuffer _instancedEBO;
        private int _vao;

        private float[] _modelRawPositions;
        private uint[] _modelRawIndices;

        private bool _needsVBORecalc = false;
        private int _numInstances = 0;

        private DrawingPointContainer()
        {
        }

        public DrawingPointContainer(Guid id, Guid oId, float radius, Vector4 color)
        {
            this.ID = id;
            this.OwnerID = oId;
            this.Radius = radius;
            this.Color = color;
        }

        public void UpdateFrom(DrawingPointContainer local)
        {
            // Assume most attributes are unchanged but the points
            this.Points.Clear();
            this.Points.AddRange(local.Points);
        }

        public void NotifyUpdate()
        {
            this._needsVBORecalc = true;
            this.CalculateTotalBounds();
            this._numInstances = this.Points.Count;
        }

        public void CalculateTotalBounds()
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (DrawingPoint dp in this.Points)
            {
                minX = MathF.Min(minX, dp.x);
                minY = MathF.Min(minY, dp.y);
                minZ = MathF.Min(minZ, dp.z);
                maxX = MathF.Max(maxX, dp.x);
                maxY = MathF.Max(maxY, dp.y);
                maxZ = MathF.Max(maxZ, dp.z);
            }

            minX -= this.Radius;
            minY -= this.Radius;
            minZ -= this.Radius;
            maxX += this.Radius;
            maxY += this.Radius;
            maxZ += this.Radius;

            this.TotalBounds = new AABox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        public void RecalculateVBO()
        {
            OGL.BindVertexArray(this._vao);
            OGL.EnableVertexAttribArray(1);
            this._instancedVBO.Bind();
            float[] dArr = new float[this.Points.Count * 3];
            for (int i = 0; i < this.Points.Count; ++i)
            {
                int dIdx = i * 3;
                DrawingPoint dp = this.Points[i];
                dArr[dIdx + 0] = dp.x;
                dArr[dIdx + 1] = dp.y;
                dArr[dIdx + 2] = dp.z;
            }

            this._instancedVBO.SetData(dArr);
        }

        public void InitGl()
        {
            this.SetupInitialModelData();
            this._vao = OGL.GenVertexArray();
            OGL.BindVertexArray(this._vao);
            this._vbo = new GPUBuffer(OpenTK.Graphics.OpenGL.BufferTarget.ArrayBuffer);
            this._vbo.Bind();
            this._vbo.SetData(this._modelRawPositions);
            this._instancedEBO = new GPUBuffer(OpenTK.Graphics.OpenGL.BufferTarget.ElementArrayBuffer);
            this._instancedEBO.Bind();
            this._instancedEBO.SetData(this._modelRawIndices);
            OGL.EnableVertexAttribArray(0);
            OGL.VertexAttribPointer(0, 3, OpenTK.Graphics.OpenGL.VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            OGL.EnableVertexAttribArray(1);
            this._instancedVBO = new GPUBuffer(OpenTK.Graphics.OpenGL.BufferTarget.ArrayBuffer);
            this._instancedVBO.Bind();
            OGL.VertexAttribPointer(1, 3, OpenTK.Graphics.OpenGL.VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            OGL.VertexAttribDivisor(1, 1);
            this._haveBuffers = true;
        }

        public void Free()
        {
            OGL.DeleteVertexArray(this._vao);
            this._instancedVBO.Dispose();
            this._instancedEBO.Dispose();
            this._vbo.Dispose();
        }

        public int Draw()
        {
            if (!this._haveBuffers)
            {
                this.InitGl();
            }

            if (this._needsVBORecalc)
            {
                this._needsVBORecalc = false;
                this.RecalculateVBO();
            }

            if (this._numInstances > 0)
            {
                OGL.BindVertexArray(this._vao);
                OGL.DrawElementsInstanced(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, this._modelRawIndices.Length, OpenTK.Graphics.OpenGL.DrawElementsType.UnsignedInt, IntPtr.Zero, this._numInstances);
            }

            return this._numInstances;
        }

        private void SetupInitialModelData()
        {
            if (this._modelRawIndices == null)
            {
                WavefrontObject.ReadPositionsAndIndices(IOVTT.ResourceToLines("VTT.Embed.sphere_mediumres.obj"), out this._modelRawPositions, out this._modelRawIndices);
                for (int i = 0; i < this._modelRawPositions.Length; i += 3)
                {
                    this._modelRawPositions[i + 0] *= this.Radius;
                    this._modelRawPositions[i + 1] *= this.Radius;
                    this._modelRawPositions[i + 2] *= this.Radius;
                }
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("id", this.ID);
            ret.SetGuid("oid", this.OwnerID);
            ret.Set("r", this.Radius);
            ret.SetVec4("c", this.Color);
            ret.SetArray("pts", this.Points.ToArray(), (n, c, v) => c.SetVec3(n, new Vector3(v.x, v.y, v.z)));
            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("id");
            this.OwnerID = e.GetGuid("oid");
            this.Radius = e.Get<float>("r");
            this.Color = e.GetVec4("c");
            this.Points.Clear();
            this.Points.AddRange(e.GetArray("pts", (n, c) => new DrawingPoint(c.GetVec3(n)), Array.Empty<DrawingPoint>()));
        }
    }

    public readonly struct DrawingPoint
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;

        public DrawingPoint(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public DrawingPoint(Vector3 vec)
        {
            this.x = vec.X;
            this.y = vec.Y;
            this.z = vec.Z;
        }

        public bool IsInRange(Vector3 mouse, float radius, float distance)
        {
            float dx = mouse.X - x;
            float dy = mouse.Y - y;
            float dz = mouse.Z - z;
            float dt = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            return dt <= distance + radius;
        }
    }
}
