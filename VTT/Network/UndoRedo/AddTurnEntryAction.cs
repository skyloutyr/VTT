namespace VTT.Network.UndoRedo
{
    using System;
    using VTT.Control;
    using VTT.Network.Packet;

    public class AddTurnEntryAction : ServerAction
    {
        public override ServerActionType ActionType => ServerActionType.AddTurnEntry;
        public Guid EntryObjectID { get; set; }
        public int AdditionIndex { get; set; }
        public float NumericValue { get; set; }
        public string TeamName { get; set; }
        public int EntryIndex { get; set; }
        public Map Map { get; set; }

        public int SafeGetEntryAtIndex()
        {
            lock (this.Map.TurnTracker.Lock)
            {
                if (this.EntryIndex < 0 || this.EntryIndex >= this.Map.TurnTracker.Entries.Count)
                {
                    return -1;
                }

                return this.EntryIndex;
            }
        }

        public override void Redo()
        {
            int e = this.SafeGetEntryAtIndex();
            if (e == -1)
            {
                TurnTracker.Team t = (string.IsNullOrEmpty(this.TeamName) ? this.Map.TurnTracker.Teams[0] : this.Map.TurnTracker.Teams.Find(x => x.Name.Equals(this.TeamName))) ?? this.Map.TurnTracker.Teams[0];
                lock (this.Map.TurnTracker.Lock)
                {
                    this.Map.TurnTracker.Add(new TurnTracker.Entry() { NumericValue = this.NumericValue, ObjectID = this.EntryObjectID, Team = t }, this.AdditionIndex == -1 ? this.Map.TurnTracker.Entries.Count : this.AdditionIndex);
                }

                this.Map.NeedsSave = true;
                new PacketAddTurnEntry() { AdditionIndex = this.AdditionIndex, ObjectID = this.EntryObjectID, TeamName = this.TeamName, Value = this.NumericValue }.Broadcast(x => x.ClientMapID.Equals(this.Map.ID));
            }
        }

        public override void Undo()
        {
            int e = this.SafeGetEntryAtIndex();
            if (e != -1)
            {
                lock (this.Map.TurnTracker.Lock)
                {
                    this.Map.TurnTracker.Remove(e);
                }
            }
        }
    }
}
