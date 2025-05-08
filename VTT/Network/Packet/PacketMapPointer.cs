namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;

    public class PacketMapPointer : PacketBaseWithCodec
    {
        public override uint PacketID => 46;

        public bool Remove { get; set; }
        public List<(Guid, string, string)> Data { get; set; } = new List<(Guid, string, string)>();

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

        public override void LookupData(Codec c)
        {
            this.Remove = c.Lookup(this.Remove);
            this.Data = c.Lookup(this.Data, x =>
            {
                Guid i1 = c.Lookup(x.Item1);
                string i2 = c.Lookup(x.Item2);
                string i3 = c.Lookup(x.Item3);
                return x = (i1, i2, i3);
            });
        }
    }
}
