namespace VTT.Network.UndoRedo
{
    using SixLabors.ImageSharp;
    using System;
    using VTT.Control;
    using VTT.Network.Packet;

    public class AuraChangeAction : SmallChangeAction
    {
        public override ServerActionType ActionType => ServerActionType.AuraAddOrRemove;

        public MapObject AuraContainer { get; set; }
        public Color InitialAuraColor { get; set; }
        public float InitialAuraRange { get; set; }

        public Color NewAuraColor { get; set; }
        public float NewAuraRange { get; set; }

        public int AuraIndex { get; set; }

        public override bool AcceptSmallChange(SmallChangeAction newAction)
        {
            AuraChangeAction aca = newAction as AuraChangeAction;
            if (aca != null && aca.AuraIndex == this.AuraIndex && this.CheckIfRecent(aca.LastModifyTime, 3000))
            {
                this.NewAuraColor = aca.NewAuraColor;
                this.NewAuraRange = aca.NewAuraRange;
                this.LastModifyTime = newAction.LastModifyTime;
                return true;
            }

            return false;
        }

        public override void Redo()
        {
            lock (this.AuraContainer.Lock)
            {
                if (this.AuraIndex >= 0 && this.AuraIndex < this.AuraContainer.Auras.Count)
                {
                    this.AuraContainer.Auras[this.AuraIndex] = (this.NewAuraRange, this.NewAuraColor);
                    new PacketAura() { ActionType = PacketAura.Action.Update, AuraColor = this.NewAuraColor, AuraRange = this.NewAuraRange, Index = this.AuraIndex, MapID = this.AuraContainer.MapID, ObjectID = this.AuraContainer.ID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                }
            }
        }

        public override void Undo()
        {
            lock (this.AuraContainer.Lock)
            {
                if (this.AuraIndex >= 0 && this.AuraIndex < this.AuraContainer.Auras.Count)
                {
                    this.AuraContainer.Auras[this.AuraIndex] = (this.InitialAuraRange, this.InitialAuraColor);
                    new PacketAura() { ActionType = PacketAura.Action.Update, AuraColor = this.InitialAuraColor, AuraRange = this.InitialAuraRange, Index = this.AuraIndex, MapID = this.AuraContainer.MapID, ObjectID = this.AuraContainer.ID }.Broadcast(x => x.ClientMapID.Equals(this.AuraContainer.MapID));
                }
            }
        }
    }
}
