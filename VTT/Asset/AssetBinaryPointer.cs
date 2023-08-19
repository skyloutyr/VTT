namespace VTT.Asset
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Network;

    public class AssetBinaryPointer
    {
        public string FileLocation { get; set; } // Assets are files on the hard drive
        public Guid PreviewPointer { get; set; }

        public static void ChangeAssetNameForOldEncoding(string path, AssetMetadata newMeta)
        {
            byte[] assetBinary = GetRawAssetBinary(File.ReadAllBytes(path));
            using BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write));
            bw.Write(new char[] { 'V', 'T', 'A', 'B' }); // Write header
            bw.Write((byte)1);                           // Update version to 1
            bw.Write(assetBinary);                       // Write old binary, preserve data
            string metaLoc = path + ".json";
            File.WriteAllText(metaLoc, JsonConvert.SerializeObject(newMeta));
        }

        public static bool ReadAssetMetadata(string path, out AssetMetadata metadata)
        {
            string aMeta = path + ".json";
            if (File.Exists(aMeta))
            {
                try
                {
                    metadata = JsonConvert.DeserializeObject<AssetMetadata>(File.ReadAllText(aMeta));
                    if (metadata.Type == AssetType.Texture && metadata.TextureInfo == null) // Broken texture info
                    {
                        Server.Instance.Logger.Log(Util.LogLevel.Warn, "Texture for " + path + " doesn't specify texture info, converted from earlier versions?");
                        metadata.TextureInfo = new TextureData.Metadata() { Compress = true, EnableBlending = true, FilterMag = GL.FilterParam.Linear, FilterMin = GL.FilterParam.LinearMipmapLinear, GammaCorrect = true, WrapS = GL.WrapParam.Repeat, WrapT = GL.WrapParam.Repeat };
                        File.WriteAllText(aMeta, JsonConvert.SerializeObject(metadata));
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Server.Instance.Logger.Log(Util.LogLevel.Error, "Could not read asset metadata for " + path);
                    Server.Instance.Logger.Exception(Util.LogLevel.Error, e);
                    metadata = AssetMetadata.Broken;
                    return false;
                }
            }
            else
            {
                using BinaryReader br = new BinaryReader(File.OpenRead(path));
                char[] c = br.ReadChars(4);
                if (c[0] != 'V' || c[1] != 'T' || c[2] != 'A' || c[3] != 'B')
                {
                    metadata = AssetMetadata.Broken;
                    return false;
                }

                byte v = br.ReadByte();
                if (v == 0)
                {
                    AssetType type = (AssetType)br.ReadInt32();
                    string name = br.ReadString();
                    metadata = new AssetMetadata() { Name = name, Type = type, ConstructedFromOldBinaryEncoding = true };
                    return true;
                }
                else
                {
                    Server.Instance.Logger.Log(Util.LogLevel.Error, "Asset metadata format specified as separate (version > 0) but no such file exists! Can't read asset!");
                    metadata = AssetMetadata.Broken;
                    return false;
                }
            }
        }

        public static byte[] GetRawAssetBinary(byte[] assetBinary)
        {
            using MemoryStream ms = new MemoryStream(assetBinary);
            using BinaryReader br = new BinaryReader(ms);
            br.ReadChars(4);
            int version = br.ReadByte();
            if (version == 0) // Assets v0 were type/name prefixed, need to skip to data chunk
            {
                br.ReadInt32();
                br.ReadString();
            }

            int mPos = (int)ms.Position;
            byte[] ret = new byte[assetBinary.Length - mPos];
            Array.Copy(assetBinary, mPos, ret, 0, ret.Length);
            return ret;
        }
    }
}
