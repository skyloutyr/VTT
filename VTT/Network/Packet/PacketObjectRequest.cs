namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketObjectRequest : PacketBase
    {
        public Guid ObjectID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(VTT.Util.LogLevel.Info, "Client " + this.Sender.ID + " asked for object data for " + this.ObjectID);
                Map m = server.Maps[this.Sender.ClientMapID];
                if (m.GetObject(this.ObjectID, out MapObject mo))
                {
                    PacketMapObject pmo = new PacketMapObject() { IsServer = true, Session = sessionID, Obj = mo };
                    pmo.Send(this.Sender);
                }
                else
                {
                    server.Logger.Log(VTT.Util.LogLevel.Warn, "The client requested data for a non-existing object!");
                }
            }
        }

        public override void Decode(BinaryReader br) => this.ObjectID = new Guid(br.ReadBytes(16));

        public override void Encode(BinaryWriter bw) => bw.Write(this.ObjectID.ToByteArray());
    }
}
