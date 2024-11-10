namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketDeleteJournal : PacketBase
    {
        public Guid JournalID { get; set; }
        public override uint PacketID => 31;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got journal deletion request");
            if (isServer)
            {
                if (this.Sender.IsAdmin)
                {
                    if (server.Journals.TryGetValue(this.JournalID, out TextJournal val))
                    {
                        val.NeedsDeletion = true;
                        this.Broadcast();
                    }
                    else
                    {
                        l.Log(LogLevel.Warn, "Could not delete journal - internal error");
                    }
                }
                else
                {
                    l.Log(LogLevel.Warn, "A client asked to deleta a journal without permissions!");
                }
            }
            else
            {
                if (client.Journals.ContainsKey(this.JournalID))
                {
                    if (!client.Journals.TryRemove(this.JournalID, out _))
                    {
                        l.Log(LogLevel.Warn, "Could not delete journal - internal error");
                    }
                }
                else
                {
                    l.Log(LogLevel.Warn, "Server asked to delete a non-existing journal, ignoring");
                }
            }
        }

        public override void Decode(BinaryReader br) => this.JournalID = new Guid(br.ReadBytes(16));
        public override void Encode(BinaryWriter bw) => bw.Write(this.JournalID.ToByteArray());
    }
}
