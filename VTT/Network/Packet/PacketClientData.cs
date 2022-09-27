namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class PacketClientData : PacketBase
    {
        public List<ClientInfo> InfosToUpdate { get; set; } = new List<ClientInfo>();
        public override uint PacketID => 24;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            List<ClientInfo> broadcastedChanges = new List<ClientInfo>();
            foreach (ClientInfo ci in this.InfosToUpdate)
            {
                if (!isServer || this.Sender.IsAdmin || this.Sender.ID.Equals(ci.ID))
                {
                    if (isServer)
                    {
                        ServerClient sc = server.ClientsByID[ci.ID];
                        sc.Info.Color = ci.Color;
                        sc.Info.Name = ci.Name;
                        sc.Info.IsAdmin = ci.IsAdmin;
                        sc.Info.IsObserver = ci.IsObserver;
                        sc.SaveClientData();
                        broadcastedChanges.Add(sc.Info);
                    }
                    else
                    {
                        client.ClientInfos[ci.ID] = ci;
                    }
                }
            }

            if (isServer)
            {
                new PacketClientData() { InfosToUpdate = broadcastedChanges }.Broadcast();
            }
        }

        public override void Decode(BinaryReader br)
        {
            int amt = br.ReadInt32();
            while (amt-- > 0)
            {
                this.InfosToUpdate.Add(new ClientInfo(br));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.InfosToUpdate.Count);
            foreach (ClientInfo ci in this.InfosToUpdate)
            {
                ci.Write(bw);
            }
        }
    }
}
