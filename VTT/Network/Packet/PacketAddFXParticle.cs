namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;

    public class PacketAddFXParticle : PacketBaseWithCodec
    {
        public override uint PacketID => 74;

        public Vector3 Location { get; set; }
        public int NumParticles { get; set; }
        public Guid SystemID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                this.Broadcast(x => x.ClientMapID.Equals(this.Sender.ClientMapID));
            }
            else
            {
                if (client.Settings.ParticlesEnabled)
                {
                    client.Frontend.Renderer.ParticleRenderer.AddFXEmitter(this.SystemID, this.Location, this.NumParticles);
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.SystemID = c.Lookup(this.SystemID);
            this.NumParticles = c.Lookup(this.NumParticles);
            this.Location = c.Lookup(this.Location);
        }
    }
}
