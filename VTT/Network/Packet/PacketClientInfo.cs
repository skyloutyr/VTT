namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class PacketClientInfo : PacketBase
    {
        public bool IsAdmin { get; set; }
        public bool IsObserver { get; set; }
        public override uint PacketID => 25;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                client.Logger.Log(Util.LogLevel.Debug, "Server handshake complete, got - A:" + this.IsAdmin + ", O:" + this.IsObserver);
                client.IsAdmin = this.IsAdmin;
                client.IsObserver = this.IsObserver;

                PacketClientData pcd = new PacketClientData() { InfosToUpdate = new List<ClientInfo>() { client.CreateSelfInfo() } };
                pcd.Send();
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.IsAdmin = br.ReadBoolean();
            this.IsObserver = br.ReadBoolean();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.IsAdmin);
            bw.Write(this.IsObserver);
        }
    }
}
