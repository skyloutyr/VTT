namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketFastLight : PacketBase
    {
        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public int Index { get; set; }
        public FastLight Light { get; set; }
        public Action ActionType { get; set; }
        public override uint PacketID => 60;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got fast light packet");
            bool allowed = true;
            Map m = null;
            MapObject mo = null;
            if (isServer)
            {
                if (server.TryGetMap(this.MapID, out m))
                {
                    if (m.GetObject(this.ObjectID, out mo))
                    {
                        allowed = this.Sender.IsAdmin;
                    }
                }
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
                m?.GetObject(this.ObjectID, out mo);
            }

            if (m == null)
            {
                l.Log(LogLevel.Warn, "Got fast light change packet for non existing map, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Got fast light change packet for non existing object, discarding.");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for fast light change without permissions!");
                return;
            }

            switch (this.ActionType)
            {
                case Action.Add:
                {
                    lock (mo.FastLightsLock)
                    {
                        mo.FastLights.Add(this.Light);
                    }

                    break;
                }

                case Action.Delete:
                {
                    lock (mo.FastLightsLock)
                    {
                        mo.FastLights.RemoveAt(this.Index);
                    }

                    break;
                }

                case Action.Update:
                {
                    mo.FastLights[this.Index] = this.Light;
                    break;
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.ObjectID = br.ReadGuid();
            this.ActionType = br.ReadEnumSmall<Action>();
            if (this.ActionType != Action.Add)
            {
                this.Index = br.ReadInt32();
            }

            if (this.ActionType != Action.Delete)
            {
                DataElement de = new DataElement();
                de.Read(br);
                this.Light = FastLight.FromData(de);
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.ObjectID);
            bw.WriteEnumSmall(this.ActionType);
            if (this.ActionType != Action.Add)
            {
                bw.Write(this.Index);
            }

            if (this.ActionType != Action.Delete)
            {
                DataElement de = this.Light.Serialize();
                de.Write(bw);
            }
        }

        public enum Action
        {
            Add,
            Delete,
            Update
        }
    }
}
