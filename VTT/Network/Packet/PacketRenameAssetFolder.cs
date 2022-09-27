namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;

    public class PacketRenameAssetFolder : PacketBase
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public override uint PacketID => 53;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                ServerClient sc = (ServerClient)server.FindSession(sessionID);
                if (sc.IsAdmin)
                {
                    server.Logger.Log(VTT.Util.LogLevel.Info, "Client asked to rename " + this.Path + " to " + this.Name);
                    AssetDirectory ad = server.AssetManager.GetDirAt(this.Path);
                    if (ad != null)
                    {
                        string oldFS = server.AssetManager.GetFSPath(ad);
                        ad.Name = ad.Parent.GetUniqueSubdirName(this.Name);
                        string newFS = server.AssetManager.GetFSPath(ad);
                        Directory.Move(oldFS, newFS);
                        new PacketRenameAssetFolder() { Name = ad.Name, Path = this.Path }.Broadcast(c => c.IsAdmin);
                    }
                    else
                    {
                        server.Logger.Log(VTT.Util.LogLevel.Error, "Client asked to rename a non-existing asset directory!");
                    }
                }
                else
                {
                    server.Logger.Log(VTT.Util.LogLevel.Warn, "Client " + sc.ID + " asked to modify asset data without being an administrator!");
                }
            }
            else
            {
                client.Logger.Log(VTT.Util.LogLevel.Info, "Server asked to rename " + this.Path + " to " + this.Name);
                AssetDirectory ad = client.AssetManager.GetDirAt(this.Path);
                if (ad != null)
                {
                    ad.Name = this.Name;
                }
                else
                {
                    client.Logger.Log(VTT.Util.LogLevel.Error, "Server asked to rename a non-existing asset directory!");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Path = br.ReadString();
            this.Name = br.ReadString();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Path);
            bw.Write(this.Name);
        }
    }
}
