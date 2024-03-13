namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class PacketClientAvatar : PacketBase
    {
        public override uint PacketID => 73;

        public Image<Rgba32> Image { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                client.Logger.Log(Util.LogLevel.Error, "Server sent client-only avatar packet!");
                return;
            }

            if (!server.ClientInfos.TryGetValue(this.Sender.ID, out ClientInfo value))
            {
                server.Logger.Log(Util.LogLevel.Error, "Non-existing client attempted to change their avatar!");
                return;
            }

            value.Image = this.Image;
            ServerClient sc = server.ClientsByID[value.ID];
            sc.Image = value.Image;
            sc.SaveClientData();
            new PacketClientData() { InfosToUpdate = new List<ClientInfo>() { value } }.Broadcast();
        }

        public override void Decode(BinaryReader br)
        {
            int len = br.ReadInt32();
            if (len != 0)
            {
                byte[] arr = br.ReadBytes(len);
                this.Image = SixLabors.ImageSharp.Image.Load<Rgba32>(arr);
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            if (this.Image != null)
            {
                using MemoryStream ms = new MemoryStream();
                this.Image.SaveAsPng(ms);
                byte[] arr = ms.ToArray();
                bw.Write(arr.Length);
                bw.Write(arr);
            }
            else
            {
                bw.Write(0);
            }
        }
    }
}
