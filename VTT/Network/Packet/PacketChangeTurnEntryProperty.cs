namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeTurnEntryProperty : PacketBase
    {
        public ChangeType Type { get; set; }

        public int EntryIndex { get; set; }
        public Guid EntryRefID { get; set; }

        public string NewTeam { get; set; }
        public string ValueExpression
        {
            get => this.NewTeam;
            set => this.NewTeam = value;
        }

        public float NewValue { get; set; }
        public override uint PacketID => 19;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got turn tracker entry property change request");
            Map m = isServer ? server.GetExistingMap(this.Sender.ClientMapID) : client.CurrentMap;
            if (m == null)
            {
                l.Log(LogLevel.Warn, "Turn request map was null!");
                return;
            }

            TurnTracker.Entry e = m.TurnTracker.GetContextAwareEntry(this.EntryIndex, this.EntryRefID);
            if (e != null)
            {
                if (isServer)
                {
                    bool canEditMo = this.Sender.IsAdmin;
                    if (m.GetObject(e.ObjectID, out MapObject mo))
                    {
                        canEditMo &= mo.CanEdit(this.Sender.ID);
                    }

                    if (!canEditMo)
                    {
                        l.Log(LogLevel.Warn, "Client tried to edit a turn tracker entry property without permissions!");
                        return;
                    }
                }

                switch (this.Type)
                {
                    case ChangeType.Value:
                    {
                        e.NumericValue = this.NewValue;
                        break;
                    }

                    case ChangeType.Team:
                    {
                        TurnTracker.Team t = m.TurnTracker.Teams.Find(p => p.Name.Equals(this.NewTeam));
                        if (t != null)
                        {
                            e.Team = t;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, "Could not find team specified!");
                        }

                        break;
                    }

                    case ChangeType.ValueExpression:
                    {
                        if (ChatParser.TryParseTextAsExpression(this.NewTeam, true, out double d))
                        {
                            this.NewValue = e.NumericValue = (float)d;
                        }
                        else
                        {
                            this.NewValue = e.NumericValue;
                            l.Log(LogLevel.Error, "Expression passed as turn tracker new value could not be evaluated!");
                        }

                        break;
                    }
                }

                if (isServer)
                {
                    m.NeedsSave = true;
                    if (this.Type == ChangeType.ValueExpression)
                    {
                        this.Type = ChangeType.Value;
                        this.NewTeam = string.Empty;
                    }

                    this.Broadcast(c => c.ClientMapID.Equals(m.ID));
                }
            }
            else
            {
                l.Log(LogLevel.Warn, "Could not find specified entry, ignoring");
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Type = (ChangeType)br.ReadByte();
            this.EntryIndex = br.ReadInt32();
            this.EntryRefID = new Guid(br.ReadBytes(16));
            if (this.Type is ChangeType.Team or ChangeType.ValueExpression)
            {
                this.NewTeam = br.ReadString();
            }
            else
            {
                this.NewValue = br.ReadSingle();
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write((byte)this.Type);
            bw.Write(this.EntryIndex);
            bw.Write(this.EntryRefID.ToByteArray());
            if (this.Type is ChangeType.Team or ChangeType.ValueExpression)
            {
                bw.Write(this.NewTeam);
            }
            else
            {
                bw.Write(this.NewValue);
            }
        }

        public enum ChangeType
        {
            Team,
            Value,
            ValueExpression
        }
    }
}
