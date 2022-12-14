namespace VTT.Network.Packet
{
    using System;
    using System.IO;

    public class PacketSetDefaultMap : PacketBase
    {
        public Guid MapID { get; set; }
        public override uint PacketID => 55;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer && this.Sender.IsAdmin && server.Maps.ContainsKey(this.MapID))
            {
                server.Logger.Log(Util.LogLevel.Info, "Changing default map");
                server.Settings.DefaultMapID = this.MapID;
                server.Settings.Save();
            }
        }

        public override void Decode(BinaryReader br) => this.MapID = new Guid(br.ReadBytes(16));
        public override void Encode(BinaryWriter bw) => bw.Write(this.MapID.ToByteArray());
    }
}
