namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketMap : PacketBase
    {
        public Map Map { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                Map m = client.CurrentMap;
                if (m != null && this.Map != null && m.ID.Equals(this.Map.ID))
                {
                    m.Deserialize(this.Map.SerializeWithoutObjects());
                }
                else
                {
                    client.DoTask(() =>
                    {
                        client.SetCurrentMap(this.Map, () =>
                        {
                            client.ClientInfos[client.ID].MapID = this.Map.ID;
                            PacketCommunique cp = new PacketCommunique() { Request = RequestType.ClientMapAck, RequestData = 0 };
                            cp.Send(client.NetClient); // Send Ack response, await object data
                        });
                    });
                }

                client.Logger.Log(VTT.Util.LogLevel.Debug, "Got map from server, acknowledging");
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Map = new Map() { IsServer = false }; // S->C only
            DataElement de = new DataElement();
            de.Read(br);
            this.Map.Deserialize(de);
        }

        public override void Encode(BinaryWriter bw)
        {
            DataElement de = this.Map.SerializeWithoutObjects(); // S->C no object data needed, client needs to confirm the map first
            de.Write(bw);
        }
    }

    public class PacketMapAction : PacketBase
    {
        public override void Act(Guid sessionID, Server server, Client client, bool isServer) => throw new NotImplementedException();
        public override void Decode(BinaryReader br) => throw new NotImplementedException();
        public override void Encode(BinaryWriter bw) => throw new NotImplementedException();
    }
}
