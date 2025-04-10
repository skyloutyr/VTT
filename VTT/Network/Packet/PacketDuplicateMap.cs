namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketDuplicateMap : PacketBase
    {
        public override uint PacketID => 82;

        public Guid MapID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    this.ContextLogger.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to duplicate a map without permissions!");
                    return;
                }

                if (!server.TryGetMapPointer(this.MapID, out ServerMapPointer smp) || !smp.Valid)
                {
                    this.ContextLogger.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to duplicate a non-existing map {this.MapID}!");
                    return;
                }

                if (!server.TryGetMap(this.MapID, out Map m))
                {
                    this.ContextLogger.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to duplicate map {this.MapID} but the server could not load this map!");
                    return;
                }

                Map m1 = m.Clone();
                if (!server.AddMap(m1))
                {
                    this.ContextLogger.Log(LogLevel.Error, $"Client {this.Sender.ID} asked to duplicate map {this.MapID} but the duplicate map already exists!");
                    return;
                }

                new PacketMapPointer() { Data = new System.Collections.Generic.List<(Guid, string, string)>() { (m1.ID, m1.Folder, m1.Name) }, Remove = false, }.Broadcast(c => c.IsAdmin);
            }
        }

        public override void Decode(BinaryReader br) => this.MapID = br.ReadGuid();
        public override void Encode(BinaryWriter bw) => bw.Write(this.MapID);
    }
}
