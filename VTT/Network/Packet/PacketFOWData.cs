namespace VTT.Network.Packet
{
    using System.Numerics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.IO;

    public class PacketFOWData : PacketBase
    {
        public bool Status { get; set; }
        public Guid MapID { get; set; }
        public Image<Rgba64> Image { get; set; }
        public override uint PacketID => 37;
        public override bool Compressed => true;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer) // Client got server fow data
            {
                client.Logger.Log(Util.LogLevel.Debug, "Got FOW packet");
                if (client.CurrentMap?.ID.Equals(this.MapID) ?? false)
                {
                    if (this.Status)
                    {
                        Vector2 nSize = new Vector2(this.Image.Width, this.Image.Height);
                        client.DoTask(() => client.Frontend.Renderer.MapRenderer.FOWRenderer.UploadFOW(nSize, this.Image));
                    }
                    else
                    {
                        client.DoTask(() => client.Frontend.Renderer.MapRenderer.FOWRenderer.DeleteFOW());
                    }
                }
                else
                {
                    client.Logger.Log(Util.LogLevel.Warn, "Got fow data for non-loaded map, discarding.");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Status = br.ReadBoolean();
            this.MapID = new Guid(br.ReadBytes(16));
            if (this.Status)
            {
                this.Image = SixLabors.ImageSharp.Image.Load<Rgba64>(br.BaseStream);
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Status);
            bw.Write(this.MapID.ToByteArray());
            if (this.Status)
            {
                this.Image.SaveAsPng(bw.BaseStream);
            }
        }
    }
}
