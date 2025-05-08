namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;

    public class PacketAddRemoveAssetFolder : PacketBaseWithCodec
    {
        public override uint PacketID => 1;

        public string Path { get; set; } // Where to add/remove
        public string Name { get; set; } // Name of thing to add/remove
        public bool Remove { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // C->S request
            {
                ServerClient sc = (ServerClient)server.FindSession(sessionID);
                if (sc.IsAdmin) // Only admins can manage assets
                {
                    AssetDirectory dir = server.AssetManager.GetDirAt(this.Path);
                    if (this.Remove)
                    {
                        server.AssetManager.RecursivelyDeleteDirectory(dir);
                        server.Logger.Log(Util.LogLevel.Info, "Deleting asset directory at " + dir.GetPath());
                        new PacketAssetDef() { ActionType = AssetDefActionType.RemoveDir, Dir = dir, Root = this.Path }.Broadcast(c => c.IsAdmin);
                    }
                    else
                    {
                        AssetDirectory newDir = new AssetDirectory() { Name = dir.GetUniqueSubdirName(this.Name) };
                        dir.Directories.Add(newDir);
                        newDir.Parent = dir;
                        Directory.CreateDirectory(server.AssetManager.GetFSPath(newDir));
                        server.Logger.Log(Util.LogLevel.Info, "Adding asset directory at " + newDir.GetPath());
                        server.Logger.Log(Util.LogLevel.Debug, "FS path: " + server.AssetManager.GetFSPath(newDir));
                        new PacketAssetDef() { ActionType = AssetDefActionType.AddDir, Dir = newDir, Root = this.Path }.Broadcast(c => c.IsAdmin);
                    }
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "Client " + sc.ID + " asked to modify asset data without being an administrator!");
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.Path = c.Lookup(this.Path);
            this.Name = c.Lookup(this.Name);
            this.Remove = c.Lookup(this.Remove);
        }
    }
}
