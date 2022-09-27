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
                client.Logger.Log(VTT.Util.LogLevel.Debug, "Got server maps pointer data");
                lock (client.ServerMapPointersLock)
                {
                    foreach ((Guid, string, string) d in this.Data)
                    {
                        if (this.Remove)
                        {
                            bool b = this.TryFindDataIndex(d.Item1, out _, out List<(Guid, string)> container, out int cIndex);
                            if (b)
                            {
                                container.RemoveAt(cIndex);
                                if (container.Count > 1)
                                {
                                    container.Sort((l, r) => l.Item2.CompareTo(r.Item2));
                                }
                            }
                        }
                        else
                        {
                            bool b = this.TryFindDataIndex(d.Item1, out string cFolder, out List<(Guid, string)> container, out int cIndex);
                            if (b)
                            {
                                if (string.Equals(cFolder, d.Item2))
                                {
                                    container[cIndex] = (d.Item1, d.Item3);
                                    if (container.Count > 1)
                                    {
                                        container.Sort((l, r) => l.Item2.CompareTo(r.Item2));
                                    }
                                }
                                else
                                {
                                    container.RemoveAt(cIndex);
                                    if (container.Count > 1)
                                    {
                                        container.Sort((l, r) => l.Item2.CompareTo(r.Item2));
                                    }

                                    if (!client.ServerMapPointers.ContainsKey(d.Item2))
                                    {
                                        client.ServerMapPointers[d.Item2] = new List<(Guid, string)>();
                                    }

                                    client.ServerMapPointers[d.Item2].Add((d.Item1, d.Item3));
                                    if (client.ServerMapPointers[d.Item2].Count > 1)
                                    {
                                        client.ServerMapPointers[d.Item2].Sort((l, r) => l.Item2.CompareTo(r.Item2));
                                    }
                                }
                            }
                            else
                            {
                                if (!client.ServerMapPointers.ContainsKey(d.Item2))
                                {
                                    client.ServerMapPointers[d.Item2] = new List<(Guid, string)>();
                                }

                                client.ServerMapPointers[d.Item2].Add((d.Item1, d.Item3));
                                if (client.ServerMapPointers[d.Item2].Count > 1)
                                {
                                    client.ServerMapPointers[d.Item2].Sort((l, r) => l.Item2.CompareTo(r.Item2));
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool TryFindDataIndex(Guid mId, out string cFolder, out List<(Guid, string)> container, out int containerIndex)
        {
            foreach (KeyValuePair<string, List<(Guid, string)>> kv in Client.Instance.ServerMapPointers)
            {
                int idx = kv.Value.FindIndex(d => d.Item1.Equals(mId));
                if (idx != -1)
                {
                    cFolder = kv.Key;
                    container = kv.Value;
                    containerIndex = idx;
                    return true;
                }
            }

            cFolder = string.Empty;
            containerIndex = -1;
            container = null;
            return false;
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
