namespace VTT.Asset
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Asset.Glb;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class TextureData : IAssetData
    {
        public Frame[] Frames { get; set; }
        public Metadata Meta { get; set; }

        private Texture _glTex;
        private TextureAnimation _cachedAnim;

        public volatile bool glReady;

        public GlbScene ToGlbModel()
        {
            GlbScene ret = new GlbScene();

            GlbObject camera = new GlbObject(null);
            GlbObject sun = new GlbObject(null);
            GlbObject mesh = new GlbObject(null);

            GlbMaterial mat = new GlbMaterial();
            mat.AlphaMode = this.Meta.EnableBlending ? glTFLoader.Schema.Material.AlphaModeEnum.BLEND : glTFLoader.Schema.Material.AlphaModeEnum.OPAQUE;
            mat.NormalTexture = OpenGLUtil.LoadFromOnePixel(new Rgba32(0.5f, 0.5f, 1f, 1f));
            mat.NormalAnimation = new TextureAnimation(null);
            mat.EmissionTexture = OpenGLUtil.LoadFromOnePixel(new Rgba32(0, 0, 0, 0));
            mat.EmissionAnimation = new TextureAnimation(null);
            mat.OcclusionMetallicRoughnessTexture = OpenGLUtil.LoadFromOnePixel(new Rgba32(1f, 0, 1f, 1f));
            mat.OcclusionMetallicRoughnessAnimation = new TextureAnimation(null);
            mat.RoughnessFactor = 1f;
            mat.AlphaCutoff = 0f;
            mat.BaseColorTexture = this.GetOrCreateGLTexture(out TextureAnimation dta);
            mat.BaseColorAnimation = dta;
            mat.BaseColorFactor = Vector4.One;
            mat.CullFace = true;
            mat.MetallicFactor = 0f;
            mat.Name = "converted_texture_material";

            ret.Materials.Add(mat);
            ret.DefaultMaterial = mat;

            glTFLoader.Schema.Camera glbCam = new glTFLoader.Schema.Camera() { Name = "camera", Type = glTFLoader.Schema.Camera.TypeEnum.perspective, Perspective = new glTFLoader.Schema.CameraPerspective() { AspectRatio = 1, Yfov = MathHelper.DegreesToRadians(60), Znear = 0.0001f, Zfar = 10f } };
            camera.Camera = glbCam;
            camera.Name = "camera";
            camera.Type = GlbObjectType.Camera;
            camera.Position = Vector3.UnitZ;
            camera.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(179));
            camera.Scale = Vector3.One;
            ret.Camera = camera;
            ret.PortraitCamera = camera;
            ret.RootObjects.Add(camera);

            GlbMesh glbm = new GlbMesh();
            List<Vector3> positions = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            List<Vector4> bitangents = new List<Vector4>();
            List<Vector4> colors = new List<Vector4>();

            int vertexSize = 3 + 2 + 3 + 3 + 3 + 4;
            positions.Add(new Vector3(-0.5f, -0.5f, 0f));
            positions.Add(new Vector3(0.5f, -0.5f, 0f));
            positions.Add(new Vector3(0.5f, 0.5f, 0f));
            positions.Add(new Vector3(-0.5f, 0.5f, 0f));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 0));
            normals.Add(Vector3.UnitZ);
            normals.Add(Vector3.UnitZ);
            normals.Add(Vector3.UnitZ);
            normals.Add(Vector3.UnitZ);
            tangents.Add(Vector4.UnitX);
            tangents.Add(Vector4.UnitX);
            tangents.Add(Vector4.UnitX);
            tangents.Add(Vector4.UnitX);
            bitangents.Add(Vector4.UnitY);
            bitangents.Add(Vector4.UnitY);
            bitangents.Add(Vector4.UnitY);
            bitangents.Add(Vector4.UnitY);
            colors.Add(Vector4.One);
            colors.Add(Vector4.One);
            colors.Add(Vector4.One);
            colors.Add(Vector4.One);
            uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };
            float[] vBuffer = new float[positions.Count * vertexSize];
            int vBufIndex = 0;
            for (int j = 0; j < positions.Count; ++j)
            {
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
            float areaSum = 0;
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
            glbm.Bounds = new AABox(new Vector3(-0.5f, -0.5f, -0.01f), new Vector3(0.5f, 0.5f, 0.01f));
            glbm.VertexBuffer = vBuffer;
            glbm.IndexBuffer = indices;
            glbm.AmountToRender = 6;
            glbm.Material = mat;
            glbm.CreateGl();

            mesh.Position = Vector3.Zero;
            mesh.Rotation = Quaternion.Identity;
            mesh.Scale = Vector3.One;
            mesh.Bounds = glbm.Bounds;
            mesh.Meshes.Add(glbm);
            mesh.Name = "generated_mesh";
            mesh.Type = GlbObjectType.Mesh;
            ret.Meshes.Add(mesh);
            ret.RootObjects.Add(mesh);

            GlbLight sunlight = new GlbLight(Vector4.One, 10, KhrLight.LightTypeEnum.Directional);
            sun.Scale = Vector3.One;
            sun.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(179));
            sun.Position = Vector3.Zero;
            sun.Light = sunlight;
            sun.Name = "generated_sun";
            sun.Type = GlbObjectType.Light;
            ret.DirectionalLight = sun;
            ret.Lights.Add(sun);
            ret.RootObjects.Add(sun);

            ret.CombinedBounds = glbm.Bounds;
            ret.glReady = true;
            ret.HasTransparency = this.Meta.EnableBlending;
            return ret;
        }

        public static TextureData CreateDefaultFromImage(Image<Rgba32> clientImage, out byte[] selfBinary, out Metadata meta)
        {
            MemoryStream ms = new MemoryStream();
            clientImage.SaveAsPng(ms);
            byte[] imgBin = ms.ToArray();
            ms.Dispose();
            TextureData ret = new TextureData()
            {
                Meta = new Metadata()
                {
                    WrapS = WrapParam.Repeat,
                    WrapT = WrapParam.Repeat,
                    FilterMag = FilterParam.Linear,
                    FilterMin = FilterParam.LinearMipmapLinear,
                    EnableBlending = true,
                    Compress = true,
                    GammaCorrect = true,
                },

                Frames = new Frame[1] { new Frame(0, 1, false, imgBin) }
            };

            meta = ret.Meta;
            selfBinary = ret.Write();
            return ret;
        }

        public static TextureData CreateFromImageArray(IEnumerable<Image<Rgba32>> images, out byte[] selfBinary, out Metadata meta)
        {
            List<Frame> frames = new List<Frame>();
            int j = 0;
            foreach (Image<Rgba32> i in images)
            {
                MemoryStream ms = new MemoryStream();
                i.SaveAsPng(ms);
                byte[] imgBin = ms.ToArray();
                ms.Dispose();
                Frame f = new Frame(j++, 1, false, imgBin);
                frames.Add(f);
            }

            TextureData ret = new TextureData()
            {
                Meta = new Metadata()
                {
                    WrapS = WrapParam.Repeat,
                    WrapT = WrapParam.Repeat,
                    FilterMag = FilterParam.Linear,
                    FilterMin = FilterParam.LinearMipmapLinear,
                    EnableBlending = true,
                    Compress = true,
                    GammaCorrect = true,
                },

                Frames = frames.ToArray()
            };

            meta = ret.Meta;
            selfBinary = ret.Write();
            return ret;
        }

        public void Accept(byte[] binary)
        {
            using MemoryStream ms = new MemoryStream(binary);
            using BinaryReader br = new BinaryReader(ms);
            int version = br.ReadByte();
            switch (version)
            {
                case 0:
                {
                    this.Meta = new Metadata();
                    this.Meta.WrapS = (WrapParam)br.ReadInt32();
                    this.Meta.WrapT = (WrapParam)br.ReadInt32();
                    this.Meta.FilterMin = (FilterParam)br.ReadInt32();
                    this.Meta.FilterMag = (FilterParam)br.ReadInt32();
                    this.Meta.EnableBlending = br.ReadBoolean();
                    this.Meta.Compress = br.ReadBoolean();
                    this.Meta.GammaCorrect = br.ReadBoolean();
                    int nFrames = br.ReadInt32();
                    this.Frames = new Frame[nFrames];
                    for (int i = 0; i < nFrames; ++i)
                    {
                        int d = br.ReadInt32();
                        bool b = br.ReadBoolean();
                        int bLen = br.ReadInt32();
                        byte[] bin = br.ReadBytes(bLen);
                        this.Frames[i] = new Frame(i, d, b, bin);
                    }

                    break;
                }

                case 1:
                {
                    // On version 1 metadata migrated to AssetRef, no need to read it.
                    int nFrames = br.ReadInt32();
                    this.Frames = new Frame[nFrames];
                    for (int i = 0; i < nFrames; ++i)
                    {
                        int d = br.ReadInt32();
                        bool b = br.ReadBoolean();
                        int bLen = br.ReadInt32();
                        byte[] bin = br.ReadBytes(bLen);
                        this.Frames[i] = new Frame(i, d, b, bin);
                    }

                    break;
                }

                default:
                    break;
            }

            this.glReady = true;
        }

        public byte[] Write()
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)1);
            bw.Write(this.Frames.Length);
            foreach (Frame f in this.Frames)
            {
                bw.Write(f.Duration);
                bw.Write(f.Blend);
                bw.Write(f.ImageBinary.Length);
                bw.Write(f.ImageBinary);
            }

            return ms.ToArray();
        }

        public Texture CopyGlTexture(PixelInternalFormat internalFormat = PixelInternalFormat.One)
        {
            Texture tex = this.GetOrCreateGLTexture(out _);
            Texture ret = new Texture(TextureTarget.Texture2D);
            ret.Bind();
            if (internalFormat == PixelInternalFormat.One)
            {
                internalFormat = this.Meta.Compress ? this.Meta.GammaCorrect ? PixelInternalFormat.CompressedSrgbAlpha : PixelInternalFormat.CompressedRgba :
                    this.Meta.GammaCorrect ? PixelInternalFormat.SrgbAlpha : PixelInternalFormat.Rgba;
            }

            ret.SetFilterParameters(this.Meta.FilterMin, this.Meta.FilterMag);
            ret.SetWrapParameters(this.Meta.WrapS, this.Meta.WrapT, WrapParam.Repeat);
            tex.Bind();
            using Image<Rgba32> img = tex.GetImage<Rgba32>();
            ret.Bind();
            ret.SetImage(img, internalFormat);
            if (this.Meta.FilterMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest)
            {
                ret.GenerateMipMaps();
            }

            return ret;
        }

        public Texture GetOrCreateGLTexture(out TextureAnimation animationData)
        {
            if (this._glTex == null)
            {
                using Image<Rgba32> img = this.CompoundImage();

                this._glTex = new Texture(TextureTarget.Texture2D);
                this._glTex.Bind();
                PixelInternalFormat pif =
                    this.Meta.Compress ?
                        this.Meta.GammaCorrect ? PixelInternalFormat.CompressedSrgbAlpha :
                    PixelInternalFormat.CompressedRgba :
                this.Meta.GammaCorrect ? PixelInternalFormat.SrgbAlpha :
                    PixelInternalFormat.Rgba;

                pif = OpenGLUtil.MapCompressedFormat(pif);

                this._glTex.SetFilterParameters(this.Meta.FilterMin, this.Meta.FilterMag);
                this._glTex.SetWrapParameters(this.Meta.WrapS, this.Meta.WrapT, WrapParam.Repeat);
                this._glTex.SetImage(img, pif);
                if ((this.Meta.FilterMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest))
                {
                    this._glTex.GenerateMipMaps();
                }
            }

            animationData = this._cachedAnim;
            return this._glTex;
        }

        public TextureAnimation CachedAnimation => this._cachedAnim;

        public Image<Rgba32> CompoundImage()
        {
            TextureAnimation.Frame[] allFrames = new TextureAnimation.Frame[this.Frames.Length];
            RectangleF[] positions = new RectangleF[this.Frames.Length];
            int imgW = 0, imgH = 0;
            int cX = 0, cY = 0;
            int lH = 0;
            int maxS = Client.Instance.AssetManager.ClientAssetLibrary.GlMaxTextureSize;
            for (int i = 0; i < this.Frames.Length; ++i)
            {
                Frame f = this.Frames[i];
                IImageInfo ii = Image.Identify(f.ImageBinary);
                lH = Math.Max(ii.Height, lH);
                if (cX + ii.Width > maxS)
                {
                    imgW = Math.Max(cX, imgW);
                    imgH += lH;
                    cX = 0;
                    cY += lH;
                    lH = ii.Height;
                }

                RectangleF p = new RectangleF(cX, cY, ii.Width, ii.Height);
                positions[i] = p;
                cX += ii.Width;
            }

            imgW = Math.Max(cX, imgW);
            imgH += lH;
            bool mayBeContinuous = ((long)imgW * (long)imgH * 4L) < int.MaxValue; // 32bpp
            Configuration cfg = Configuration.Default.Clone();
            cfg.PreferContiguousImageBuffers = mayBeContinuous;
            Image<Rgba32> img = new Image<Rgba32>(cfg, imgW, imgH);
            GraphicsOptions go = new GraphicsOptions() { Antialias = false, BlendPercentage = 1, ColorBlendingMode = PixelColorBlendingMode.Normal };
            for (int i = 0; i < this.Frames.Length; ++i)
            {
                Image<Rgba32> f = Image.Load<Rgba32>(this.Frames[i].ImageBinary);
                RectangleF r = positions[i];
                allFrames[i] = new TextureAnimation.Frame() { Duration = (uint)this.Frames[i].Duration, Location = new RectangleF(r.X / img.Width, r.Y / img.Height, r.Width / img.Width, r.Height / img.Height) };
                img.Mutate(x => x.DrawImage(f, new Point((int)r.X, (int)r.Y), go));
                f.Dispose();
                //this.Frames[i] = this.Frames[i].ClearBinary();
            }

            this._cachedAnim = new TextureAnimation(allFrames);
            return img;
        }

        public void Dispose()
        {
            this._glTex?.Dispose();
            this._cachedAnim = null;
        }

        public struct Frame
        {
            public int Idx { get; set; }
            public int Duration { get; set; }
            public bool Blend { get; set; }
            public byte[] ImageBinary { get; set; }

            public Frame(int idx, int duration, bool blend, byte[] imageBinary)
            {
                this.Idx = idx;
                this.Duration = duration;
                this.Blend = blend;
                this.ImageBinary = imageBinary;
            }

            public readonly Frame ClearBinary() => new Frame(this.Idx, this.Duration, this.Blend, null);
        }

        public class Metadata : ISerializable
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public WrapParam WrapS { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public WrapParam WrapT { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public FilterParam FilterMin { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public FilterParam FilterMag { get; set; }

            public bool EnableBlending { get; set; }
            public bool Compress { get; set; }
            public bool GammaCorrect { get; set; }

            public Metadata Copy() =>
                new Metadata()
                {
                    WrapS = this.WrapS,
                    WrapT = this.WrapT,
                    FilterMin = this.FilterMin,
                    FilterMag = this.FilterMag,
                    EnableBlending = this.EnableBlending,
                    Compress = this.Compress,
                    GammaCorrect = this.GammaCorrect
                };

            public void Deserialize(DataElement e)
            {
                this.WrapS = e.GetEnum<WrapParam>("WrapS");
                this.WrapT = e.GetEnum<WrapParam>("WrapT");
                this.FilterMin = e.GetEnum<FilterParam>("FilterMin");
                this.FilterMag = e.GetEnum<FilterParam>("FilterMag");
                this.EnableBlending = e.Get<bool>("Blend");
                this.Compress = e.Get<bool>("Compress");
                this.GammaCorrect = e.Get<bool>("Gamma");
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetEnum("WrapS", this.WrapS);
                ret.SetEnum("WrapT", this.WrapT);
                ret.SetEnum("FilterMin", this.FilterMin);
                ret.SetEnum("FilterMag", this.FilterMag);
                ret.Set("Blend", this.EnableBlending);
                ret.Set("Compress", this.Compress);
                ret.Set("Gamma", this.GammaCorrect);
                return ret;
            }
        }
    }
}
