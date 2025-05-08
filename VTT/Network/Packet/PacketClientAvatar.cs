namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;

    public class PacketClientAvatar : PacketBaseWithCodec
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

        public override void LookupData(Codec c) => this.Image = c.Lookup(this.Image);
    }
}
