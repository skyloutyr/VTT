namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketRemoveDrawing : PacketBase
    {
        public Guid MapID { get; set; }
        public Guid DrawingID { get; set; }

        public override uint PacketID => 64;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got Drawing remove packet.");
            if (isServer)
            {
                if (!this.Sender.CanDraw && !this.Sender.IsAdmin)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to remove a drawing but they don't have draw permissions!");
                    return;
                }

                if (this.Sender.IsObserver)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to remove a drawing but they are an observer!");
                    return;
                }

                if (!server.TryGetMap(this.MapID, out Map m))
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to remove a drawing from a non-existing map!");
                    return;
                }

                DrawingPointContainer drawing = m.Drawings.Find(d => d.ID.Equals(this.DrawingID));
                if (drawing == null)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to remove a non-existant drawing!");
                    return;
                }

                if (!this.Sender.IsAdmin && !drawing.OwnerID.Equals(this.Sender.ID))
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to remove a drawing without permissions!");
                    return;
                }

                m.Drawings.Remove(drawing);
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
            else
            {
                if (!client.CurrentMap?.ID.Equals(this.MapID) ?? false)
                {
                    l.Log(LogLevel.Warn, "Server asked to remove a drawing but provided a non-current map!");
                    return;
                }

                DrawingPointContainer drawing = client.CurrentMap.Drawings.Find(d => d.ID.Equals(this.DrawingID));
                if (drawing == null)
                {
                    l.Log(LogLevel.Error, $"Server asked to remove a non-existant drawing!");
                    return;
                }

                client.CurrentMap.Drawings.Remove(drawing);
                client.DoTask(() => Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.RemoveContainer(drawing.ID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.DrawingID = br.ReadGuid();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.DrawingID);
        }
    }
}
