namespace VTT.Network.Packet
{
    using VTT.Util;
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketFullJournal : PacketBase
    {
        public TextJournal Journal { get; set; }
        public override uint PacketID => 39;
        public override bool Compressed => true;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got journal data");
            if (isServer)
            {
                bool allowed = this.Sender.IsAdmin;
                if (server.Journals.ContainsKey(this.Journal.SelfID))
                {
                    if (Server.Journals.TryGetValue(this.Journal.SelfID, out TextJournal tj))
                    {
                        allowed |= tj.IsEditable;
                    }
                }

                if (allowed)
                {
                    server.Journals[this.Journal.SelfID] = this.Journal;
                    this.Broadcast(c => this.Journal.IsPublic || c.IsAdmin);
                }
                else
                {
                    l.Log(LogLevel.Warn, "Client asked for full journal data without permissions!");
                }
            }
            else
            {
                client.Journals[this.Journal.SelfID] = this.Journal;
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Journal = new TextJournal();
            this.Journal.Deserialize(new DataElement(br));
        }

        public override void Encode(BinaryWriter bw) => this.Journal.Serialize().Write(bw);
    }
}
