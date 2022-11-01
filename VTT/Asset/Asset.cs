namespace VTT.Asset
{
    using System;
    using System.IO;
    using VTT.Asset.Glb;

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

        public void Dispose()
        {
            this.Texture?.Dispose();
            this.Shader?.Dispose();
            this.Model?.Dispose();
            this.Sound?.Dispose();
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
        ParticleSystem
    }

    public interface IAssetData
    {
        void Accept(byte[] binary);
        void Dispose();
    }

    public class ModelData : IAssetData
    {
        public GlbScene GLMdl { get; set; }

        public void Accept(byte[] binary)
        {
            using MemoryStream ms = new MemoryStream(binary);
            this.GLMdl = new GlbScene(ms);
        }

        public void Dispose() => this.GLMdl?.Dispose();
    }

    public class ShaderData : IAssetData
    {
        public void Accept(byte[] binary)
        {
        }

        public void Dispose()
        {
            // NOOP
            // TODO figure out shader disposal structure
        }
    }

    public class SoundData : IAssetData
    {
        public void Accept(byte[] binary)
        {
        }

        public void Dispose()
        {
            // NOOP, no audio cleanup?
        }
    }
}
