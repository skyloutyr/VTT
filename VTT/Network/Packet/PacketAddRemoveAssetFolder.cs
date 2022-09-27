﻿namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;

    public class PacketAddRemoveAssetFolder : PacketBase
    {
        public string Path { get; set; } // Where to add/remove
        public string Name { get; set; } // Name of thing to add/remove
        public bool Remove { get; set; }
        public override uint PacketID => 1;

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
                        server.Logger.Log(VTT.Util.LogLevel.Info, "Deleting asset directory at " + dir.GetPath());
                        new PacketAssetDef() { ActionType = AssetDefActionType.RemoveDir, Dir = dir, Root = this.Path }.Broadcast(c => c.IsAdmin);
                    }
                    else
                    {
                        AssetDirectory newDir = new AssetDirectory() { Name = dir.GetUniqueSubdirName(this.Name) };
                        dir.Directories.Add(newDir);
                        newDir.Parent = dir;
                        Directory.CreateDirectory(server.AssetManager.GetFSPath(newDir));
                        server.Logger.Log(VTT.Util.LogLevel.Info, "Adding asset directory at " + newDir.GetPath());
                        server.Logger.Log(VTT.Util.LogLevel.Debug, "FS path: " + server.AssetManager.GetFSPath(newDir));
                        new PacketAssetDef() { ActionType = AssetDefActionType.AddDir, Dir = newDir, Root = this.Path }.Broadcast(c => c.IsAdmin);
                    }
                }
                else
                {
                    server.Logger.Log(VTT.Util.LogLevel.Warn, "Client " + sc.ID + " asked to modify asset data without being an administrator!");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Path = br.ReadString();
            this.Name = br.ReadString();
            this.Remove = br.ReadBoolean();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Path);
            bw.Write(this.Name);
            bw.Write(this.Remove);
        }
    }
}
