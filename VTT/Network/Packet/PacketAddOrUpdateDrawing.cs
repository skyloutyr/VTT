namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Network.UndoRedo;
    using VTT.Util;

    public class PacketAddOrUpdateDrawing : PacketBaseWithCodec
    {
        public override uint PacketID => 63;

        public Guid MapID { get; set; }
        public DrawingPointContainer DPC { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got Drawing packet.");
            if (isServer)
            {
                if (!this.Sender.CanDraw && !this.Sender.IsAdmin)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to add their drawing but they don't have draw permissions!");
                    return;
                }

                if (this.Sender.IsObserver)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to add their drawing but they are an observer!");
                    return;
                }

                if (!server.TryGetMap(this.MapID, out Map m))
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to add their drawing to a non-existing map!");
                    return;
                }

                if (!m.EnableDrawing)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to add their drawing but drawings are disabled for the map provided!");
                    return;
                }

                DrawingPointContainer lDpc = m.Drawings.Find(x => x.ID.Equals(this.DPC.ID));
                if (lDpc != null)
                {
                    lDpc.UpdateFrom(this.DPC);
                }
                else
                {
                    m.Drawings.Add(this.DPC);
                }

                this.Sender.ActionMemory.NewAction(new DrawingAction() { Container = m, DPC = this.DPC, LastModifyTime = DateTime.Now });
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
            else
            {
                Map m = client.CurrentMap;
                if (!m?.ID.Equals(this.MapID) ?? false)
                {
                    l.Log(LogLevel.Warn, "Server asked to add a drawing but provided a non-current map!");
                    return;
                }

                DrawingPointContainer lDpc = m.Drawings.Find(x => x.ID.Equals(this.DPC.ID));
                if (lDpc != null)
                {
                    lDpc.UpdateFrom(this.DPC);
                    client.DoTask(() => Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.UpdateContainer(this.DPC));
                }
                else
                {
                    m.Drawings.Add(this.DPC);
                    client.DoTask(() => Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.AddContainer(this.DPC));
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            c.Lookup(this.DPC ??= new DrawingPointContainer(Guid.Empty, Guid.Empty, 0, Vector4.Zero));
        }
    }
}
