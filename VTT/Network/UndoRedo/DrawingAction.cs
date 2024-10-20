namespace VTT.Network.UndoRedo
{
    using VTT.Control;
    using VTT.Network.Packet;

    public class DrawingAction : SmallChangeAction
    {
        public override ServerActionType ActionType => ServerActionType.AddDrawing;

        public DrawingPointContainer DPC { get; set; }
        public Map Container { get; set; }

        public override bool AcceptSmallChange(SmallChangeAction newAction)
        {
            if (newAction is DrawingAction da && da.DPC.ID.Equals(this.DPC.ID))
            {
                this.DPC = da.DPC;
                return true;
            }

            return false;
        }

        public override void Redo()
        {
            DrawingPointContainer dpc = this.Container.Drawings.Find(x => x.ID.Equals(this.DPC.ID));
            if (dpc == null)
            {
                this.Container.Drawings.Add(this.DPC);
                this.Container.NeedsSave = true;
                new PacketAddOrUpdateDrawing() { DPC = this.DPC, MapID = this.Container.ID }.Broadcast(x => x.ClientMapID.Equals(this.Container.ID));
            }
        }

        public override void Undo()
        {
            DrawingPointContainer dpc = this.Container.Drawings.Find(x => x.ID.Equals(this.DPC.ID));
            if (dpc != null)
            {
                this.Container.Drawings.Remove(dpc);
                this.Container.NeedsSave = true;
                new PacketRemoveDrawing() { DrawingID = this.DPC.ID, MapID = this.Container.ID }.Broadcast(x => x.ClientMapID.Equals(this.Container.ID));
            }
        }
    }
}
