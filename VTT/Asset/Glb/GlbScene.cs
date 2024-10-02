namespace VTT.Asset.Glb
{
    using glTFLoader;
    using glTFLoader.Schema;
    using Newtonsoft.Json.Linq;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
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
        public List<GlbArmature> Armatures { get; } = new List<GlbArmature>();
        public List<GlbAnimation> Animations { get; } = new List<GlbAnimation>();

        public GlbObject SimplifiedRaycastMesh { get; }

        public AABox CombinedBounds { get; set; }
        public AABox RaycastBounds { get; set; }
        public bool HasTransparency { get; set; }
        public bool IsAnimated { get; set; }

        public volatile bool glReady;

        private readonly bool _createdOnGlThread;
        private volatile int _glRequestsTodo;
        private readonly bool _checkGlRequests;
        private readonly ModelData.Metadata _meta;
        private bool _matsReady;

        public bool GlReady => this.glReady && this.MaterialsGlReady;

        public bool MaterialsGlReady
        {
            get
            {
                if (this._matsReady)
                {
                    return true;
                }
                else
                {
                    foreach (GlbMaterial mat in this.Materials)
                    {
                        if (!mat.GetTexturesAsyncStatus())
                        {
                            return false;
                        }
                    }

                    this._matsReady = true;
                    return true;
                }
            }
        }

        public GlbScene()
        {
        }

        public unsafe GlbScene(ModelData.Metadata meta, Stream modelStream)
        {
            this._createdOnGlThread = Client.Instance.Frontend.CheckThread();
            this._meta = meta;

            // Have to do these stream gymnastics because khronos' Gltf implementation implicitly closes the stream it reads data from...
            this.LoadGLBFromStream(modelStream, out Gltf g, out byte[] bin);
            List<GlbObject> objs = new List<GlbObject>();
            this.PopulateGlbObjects(g, objs);
            this.LoadMaterials(g, bin);
            List<GlbLight> lights = new List<GlbLight>();
            this.LoadLights(g, lights);
            this.LoadBones(g, bin);
            this.LoadAnimations(g, bin);

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
                        // vec4 weights
                        // vec2 bones
                        List<Vector3> positions = new List<Vector3>();
                        List<Vector2> uvs = new List<Vector2>();
                        List<Vector3> normals = new List<Vector3>();
                        List<Vector4> tangents = new List<Vector4>();
                        List<Vector4> bitangents = new List<Vector4>();
                        List<Vector4> colors = new List<Vector4>();
                        List<Vector4> weightsList = new List<Vector4>();
                        List<Vector2> bones = new List<Vector2>();

                        int vertexSize = 3 + 2 + 3 + 3 + 3 + 4 + 4 + 2;

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
                                    positions.Add(new(
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

                            if (name.Equals("JOINTS_0")) // Bone indices
                            {
                                this.IsAnimated = glbm.IsAnimated = true;
                                Accessor.ComponentTypeEnum cType = accessor.ComponentType;
                                int elementSize = cType switch
                                {
                                    Accessor.ComponentTypeEnum.UNSIGNED_SHORT => 2,
                                    Accessor.ComponentTypeEnum.UNSIGNED_BYTE => 1,
                                    _ => throw new Exception("Illegal component type specified for joints, expected uint16 or uint8")
                                };

                                int stride = elementSize * 4; // Must be vec4 by spec
                                for (int index = 0; index < contextArray.Length; index += stride)
                                {
                                    ushort us1 = 0, us2 = 0, us3 = 0, us4 = 0;
                                    switch (cType)
                                    {
                                        case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                                        {
                                            us1 = contextArray[index + 0];
                                            us2 = contextArray[index + 1];
                                            us3 = contextArray[index + 2];
                                            us4 = contextArray[index + 3];
                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                                        {
                                            us1 = BitConverter.ToUInt16(contextArray, index + 0);
                                            us2 = BitConverter.ToUInt16(contextArray, index + 2);
                                            us3 = BitConverter.ToUInt16(contextArray, index + 4);
                                            us4 = BitConverter.ToUInt16(contextArray, index + 6);
                                            break;
                                        }
                                    }

                                    float f1 = VTTMath.UInt32BitsToSingle((((uint)us1) << 16) | (us2));
                                    float f2 = VTTMath.UInt32BitsToSingle((((uint)us3) << 16) | (us4));
                                    bones.Add(new Vector2(f1, f2));
                                }
                            }

                            if (name.Equals("WEIGHTS_0")) // Bone weights
                            {
                                this.IsAnimated = glbm.IsAnimated = true;
                                Accessor.ComponentTypeEnum cType = accessor.ComponentType;
                                int elementSize = cType switch
                                {
                                    Accessor.ComponentTypeEnum.FLOAT => 4,
                                    Accessor.ComponentTypeEnum.UNSIGNED_SHORT => 2,
                                    Accessor.ComponentTypeEnum.UNSIGNED_BYTE => 1,
                                    _ => throw new Exception("Illegal component type specified for weights, expected single, uint16 or uint8")
                                };

                                int stride = elementSize * 4; // Must be vec4 by spec
                                for (int index = 0; index < contextArray.Length; index += stride)
                                {
                                    float f1 = 0, f2 = 0, f3 = 0, f4 = 0;
                                    switch (cType)
                                    {
                                        case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                                        {
                                            byte b1 = contextArray[index + 0];
                                            byte b2 = contextArray[index + 1];
                                            byte b3 = contextArray[index + 2];
                                            byte b4 = contextArray[index + 3];
                                            f1 = b1 / 255.0f;
                                            f2 = b2 / 255.0f;
                                            f3 = b3 / 255.0f;
                                            f4 = b4 / 255.0f;
                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                                        {
                                            ushort us1 = BitConverter.ToUInt16(contextArray, index + 0);
                                            ushort us2 = BitConverter.ToUInt16(contextArray, index + 2);
                                            ushort us3 = BitConverter.ToUInt16(contextArray, index + 4);
                                            ushort us4 = BitConverter.ToUInt16(contextArray, index + 6);
                                            f1 = us1 / 65535f;
                                            f2 = us2 / 65535f;
                                            f3 = us3 / 65535f;
                                            f4 = us4 / 65535f;
                                            break;
                                        }

                                        case Accessor.ComponentTypeEnum.FLOAT:
                                        {
                                            f1 = BitConverter.ToSingle(contextArray, index + 0);
                                            f2 = BitConverter.ToSingle(contextArray, index + 4);
                                            f3 = BitConverter.ToSingle(contextArray, index + 8);
                                            f4 = BitConverter.ToSingle(contextArray, index + 12);
                                            break;
                                        }
                                    }

                                    weightsList.Add(new Vector4(f1, f2, f3, f4));
                                }
                            }

                            glbm.AmountToRender = accessor.Count;
                        }

                        if (glbm.IsAnimated)
                        {
                            if (o._node.Skin.HasValue)
                            {
                                glbm.AnimationArmature = this.Armatures[o._node.Skin.Value];
                            }
                            else
                            {
                                glbm.IsAnimated = false;
                            }
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
                                Vector4 bitangentVec = new Vector4(Vector3.Cross(normalVec, tangentVec.Xyz()) * tangentVec.W, 1.0f);
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
                        Matrix4x4 mat = this.LookupChildMatrix(o);
                        for (int j = 0; j < positions.Count; ++j)
                        {
                            Vector4 mpos = new Vector4(positions[j], 0.0f);
                            mpos = Vector4.Transform(mpos, mat);

                            posMin = Vector3.Min(posMin, mpos.Xyz());
                            posMax = Vector3.Max(posMax, mpos.Xyz());
                            // vec3 pos
                            // vec2 uv
                            // vec3 norm
                            // vec3 tangent
                            // vec3 bitangent
                            // vec4 color
                            Vector3 pos = positions[j];
                            Vector2 uv = uvs[j];
                            Vector3 norm = normals[j];
                            Vector3 tan = tangents[j].Xyz();
                            Vector3 bitan = bitangents[j].Xyz();
                            Vector4 color = colors[j];
                            Vector4 weights = weightsList.Count == 0 ? Vector4.Zero : weightsList[j];
                            Vector2 boneIndices = bones.Count == 0 ? Vector2.Zero : bones[j];
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
                            vBuffer[vBufIndex++] = weights.X;
                            vBuffer[vBufIndex++] = weights.Y;
                            vBuffer[vBufIndex++] = weights.Z;
                            vBuffer[vBufIndex++] = weights.W;
                            vBuffer[vBufIndex++] = boneIndices.X;
                            vBuffer[vBufIndex++] = boneIndices.Y;
                        }

                        List<Vector3> simplifiedTriangles = new List<Vector3>();
                        List<GlbMesh.BoneData> simplifiedBones = new List<GlbMesh.BoneData>();
                        List<float> areaSums = new List<float>();
                        float areaSum = 0f;
                        for (int j = 0; j < indices.Length; j += 3)
                        {
                            int index0 = (int)indices[j + 0];
                            int index1 = (int)indices[j + 1];
                            int index2 = (int)indices[j + 2];
                            Vector3 a = positions[index0];
                            Vector3 b = positions[index1];
                            Vector3 c = positions[index2];
                            simplifiedTriangles.Add(a);
                            simplifiedTriangles.Add(b);
                            simplifiedTriangles.Add(c);
                            Vector3 ab = b - a;
                            Vector3 ac = c - a;
                            float l = Vector3.Cross(ab, ac).Length() * 0.5f;
                            if (!float.IsNaN(l)) // Degenerate triangle
                            {
                                areaSum += l;
                            }

                            areaSums.Add(areaSum);
                            if (weightsList.Count > 0)
                            {
                                Vector4 ws0 = weightsList[index0];
                                Vector4 ws1 = weightsList[index1];
                                Vector4 ws2 = weightsList[index2];
                                Vector2 inds0 = bones[index0];
                                Vector2 inds1 = bones[index1];
                                Vector2 inds2 = bones[index2];
                                simplifiedBones.Add(new GlbMesh.BoneData(ws0, inds0));
                                simplifiedBones.Add(new GlbMesh.BoneData(ws1, inds1));
                                simplifiedBones.Add(new GlbMesh.BoneData(ws2, inds2));
                            }
                        }

                        glbm.simplifiedTriangles = new(simplifiedTriangles);
                        glbm.BoundingVolumeHierarchy = new BoundingVolumeHierarchy();
                        glbm.BoundingVolumeHierarchy.Build(glbm.simplifiedTriangles);
                        glbm.boneData = new(simplifiedBones);
                        glbm.areaSums = new(areaSums);
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

            this.RaycastBounds = this.SimplifiedRaycastMesh?.Bounds ?? this.CombinedBounds;

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

        private void LoadBones(Gltf g, byte[] bin)
        {
            if (g.Skins != null && g.Skins.Length > 0)
            {
                foreach (Skin arm in g.Skins)
                {
                    GlbArmature armature = new GlbArmature();

                    // Load all bones
                    for (int i = 0; i < arm.Joints.Length; i++)
                    {
                        int joint = arm.Joints[i];
                        GlbBone bone = new GlbBone();
                        bone.ModelIndex = joint;
                        armature.UnsortedBones.Add(bone);
                        armature.BonesByModelIndex[bone.ModelIndex] = bone;
                        // No need to pass node transform to the bone, as the node's transform is always identity
                    }

                    // Establish parent-child relationships
                    for (int i = 0; i < arm.Joints.Length; i++)
                    {
                        int joint = arm.Joints[i];
                        Node n = g.Nodes[joint];
                        GlbBone bone = armature.UnsortedBones[i];
                        Matrix4x4 world = Matrix4x4.CreateTranslation(n.Translation[0], n.Translation[1], n.Translation[2]) * Matrix4x4.CreateFromQuaternion(new Quaternion(n.Rotation[0], n.Rotation[1], n.Rotation[2], n.Rotation[3])) * Matrix4x4.CreateScale(n.Scale[0], n.Scale[1], n.Scale[2]);
                        Matrix4x4.Invert(world, out Matrix4x4 biwt);
                        bone.InverseWorldTransform = biwt;
                        List<GlbBone> cBones = new List<GlbBone>();
                        if (n.Children != null && n.Children.Length > 0)
                        {
                            for (int j = 0; j < n.Children.Length; ++j)
                            {
                                int cIndex = n.Children[j];
                                GlbBone childBone = armature.UnsortedBones.Find(x => x.ModelIndex == cIndex);
                                if (childBone != null)
                                {
                                    cBones.Add(childBone);
                                    childBone.Parent = bone;
                                }
                            }
                        }

                        bone.Children = cBones.ToArray();
                    }

                    void RecursivelyPopulateSortedParentList(IEnumerable<GlbBone> bones)
                    {
                        foreach (GlbBone b in bones)
                        {
                            armature.SortedBones.Add(b);
                        }

                        foreach (GlbBone b in bones)
                        {
                            RecursivelyPopulateSortedParentList(b.Children);
                        }
                    }

                    // Populate root nodes and create a sorted parent->children list
                    foreach (GlbBone bone in armature.UnsortedBones)
                    {
                        if (bone.Parent == null)
                        {
                            armature.Root.Add(bone);
                        }
                    }

                    RecursivelyPopulateSortedParentList(armature.Root);

                    if (arm.InverseBindMatrices.HasValue)
                    {
                        Accessor a = g.Accessors[arm.InverseBindMatrices.Value];
                        BufferView matrixView = g.BufferViews[a.BufferView.Value];
                        byte[] matrixByteBuffer = new byte[matrixView.ByteLength];
                        Array.Copy(bin, matrixView.ByteOffset, matrixByteBuffer, 0, matrixView.ByteLength);
                        int sz = sizeof(float) * 16; // 4x4 matrix = 16 floats
                        for (int i = 0; i < arm.Joints.Length; ++i)
                        {
                            int offset = i * sz;
                            float f1 = BitConverter.ToSingle(matrixByteBuffer, offset + 0);
                            float f2 = BitConverter.ToSingle(matrixByteBuffer, offset + 4);
                            float f3 = BitConverter.ToSingle(matrixByteBuffer, offset + 8);
                            float f4 = BitConverter.ToSingle(matrixByteBuffer, offset + 12);
                            float f5 = BitConverter.ToSingle(matrixByteBuffer, offset + 16);
                            float f6 = BitConverter.ToSingle(matrixByteBuffer, offset + 20);
                            float f7 = BitConverter.ToSingle(matrixByteBuffer, offset + 24);
                            float f8 = BitConverter.ToSingle(matrixByteBuffer, offset + 28);
                            float f9 = BitConverter.ToSingle(matrixByteBuffer, offset + 32);
                            float f10 = BitConverter.ToSingle(matrixByteBuffer, offset + 36);
                            float f11 = BitConverter.ToSingle(matrixByteBuffer, offset + 40);
                            float f12 = BitConverter.ToSingle(matrixByteBuffer, offset + 44);
                            float f13 = BitConverter.ToSingle(matrixByteBuffer, offset + 48);
                            float f14 = BitConverter.ToSingle(matrixByteBuffer, offset + 52);
                            float f15 = BitConverter.ToSingle(matrixByteBuffer, offset + 56);
                            float f16 = BitConverter.ToSingle(matrixByteBuffer, offset + 60);
                            Matrix4x4 inverseBind = new Matrix4x4(
                                f1, f2, f3, f4,
                                f5, f6, f7, f8,
                                f9, f10, f11, f12,
                                f13, f14, f15, f16
                            );

                            armature.UnsortedBones[i].InverseBindMatrix = inverseBind;
                        }
                    }
                    else
                    {
                        // Uh-oh
                        foreach (GlbBone bone in armature.UnsortedBones)
                        {
                            bone.InverseBindMatrix = Matrix4x4.Identity; // Invalid, but can't do much more, no inverse bind were provided anyway
                        }
                    }

                    this.Armatures.Add(armature);
                }
            }
        }

        private void LoadAnimations(Gltf g, byte[] bin)
        {
            if (g.Animations != null && g.Animations.Length > 0)
            {
                foreach (Animation a in g.Animations)
                {
                    GlbAnimation animation = new GlbAnimation();
                    animation.Name = a.Name;
                    float maxDuration = 0;
                    List<GlbAnimation.Sampler> samplers = new List<GlbAnimation.Sampler>();

                    // Load all samplers
                    foreach (AnimationSampler asa in a.Samplers)
                    {
                        GlbAnimation.Sampler sampler = new GlbAnimation.Sampler();
                        sampler.Interpolation = asa.Interpolation switch
                        {
                            AnimationSampler.InterpolationEnum.LINEAR => GlbAnimation.Interpolation.Linear,
                            AnimationSampler.InterpolationEnum.STEP => GlbAnimation.Interpolation.Step,
                            AnimationSampler.InterpolationEnum.CUBICSPLINE => GlbAnimation.Interpolation.CubicSpline,
                            _ => throw new Exception("Illegal interpolation method specified")
                        };

                        Accessor keyframeAccessor = g.Accessors[asa.Input];
                        BufferView kfbv = g.BufferViews[keyframeAccessor.BufferView.Value];
                        float[] keyframes = new float[keyframeAccessor.Count];
                        byte[] data = new byte[kfbv.ByteLength];
                        Array.Copy(bin, kfbv.ByteOffset, data, 0, kfbv.ByteLength);
                        float mVal = 0;
                        for (int i = 0; i < keyframes.Length; ++i)
                        {
                            keyframes[i] = BitConverter.ToSingle(data, i * sizeof(float));
                            mVal = MathF.Max(keyframes[i], mVal);
                        }

                        sampler.Timestamps = keyframes;
                        Accessor valueAccessor = g.Accessors[asa.Output];
                        BufferView vabv = g.BufferViews[valueAccessor.BufferView.Value];
                        data = new byte[vabv.ByteLength];
                        Array.Copy(bin, vabv.ByteOffset, data, 0, vabv.ByteLength);
                        Vector4[] values = new Vector4[keyframes.Length]; // Value count must match keyframe count
                        bool isCublicSpline = asa.Interpolation == AnimationSampler.InterpolationEnum.CUBICSPLINE;
                        // Interpolation types are always floats
                        for (int i = 0; i < values.Length; ++i)
                        {
                            int elementSize = valueAccessor.Type switch
                            {
                                Accessor.TypeEnum.VEC2 => 2,
                                Accessor.TypeEnum.VEC3 => 3,
                                Accessor.TypeEnum.VEC4 => 4,
                                _ => throw new Exception("Invalid accessor type specified - expected vec2/3/4, got " + valueAccessor.Type)
                            };

                            int cubicSpline3ComponentAccountance = isCublicSpline ? 3 : 1;
                            int bOffset = i * sizeof(float) * elementSize * cubicSpline3ComponentAccountance;
                            Vector4 result = default;
                            result.X = BitConverter.ToSingle(data, bOffset + 0);
                            result.Y = BitConverter.ToSingle(data, bOffset + sizeof(float));
                            if (elementSize > 2)
                            {
                                result.Z = BitConverter.ToSingle(data, bOffset + (sizeof(float) * 2));
                                if (elementSize == 4)
                                {
                                    result.W = BitConverter.ToSingle(data, bOffset + (sizeof(float) * 3));
                                }
                            }

                            values[i] = result;
                        }

                        sampler.Values = values;
                        animation.Samplers.Add(sampler);
                        maxDuration = MathF.Max(maxDuration, mVal);
                    }

                    // Load channels
                    foreach (AnimationChannel ac in a.Channels)
                    {
                        if (!ac.Target.Node.HasValue || ac.Target.Path == AnimationChannelTarget.PathEnum.weights)
                        {
                            continue; // Do not support extensions
                        }

                        GlbAnimation.Channel channel = new GlbAnimation.Channel();
                        channel.Sampler = animation.Samplers[ac.Sampler];
                        channel.BoneIndex = ac.Target.Node.Value;
                        channel.Path = ac.Target.Path switch
                        {
                            AnimationChannelTarget.PathEnum.rotation => GlbAnimation.Path.Rotation,
                            AnimationChannelTarget.PathEnum.scale => GlbAnimation.Path.Scale,
                            AnimationChannelTarget.PathEnum.translation => GlbAnimation.Path.Translation,
                            _ => throw new Exception("Path not supported!")
                        };

                        animation.Channels.Add(channel);
                    }

                    this.Animations.Add(animation);
                    animation.Container = this;
                    animation.Duration = maxDuration;
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
                glmat.BaseColorTexture = this.LoadIndependentTextureFromBinary(g, mat.PbrMetallicRoughness.BaseColorTexture?.Index, bin, new Rgba32(0, 0, 0, 1f), this._meta.CompressAlbedo ? SizedInternalFormat.CompressedSrgbAlphaBPTC : SizedInternalFormat.Srgb8Alpha8);
                glmat.BaseColorAnimation = new TextureAnimation(null);
                glmat.CullFace = !mat.DoubleSided;
                glmat.EmissionTexture = this.LoadIndependentTextureFromBinary(g, mat.EmissiveTexture?.Index, bin, new Rgba32(0, 0, 0, 1f), this._meta.CompressEmissive ? SizedInternalFormat.CompressedSrgbAlphaBPTC : SizedInternalFormat.Srgb8Alpha8);
                glmat.EmissionAnimation = new TextureAnimation(null);
                glmat.MetallicFactor = mat.PbrMetallicRoughness.MetallicFactor;
                glmat.OcclusionMetallicRoughnessTexture = this.LoadAOMRTextureFromBinary(g, mat.OcclusionTexture?.Index, mat.PbrMetallicRoughness.MetallicRoughnessTexture?.Index, mat.PbrMetallicRoughness.MetallicRoughnessTexture?.Index, bin);
                glmat.OcclusionMetallicRoughnessAnimation = new TextureAnimation(null);
                glmat.Name = mat.Name;
                glmat.Index = (uint)i;
                glmat.NormalTexture = this._meta.FullRangeNormals
                    ? this.LoadIndependentTextureFromBinary(g, mat.NormalTexture?.Index, bin, new RgbaVector(0.5f, 0.5f, 1.0f, 0.0f), this._meta.CompressNormal ? SizedInternalFormat.RgbHalf : SizedInternalFormat.RgbFloat)
                    : this.LoadIndependentTextureFromBinary(g, mat.NormalTexture?.Index, bin, new Rgba32(0.5f, 0.5f, 1.0f, 0.0f), this._meta.CompressNormal ? SizedInternalFormat.CompressedRgbaBPTC : SizedInternalFormat.Rgb8);

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
            this.DefaultMaterial.OcclusionMetallicRoughnessTexture = this.LoadBaseTexture(new(1, 1, 1, 1f));
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
                return this.LoadIndependentTextureFromBinary<Rgba32>(g, ao, bin, default, this._meta.CompressAOMR ? SizedInternalFormat.CompressedRgbaBPTC : SizedInternalFormat.Rgba8, i => i.ProcessPixelRows(a =>
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

            Image<Rgba32> imgAO = ao.HasValue ? this.LoadBinaryImage<Rgba32>(g, bin, ao.Value) : new Image<Rgba32>(1, 1) { [0, 0] = new Rgba32(1, 1, 1, 1f) };
            Image<Rgba32> imgM = m.HasValue ? this.LoadBinaryImage<Rgba32>(g, bin, m.Value) : new Image<Rgba32>(1, 1) { [0, 0] = new Rgba32(1, 1, 1, 1f) };
            Image<Rgba32> imgR = r.HasValue ? this.LoadBinaryImage<Rgba32>(g, bin, r.Value) : new Image<Rgba32>(1, 1) { [0, 0] = new Rgba32(1, 1, 1, 1f) };
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

            VTT.GL.Texture glTex = this.LoadGLTexture(imgb, new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT }, this._meta.CompressAOMR ? SizedInternalFormat.CompressedRgbaBPTC : SizedInternalFormat.Rgba8);
            imgAO.Dispose();
            imgM.Dispose();
            imgR.Dispose();

            return glTex;
        }

        private VTT.GL.Texture LoadIndependentTextureFromBinary<TPixel>(Gltf g, int? tIndex, byte[] bin, TPixel defaultValue, SizedInternalFormat pif = SizedInternalFormat.Rgba8, Action<Image<TPixel>> processor = null) where TPixel : unmanaged, IPixel<TPixel>
        {
            if (tIndex == null) // No independent texture present
            {
                Image<TPixel> imgb = new Image<TPixel>(1, 1);
                imgb[0, 0] = defaultValue;
                processor?.Invoke(imgb);
                VTT.GL.Texture texb = this.LoadGLTexture(imgb, new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT });
                return texb;
            }

            glTFLoader.Schema.Texture tex = g.Textures[tIndex.Value];
            Image<TPixel> img = this.LoadBinaryImage<TPixel>(g, bin, tIndex.Value);
            processor?.Invoke(img);
            Sampler s = tex.Sampler.HasValue
                ? g.Samplers[tex.Sampler.Value]
                : new Sampler() { MagFilter = Sampler.MagFilterEnum.NEAREST, MinFilter = Sampler.MinFilterEnum.NEAREST, WrapS = Sampler.WrapSEnum.REPEAT, WrapT = Sampler.WrapTEnum.REPEAT };
            VTT.GL.Texture glTex = this.LoadGLTexture(img, s, pif);
            return glTex;
        }

        private Image<TPixel> LoadBinaryImage<TPixel>(Gltf g, byte[] bin, int id) where TPixel : unmanaged, IPixel<TPixel>
        {
            glTFLoader.Schema.Image refImg = g.Images[g.Textures[id].Source.Value];
            if (refImg.BufferView == null)
            {
                throw new Exception("Image specified that is not embedded, that is not allowed!");
            }

            BufferView bv = g.BufferViews[refImg.BufferView.Value];
            byte[] imgData = new byte[bv.ByteLength];
            Array.Copy(bin, bv.ByteOffset, imgData, 0, bv.ByteLength);
            Image<TPixel> img = SixLabors.ImageSharp.Image.Load<TPixel>(imgData);
            return img;
        }

        private readonly MatrixStack _modelStack = new MatrixStack() { Reversed = true };
        public void Render(ShaderProgram shader, Matrix4x4 baseMatrix, Matrix4x4 projection, Matrix4x4 view, double textureAnimationIndex, GlbAnimation animation, float animationTime, IAnimationStorage animationStorage, Action<GlbMesh> renderer = null)
        {
            if (!this.GlReady)
            {
                return;
            }

            this._modelStack.Push(baseMatrix);
            foreach (GlbObject o in this.RootObjects)
            {
                o.Render(shader, this._modelStack, projection, view, textureAnimationIndex, animation, animationTime, animationStorage, renderer);
            }

            this._modelStack.Pop();
        }

        public Image<Rgba32> CreatePreview(int width, int height, Vector4 clearColor, bool portrait = false)
        {
            // TODO update preview creation code to account for async texture loading

            // Create camera
            glTFLoader.Schema.Camera sceneCamera = portrait ? (this.PortraitCamera?.Camera ?? this.Camera.Camera) : this.Camera.Camera;
            GlbObject cameraObject = portrait ? (this.PortraitCamera ?? this.Camera) : this.Camera;
            Matrix4x4 mat = this.LookupChildMatrix(cameraObject);
            Matrix4x4.Decompose(mat, out Vector3 scale, out Quaternion q, out Vector3 camPos);
            Vector3 camLook = (Vector4.Transform(new Vector4(0, 0, -1, 1), q)).Xyz().Normalized();
            Util.Camera camera = new VectorCamera(camPos, camLook);
            camera.Projection = Matrix4x4.CreatePerspectiveFieldOfView(sceneCamera.Perspective.Yfov, (float)width / height, sceneCamera.Perspective.Znear, 100f);
            camera.RecalculateData(assumedUpAxis: Vector3.UnitZ);

            // Create sun
            DirectionalLight sun;
            GlbLight? sceneSun = this.DirectionalLight?.Light;
            if (sceneSun != null)
            {
                mat = this.LookupChildMatrix(this.DirectionalLight);
                Matrix4x4.Decompose(mat, out _, out q, out _);
                Vector3 lightDir = new Vector3(0, 0, -1);
                lightDir = (Vector4.Transform(new Vector4(lightDir, 1.0f), q)).Xyz();
                sun = new DirectionalLight(lightDir.Normalized(), sceneSun.Value.Color.Xyz());
            }
            else
            {
                sun = new DirectionalLight(-Vector3.UnitZ, Vector3.Zero);
            }

            // Create framebuffer
            uint fbo = Client.Instance.Frontend.Renderer.Pipeline.CreateDummyForwardFBO(new Size(width, height), out VTT.GL.Texture d0, out VTT.GL.Texture d1, out VTT.GL.Texture d2, out VTT.GL.Texture d3, out VTT.GL.Texture d4, out VTT.GL.Texture d5, out VTT.GL.Texture tex);
            GL.BindFramebuffer(FramebufferTarget.All, fbo);
            GL.ActiveTexture(0);

            ReadOnlySpan<int> data = GL.GetInteger(GLPropertyName.Viewport);
            ShaderProgram shader = Client.Instance.Frontend.Renderer.Pipeline.Forward;
            shader.Bind();
            Client.Instance.Frontend.Renderer.ObjectRenderer.SetDummyUBO(camera, sun, clearColor, Client.Instance.Settings.UseUBO ? null : shader);
            shader["ambient_intensity"].Set(0.03f);
            shader["gamma_correct"].Set(true);

            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            plr.Clear();
            plr.ProcessScene(Matrix4x4.Identity, this);
            plr.DrawLights(null, false, 0, null);
            plr.UniformLights(shader);

            GL.ActiveTexture(14);
            shader["dl_shadow_map"].Set(14);
            Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            GL.ActiveTexture(13);
            shader["pl_shadow_maps"].Set(13);
            plr.DepthMap.Bind();
            GL.ActiveTexture(0);

            shader["alpha"].Set(1.0f);
            shader["tint_color"].Set(Vector4.One);

            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(shader);

            GL.BindFramebuffer(FramebufferTarget.All, fbo);
            GL.Viewport(0, 0, width, height);
            GL.ActiveTexture(0);
            GL.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            GL.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);

            this.Render(shader, Matrix4x4.Identity, camera.Projection, camera.View, 0, null, 0, null);

            GL.ActiveTexture(0);
            tex.Bind();
            Image<Rgba32> retImg = tex.GetImage<Rgba32>();

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
            GL.DeleteFramebuffer(fbo);
            d0.Dispose();
            d1.Dispose();
            d2.Dispose();
            d3.Dispose();
            d4.Dispose();
            d5.Dispose();
            tex.Dispose();
            GL.Viewport(data[0], data[1], data[2], data[3]);

            retImg.Mutate(x => x.Flip(FlipMode.Vertical));
            return retImg;
        }

        private Matrix4x4 LookupChildMatrix(GlbObject obj)
        {
            Matrix4x4 ret = Matrix4x4.Identity;
            Stack<GlbObject> walkStack = new Stack<GlbObject>();
            while (obj != null)
            {
                walkStack.Push(obj);
                obj = obj.Parent;
            }

            while (walkStack.Count > 0)
            {
                obj = walkStack.Pop();
                ret *= Matrix4x4.CreateScale(obj.Scale) * Matrix4x4.CreateFromQuaternion(obj.Rotation) * Matrix4x4.CreateTranslation(obj.Position);
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

        private VTT.GL.Texture LoadGLTexture<TPixel>(Image<TPixel> img, Sampler s, SizedInternalFormat pif = SizedInternalFormat.Rgba8) where TPixel : unmanaged, IPixel<TPixel>
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
                glTex.SetImage(img, OpenGLUtil.MapCompressedFormat(pif), type: pif is SizedInternalFormat.RgbHalf or SizedInternalFormat.RgbaFloat ? PixelDataType.Float : PixelDataType.Byte);
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
