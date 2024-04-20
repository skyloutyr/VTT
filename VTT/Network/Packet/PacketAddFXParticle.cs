namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using System.Numerics;
    using VTT.Util;

    public class PacketAddFXParticle : PacketBase
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

        public override void Decode(BinaryReader br)
        {
            this.SystemID = br.ReadGuid();
            this.NumParticles = br.ReadInt32();
            this.Location = br.ReadSysVec3();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.SystemID);
            bw.Write(this.NumParticles);
            bw.Write(this.Location);
        }
    }
}
