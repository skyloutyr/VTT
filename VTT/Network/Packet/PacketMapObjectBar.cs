namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketMapObjectBar : PacketBaseWithCodec
    {
        public override uint PacketID => 44;

        public Action BarAction { get; set; }
        public Guid MapID { get; set; }
        public Guid ContainerID { get; set; }
        public DisplayBar Bar { get; set; }
        public int Index { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Debug, "Got client bar packet of type " + this.BarAction);
                if (server.TryGetMap(this.MapID, out Map m))
                {
                    if (m.GetObject(this.ContainerID, out MapObject mo))
                    {
                        bool canEdit = this.Sender.IsAdmin || mo.CanEdit(this.Sender.ID);
                        if (canEdit)
                        {
                            switch (this.BarAction)
                            {
                                case Action.Add:
                                {
                                    mo.Bars.Add(this.Bar);
                                    break;
                                }

                                case Action.Change:
                                case Action.Delete:
                                {
                                    if (mo.Bars.Count > this.Index)
                                    {
                                        if (this.BarAction == Action.Delete)
                                        {
                                            mo.Bars.RemoveAt(this.Index);
                                        }
                                        else
                                        {
                                            mo.Bars[this.Index] = this.Bar;
                                        }
                                    }
                                    else
                                    {
                                        server.Logger.Log(LogLevel.Error, "Client asked for bar deletion or change at a non-existing bar index!");
                                    }

                                    break;
                                }
                            }

                            m.NeedsSave = true;
                            server.Logger.Log(LogLevel.Info, "Notifying all clients of bar data change");
                            new PacketMapObjectBar() { Bar = this.Bar, BarAction = this.BarAction, ContainerID = this.ContainerID, Index = this.Index, MapID = this.MapID }.Broadcast();
                        }
                        else
                        {
                            server.Logger.Log(LogLevel.Error, "Client asked for bar change without permissions! Resending base object data");
                            PacketMapObject pmo = new PacketMapObject() { IsServer = true, Obj = mo, Session = sessionID };
                            pmo.Send(this.Sender);
                        }
                    }
                    else
                    {
                        server.Logger.Log(LogLevel.Warn, "Client asked for bar change for non-existing object!");
                    }
                }
                else
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for bar change for non-existing map!");
                }
            }
            else
            {
                client.Logger.Log(LogLevel.Debug, "Got server bar notify packet of type " + this.BarAction);
                Map m = client.CurrentMap;
                if (this.MapID.Equals(m.ID))
                {
                    if (m.GetObject(this.ContainerID, out MapObject mo))
                    {
                        switch (this.BarAction)
                        {
                            case Action.Add:
                            {
                                Client.Instance.DoTask(() => mo.Bars.Add(this.Bar));
                                break;
                            }

                            case Action.Change:
                            case Action.Delete:
                            {
                                if (mo.Bars.Count > this.Index)
                                {
                                    if (this.BarAction == Action.Delete)
                                    {
                                        Client.Instance.DoTask(() => mo.Bars.RemoveAt(this.Index));
                                    }
                                    else
                                    {
                                        Client.Instance.DoTask(() => mo.Bars[this.Index] = this.Bar);
                                    }
                                }
                                else
                                {
                                    client.Logger.Log(LogLevel.Error, "Server asked for bar deletion or change at a non-existing bar index, discarding!");
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        client.Logger.Log(LogLevel.Warn, "Server asked for bar change for non-existing object, asking for object");
                        PacketObjectRequest por = new PacketObjectRequest() { IsServer = false, ObjectID = this.ContainerID, Session = sessionID };
                        por.Send();
                    }
                }
                else
                {
                    client.Logger.Log(LogLevel.Warn, "Server asked for a bar change on a different map, discarding.");
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.BarAction = c.Lookup(this.BarAction);
            this.Index = c.Lookup(this.Index);
            this.MapID = c.Lookup(this.MapID);
            this.ContainerID = c.Lookup(this.ContainerID);
            if (this.BarAction != Action.Delete)
            {
                c.Lookup(this.Bar ??= new DisplayBar());
            }
        }

        public enum Action
        {
            Add,
            Delete,
            Change
        }
    }
}
