namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketAddTurnEntry : PacketBase
    {
        public Guid ObjectID { get; set; }
        public float Value { get; set; }
        public string TeamName { get; set; }
        public int AdditionIndex { get; set; }
        public override uint PacketID => 2;


        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got turn tracker addition request");
            if (isServer && !this.Sender.IsAdmin)
            {
                l.Log(LogLevel.Warn, "Client asked for turn tracker modification without permissions!");
                return;
            }

            Map m = isServer ? server.Maps[this.Sender.ClientMapID] : client.CurrentMap;
            if (m == null)
            {
                l.Log(LogLevel.Warn, "Got turn tracker packet for non-existing map!");
                return;
            }

            TurnTracker.Team t = m.TurnTracker.Teams.Find(p => p.Name.Equals(this.TeamName)) ?? m.TurnTracker.Teams[0];
            TurnTracker.Entry e = new TurnTracker.Entry() { NumericValue = this.Value, ObjectID = this.ObjectID, Team = t };
            lock (m.TurnTracker.Lock)
            {
                m.TurnTracker.Add(e, this.AdditionIndex == -1 ? m.TurnTracker.Entries.Count : this.AdditionIndex);
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(m.ID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ObjectID = new Guid(br.ReadBytes(16));
            this.Value = br.ReadSingle();
            this.TeamName = br.ReadString();
            this.AdditionIndex = br.ReadInt32();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.ObjectID.ToByteArray());
            bw.Write(this.Value);
            bw.Write(this.TeamName);
            bw.Write(this.AdditionIndex);
        }
    }
}
