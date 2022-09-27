namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketCommunique : PacketBase
    {
        public RequestType Request { get; set; }
        public int RequestData { get; set; }
        public override uint PacketID => 27;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = isServer ? server.Logger : client.Logger;
            l.Log(LogLevel.Debug, "Got a generic message packet of type " + this.Request + " and data " + this.RequestData);
            switch (this.Request)
            {
                case RequestType.ClientMapAck:
                {
                    if (isServer) // Server side only
                    {
                        ServerClient sc = (ServerClient)server.FindSession(sessionID);
                        Map m = server.Maps[sc.ClientMapID];
                        if (this.RequestData == 1) // Resend map data request
                        {
                            PacketMap mp = new PacketMap() { Map = m, Session = sessionID, IsServer = isServer };
                            mp.Send(sc); // Send the client current map information, wait for MapAck packet
                        }
                        else // Send object datas
                        {
                            for (int i = m.Objects.Count - 1; i >= 0; i--)
                            {
                                MapObject mo = m.Objects[i];
                                PacketMapObject mop = new PacketMapObject() { Obj = mo, Session = sessionID, IsServer = isServer };
                                mop.Send(sc);
                                l.Log(LogLevel.Debug, "Sent a object packet to client for object " + mo.ID);
                            }

                            PacketFOWData pfowd = new PacketFOWData() { Image = m.FOW?.Canvas, MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted };
                            pfowd.Send(sc);
                        }
                    }

                    break;
                }

                case RequestType.ChatMoveToEndRequest:
                {
                    if (!isServer) // Client-side only
                    {
                        client.DoTask(() => client.Frontend.Renderer.GuiRenderer.MoveChatToEnd = true);
                    }

                    break;
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Request = (RequestType)br.ReadInt32();
            this.RequestData = br.ReadInt32();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write((int)this.Request);
            bw.Write(this.RequestData);
        }
    }

    public enum RequestType
    {
        /// <summary>
        /// Client->Server, acknowledging map being received.
        ///     Data: 
        ///         0 - OK, object request
        ///         1 - Client error, map resend request
        /// </summary>
        ClientMapAck,

        /// <summary>
        /// Server->Client, generic response.
        ///     Data:
        ///         0 - OK,
        ///         1 - Permission error.
        /// </summary>
        ServerResponse,
        ChatMoveToEndRequest,
    }
}
