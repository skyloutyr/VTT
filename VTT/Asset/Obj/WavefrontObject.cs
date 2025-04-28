namespace VTT.Asset.Obj
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Numerics;
    using System.Threading;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Util;

    public class WavefrontObject
    {
        private readonly VertexArray _vao;
        private readonly GPUBuffer _vbo;
        private readonly GPUBuffer? _ebo;
        private readonly int _numElements;

        public Vector3[] triangles;

        public WavefrontObject(string[] lines, VertexFormat desiredFormat)
        {
            List<Vector3> positions = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();

            List<(int X, int Y, int Z)> faces = new List<(int X, int Y, int Z)>();

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            foreach (string line in lines)
            {
                if (line.StartsWith("v ")) // Vertex data
                {
                    string[] d = line.Split(' ');
                    float x = float.Parse(d[1]);
                    float y = float.Parse(d[2]);
                    float z = float.Parse(d[3]);
                    positions.Add(new Vector3(x, y, z));
                    continue;
                }

                if (line.StartsWith("vt "))
                {
                    string[] d = line.Split(' ');
                    float x = float.Parse(d[1]);
                    float y = float.Parse(d[2]);
                    uvs.Add(new Vector2(x, y));
                    continue;
                }

                if (line.StartsWith("vn "))
                {
                    string[] d = line.Split(' ');
                    float x = float.Parse(d[1]);
                    float y = float.Parse(d[2]);
                    float z = float.Parse(d[3]);
                    normals.Add(new Vector3(x, y, z));
                    continue;
                }

                if (line.StartsWith("f "))
                {
                    string[] d = line.Split(' ');
                    for (int i = 1; i < d.Length; i++)
                    {
                        string dt = d[i];
                        string[] dd = dt.Split('/');
                        int x = int.Parse(dd[0]);
                        int y = dd.Length == 2 ? -1 : int.Parse(dd[1]); // Format may be v1//vn1
                        int z = dd.Length == 1 ? -1 : int.Parse(dd[^1]);
                        faces.Add(new (x, y, z));
                    }

                    continue;
                }
            }

            if (faces.Count % 3 != 0)
            {
                throw new Exception("Model not triangulated!");
            }

            if (desiredFormat == VertexFormat.Pos)
            {
                // If asking for position only we can use EBOs
                uint[] indices = new uint[faces.Count];
                for (int i = 0; i < faces.Count; ++i)
                {
                    indices[i] = (uint)(faces[i].X - 1);
                }

                float[] vertices = new float[positions.Count * 3];
                for (int i = 0; i < positions.Count; ++i)
                {
                    int idx = i * 3;
                    Vector3 p = positions[i];
                    vertices[idx + 0] = p.X;
                    vertices[idx + 1] = p.Y;
                    vertices[idx + 2] = p.Z;
                }

                this._numElements = indices.Length;
                this.triangles = new Vector3[indices.Length];
                for (int i = 0; i < this.triangles.Length; ++i)
                {
                    int j = (int)(indices[i] * 3);
                    this.triangles[i] = new Vector3(vertices[j + 0], vertices[j + 1], vertices[j + 2]);
                }

                this._vao = new VertexArray();
                this._vbo = new GPUBuffer(BufferTarget.Array);
                this._ebo = new GPUBuffer(BufferTarget.ElementArray);
                this._vao.Bind();
                this._vbo.Bind();
                this._vbo.SetData(vertices);
                this._ebo?.Bind();
                this._ebo?.SetData(indices);
                this._vao.Reset();
                desiredFormat.SetupVAO(this._vao);
            }
            else
            {
                List<Vertex> vertices = new List<Vertex>();
                List<Vector3> tris = new List<Vector3>();

                for (int i = 0; i < faces.Count; i += 3)
                {
                    (int X, int Y, int Z) v1 = faces[i + 0];
                    (int X, int Y, int Z) v2 = faces[i + 1];
                    (int X, int Y, int Z) v3 = faces[i + 2];

                    Vector3 p1 = positions[v1.X - 1];
                    Vector3 p2 = positions[v2.X - 1];
                    Vector3 p3 = positions[v3.X - 1];

                    tris.Add(p1);
                    tris.Add(p2);
                    tris.Add(p3);

                    Vector3 a = p2 - p1;
                    Vector3 b = p3 - p1;

                    Vector2 uv1 = uvs.Count > 0 && v1.Y != -1 ? uvs[v1.Y - 1] : Vector2.Zero;
                    Vector2 uv2 = uvs.Count > 0 && v2.Y != -1 ? uvs[v2.Y - 1] : Vector2.Zero;
                    Vector2 uv3 = uvs.Count > 0 && v3.Y != -1 ? uvs[v3.Y - 1] : Vector2.Zero;

                    Vector3 n1, n2, n3;
                    if (normals.Count > 0 && v1.Z != -1 && v2.Z != -1 && v3.Z != -1)
                    {
                        n1 = normals[v1.Z - 1];
                        n2 = normals[v2.Z - 1];
                        n3 = normals[v3.Z - 1];
                    }
                    else
                    {
                        n1 = n2 = n3 = new Vector3(
                            (a.Y * b.Z) - (a.Z * b.Y),
                            (a.Z * b.X) - (a.X * b.Z),
                            (a.X * b.Y) - (a.Y * b.X)
                        );
                    }

                    Vector2 deltaUV1 = uv2 - uv1;
                    Vector2 deltaUV2 = uv3 - uv1;
                    float f = 1.0f / ((deltaUV1.X * deltaUV2.Y) - (deltaUV2.X * deltaUV1.Y));
                    Vector4 tan = new Vector4(
                            f * ((deltaUV2.Y * a.X) - (deltaUV1.Y * b.X)),
                            f * ((deltaUV2.Y * a.Y) - (deltaUV1.Y * b.Y)),
                            f * ((deltaUV2.Y * a.Z) - (deltaUV1.Y * b.Z)), 1.0f
                        ).Normalized();
                    Vector4 bitan = new Vector4(
                            f * ((-deltaUV2.X * a.X) + (deltaUV1.X * b.X)),
                            f * ((-deltaUV2.X * a.Y) + (deltaUV1.X * b.Y)),
                            f * ((-deltaUV2.X * a.Z) + (deltaUV1.X * b.Z)), 1.0f
                        ).Normalized();

                    Vertex vert1 = new Vertex()
                    {
                        [VertexData.Position] = p1,
                        [VertexData.Normal] = n1,
                        [VertexData.UV] = uv1,
                        [VertexData.Tangent] = tan,
                        [VertexData.Bitangent] = bitan,
                        [VertexData.Color] = Vector4.One
                    };

                    Vertex vert2 = new Vertex()
                    {
                        [VertexData.Position] = p2,
                        [VertexData.Normal] = n2,
                        [VertexData.UV] = uv2,
                        [VertexData.Tangent] = tan,
                        [VertexData.Bitangent] = bitan,
                        [VertexData.Color] = Vector4.One
                    };

                    Vertex vert3 = new Vertex()
                    {
                        [VertexData.Position] = p3,
                        [VertexData.Normal] = n3,
                        [VertexData.UV] = uv3,
                        [VertexData.Tangent] = tan,
                        [VertexData.Bitangent] = bitan,
                        [VertexData.Color] = Vector4.One
                    };

                    vertices.Add(vert1);
                    vertices.Add(vert2);
                    vertices.Add(vert3);
                }

                this.triangles = tris.ToArray();
                List<float> data = new List<float>();
                foreach (Vertex v in vertices)
                {
                    data.AddRange(desiredFormat.ToArray(v));
                }

                this._numElements = vertices.Count;

                this._vao = new VertexArray();
                this._vbo = new GPUBuffer(BufferTarget.Array);
                this._vao.Bind();
                this._vbo.Bind();
                this._vbo.SetData(data.ToArray());
                this._vao.Reset();
                desiredFormat.SetupVAO(this._vao);
            }
        }

        public void Render()
        {
            this._vao.Bind();
            if (this._ebo.HasValue)
            {
                GL.DrawElements(PrimitiveType.Triangles, this._numElements, ElementsType.UnsignedInt, IntPtr.Zero);
            }
            else
            {
                GL.DrawArrays(PrimitiveType.Triangles, 0, this._numElements);
            }
        }

        public static void ReadPositionsAndIndices(string[] lines, out float[] vertices, out uint[] indices)
        {
            List<Vector3> positions = new List<Vector3>();

            List<(int X, int Y, int Z)> faces = new List<(int X, int Y, int Z)>();

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            foreach (string line in lines)
            {
                if (line.StartsWith("v ")) // Vertex data
                {
                    string[] d = line.Split(' ');
                    float x = float.Parse(d[1]);
                    float y = float.Parse(d[2]);
                    float z = float.Parse(d[3]);
                    positions.Add(new Vector3(x, y, z));
                    continue;
                }

                if (line.StartsWith("f "))
                {
                    string[] d = line.Split(' ');
                    for (int i = 1; i < d.Length; i++)
                    {
                        string dt = d[i];
                        string[] dd = dt.Split('/');
                        int x = int.Parse(dd[0]);
                        int y = dd.Length == 2 ? -1 : int.Parse(dd[1]); // Format may be v1//vn1
                        int z = dd.Length == 1 ? -1 : int.Parse(dd[^1]);
                        faces.Add(new (x, y, z));
                    }

                    continue;
                }
            }

            if (faces.Count % 3 != 0)
            {
                throw new Exception("Model not triangulated!");
            }

            // If asking for position only we can use EBOs
            indices = new uint[faces.Count];
            for (int i = 0; i < faces.Count; ++i)
            {
                indices[i] = (uint)(faces[i].X - 1);
            }

            vertices = new float[positions.Count * 3];
            for (int i = 0; i < positions.Count; ++i)
            {
                int idx = i * 3;
                Vector3 p = positions[i];
                vertices[idx + 0] = p.X;
                vertices[idx + 1] = p.Y;
                vertices[idx + 2] = p.Z;
            }
        }
    }
}
