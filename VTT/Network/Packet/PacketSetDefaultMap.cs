namespace VTT.Network.Packet
{
    using System;

    public class PacketSetDefaultMap : PacketBaseWithCodec
    {
        public override uint PacketID => 55;

        public Guid MapID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer && this.Sender.IsAdmin && server.TryGetMap(this.MapID, out _))
            {
                server.Logger.Log(Util.LogLevel.Info, "Changing default map");
                server.Settings.DefaultMapID = this.MapID;
                server.Settings.Save();
                this.Broadcast();
            }

            if (!isServer)
            {
                client.DefaultMPMapID = this.MapID;
            }
        }

        public override void LookupData(Codec c) => this.MapID = c.Lookup(this.MapID);
    }
}
