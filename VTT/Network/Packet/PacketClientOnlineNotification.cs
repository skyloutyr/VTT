namespace VTT.Network.Packet
{
    using System;
    using VTT.Util;

    public class PacketClientOnlineNotification : PacketBaseWithCodec
    {
        public override uint PacketID => 26;

        public Guid ClientID { get; set; }
        public bool Status { get; set; }

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

        public override void LookupData(Codec c)
        {
            this.ClientID = c.Lookup(this.ClientID);
            this.Status = c.Lookup(this.Status);
        }
    }
}
