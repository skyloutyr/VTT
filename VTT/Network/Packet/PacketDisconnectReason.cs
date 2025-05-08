namespace VTT.Network.Packet
{
    using System;

    public class PacketDisconnectReason : PacketBaseWithCodec
    {
        public override uint PacketID => 35;

        public DisconnectReason DCR { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer) => client.SetDisconnectReason(this.DCR);
        public override void LookupData(Codec c) => this.DCR = c.Lookup(this.DCR);
    }
}
