namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketRemoveAllDrawings : PacketBaseWithCodec
    {
        public override uint PacketID => 65;

        public Guid MapID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
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
                Map m = client.CurrentMap;
                if (!m?.ID.Equals(this.MapID) ?? false)
                {
                    l.Log(LogLevel.Warn, "Server asked to remove all drawings for non-current map!");
                    return;
                }

                m.Drawings.Clear();
                client.DoTask(() => Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.FreeAll());
            }
        }

        public override void LookupData(Codec c) => this.MapID = c.Lookup(this.MapID);
    }
}
