namespace VTT.Network.Packet
{
    using System;

    public class PacketPersonalTimeoutInfo : PacketBaseWithCodec
    {
        public override uint PacketID => 88;
        public long TimeoutSetting { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                this.Sender.PersonalTimeoutInterval = this.TimeoutSetting;
            }
        }

        public override void LookupData(Codec c)
        {
            this.TimeoutSetting = c.Lookup(this.TimeoutSetting);
        }
    }
}
