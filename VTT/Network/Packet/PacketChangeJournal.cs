namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeJournal : PacketBase
    {
        public Guid JournalID { get; set; }
        public FieldType Change { get; set; }
        public object Value { get; set; }
        public override uint PacketID => 13;
        public override bool Compressed => true;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got journal change request.");
            bool allowed = !isServer;
            TextJournal tj;
            if (isServer)
            {
                server.Journals.TryGetValue(this.JournalID, out tj);
                allowed = tj != null && (this.Sender.IsAdmin || this.Sender.ID.Equals(tj.OwnerID) || tj.IsEditable);
            }
            else
            {
                client.Journals.TryGetValue(this.JournalID, out tj);
            }

            if (tj == null)
            {
                l.Log(LogLevel.Warn, "Got journal change request for non-existing journal!");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "No permissions for journal editing.");
                return;
            }

            switch (this.Change)
            {
                case FieldType.IsPublic:
                {
                    tj.IsPublic = (bool)this.Value;
                    tj.NeedsSave = true;
                    if (isServer)
                    {
                        if (!tj.IsPublic)
                        {
                            new PacketDeleteJournal() { JournalID = tj.SelfID }.Broadcast(c => !c.IsAdmin);
                        }
                        else
                        {
                            new PacketFullJournal() { Journal = tj }.Broadcast();
                        }
                    }

                    return;
                }

                case FieldType.IsEditable:
                {
                    tj.IsEditable = (bool)this.Value;
                    break;
                }

                case FieldType.Title:
                {
                    tj.Title = (string)this.Value;
                    break;
                }

                case FieldType.Text:
                {
                    tj.Text = (string)this.Value;
                    break;
                }
            }

            if (this.IsServer)
            {
                tj.NeedsSave = true;
                this.Broadcast();
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.JournalID = new Guid(br.ReadBytes(16));
            this.Change = (FieldType)br.ReadByte();
            switch (this.Change)
            {
                case FieldType.Title:
                case FieldType.Text:
                {
                    this.Value = br.ReadString();
                    break;
                }

                case FieldType.IsEditable:
                case FieldType.IsPublic:
                {
                    this.Value = br.ReadBoolean();
                    break;
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.JournalID.ToByteArray());
            bw.Write((byte)this.Change);
            switch (this.Change)
            {
                case FieldType.Title:
                case FieldType.Text:
                {
                    bw.Write((string)this.Value);
                    break;
                }

                case FieldType.IsEditable:
                case FieldType.IsPublic:
                {
                    bw.Write((bool)this.Value);
                    break;
                }
            }
        }

        public enum FieldType
        {
            Title,
            Text,
            IsPublic,
            IsEditable
        }
    }
}
