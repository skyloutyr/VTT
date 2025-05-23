﻿namespace VTT.Network.Packet
{
    using System;

    public class PacketDeleteMap : PacketBaseWithCodec
    {
        public override uint PacketID => 32;

        public Guid MapID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Info, "Got map deletion request");
                if (this.Sender.IsAdmin)
                {
                    if (server.Settings.DefaultMapID.Equals(this.MapID))
                    {
                        server.Logger.Log(Util.LogLevel.Error, "Can't delete a default map!");
                        return;
                    }

                    foreach (ServerClient sc in server.ClientsByID.Values)
                    {
                        if (sc.ClientMapID.Equals(this.MapID))
                        {
                            sc.ClientMapID = server.Settings.DefaultMapID;
                            PacketChangeMap pcm = new PacketChangeMap() { Clients = new Guid[1] { sc.ID }, NewMapID = sc.ClientMapID };
                            pcm.Send(sc);
                        }

                        if (sc.IsAdmin)
                        {
                            PacketMapPointer pmp = new PacketMapPointer() { Data = new System.Collections.Generic.List<(Guid, string, string)>() { (this.MapID, string.Empty, string.Empty) }, Remove = true };
                            pmp.Send(sc);
                        }
                    }

                    server.Logger.Log(Util.LogLevel.Info, "Map deleted");
                    server.RemoveMap(this.MapID);
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "A client asked for map deletion without permissions!");
                }
            }
        }

        public override void LookupData(Codec c) => this.MapID = c.Lookup(this.MapID);
    }
}
