namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketCreateJournal : PacketBase
    {
        public Guid JournalID { get; set; }
        public Guid CreatorID { get; set; }
        public string Title { get; set; }
        public override uint PacketID => 28;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got journal creation request");
            if (isServer)
            {
                if (this.Sender.IsAdmin)
                {
                    TextJournal tj = new TextJournal() { IsEditable = false, IsPublic = false, NeedsSave = true, OwnerID = this.CreatorID, SelfID = this.JournalID, Text = String.Empty, Title = this.Title };
                    if (server.Journals.TryAdd(tj.SelfID, tj))
                    {
                        this.Broadcast(c => c.IsAdmin);
                    }
                    else
                    {
                        l.Log(LogLevel.Warn, "Could not add journal - internal error");
                    }
                }
                else
                {
                    l.Log(LogLevel.Warn, "A client asked to create a journal without permissions!");
                }
            }
            else
            {
                TextJournal tj = new TextJournal() { IsEditable = false, IsPublic = false, NeedsSave = true, OwnerID = this.CreatorID, SelfID = this.JournalID, Text = String.Empty, Title = this.Title };
                if (!client.Journals.TryAdd(tj.SelfID, tj))
                {
                    l.Log(LogLevel.Warn, "Could not add journal - internal error");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.JournalID = new Guid(br.ReadBytes(16));
            this.CreatorID = new Guid(br.ReadBytes(16));
            this.Title = br.ReadString();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.JournalID.ToByteArray());
            bw.Write(this.CreatorID.ToByteArray());
            bw.Write(this.Title);
        }
    }
}
