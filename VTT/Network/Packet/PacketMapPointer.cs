namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class PacketMapPointer : PacketBase
    {
        public bool Remove { get; set; }
        public List<(Guid, string, string)> Data { get; set; } = new List<(Guid, string, string)>();
        public override uint PacketID => 46;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer) // Client-only
            {
                client.Logger.Log(Util.LogLevel.Debug, "Got server maps pointer data");
                bool didWork = false;
                lock (client.ServerMapPointersLock)
                {
                    foreach ((Guid, string, string) d in this.Data)
                    {
                        if (this.Remove)
                        {
                            didWork |= client.RawClientMPMapsData.RemoveAll(x => x.Item3 == d.Item1) > 0;
                        }
                        else
                        {
                            didWork = true;
                            int existing = client.RawClientMPMapsData.FindIndex(x => x.Item3 == d.Item1);
                            if (existing != -1)
                            {
                                client.RawClientMPMapsData.RemoveAt(existing);
                            }

                            client.RawClientMPMapsData.Add((d.Item2, d.Item3, d.Item1));
                        }
                    }
                }

                if (didWork)
                {
                    client.SortClientMaps();
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Remove = br.ReadBoolean();
            int amt = br.ReadInt32();
            while (amt-- > 0)
            {
                Guid id = new Guid(br.ReadBytes(16));
                string folder = br.ReadString();
                string name = br.ReadString();
                this.Data.Add((id, folder, name));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Remove);
            bw.Write(this.Data.Count);
            foreach ((Guid, string, string) d in this.Data)
            {
                bw.Write(d.Item1.ToByteArray());
                bw.Write(d.Item2);
                bw.Write(d.Item3);
            }
        }
    }
}
