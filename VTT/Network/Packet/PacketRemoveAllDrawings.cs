namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketRemoveAllDrawings : PacketBase
    {
        public Guid MapID { get; set; }
        public override uint PacketID => 65;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got all drawings remove packet.");
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to remove all drawings without permissions!");
                    return;
                }

                if (!server.TryGetMap(this.MapID, out Map m))
                {
                    l.Log(LogLevel.Error, "Client asked to remove all drawings for non-existent map!");
                    return;
                }

                m.Drawings.Clear();
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
            else
            {
                if (!client.CurrentMap?.ID.Equals(this.MapID) ?? false)
                {
                    l.Log(LogLevel.Warn, "Server asked to remove all drawings for non-current map!");
                    return;
                }

                client.CurrentMap.Drawings.Clear();
                client.DoTask(() => Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.FreeAll());
            }
        }

        public override void Decode(BinaryReader br) => this.MapID = br.ReadGuid();

        public override void Encode(BinaryWriter bw) => bw.Write(this.MapID);
    }
}
