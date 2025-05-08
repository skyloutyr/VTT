namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetMove : PacketBaseWithCodec
    {
        public override uint PacketID => 4;

        public Guid MovedRefID { get; set; }
        public string MovedFrom { get; set; }
        public string MovedTo { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = isServer ? server.Logger : client.Logger;
            l.Log(LogLevel.Debug, "Got an asset ref move request");
            AssetManager am = isServer ? server.AssetManager : client.AssetManager;
            AssetDirectory aFrom = am.GetDirAt(this.MovedFrom);
            AssetDirectory aTo = am.GetDirAt(this.MovedTo);
            if (aFrom == null || aTo == null)
            {
                l.Log(LogLevel.Warn, "Asset move requested to/from a non-existing directory!");
                return;
            }

            AssetRef found = aFrom.Refs.Find(r => r.AssetID.Equals(this.MovedRefID));
            if (found == null)
            {
                l.Log(LogLevel.Warn, "No asset " + this.MovedRefID + " exists at " + this.MovedFrom);
                return;
            }

            aFrom.Refs.Remove(found);
            aTo.Refs.Add(found);

            if (isServer)
            {
                string oldLoc = Path.Combine(am.GetFSPath(aFrom), this.MovedRefID.ToString() + ".ab");
                if (File.Exists(oldLoc))
                {
                    string newLoc = Path.Combine(am.GetFSPath(aTo), this.MovedRefID.ToString() + ".ab");
                    File.Move(oldLoc, newLoc);
                    found.ServerPointer.FileLocation = newLoc;
                    oldLoc = oldLoc + ".json";
                    if (File.Exists(oldLoc))
                    {
                        File.Move(oldLoc, newLoc + ".json");
                    }

                    PacketAssetMove pam = new PacketAssetMove() { MovedFrom = this.MovedFrom, MovedTo = this.MovedTo, MovedRefID = this.MovedRefID };
                    pam.Broadcast();
                }
                else
                {
                    l.Log(LogLevel.Error, "Filesystem mismatch - no asset in fs but have asset in manager!");
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.MovedRefID = c.Lookup(this.MovedRefID);
            this.MovedFrom = c.Lookup(this.MovedFrom);
            this.MovedTo = c.Lookup(this.MovedTo);
        }
    }
}
