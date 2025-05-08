namespace VTT.Network.Packet
{
    using System.Numerics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;

    public class PacketFOWData : PacketBaseWithCodec
    {
        public override uint PacketID => 37;
        public override bool Compressed => true;

        public bool Status { get; set; }
        public Guid MapID { get; set; }
        public Image<Rgba64> Image { get; set; }

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

        public override void LookupData(Codec c)
        {
            this.Status = c.Lookup(this.Status);
            this.MapID = c.Lookup(this.MapID);
            if (this.Status)
            {
                this.Image = c.Lookup(this.Image);
            }
        }
    }
}
