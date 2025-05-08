namespace VTT.Network.Packet
{
    using System;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetDef : PacketBaseWithCodec
    {
        public override uint PacketID => 3;

        public AssetDefActionType ActionType { get; set; }
        public AssetDirectory Dir { get; set; }
        public AssetRef Ref { get; set; }
        public string Root { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // Client request (request for what?)
            {
                server.Logger.Log(LogLevel.Error, "Client sent a PacketAssetDef, not allowed!");
            }
            else // Client update
            {
                client.Logger.Log(LogLevel.Info, "AssetDef packet accepted, action: " + this.ActionType);
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

        public override void LookupData(Codec c)
        {
            this.ActionType = c.Lookup(this.ActionType);
            switch (this.ActionType)
            {
                case AssetDefActionType.Initialize:
                {
                    c.Lookup(this.Dir ??= new AssetDirectory());
                    break;
                }

                case AssetDefActionType.Add:
                case AssetDefActionType.Remove:
                {
                    this.Root = c.Lookup(this.Root);
                    c.Lookup(this.Ref ??= new AssetRef());
                    break;
                }

                case AssetDefActionType.AddDir:
                {
                    this.Root = c.Lookup(this.Root);
                    c.Lookup(this.Dir ??= new AssetDirectory());
                    break;
                }

                case AssetDefActionType.RemoveDir:
                {
                    this.Root = c.Lookup(this.Root);
                    break;
                }
            }
        }
    }
}
