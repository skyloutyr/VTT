namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketCreateMap : PacketBase
    {
        public override uint PacketID => 29;
        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Info, "Asked for new map creation");
                Map m = new Map()
                {
                    IsServer = true,
                    ID = Guid.NewGuid(),
                    AmbientColor = Color.Black,
                    GridColor = Color.White,
                    GridEnabled = true,
                    GridUnit = 5,
                    GridSize = 1,
                    NeedsSave = true,
                    Name = "New Map"
                };

                server.AddMap(m);
                new PacketMapPointer() { Data = new System.Collections.Generic.List<(Guid, string, string)>() { (m.ID, m.Folder, m.Name) }, Remove = false, }.Broadcast(c => c.IsAdmin);
            }
        }

        public override void Decode(BinaryReader br)
        {
        }

        public override void Encode(BinaryWriter bw)
        {
        }
    }
}
