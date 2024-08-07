﻿namespace VTT.Asset
{
    using System;
    using System.IO;
    using System.Text;
    using VTT.Asset.Glb;
    using VTT.Asset.Shader.NodeGraph;
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

        public void Accept(byte[] binary)
        {
            this.Data = Encoding.UTF8.GetString(binary);
        }

        public void Dispose()
        {
            // NOOP
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
                this.CompressAlbedo = e.Get<bool>("CompressA");
                this.CompressAOMR = e.Get<bool>("CompressC");
                this.CompressNormal = e.Get<bool>("CompressN");
                this.CompressEmissive = e.Get<bool>("CompressE");
                this.FullRangeNormals = e.Get<bool>("FRN", false);
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.Set("CompressA", this.CompressAlbedo);
                ret.Set("CompressC", this.CompressAOMR);
                ret.Set("CompressN", this.CompressNormal);
                ret.Set("CompressE", this.CompressEmissive);
                ret.Set("FRN", this.FullRangeNormals);
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

        public void Dispose()
        {
            // NOOP
            // TODO figure out shader disposal structure
        }
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
                this.IsFullData = e.Get<bool>("FullData");
                this.TotalChunks = e.Get<int>("NumChunks");
                this.SampleRate = e.Get<int>("Frequency");
                this.NumChannels = e.Get<int>("Channels");
                this.TotalDuration = e.Get("Duration", double.NaN);
                this.CompressedChunkOffsets = e.GetArray("Offsets", (n, c) => c.Get<long>(n), Array.Empty<long>());
                this.SoundAssetName = e.Get("Name", " ");
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetEnum("Storage", this.SoundType);
                ret.Set("FullData", this.IsFullData);
                ret.Set("NumChunks", this.TotalChunks);
                ret.Set("Frequency", this.SampleRate);
                ret.Set("Channels", this.NumChannels);
                ret.Set("Duration", this.TotalDuration);
                ret.SetArray("Offsets", this.CompressedChunkOffsets, (n, c, v) => c.Set(n, v));
                ret.Set("Name", " ");
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
