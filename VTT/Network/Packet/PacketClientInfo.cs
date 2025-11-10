namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;

    public class PacketClientInfo : PacketBaseWithCodec
    {
        public override uint PacketID => 25;

        public bool IsAdmin { get; set; }
        public bool IsObserver { get; set; }
        public bool ServerAllowsEmbeddedImages { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                client.Logger.Log(Util.LogLevel.Debug, "Server handshake complete, got - A:" + this.IsAdmin + ", O:" + this.IsObserver);
                client.IsAdmin = this.IsAdmin;
                client.IsObserver = this.IsObserver;
                client.ServerAllowsEmbeddedImages = this.ServerAllowsEmbeddedImages;

                PacketClientData pcd = new PacketClientData() { InfosToUpdate = new List<ClientInfo>() { client.CreateSelfInfo() } };
                pcd.Send();
            }
        }

        public override void LookupData(Codec c)
        {
            this.IsAdmin = c.Lookup(this.IsAdmin);
            this.IsObserver = c.Lookup(this.IsObserver);
            this.ServerAllowsEmbeddedImages = c.Lookup(this.ServerAllowsEmbeddedImages);
        }
    }
}
