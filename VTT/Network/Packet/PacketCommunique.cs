namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    /// <summary>
    /// Obsolete without the attribute for now - was supposed to be a generic two way message for communications but there was never a need for such a packet.
    /// </summary>
    public class PacketCommunique : PacketBaseWithCodec
    {
        public override uint PacketID => 27;

        public RequestType Request { get; set; }
        public int RequestData { get; set; }

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
                        if (server.TryGetMap(sc.ClientMapID, out Map m))
                        {
                            if (this.RequestData == 1) // Resend map data request
                            {
                                PacketMap mp = new PacketMap() { Map = m, Session = sessionID, IsServer = isServer };
                                mp.Send(sc); // Send the client current map information, wait for MapAck packet
                            }
                            else // Send object datas
                            {
                                foreach (MapObject mo in m.IterateObjects(null))
                                {
                                    PacketMapObject mop = new PacketMapObject() { Obj = mo, Session = sessionID, IsServer = isServer };
                                    mop.Send(sc);
                                    l.Log(LogLevel.Debug, "Sent a object packet to client for object " + mo.ID);
                                }

                                PacketFOWData pfowd = new PacketFOWData() { Image = m.FOW?.Canvas, MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted };
                                pfowd.Send(sc);
                            }
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {sc.ID} 's map is set to non-existing ID!");
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

        public override void LookupData(Codec c)
        {
            this.Request = c.Lookup(this.Request);
            this.RequestData = c.Lookup(this.RequestData);
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
