namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Network.UndoRedo;
    using VTT.Util;

    public class PacketAddTurnEntry : PacketBaseWithCodec
    {
        public override uint PacketID => 2;

        public Guid ObjectID { get; set; }
        public float Value { get; set; }
        public string TeamName { get; set; }
        public int AdditionIndex { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got turn tracker addition request");
            if (isServer && !this.Sender.IsAdmin)
            {
                l.Log(LogLevel.Warn, "Client asked for turn tracker modification without permissions!");
                return;
            }

            Map m = isServer ? server.GetExistingMap(this.Sender.ClientMapID) : client.CurrentMap;
            if (m == null)
            {
                l.Log(LogLevel.Warn, "Got turn tracker packet for non-existing map!");
                return;
            }

            TurnTracker.Team t = m.TurnTracker.Teams.Find(p => p.Name.Equals(this.TeamName)) ?? m.TurnTracker.Teams[0];
            TurnTracker.Entry e = new TurnTracker.Entry() { NumericValue = this.Value, ObjectID = this.ObjectID, Team = t };
            int ei;
            lock (m.TurnTracker.Lock)
            {
                ei = m.TurnTracker.Add(e, this.AdditionIndex == -1 ? m.TurnTracker.Entries.Count : this.AdditionIndex);
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Sender.ActionMemory.NewAction(new AddTurnEntryAction() { EntryIndex = ei, AdditionIndex = this.AdditionIndex, EntryObjectID = this.ObjectID, Map = m, NumericValue = this.Value, TeamName = this.TeamName });
                this.Broadcast(c => c.ClientMapID.Equals(m.ID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.ObjectID = c.Lookup(this.ObjectID);
            this.Value = c.Lookup(this.Value);
            this.TeamName = c.Lookup(this.TeamName);
            this.AdditionIndex = c.Lookup(this.AdditionIndex);
        }
    }
}
