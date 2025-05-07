namespace VTT.Asset.Shader
{
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Asset.Glb;
    using VTT.GL.Bindings;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class LinkedTextureContainer : ISerializable
    {
        public List<Guid> ExtraTexturesAttachments { get; } = new List<Guid>();
        public Texture CombinedExtraTextures { get; set; }
        public Vector2[] CombinedExtraTexturesData { get; set; }
        public TextureAnimation[] CombinedExtraTexturesAnimations { get; set; }
        public bool HasExtraTexture { get; set; }

        public void AddAttachment(Guid attach) => this.ExtraTexturesAttachments.Add(attach);

        public AssetStatus GetExtraTexture(out Texture t, out Vector2[] sizes, out TextureAnimation[] cachedAnimData)
        {
            if (this.HasExtraTexture)
            {
                t = this.CombinedExtraTextures;
                sizes = this.CombinedExtraTexturesData;
                cachedAnimData = this.CombinedExtraTexturesAnimations;
                return AssetStatus.Return;
            }

            foreach (Guid id in this.ExtraTexturesAttachments)
            {
                AssetStatus ast;
                if ((ast = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(id, AssetType.Texture, out Asset tAss)) != AssetStatus.Return || tAss == null || tAss.Texture == null || !tAss.Texture.glReady)
                {
                    t = null;
                    sizes = Array.Empty<Vector2>();
                    cachedAnimData = Array.Empty<TextureAnimation>();
                    return ast == AssetStatus.Return ? AssetStatus.Await : ast; // May return that asset is present but async data is not present yet
                }
            }

            // If we are here then all textures are ready
            this.GenerateUnifiedExtraTexture();
            t = this.CombinedExtraTextures;
            sizes = this.CombinedExtraTexturesData;
            cachedAnimData = this.CombinedExtraTexturesAnimations;
            return AssetStatus.Return;
        }

        public unsafe void GenerateUnifiedExtraTexture() // Ensure that all asset data was transmitted
        {
            if (this.ExtraTexturesAttachments.Count == 0)
            {
                this.HasExtraTexture = true;
                return;
            }

            this.HasExtraTexture = false;
            int maxW = 0;
            int maxH = 0;
            bool s = true;
            if (this.CombinedExtraTexturesData == null || this.CombinedExtraTexturesData.Length != this.ExtraTexturesAttachments.Count)
            {
                this.CombinedExtraTexturesData = new Vector2[this.ExtraTexturesAttachments.Count];
                this.CombinedExtraTexturesAnimations = new TextureAnimation[this.ExtraTexturesAttachments.Count];
            }

            Image<Rgba32>[] imgs = new Image<Rgba32>[this.ExtraTexturesAttachments.Count];
            int i = 0;
            foreach (Guid id in this.ExtraTexturesAttachments) // Ensure data loaded
            {
                if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(id, AssetType.Texture, out Asset tAss) == AssetStatus.Return && tAss != null && tAss.Texture != null && tAss.Texture.glReady)
                {
                    imgs[i++] = tAss.Texture.CompoundImage();
                    maxW = Math.Max(maxW, imgs[i - 1].Width);
                    maxH = Math.Max(maxH, imgs[i - 1].Height);
                    this.CombinedExtraTexturesAnimations[i - 1] = tAss.Texture.CachedAnimation;
                }
                else
                {
                    s = false;
                }
            }

            if (s)
            {
                this.CombinedExtraTextures?.Dispose();
                this.CombinedExtraTextures = new Texture(TextureTarget.Texture2DArray);
                this.CombinedExtraTextures.Bind();
                this.CombinedExtraTextures.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                this.CombinedExtraTextures.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
                GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.Rgba8, maxW, maxH, this.ExtraTexturesAttachments.Count, PixelDataFormat.Rgba, PixelDataType.Byte, IntPtr.Zero);
                for (i = 0; i < this.ExtraTexturesAttachments.Count; ++i)
                {
                    Image<Rgba32> img = imgs[i];
                    this.CombinedExtraTexturesData[i] = new Vector2(img.Width, img.Height);
                    img.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem);
                    System.Buffers.MemoryHandle hnd = mem.Pin();
                    GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, img.Width, img.Height, 1, PixelDataFormat.Rgba, PixelDataType.Byte, new IntPtr(hnd.Pointer));
                    hnd.Dispose();
                    img.Dispose();
                }

                this.HasExtraTexture = true;
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetPrimitiveArray("Textures", this.ExtraTexturesAttachments.ToArray());
            return ret;
        }

        public void SerializeCompatibility(DataElement ret) => ret.SetPrimitiveArray("Textures", this.ExtraTexturesAttachments.ToArray());

        public void Deserialize(DataElement e)
        {
            this.ExtraTexturesAttachments.Clear();
            this.ExtraTexturesAttachments.AddRange(e.GetPrimitiveArrayWithLegacySupport("Textures", (n, c) => c.GetGuidLegacy(n), Array.Empty<Guid>()));
        }

        public void DeserializeCompatibility(DataElement e) => this.Deserialize(e);

        public LinkedTextureContainer FullCopy()
        {
            LinkedTextureContainer ret = new LinkedTextureContainer();
            ret.ExtraTexturesAttachments.AddRange(this.ExtraTexturesAttachments);
            return ret;
        }
    }
}
