namespace VTT.Network.Packet
{
    using OpenTK.Mathematics;
    using System;
    using System.IO;

    public class PacketCameraSnap : PacketBase
    {
        public Vector3 CameraPosition { get; set; }
        public Vector3 CameraDirection { get; set; }
        public override uint PacketID => 12;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            this.GetContextLogger().Log(VTT.Util.LogLevel.Debug, "Got camera snap request");
            if (isServer)
            {
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID));
            }
            else
            {
                if (Client.Instance.CurrentMap != null)
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position = this.CameraPosition;
                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction = this.CameraDirection;
                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.CameraPosition = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            this.CameraDirection = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }
        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.CameraPosition.X);
            bw.Write(this.CameraPosition.Y);
            bw.Write(this.CameraPosition.Z);
            bw.Write(this.CameraDirection.X);
            bw.Write(this.CameraDirection.Y);
            bw.Write(this.CameraDirection.Z);
        }
    }
}
