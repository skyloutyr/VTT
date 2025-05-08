namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketFullJournal : PacketBaseWithCodec
    {
        public override uint PacketID => 39;
        public override bool Compressed => true;

        public TextJournal Journal { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
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

        public override void LookupData(Codec c) => c.Lookup(this.Journal ??= new TextJournal());
    }
}
