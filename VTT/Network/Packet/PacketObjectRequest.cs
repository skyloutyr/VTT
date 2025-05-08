namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;

    public class PacketObjectRequest : PacketBaseWithCodec
    {
        public override uint PacketID => 49;

        public Guid ObjectID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Info, "Client " + this.Sender.ID + " asked for object data for " + this.ObjectID);
                Map m = server.GetExistingMap(this.Sender.ClientMapID);
                if (m.GetObject(this.ObjectID, out MapObject mo))
                {
                    PacketMapObject pmo = new PacketMapObject() { IsServer = true, Session = sessionID, Obj = mo };
                    pmo.Send(this.Sender);
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "The client requested data for a non-existing object!");
                }
            }
        }

        public override void LookupData(Codec c) => this.ObjectID = c.Lookup(this.ObjectID);
    }
}
