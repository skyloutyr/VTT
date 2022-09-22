namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetDef : PacketBase
    {
        public AssetDefActionType ActionType { get; set; }
        public AssetDirectory Dir { get; set; }
        public AssetRef Ref { get; set; }
        public string Root { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // Client request (request for what?)
            {
                server.Logger.Log(VTT.Util.LogLevel.Error, "Client sent a PacketAssetDef, not allowed!");
            }
            else // Client update
            {
                client.Logger.Log(VTT.Util.LogLevel.Info, "AssetDef packet accepted, action: " + this.ActionType);
                switch (this.ActionType)
                {
                    case AssetDefActionType.Initialize:
                    {
                        client.DoTask(() =>
                        {
                            client.AssetManager.ClientAssetLibrary.Clear();
                            client.AssetManager.Root.Directories.Clear();
                            client.AssetManager.Root.Refs.Clear();
                            client.AssetManager.Root.Directories.AddRange(this.Dir.Directories);
                            client.AssetManager.Root.Refs.AddRange(this.Dir.Refs);
                            foreach (AssetRef aRef in this.Dir.EnumerateAllRefsRecursively())
                            {
                                client.AssetManager.Refs[aRef.AssetID] = aRef;
                            }
                        });

                        break;
                    }

                    case AssetDefActionType.Add:
                    {
                        AssetDirectory dir = client.AssetManager.GetDirAt(this.Root);
                        dir.Refs.Add(this.Ref);
                        client.AssetManager.Refs[this.Ref.AssetID] = this.Ref;
                        break;
                    }

                    case AssetDefActionType.Remove:
                    {
                        AssetDirectory dir = client.AssetManager.FindDirForRef(this.Ref.AssetID);
                        AssetRef uRef = dir.Refs.Find(r => r.AssetID.Equals(this.Ref.AssetID));
                        dir.Refs.Remove(uRef);
                        break;
                    }

                    case AssetDefActionType.RemoveDir:
                    {
                        AssetDirectory dir = client.AssetManager.GetDirAt(this.Root);
                        client.AssetManager.RecursivelyDeleteDirectory(dir);
                        break;
                    }

                    case AssetDefActionType.AddDir:
                    {
                        AssetDirectory parent = client.AssetManager.GetDirAt(this.Root);
                        parent.Directories.Add(this.Dir);
                        this.Dir.Parent = parent;
                        client.AssetManager.RecursivelyPopulateRefs(this.Dir);
                        break;
                    }
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ActionType = (AssetDefActionType)br.ReadInt32();
            switch (this.ActionType)
            {
                case AssetDefActionType.Initialize:
                {
                    DataElement e = new DataElement();
                    this.Dir = new AssetDirectory();
                    e.Read(br);
                    this.Dir.Deserialize(e);
                    break;
                }

                case AssetDefActionType.Add:
                case AssetDefActionType.Remove:
                {
                    this.Root = br.ReadString();
                    DataElement e = new DataElement();
                    e.Read(br);
                    this.Ref = new AssetRef();
                    this.Ref.Deserialize(e);
                    break;
                }

                case AssetDefActionType.AddDir:
                {
                    this.Root = br.ReadString();
                    DataElement e = new DataElement();
                    e.Read(br);
                    this.Dir = new AssetDirectory();
                    this.Dir.Deserialize(e);
                    break;
                }

                case AssetDefActionType.RemoveDir:
                {
                    this.Root = br.ReadString();
                    break;
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write((int)this.ActionType);
            switch (this.ActionType)
            {
                case AssetDefActionType.Initialize:
                {
                    DataElement e = this.Dir.Serialize();
                    e.Write(bw);
                    break;
                }

                case AssetDefActionType.Add:
                case AssetDefActionType.Remove:
                {
                    bw.Write(this.Root);
                    DataElement e = this.Ref.Serialize();
                    e.Write(bw);
                    break;
                }

                case AssetDefActionType.AddDir:
                {
                    bw.Write(this.Root);
                    DataElement e = this.Dir.Serialize();
                    e.Write(bw);
                    break;
                }

                case AssetDefActionType.RemoveDir:
                {
                    bw.Write(this.Root);
                    break;
                }
            }
        }
    }
}
