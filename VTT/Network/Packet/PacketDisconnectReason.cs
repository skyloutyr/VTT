namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Util;

    public class PacketDisconnectReason : PacketBase
    {
        public DisconnectReason DCR { get; set; }
        public override uint PacketID => 35;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer) => client.SetDisconnectReason(this.DCR);
        public override void Decode(BinaryReader br) => this.DCR = br.ReadEnumSmall<DisconnectReason>();
        public override void Encode(BinaryWriter bw) => bw.WriteEnumSmall(this.DCR);
    }
}
