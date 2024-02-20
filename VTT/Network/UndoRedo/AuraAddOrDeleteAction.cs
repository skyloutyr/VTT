namespace VTT.Network.UndoRedo
{
    using SixLabors.ImageSharp;
    using VTT.Control;
    using VTT.Network.Packet;

    public class AuraAddOrDeleteAction : ServerAction
    {
        public override ServerActionType ActionType => ServerActionType.AuraAddOrRemove;

        public MapObject AuraContainer { get; set; }
        public Color AuraColor { get; set; }
        public float AuraRange { get; set; }
        public int AuraIndex { get; set; }

        public bool IsAddition { get; set; }

        public override void Redo()
        {
            lock (this.AuraContainer.Lock)
            {
                if (this.IsAddition)
                {
                    if (this.AuraContainer.Container != null)
                    {
                        this.AuraContainer.Auras.Add((this.AuraRange, this.AuraColor));
                        this.AuraContainer.Container.NeedsSave = true;
                        new PacketAura() { ActionType = PacketAura.Action.Add, AuraColor = this.AuraColor, AuraRange = this.AuraRange, ObjectID = this.AuraContainer.ID, MapID = this.AuraContainer.MapID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                    }
                }
                else
                {
                    this.AuraContainer.Auras.RemoveAt(this.AuraIndex);
                    this.AuraContainer.Container.NeedsSave = true;
                    new PacketAura() { ActionType = PacketAura.Action.Delete, Index = this.AuraIndex, ObjectID = this.AuraContainer.ID, MapID = this.AuraContainer.MapID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                }
            }
        }

        public override void Undo()
        {
            lock (this.AuraContainer.Lock)
            {
                if (this.IsAddition)
                {
                    if (this.AuraContainer.Container != null && this.AuraIndex >= 0 && this.AuraIndex < this.AuraContainer.Auras.Count)
                    {
                        this.AuraContainer.Auras.RemoveAt(this.AuraIndex);
                        this.AuraContainer.Container.NeedsSave = true;
                        new PacketAura() { ActionType = PacketAura.Action.Delete, Index = this.AuraIndex, ObjectID = this.AuraContainer.ID, MapID = this.AuraContainer.MapID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                    }
                }
                else
                {
                    if (this.AuraIndex >= this.AuraContainer.Auras.Count)
                    {
                        this.AuraContainer.Auras.Add((this.AuraRange, this.AuraColor));
                        this.AuraContainer.Container.NeedsSave = true;
                        new PacketAura() { ActionType = PacketAura.Action.Add, AuraColor = this.AuraColor, AuraRange = this.AuraRange, ObjectID = this.AuraContainer.ID, MapID = this.AuraContainer.MapID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                    }
                    else
                    {
                        this.AuraContainer.Auras.Insert(this.AuraIndex, (this.AuraRange, this.AuraColor));
                        this.AuraContainer.Container.NeedsSave = true;
                        new PacketAura() { Index = this.AuraIndex, ActionType = PacketAura.Action.Add, AuraColor = this.AuraColor, AuraRange = this.AuraRange, ObjectID = this.AuraContainer.ID, MapID = this.AuraContainer.MapID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                    }
                }
            }
        }
    }
}
