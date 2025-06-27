namespace VTT.Asset
{
    using System;
    using System.IO;
    using System.Text;
    using VTT.Asset.Glb;
    using VTT.Asset.Shader.NodeGraph;
    using VTT.Network;
    using VTT.Render.Shaders;
    using VTT.Sound;
    using VTT.Util;

    /*
     * Asset primer:
     *  Client hosts a library of assets, as client only has 1 map at a time all assets can be discharged once the map is unloaded.
     *  As such the client is a simple Dictionary<Guid, Asset> map, with the map cleared as the client changes maps.
     *  
     *  Server is more complicated, as all server assets are simply AssetPointer holders, enough to give the server info on asset binary
     *  However the server keeps the asset manager around, with asset previews for the clients.
     *  All server asset manipulations come from the asset manager
     */

    public class Asset
    {
        public Guid ID { get; set; }
        public AssetType Type { get; set; }

        public TextureData Texture { get; set; }
        public ShaderData Shader { get; set; }
        public ModelData Model { get; set; }
        public SoundData Sound { get; set; }
        public ParticleSystem ParticleSystem { get; set; }
        public GlslFragmentData GlslFragment { get; set; }

        private bool _cachedModelGlReadiness;
        public bool ModelGlReady => this._cachedModelGlReadiness || (this._cachedModelGlReadiness = (this.Model?.GLMdl?.GlReady ?? false));

        public void Dispose()
        {
            this.Texture?.Dispose();
            this.Shader?.Dispose();
            this.Model?.Dispose();
            this.Sound?.Dispose();
            this.GlslFragment?.Dispose();
            this.ParticleSystem = null;
        }

        public byte[] ToBinary(byte[] objectBinary) // Strangely enough client-only
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(new char[] { 'V', 'T', 'A', 'B' });
            bw.Write((byte)1);
            bw.Write(objectBinary);
            return ms.ToArray();
        }
    }

    public enum AssetType
    {
        Texture,
        Model,
        Shader,
        Sound,
        ParticleSystem,
        GlslFragmentShader
    }

    public interface IAssetData
    {
        void Accept(byte[] binary);
        void Dispose();
    }

    public class GlslFragmentData : IAssetData
    {
        public string Data { get; set; }
        private FastAccessShader<ForwardUniforms> _cachedShaderFwd;
        private FastAccessShader<ParticleUniforms> _cachedShaderPtc;
        private bool _glValid;
        private bool _glGen;

        public void Accept(byte[] binary) => this.Data = Encoding.UTF8.GetString(binary);

        public FastAccessShader<T> GetGLShader<T>(bool isParticleShader) where T : new()
        {
            if (string.IsNullOrEmpty(this.Data))
            {
                return null;
            }

            if (!this._glValid && this._glGen)
            {
                return null;
            }

            if (!this._glGen)
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Info, "Compiling custom glsl shader");
                if (ShaderGraph.TryInjectCustomShaderCode(isParticleShader, true, this.Data, out string fullVertCode, out string fullFragCode))
                {
                    string err = string.Empty;
                    bool result = isParticleShader
                        ? ShaderGraph.TryCompileCustomShader(true, fullVertCode, fullFragCode, out err, out this._cachedShaderPtc)
                        : ShaderGraph.TryCompileCustomShader(false, fullVertCode, fullFragCode, out err, out this._cachedShaderFwd);
                    if (!result)
                    {
                        l.Log(LogLevel.Error, "Could not compile custom glsl shader!");
                        l.Log(LogLevel.Error, err);
                        this._cachedShaderPtc = null;
                        this._cachedShaderFwd = null;
                        this._glValid = false;
                    }
                    else
                    {
                        this._glValid = true;
                    }
                }
                else
                {
                    this._cachedShaderFwd = null;
                    this._cachedShaderPtc = null;
                    this._glValid = false;
                }

                this._glGen = true;
            }

            return isParticleShader ? this._cachedShaderPtc as FastAccessShader<T> : this._cachedShaderFwd as FastAccessShader<T>;
        }

        public void Dispose()
        {
            this._cachedShaderFwd?.Program?.Dispose();
            this._cachedShaderPtc?.Program?.Dispose();
            this._cachedShaderFwd = null;
            this._cachedShaderPtc = null;
            this._glGen = false;
        }
    }

    public class ModelData : IAssetData
    {
        public GlbScene GLMdl { get; set; }
        public Metadata Meta { get; set; }

        public void Accept(byte[] binary)
        {
            using MemoryStream ms = new MemoryStream(binary);
            this.GLMdl = new GlbScene(this.Meta, ms);
        }

        public void Dispose() => this.GLMdl?.Dispose();

        public class Metadata : ISerializable
        {
            public bool CompressAlbedo { get; set; }
            public bool CompressAOMR { get; set; }
            public bool CompressNormal { get; set; }
            public bool CompressEmissive { get; set; }
            public bool FullRangeNormals { get; set; }

            public Metadata Copy() =>
                new Metadata()
                {
                    CompressAlbedo = CompressAlbedo,
                    CompressAOMR = CompressAOMR,
                    CompressNormal = CompressNormal,
                    CompressEmissive = CompressEmissive,
                    FullRangeNormals = FullRangeNormals
                };

            public void Deserialize(DataElement e)
            {
                this.CompressAlbedo = e.GetBool("CompressA");
                this.CompressAOMR = e.GetBool("CompressC");
                this.CompressNormal = e.GetBool("CompressN");
                this.CompressEmissive = e.GetBool("CompressE");
                this.FullRangeNormals = e.GetBool("FRN", false);
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetBool("CompressA", this.CompressAlbedo);
                ret.SetBool("CompressC", this.CompressAOMR);
                ret.SetBool("CompressN", this.CompressNormal);
                ret.SetBool("CompressE", this.CompressEmissive);
                ret.SetBool("FRN", this.FullRangeNormals);
                return ret;
            }
        }
    }

    public class ShaderData : IAssetData
    {
        public ShaderGraph NodeGraph { get; set; }

        public void Accept(byte[] binary)
        {
            using MemoryStream ms = new MemoryStream(binary);
            using BinaryReader br = new BinaryReader(ms);
            DataElement de = new DataElement(br);
            this.NodeGraph = new ShaderGraph();
            this.NodeGraph.Deserialize(de);
        }

        public void Dispose() => this.NodeGraph?.Free();
    }

    public class SoundData : IAssetData
    {
        public Metadata Meta { get; set; }
        public WaveAudio RawAudio { get; set; }

        public void Accept(byte[] binary)
        {
            if (this.Meta.IsFullData)
            {
                this.RawAudio = new WaveAudio(binary, this.Meta.SampleRate, this.Meta.NumChannels);
            }
        }

        public void Dispose() => this.RawAudio?.Free();

        public class Metadata : ISerializable
        {
            public StorageType SoundType { get; set; }
            public bool IsFullData { get; set; }
            public int SampleRate { get; set; }
            public int NumChannels { get; set; }
            public int TotalChunks { get; set; }
            public double TotalDuration { get; set; }
            public long[] CompressedChunkOffsets { get; set; } = Array.Empty<long>();
            public string SoundAssetName { get; set; }

            public void Deserialize(DataElement e)
            {
                this.SoundType = e.GetEnum<StorageType>("Storage");
                this.IsFullData = e.GetBool("FullData");
                this.TotalChunks = e.GetInt("NumChunks");
                this.SampleRate = e.GetInt("Frequency");
                this.NumChannels = e.GetInt("Channels");
                this.TotalDuration = e.GetDouble("Duration", double.NaN);
                this.CompressedChunkOffsets = e.GetPrimitiveArrayWithLegacySupport("Offsets", (n, c) => c.GetLong(n), Array.Empty<long>());
                this.SoundAssetName = e.GetString("Name", " ");
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetEnum("Storage", this.SoundType);
                ret.SetBool("FullData", this.IsFullData);
                ret.SetInt("NumChunks", this.TotalChunks);
                ret.SetInt("Frequency", this.SampleRate);
                ret.SetInt("Channels", this.NumChannels);
                ret.SetDouble("Duration", this.TotalDuration);
                ret.SetPrimitiveArray("Offsets", this.CompressedChunkOffsets);
                ret.SetString("Name", this.SoundAssetName);
                return ret;
            }

            public enum StorageType
            {
                Raw,
                Mpeg
            }
        }
    }
}
