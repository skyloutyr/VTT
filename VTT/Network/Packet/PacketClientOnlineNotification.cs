namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Util;

    public class PacketClientOnlineNotification : PacketBase
    {
        public Guid ClientID { get; set; }
        public bool Status { get; set; }
        public override uint PacketID => 26;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                if (client.ClientInfos.TryGetValue(this.ClientID, out ClientInfo ci))
                {
                    ci.IsLoggedOn = this.Status;
                }
                else
                {
                    client.Logger.Log(LogLevel.Warn, "Client online notification for unknown client!");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ClientID = br.ReadGuid();
            this.Status = br.ReadBoolean();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.ClientID);
            bw.Write(this.Status);
        }
    }
}
