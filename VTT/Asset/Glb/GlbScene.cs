namespace VTT.Asset.Glb
{
    using glTFLoader;
    using glTFLoader.Schema;
    using Newtonsoft.Json.Linq;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Util;

    public class GlbScene
    {
        public List<GlbObject> RootObjects { get; } = new List<GlbObject>();
        public List<GlbMaterial> Materials { get; } = new List<GlbMaterial>();
        public GlbMaterial DefaultMaterial { get; set; }

        public GlbObject DirectionalLight { get; set; }
        public GlbObject Camera { get; set; }
        public GlbObject PortraitCamera { get; set; }
        public List<GlbObject> Lights { get; } = new List<GlbObject>();
        public List<GlbObject> Meshes { get; } = new List<GlbObject>();

        public GlbObject SimplifiedRaycastMesh { get; }

        public AABox CombinedBounds { get; set; }
        public bool HasTransparency { get; set; }

        public volatile bool glReady;

        private readonly bool _createdOnGlThread;
        private volatile int _glRequestsTodo;
        private readonly bool _checkGlRequests;

        public GlbScene()
        {
        }

        public unsafe GlbScene(Stream modelStream)
        {
            this._createdOnGlThread = Client.Instance.Frontend.CheckThread();
            // Have to do these stream gymnastics because khronos' Gltf implementation implicitly closes the stream it reads data from...
            this.LoadGLBFromStream(modelStream, out Gltf g, out byte[] bin);
            List<GlbObject> objs = new List<GlbObject>();
            this.PopulateGlbObjects(g, objs);
            this.LoadMaterials(g, bin);
            List<GlbLight> lights = new List<GlbLight>();
            this.LoadLights(g, lights);

            for (int i = 0; i < objs.Count; ++i)
            {
                GlbObject o = objs[i];
                if (o._node.Children != null)
                {
                    foreach (int j in o._node.Children)
                    {
                        GlbObject child = objs[j];
                        o.Children.Add(child);
                        child.Parent = o;
                    }
                }

                if (o._node.Camera != null)
                {
                    glTFLoader.Schema.Camera cam = g.Cameras[o._node.Camera.Value];
                    o.Camera = cam;
                    o.Type = GlbObjectType.Camera;
                    if (o._node.Name.Equals("portrait_camera", StringComparison.OrdinalIgnoreCase) || (o.Parent != null && o.Parent._node.Name.Equals("portrait_camera", StringComparison.OrdinalIgnoreCase)))
                    {
                        this.PortraitCamera = o;
                    }
                    else
                    {
                        this.Camera = o;
                    }
                }

                if (o._node.Mesh != null)
                {
                    Mesh m = g.Meshes[o._node.Mesh.Value];
                    foreach (MeshPrimitive mp in m.Primitives)
                    {
                        if (mp.Mode != MeshPrimitive.ModeEnum.TRIANGLES)
                        {
                            throw new Exception("Model is not defined as an array of triangles, triangulate before export!");
                        }

                        GlbMesh glbm = new GlbMesh();

                        // vec3 pos
                        // vec2 uv
                        // vec3 norm
                        // vec3 tangent
                        // vec3 bitangent
                        // vec4 color
                        List<Vector3> positions = new List<Vector3>();
                        List<Vector2> uvs = new List<Vector2>();
                        List<Vector3> normals = new List<Vector3>();
                        List<Vector4> tangents = new List<Vector4>();
                        List<Vector4> bitangents = new List<Vector4>();
                        List<Vector4> colors = new List<Vector4>();

                        int vertexSize = 3 + 2 + 3 + 3 + 3 + 4;

                        foreach (KeyValuePair<string, int> kv in mp.Attributes)
                        {
                            string name = kv.Key;
                            Accessor accessor = g.Accessors[kv.Value];
                            int bvIdx = accessor.BufferView != null ? accessor.BufferView.Value : -1;
                            BufferView bv = bvIdx == -1 ? null : g.BufferViews[bvIdx];
                            byte[] contextArray = new byte[bv == null ? 0 : bv.ByteLength];
                            if (bv != null)
                            {
                                Array.Copy(bin, bv.ByteOffset, contextArray, 0, bv.ByteLength);
                            }

                            if (name.Equals("POSITION"))
                            {
                                for (int index = 0; index < contextArray.Length; index += 12) // pos == vec3
                                {
                                    positions.Add(new (
                                        BitConverter.ToSingle(contextArray, index + 0),
                                        BitConverter.ToSingle(contextArray, index + 4),
                                        BitConverter.ToSingle(contextArray, index + 8)
                                    ));
                                }
                            }

                            if (name.Equals("TEXCOORD_0"))
                            {
                                Accessor.ComponentTypeEnum cType = accessor.ComponentType;
                                int stride = 2 * (cType == Accessor.ComponentTypeEnum.FLOAT ? 4 : cType == Accessor.ComponentTypeEnum.UNSIGNED_SHORT ? 2 : 1);
                                for (int index = 0; index < contextArray.Length; index += stride)
                                {
                                    float s = 0;
                                    float t = 0;
                                    switch (cType)
                                    {
                                        case Accessor.ComponentTypeEnum.FLOAT:
                                        {
                                            s = BitConverter.ToSingle(contextArray, index + 0);
                                            t = BitConverter.ToSingle(contextArray, index + 4);
                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                                        {
                                            s = ((float)BitConverter.ToUInt16(contextArray, index + 0) / ushort.MaxValue);
                                            t = ((float)BitConverter.ToUInt16(contextArray, index + 2) / ushort.MaxValue);
                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                                        {
                                            s = ((float)contextArray[index + 0] / byte.MaxValue);
                                            t = ((float)contextArray[index + 1] / byte.MaxValue);
                                            break;
                                        }
                                    }

                                    uvs.Add(new Vector2(s, t));
                                }
                            }

                            if (name.Equals("NORMAL"))
                            {
                                for (int index = 0; index < contextArray.Length; index += 12) // normal == vec3
                                {
                                    normals.Add(new(
                                        BitConverter.ToSingle(contextArray, index + 0),
                                        BitConverter.ToSingle(contextArray, index + 4),
                                        BitConverter.ToSingle(contextArray, index + 8)
                                    ));
                                }
                            }

                            if (name.Equals("TANGENT"))
                            {
                                for (int index = 0; index < contextArray.Length; index += 16) // tangent == vec4
                                {
                                    tangents.Add(new(
                                        BitConverter.ToSingle(contextArray, index + 0),
                                        BitConverter.ToSingle(contextArray, index + 4),
                                        BitConverter.ToSingle(contextArray, index + 8),
                                        BitConverter.ToSingle(contextArray, index + 12)
                                    ));
                                }
                            }

                            if (name.Equals("COLOR_0"))
                            {
                                Accessor.TypeEnum aType = accessor.Type;
                                Accessor.ComponentTypeEnum cType = accessor.ComponentType;

                                int elementSize = aType == Accessor.TypeEnum.VEC4 ? 4 : 3;
                                int stride = elementSize * (cType == Accessor.ComponentTypeEnum.FLOAT ? 4 : cType == Accessor.ComponentTypeEnum.UNSIGNED_SHORT ? 2 : 1);
                                for (int index = 0; index < contextArray.Length; index += stride)
                                {
                                    float cr = 0;
                                    float cg = 0;
                                    float cb = 0;
                                    float ca = 1.0f;
                                    switch (cType)
                                    {
                                        case Accessor.ComponentTypeEnum.FLOAT:
                                        {
                                            cr = BitConverter.ToSingle(contextArray, index + 0);
                                            cg = BitConverter.ToSingle(contextArray, index + 4);
                                            cb = BitConverter.ToSingle(contextArray, index + 8);
                                            if (elementSize == 4)
                                            {
                                                ca = BitConverter.ToSingle(contextArray, index + 12);
                                            }

                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                                        {
                                            cr = ((float)BitConverter.ToUInt16(contextArray, index + 0) / ushort.MaxValue);
                                            cg = ((float)BitConverter.ToUInt16(contextArray, index + 2) / ushort.MaxValue);
                                            cb = ((float)BitConverter.ToUInt16(contextArray, index + 4) / ushort.MaxValue);
                                            if (elementSize == 4)
                                            {
                                                ca = ((float)BitConverter.ToUInt16(contextArray, index + 6) / ushort.MaxValue);
                                            }

                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                                        {
                                            cr = ((float)contextArray[index + 0] / byte.MaxValue);
                                            cg = ((float)contextArray[index + 1] / byte.MaxValue);
                                            cb = ((float)contextArray[index + 2] / byte.MaxValue);
                                            if (elementSize == 4)
                                            {
                                                ca = ((float)contextArray[index + 3] / byte.MaxValue);
                                            }

                                            break;
                                        }
                                    }

                                    colors.Add(new Vector4(cr, cg, cb, ca));
                                }
                            }

                            glbm.AmountToRender = accessor.Count;
                        }

                        uint[] indices;
                        if (mp.Indices != null)
                        {
                            Accessor accessor = g.Accessors[mp.Indices.Value];
                            glbm.AmountToRender = accessor.Count;

                            int stride =
                                accessor.ComponentType == Accessor.ComponentTypeEnum.UNSIGNED_BYTE ? 1 :
                                accessor.ComponentType == Accessor.ComponentTypeEnum.UNSIGNED_SHORT ? 2 : 4;

                            BufferView indexView = g.BufferViews[accessor.BufferView.Value];
                            byte[] indexByteBuffer = new byte[indexView.ByteLength];
                            indices = new uint[indexView.ByteLength / stride];

                            if (!string.IsNullOrEmpty(g.Buffers[indexView.Buffer].Uri))
                            {
                                throw new Exception("Buffer must refer to built-in binary buffer!");
                            }

                            Array.Copy(bin, indexView.ByteOffset, indexByteBuffer, 0, indexView.ByteLength);
                            for (int j = 0; j < indexView.ByteLength; j += stride)
                            {
                                switch (stride)
                                {
                                    case 1:
                                    {
                                        indices[j / stride] = indexByteBuffer[j];
                                        break;
                                    }

                                    case 2:
                                    {
                                        indices[j / stride] = BitConverter.ToUInt16(indexByteBuffer, j);
                                        break;
                                    }

                                    case 4:
                                    {
                                        indices[j / stride] = BitConverter.ToUInt32(indexByteBuffer, j);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            indices = new uint[positions.Count];
                            for (uint j = 0; j < indices.Length; ++j)
                            {
                                indices[j] = j;
                            }
                        }

                        if (uvs.Count == 0)
                        {
                            for (int j = 0; j < positions.Count; ++j)
                            {
                                uvs.Add(new Vector2(0, 0));
                            }
                        }

                        if (normals.Count == 0)
                        {
                            Vector3[] normalsArray = new Vector3[positions.Count];
                            for (int j = 0; j < indices.Length; j += 3)
                            {
                                int i1 = (int)indices[j + 0];
                                int i2 = (int)indices[j + 1];
                                int i3 = (int)indices[j + 2];
                                Vector3 pos1 = positions[i1];
                                Vector3 pos2 = positions[i2];
                                Vector3 pos3 = positions[i3];
                                Vector3 a = pos2 - pos1;
                                Vector3 b = pos3 - pos1;
                                Vector3 normal = new Vector3(
                                    (a.Y * b.Z) - (a.Z * b.Y),
                                    (a.Z * b.X) - (a.X * b.Z),
                                    (a.X * b.Y) - (a.Y * b.X)
                                );

                                normalsArray[i1] = normal;
                                normalsArray[i2] = normal;
                                normalsArray[i3] = normal;
                            }

                            normals.AddRange(normalsArray);
                            tangents.Clear();
                        }

                        if (tangents.Count == 0) // Uh-oh, no tangets!
                        {
                            Vector4[] tangentArray = new Vector4[positions.Count];
                            Vector4[] bitangentArray = new Vector4[positions.Count];
                            for (int j = 0; j < indices.Length; j += 3)
                            {
                                int i1 = (int)indices[j + 0];
                                int i2 = (int)indices[j + 1];
                                int i3 = (int)indices[j + 2];
                                Vector3 pos1 = positions[i1];
                                Vector3 pos2 = positions[i2];
                                Vector3 pos3 = positions[i3];
                                Vector3 nor1 = normals[i1];
                                Vector3 nor2 = normals[i2];
                                Vector3 nor3 = normals[i3];
                                Vector2 uv1 = uvs[i1];
                                Vector2 uv2 = uvs[i2];
                                Vector2 uv3 = uvs[i3];
                                Vector3 edge1 = pos2 - pos1;
                                Vector3 edge2 = pos3 - pos1;
                                Vector2 deltaUV1 = uv2 - uv1;
                                Vector2 deltaUV2 = uv3 - uv1;
                                float f = 1.0f / ((deltaUV1.X * deltaUV2.Y) - (deltaUV2.X * deltaUV1.Y));
                                Vector4 tan = new Vector4(
                                        f * ((deltaUV2.Y * edge1.X) - (deltaUV1.Y * edge2.X)),
                                        f * ((deltaUV2.Y * edge1.Y) - (deltaUV1.Y * edge2.Y)),
                                        f * ((deltaUV2.Y * edge1.Z) - (deltaUV1.Y * edge2.Z)), 1.0f
                                    ).Normalized();
                                Vector4 bitan = new Vector4(
                                        f * ((-deltaUV2.X * edge1.X) + (deltaUV1.X * edge2.X)),
                                        f * ((-deltaUV2.X * edge1.Y) + (deltaUV1.X * edge2.Y)),
                                        f * ((-deltaUV2.X * edge1.Z) + (deltaUV1.X * edge2.Z)), 1.0f
                                    ).Normalized();

                                tangentArray[i1] = tan;
                                tangentArray[i2] = tan;
                                tangentArray[i3] = tan;
                                bitangentArray[i1] = bitan;
                                bitangentArray[i2] = bitan;
                                bitangentArray[i3] = bitan;
                            }

                            tangents.AddRange(tangentArray);
                            bitangents.AddRange(bitangentArray);
                        }

                        if (bitangents.Count == 0)
                        {
                            for (int j = 0; j < tangents.Count; ++j)
                            {
                                Vector3 normalVec = normals[j];
                                Vector4 tangentVec = tangents[j];
                                Vector4 bitangentVec = new Vector4(Vector3.Cross(normalVec, tangentVec.Xyz) * tangentVec.W, 1.0f);
                                bitangents.Add(bitangentVec);
                            }
                        }

                        if (colors.Count == 0)
                        {
                            for (int j = 0; j < positions.Count; ++j)
                            {
                                colors.Add(new Vector4(1, 1, 1, 1));
                            }
                        }

                        float[] vBuffer = new float[positions.Count * vertexSize];
                        int vBufIndex = 0;
                        Vector3 posMin = default;
                        Vector3 posMax = default;
                        Matrix4 mat = this.LookupChildMatrix(o);
                        for (int j = 0; j < positions.Count; ++j)
                        {
                            Vector4 mpos = new Vector4(positions[j], 0.0f);
                            mpos = mpos * mat;

                            posMin = Vector3.ComponentMin(posMin, mpos.Xyz);
                            posMax = Vector3.ComponentMax(posMax, mpos.Xyz);
                            // vec3 pos
                            // vec2 uv
                            // vec3 norm
                            // vec3 tangent
                            // vec3 bitangent
                            // vec4 color
                            Vector3 pos = positions[j]; 
                            Vector2 uv = uvs[j]; 
                            Vector3 norm = normals[j]; 
                            Vector3 tan = tangents[j].Xyz; 
                            Vector3 bitan = bitangents[j].Xyz; 
                            Vector4 color = colors[j];
                            vBuffer[vBufIndex++] = pos.X;
                            vBuffer[vBufIndex++] = pos.Y;
                            vBuffer[vBufIndex++] = pos.Z;
                            vBuffer[vBufIndex++] = uv.X;
                            vBuffer[vBufIndex++] = uv.Y;
                            vBuffer[vBufIndex++] = norm.X;
                            vBuffer[vBufIndex++] = norm.Y;
                            vBuffer[vBufIndex++] = norm.Z;
                            vBuffer[vBufIndex++] = tan.X;
                            vBuffer[vBufIndex++] = tan.Y;
                            vBuffer[vBufIndex++] = tan.Z;
                            vBuffer[vBufIndex++] = bitan.X;
                            vBuffer[vBufIndex++] = bitan.Y;
                            vBuffer[vBufIndex++] = bitan.Z;
                            vBuffer[vBufIndex++] = color.X;
                            vBuffer[vBufIndex++] = color.Y;
                            vBuffer[vBufIndex++] = color.Z;
                            vBuffer[vBufIndex++] = color.W;
                        }

                        List<System.Numerics.Vector3> simplifiedTriangles = new List<System.Numerics.Vector3>();
                        List<float> areaSums = new List<float>();
                        float areaSum = 0f;
                        for (int j = 0; j < indices.Length; j += 3)
                        {
                            int index0 = (int)indices[j + 0];
                            int index1 = (int)indices[j + 1];
                            int index2 = (int)indices[j + 2];
                            System.Numerics.Vector3 a = positions[index0].SystemVector();
                            System.Numerics.Vector3 b = positions[index1].SystemVector();
                            System.Numerics.Vector3 c = positions[index2].SystemVector();
                            simplifiedTriangles.Add(a);
                            simplifiedTriangles.Add(b);
                            simplifiedTriangles.Add(c);
                            System.Numerics.Vector3 ab = b - a;
                            System.Numerics.Vector3 ac = c - a;
                            float l = System.Numerics.Vector3.Cross(ab, ac).Length() * 0.5f;
                            if (!float.IsNaN(l)) // Degenerate triangle
                            {
                                areaSum += l;
                            }

                            areaSums.Add(areaSum);
                        }

                        glbm.simplifiedTriangles = simplifiedTriangles.ToArray();
                        glbm.areaSums = areaSums.ToArray();
                        glbm.Bounds = new AABox(posMin, posMax); // Bounds generated from transformed positions
                        glbm.VertexBuffer = vBuffer;
                        glbm.IndexBuffer = indices;
                        glbm.Material = mp.Material != null ? this.Materials[mp.Material.Value] : this.DefaultMaterial;

                        this.LoadMeshGl(glbm);
                        o.Meshes.Add(glbm);
                    }

                    if (o._node.Name.Equals("simplified_raycast", StringComparison.OrdinalIgnoreCase))
                    {
                        o.Type = GlbObjectType.RaycastMesh;
                        this.SimplifiedRaycastMesh = o;
                    }
                    else
                    {
                        o.Type = GlbObjectType.Mesh;
                    }

                    AABox box = o.Meshes[0].Bounds;
                    foreach (GlbMesh mesh in o.Meshes)
                    {
                        box = box.Union(mesh.Bounds);
                    }

                    o.Bounds = box;
                    this.Meshes.Add(o);
                }

                if (o._node.Extensions != null)
                {
                    string khr_lights = "KHR_lights_punctual";
                    if (o._node.Extensions.ContainsKey(khr_lights))
                    {
                        int l_Index = ((JObject)o._node.Extensions[khr_lights])["light"].ToObject<int>();
                        o.Light = lights[l_Index];
                        o.Type = GlbObjectType.Light;
                        this.Lights.Add(o);
                        if (this.DirectionalLight == null || o.Light.LightType == 0)
                        {
                            this.DirectionalLight = o;
                        }
                    }
                }
            }

            for (int i = 0; i < objs.Count; i++)
            {
                GlbObject o = objs[i];
                if (o.Parent == null)
                {
                    this.RootObjects.Add(o);
                }
            }

            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._node = null;
                this.CombinedBounds = this.CombinedBounds.Union(objs[i].Bounds);
            }

            this._checkGlRequests = true;
            if (this._glRequestsTodo <= 0)
            {
                this.glReady = true;
            }
        }

        public void Dispose()
        {
            foreach (GlbObject o in this.RootObjects)
            {
                o.Dispose();
            }

            foreach (GlbMaterial mat in this.Materials)
            {
                mat.Dispose();
            }
        }

        private void LoadLights(Gltf g, List<GlbLight> lights)
        {
            if (g.Extensions != null && g.Extensions.ContainsKey("KHR_lights_punctual"))
            {
                JObject e_object = (JObject)g.Extensions["KHR_lights_punctual"];
                if (e_object.ContainsKey("lights"))
                {
                    KhrLight[] kls = e_object["lights"].ToObject<KhrLight[]>();
                    for (int i = 0; i < kls.Length; ++i)
                    {
                        KhrLight kl = kls[i];
                        GlbLight glbl = new GlbLight(
                            new Vector4(kl.Color[0], kl.Color[1], kl.Color[2], kl.Color.Length == 4 ? kl.Color[3] : 1.0f),
                            kl.Intensity,
                            kl.Type
                        );

                        lights.Add(glbl);
                    }
                }
            }
        }

        private void LoadMaterials(Gltf g, byte[] bin)
        {
            for (int i = 0; i < g.Materials.Length; ++i)
            {
                GlbMaterial glmat = new GlbMaterial();
                Material mat = g.Materials[i];
                glmat.AlphaCutoff = mat.AlphaCutoff;
                glmat.AlphaMode = mat.AlphaMode;
                this.HasTransparency |= mat.AlphaMode != Material.AlphaModeEnum.OPAQUE;
                glmat.BaseColorFactor = new Vector4(mat.PbrMetallicRoughness.BaseColorFactor[0], mat.PbrMetallicRoughness.BaseColorFactor[1], mat.PbrMetallicRoughness.BaseColorFactor[2], mat.PbrMetallicRoughness.BaseColorFactor[3]);
                glmat.BaseColorTexture = this.LoadIndependentTextureFromBinary(g, mat.PbrMetallicRoughness.BaseColorTexture?.Index, bin, new Rgba32(0, 0, 0, 1f), PixelInternalFormat.SrgbAlpha);
                glmat.BaseColorAnimation = new TextureAnimation(null);
                glmat.CullFace = !mat.DoubleSided;
                glmat.EmissionTexture = this.LoadIndependentTextureFromBinary(g, mat.EmissiveTexture?.Index, bin, new Rgba32(0, 0, 0, 1f));
                glmat.EmissionAnimation = new TextureAnimation(null);
                glmat.MetallicFactor = mat.PbrMetallicRoughness.MetallicFactor;
                glmat.OcclusionMetallicRoughnessTexture = this.LoadAOMRTextureFromBinary(g, mat.OcclusionTexture?.Index, mat.PbrMetallicRoughness.MetallicRoughnessTexture?.Index, mat.PbrMetallicRoughness.MetallicRoughnessTexture?.Index, bin); ;
                glmat.OcclusionMetallicRoughnessAnimation = new TextureAnimation(null);
                glmat.Name = glmat.Name;
                glmat.NormalTexture = this.LoadIndependentTextureFromBinary(g, mat.NormalTexture?.Index, bin, new Rgba32(0, 1, 0, 1f));
                glmat.NormalAnimation = new TextureAnimation(null);
                glmat.RoughnessFactor = mat.PbrMetallicRoughness.RoughnessFactor;
                this.Materials.Add(glmat);
            }

            this.DefaultMaterial = new GlbMaterial();
            this.DefaultMaterial.BaseColorTexture = this.LoadBaseTexture(new Rgba32(0, 0, 0, 1f));
            this.DefaultMaterial.BaseColorAnimation = new TextureAnimation(null);
            this.DefaultMaterial.NormalTexture = this.LoadBaseTexture(new Rgba32(0, 1, 0, 1f));
            this.DefaultMaterial.NormalAnimation = new TextureAnimation(null);
            this.DefaultMaterial.EmissionTexture = this.LoadBaseTexture(new Rgba32(0, 0, 0, 1f));
            this.DefaultMaterial.EmissionAnimation = new TextureAnimation(null);
            this.DefaultMaterial.OcclusionMetallicRoughnessTexture = this.LoadBaseTexture(new (1, 1, 1, 1f));
            this.DefaultMaterial.OcclusionMetallicRoughnessAnimation = new TextureAnimation(null);
            this.DefaultMaterial.AlphaMode = Material.AlphaModeEnum.OPAQUE;
        }

        private VTT.GL.Texture LoadBaseTexture(Rgba32 color)
        {
            Image<Rgba32> img = new Image<Rgba32>(1, 1);
            img[0, 0] = color;
            VTT.GL.Texture tex = this.LoadGLTexture(img, new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT });
            return tex;
        }

        private void PopulateGlbObjects(Gltf g, List<GlbObject> objs)
        {
            for (int i = 0; i < g.Nodes.Length; i++)
            {
                Node n = g.Nodes[i];
                GlbObject glbO = new GlbObject(n)
                {
                    Name = n.Name,
                    Position = new Vector3(n.Translation[0], n.Translation[1], n.Translation[2]),
                    Rotation = new Quaternion(n.Rotation[0], n.Rotation[1], n.Rotation[2], n.Rotation[3]),
                    Scale = new Vector3(n.Scale[0], n.Scale[1], n.Scale[2])
                };

                objs.Add(glbO);
            }
        }

        private void LoadGLBFromStream(Stream modelStream, out Gltf g, out byte[] bin)
        {
            MemoryStream sCopy = new MemoryStream();
            MemoryStream sGltf = new MemoryStream();
            modelStream.CopyTo(sCopy);
            sCopy.Position = 0;
            sCopy.CopyTo(sGltf);
            sCopy.Position = 0;
            sGltf.Position = 0;
            modelStream.Close();
            modelStream.Dispose();
            g = Interface.LoadModel(sCopy);
            bin = Interface.LoadBinaryBuffer(sGltf);
            sCopy.Dispose();
            sGltf.Dispose();
        }

        private VTT.GL.Texture LoadAOMRTextureFromBinary(Gltf g, int? ao, int? m, int? r, byte[] bin)
        {
            // All images are present and refer to the same internal image, assume compressed rgb imgage, read and pass along as independant
            if (ao != null && m != null && r != null && ao.Value == m.Value && m.Value == r.Value)
            {
                return this.LoadIndependentTextureFromBinary(g, ao, bin, default, PixelInternalFormat.Rgba, i => i.ProcessPixelRows(a =>
                    {
                        for (int r = 0; r < a.Height; ++r)
                        {
                            Span<Rgba32> span = a.GetRowSpan(r);
                            for (int t = 0; t < span.Length; ++t)
                            {
                                Rgba32 cc = span[t];
                                span[t] = new Rgba32(cc.R, cc.B, cc.G, cc.A);
                            }
                        }
                    }));
            }

            Image<Rgba32> imgb;
            // We have the following possibilities:
            // No textures defined, return white canvas.
            // Metallic defined, others undefined
            // Roughness defined, others undefined
            // Metallic and roughness defined and point to the same texture, AO undefined
            // Metallic and roughness defined, point to different textures, AO undefined
            // Metallic and AO defined and point to the same texture, rougness undefined
            // Metallic and AO defined, point to different textures, rougness undefined
            // Roughness and AO defined and point to the same texture, metallic undefined
            // Roughness and AO defined, point to different textures, metallic undefined
            // Attempt to ignore most of these and load images as independent, then combine together

            if (ao == null && m == null && r == null) // No textures defined, return whitepixel by spec
            {
                imgb = new Image<Rgba32>(1, 1);
                imgb[0, 0] = new Rgba32(1, 1, 1, 1f);
                VTT.GL.Texture texb = this.LoadGLTexture(imgb, new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT });
                return texb;
            }

            Image<Rgba32> imgAO = ao.HasValue ? this.LoadBinaryImage(g, bin, ao.Value) : new Image<Rgba32>(1, 1) { [0, 0] = new Rgba32(1, 1, 1, 1f) };
            Image<Rgba32> imgM = m.HasValue ? this.LoadBinaryImage(g, bin, m.Value) : new Image<Rgba32>(1, 1) { [0, 0] = new Rgba32(1, 1, 1, 1f) };
            Image<Rgba32> imgR = r.HasValue ? this.LoadBinaryImage(g, bin, r.Value) : new Image<Rgba32>(1, 1) { [0, 0] = new Rgba32(1, 1, 1, 1f) };
            int w = Math.Max(Math.Max(imgAO.Width, imgM.Width), imgR.Width);
            int h = Math.Max(Math.Max(imgAO.Height, imgM.Height), imgR.Height);
            imgb = new Image<Rgba32>(w, h);
            for (int x = 0; x < w; ++x)
            {
                for (int y = 0; y < h; ++y)
                {
                    int xR = (int)Math.Floor(((double)x) / w * imgR.Width);
                    int yR = (int)Math.Floor(((double)y) / h * imgR.Height);
                    Rgba32 pixelRoughness = imgR[xR, yR];
                    xR = (int)Math.Floor(((double)x) / w * imgM.Width);
                    yR = (int)Math.Floor(((double)y) / h * imgM.Height);
                    Rgba32 pixelMetallic = imgM[xR, yR];
                    xR = (int)Math.Floor(((double)x) / w * imgAO.Width);
                    yR = (int)Math.Floor(((double)y) / h * imgAO.Height);
                    Rgba32 pixelAO = imgAO[xR, yR];
                    Rgba32 finalValue = new Rgba32(pixelAO.R, pixelMetallic.B, pixelRoughness.G, (byte)255);
                    imgb[x, y] = finalValue;
                }
            }

            VTT.GL.Texture glTex = this.LoadGLTexture(imgb, new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT });
            imgAO.Dispose();
            imgM.Dispose();
            imgR.Dispose();

            return glTex;
        }

        private VTT.GL.Texture LoadIndependentTextureFromBinary(Gltf g, int? tIndex, byte[] bin, Rgba32 defaultValue, PixelInternalFormat pif = PixelInternalFormat.Rgba, Action<Image<Rgba32>> processor = null)
        {
            if (tIndex == null) // No independent texture present
            {
                Image<Rgba32> imgb = new Image<Rgba32>(1, 1);
                imgb[0, 0] = defaultValue;
                processor?.Invoke(imgb);
                VTT.GL.Texture texb = this.LoadGLTexture(imgb, new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT });
                return texb;
            }

            glTFLoader.Schema.Texture tex = g.Textures[tIndex.Value];
            Image<Rgba32> img = this.LoadBinaryImage(g, bin, tex.Source.Value);
            processor?.Invoke(img);
            Sampler s = tex.Sampler.HasValue
                ? g.Samplers[tex.Sampler.Value]
                : new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT };
            VTT.GL.Texture glTex = this.LoadGLTexture(img, s);
            return glTex;
        }

        private Image<Rgba32> LoadBinaryImage(Gltf g, byte[] bin, int id)
        {
            glTFLoader.Schema.Image refImg = g.Images[id];
            if (refImg.BufferView == null)
            {
                throw new Exception("Image specified that is not embedded, that is not allowed!");
            }

            BufferView bv = g.BufferViews[refImg.BufferView.Value];
            byte[] imgData = new byte[bv.ByteLength];
            Array.Copy(bin, bv.ByteOffset, imgData, 0, bv.ByteLength);
            Image<Rgba32> img = SixLabors.ImageSharp.Image.Load<Rgba32>(imgData);
            return img;
        }

        private readonly MatrixStack _modelStack = new MatrixStack() { Reversed = true };
        public void Render(ShaderProgram shader, Matrix4 baseMatrix, Matrix4 projection, Matrix4 view, double textureAnimationIndex, Action<GlbMesh> renderer = null)
        {
            if (!this.glReady)
            {
                return;
            }

            this._modelStack.Push(baseMatrix);
            foreach (GlbObject o in this.RootObjects)
            {
                o.Render(shader, this._modelStack, projection, view, textureAnimationIndex, renderer);
            }

            this._modelStack.Pop();
        }

        public Image<Rgba32> CreatePreview(ShaderProgram shader, int width, int height, Vector4 clearColor, bool portrait = false)
        {
            // Create camera
            glTFLoader.Schema.Camera sceneCamera = portrait ? (this.PortraitCamera?.Camera ?? this.Camera.Camera) : this.Camera.Camera;
            GlbObject cameraObject = portrait ? (this.PortraitCamera ?? this.Camera) : this.Camera;
            Matrix4 mat = this.LookupChildMatrix(cameraObject);
            Quaternion q = mat.ExtractRotation();
            Vector3 camLook = (q * new Vector4(0, 0, -1, 1)).Xyz.Normalized();
            Vector3 camPos = mat.ExtractTranslation();
            Util.Camera camera = new VectorCamera(camPos, camLook);
            camera.Projection = Matrix4.CreatePerspectiveFieldOfView(sceneCamera.Perspective.Yfov, (float)width / height, sceneCamera.Perspective.Znear, 100f);
            camera.RecalculateData(assumedUpAxis: Vector3.UnitZ);

            // Create sun
            DirectionalLight sun;
            GlbLight? sceneSun = this.DirectionalLight?.Light;
            if (sceneSun != null)
            {
                mat = this.LookupChildMatrix(this.DirectionalLight);
                q = mat.ExtractRotation();
                Vector3 lightDir = new Vector3(0, 0, -1);
                lightDir = (q * new Vector4(lightDir, 1.0f)).Xyz;
                sun = new DirectionalLight(lightDir.Normalized(), sceneSun.Value.Color.Xyz);
            }
            else
            {
                sun = new DirectionalLight(-Vector3.UnitZ, Vector3.Zero);
            }

            // Create framebuffer
            VTT.GL.Texture tex = new VTT.GL.Texture(TextureTarget.Texture2D);
            int fbo = GL.GenFramebuffer();
            int lastFbo = GL.GetInteger(GetPName.FramebufferBinding);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.ActiveTexture(TextureUnit.Texture0);
            tex.Bind();
            tex.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.SrgbAlpha, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);
            int rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, rbo);
            FramebufferErrorCode fec = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (fec != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Could not complete renderbuffer!");
            }

            int[] data = new int[4];
            GL.GetInteger(GetPName.Viewport, data);

            shader.Bind();
            Client.Instance.Frontend.Renderer.ObjectRenderer.SetDummyUBO(camera, sun, clearColor, Client.Instance.Settings.UseUBO ? null : shader);
            shader["ambient_intensity"].Set(0.03f);

            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            plr.Clear();
            plr.ProcessScene(Matrix4.Identity, this);
            plr.DrawLights(null, false, null);
            plr.UniformLights(shader);

            GL.ActiveTexture(TextureUnit.Texture14);
            shader["dl_shadow_map"].Set(14);
            Client.Instance.Frontend.Renderer.White.Bind();
            GL.ActiveTexture(TextureUnit.Texture13);
            shader["pl_shadow_maps"].Set(13);
            plr.DepthMap.Bind();
            GL.ActiveTexture(TextureUnit.Texture0);

            shader["alpha"].Set(1.0f);
            shader["tint_color"].Set(Vector4.One);

            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(shader);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(0, 0, width, height);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            //GL.ClearColor(0.03f, 0.03f, 0.03f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            this.Render(shader, Matrix4.Identity, camera.Projection, camera.View, 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            tex.Bind();
            Image<Rgba32> retImg = tex.GetImage<Rgba32>();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);
            tex.Dispose();
            GL.DeleteRenderbuffer(rbo);
            GL.Viewport(data[0], data[1], data[2], data[3]);

            retImg.Mutate(x => x.Flip(FlipMode.Vertical));
            return retImg;
        }

        private Matrix4 LookupChildMatrix(GlbObject obj)
        {
            Matrix4 ret = Matrix4.Identity;
            Stack<GlbObject> walkStack = new Stack<GlbObject>();
            while (obj != null)
            {
                walkStack.Push(obj);
                obj = obj.Parent;
            }

            while (walkStack.Count > 0)
            {
                obj = walkStack.Pop();
                ret *= Matrix4.CreateScale(obj.Scale) * Matrix4.CreateFromQuaternion(obj.Rotation) * Matrix4.CreateTranslation(obj.Position);
            }

            return ret;
        }

        private void LoadMeshGl(GlbMesh mesh)
        {
            void LoadMesh() => mesh.CreateGl();

            if (this._createdOnGlThread)
            {
                LoadMesh();
            }
            else
            {
                ++this._glRequestsTodo;
                Client.Instance.Frontend.EnqueueOrExecuteTask(() =>
                {
                    mesh.CreateGl();
                    --this._glRequestsTodo;
                    if (this._checkGlRequests && this._glRequestsTodo <= 0)
                    {
                        this.glReady = true;
                    }
                });
            }
        }

        private VTT.GL.Texture LoadGLTexture<TPixel>(Image<TPixel> img, Sampler s) where TPixel : unmanaged, IPixel<TPixel>
        {
            void LoadTexture(VTT.GL.Texture glTex)
            {
                glTex.Bind();
                WrapParam ws = s.WrapS is Sampler.WrapSEnum.MIRRORED_REPEAT or Sampler.WrapSEnum.REPEAT ? WrapParam.Repeat : WrapParam.ClampToEdge;
                WrapParam wt = s.WrapT is Sampler.WrapTEnum.MIRRORED_REPEAT or Sampler.WrapTEnum.REPEAT ? WrapParam.Repeat : WrapParam.ClampToEdge;
                glTex.SetWrapParameters(ws, wt, wt);
                FilterParam fMin = s.MinFilter switch
                {
                    Sampler.MinFilterEnum.LINEAR => FilterParam.Linear,
                    Sampler.MinFilterEnum.LINEAR_MIPMAP_LINEAR => FilterParam.LinearMipmapLinear,
                    Sampler.MinFilterEnum.LINEAR_MIPMAP_NEAREST => FilterParam.LinearMipmapNearest,
                    _ => FilterParam.Nearest
                };

                FilterParam fMag = s.MagFilter == Sampler.MagFilterEnum.LINEAR ? FilterParam.Linear : FilterParam.Nearest;
                glTex.SetFilterParameters(fMin, fMag);
                glTex.SetImage(img, PixelInternalFormat.Rgba);
                if (fMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest)
                {
                    glTex.GenerateMipMaps();
                }

                img.Dispose();
            }

            if (this._createdOnGlThread)
            {
                VTT.GL.Texture glTex = new VTT.GL.Texture(TextureTarget.Texture2D);
                LoadTexture(glTex);
                return glTex;
            }
            else
            {
                VTT.GL.Texture glTex = new VTT.GL.Texture(TextureTarget.Texture2D, false);
                ++this._glRequestsTodo;
                Client.Instance.Frontend.EnqueueOrExecuteTask(() => 
                {
                    glTex.Allocate();
                    LoadTexture(glTex);
                    --this._glRequestsTodo;
                    if (this._checkGlRequests && this._glRequestsTodo <= 0)
                    {
                        this.glReady = true;
                    }
                });

                return glTex;
            }
        }
    }
}
