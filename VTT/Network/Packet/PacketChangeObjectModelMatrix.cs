namespace VTT.Network.Packet
{
    using System.Numerics;
    using System;
    using System.Collections.Generic;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeObjectModelMatrix : PacketBaseWithCodec
    {
        public override uint PacketID => 17;

        public ChangeType Type { get; set; }
        public Guid MovementInducerID { get; set; }
        public List<(Guid, Guid, Vector4)> MovedObjects { get; set; } = new List<(Guid, Guid, Vector4)>();

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // Server got object pos change packet from client
            {
                server.Logger.Log(LogLevel.Info, "Got object position/scale change request");
                List<(Guid, Guid, Vector4)> broadcastChanges = new List<(Guid, Guid, Vector4)>();
                List<(Guid, Guid, Vector4)> senderChanges = new List<(Guid, Guid, Vector4)>();
                foreach ((Guid, Guid, Vector4) d in this.MovedObjects)
                {
                    if (server.TryGetMap(d.Item1, out Map m))
                    {
                        if (m.GetObject(d.Item2, out MapObject mo))
                        {
                            if (this.Sender.IsAdmin || mo.CanEdit(this.Sender.ID))
                            {
                                broadcastChanges.Add((d.Item1, d.Item2, d.Item3));
                                switch (this.Type)
                                {
                                    case ChangeType.Position:
                                    {
                                        mo.Position = d.Item3.Xyz();
                                        break;
                                    }

                                    case ChangeType.Rotation:
                                    {
                                        mo.Rotation = new Quaternion(d.Item3.Xyz(), d.Item3.W);
                                        break;
                                    }

                                    case ChangeType.Scale:
                                    {
                                        mo.Scale = d.Item3.Xyz();
                                        break;
                                    }
                                }

                                m.NeedsSave = true;
                            }
                            else
                            {
                                server.Logger.Log(LogLevel.Error, "Client requested a position/scale change for object without sufficient permissions to do so!");
                                senderChanges.Add((d.Item1, d.Item2, this.Type == ChangeType.Scale ? new Vector4(mo.Scale, 1.0f) : this.Type == ChangeType.Position ? new Vector4(mo.Position, 1.0f) : new Vector4(mo.Rotation.X, mo.Rotation.Y, mo.Rotation.Z, mo.Rotation.W)));
                                continue;
                            }
                        }
                        else
                        {
                            server.Logger.Log(LogLevel.Error, "Client asked object pos/scale change for a non-existing object!");
                            continue;
                        }
                    }
                    else
                    {
                        server.Logger.Log(LogLevel.Error, "Client asked object pos/scale change for a non-existing map!");
                        continue;
                    }
                }

                server.Logger.Log(LogLevel.Info, "Notifying clients of " + broadcastChanges.Count + " changes and " + senderChanges.Count + " rejects");
                if (senderChanges.Count > 0)
                {
                    PacketChangeObjectModelMatrix pmoS = new PacketChangeObjectModelMatrix() { MovedObjects = senderChanges, IsServer = true, Session = this.Sender.Id, MovementInducerID = this.MovementInducerID, Type = this.Type };
                    pmoS.Send(this.Sender);
                }

                new PacketChangeObjectModelMatrix() { MovedObjects = broadcastChanges, MovementInducerID = this.MovementInducerID, Type = this.Type }.Broadcast();
            }
            else // Client got notified
            {
                client.Logger.Log(LogLevel.Info, "Got object position/scale change notification");
                foreach ((Guid, Guid, Vector4) d in this.MovedObjects)
                {
                    Map m = client.CurrentMap;
                    if (m != null && d.Item1.Equals(m.ID))
                    {
                        if (m.GetObject(d.Item2, out MapObject mo))
                        {
                            if (client.ID.Equals(this.MovementInducerID))
                            {
                                switch (this.Type)
                                {
                                    case ChangeType.Position:
                                    {
                                        mo.Position = d.Item3.Xyz();
                                        break;
                                    }

                                    case ChangeType.Rotation:
                                    {
                                        mo.Rotation = new Quaternion(d.Item3.Xyz(), d.Item3.W);
                                        break;
                                    }

                                    case ChangeType.Scale:
                                    {
                                        mo.Scale = d.Item3.Xyz();
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                switch (this.Type)
                                {
                                    case ChangeType.Position:
                                    {
                                        mo.ClientDragMoveServerInducedPositionChangeProgress = 1;
                                        mo.ClientDragMoveIsPath = false;
                                        mo.ClientDragMoveInitialPosition = mo.Position;
                                        mo.ClientDragMoveServerInducedNewPosition = d.Item3.Xyz();
                                        break;
                                    }

                                    case ChangeType.Rotation:
                                    {
                                        Quaternion q = new Quaternion(d.Item3.Xyz(), d.Item3.W);
                                        mo.ClientDragMoveServerInducedRotationChangeProgress = 1;
                                        mo.ClientDragMoveInitialRotation = mo.Rotation;
                                        mo.ClientDragMoveServerInducedNewRotation = q;
                                        break;
                                    }

                                    case ChangeType.Scale:
                                    {
                                        mo.ClientDragMoveServerInducedScaleChangeProgress = 1;
                                        mo.ClientDragMoveInitialScale = mo.Scale;
                                        mo.ClientDragMoveServerInducedNewScale = d.Item3.Xyz();
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            client.Logger.Log(LogLevel.Warn, "Got object position/scale change for non-existing object, sending an object data request");
                            PacketObjectRequest por = new PacketObjectRequest() { IsServer = false, Session = sessionID, ObjectID = d.Item2 };
                            por.Send();
                        }
                    }
                    else
                    {
                        client.Logger.Log(LogLevel.Info, "Got object position/scale change for other map, discarding");
                    }
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.MovementInducerID = c.Lookup(this.MovementInducerID);
            this.Type = c.Lookup(this.Type);
            this.MovedObjects = c.Lookup(this.MovedObjects, x =>
            {
                Guid i1 = c.Lookup(x.Item1);
                Guid i2 = c.Lookup(x.Item2);
                Vector4 i3 = c.Lookup(x.Item3);
                return x = (i1, i2, i3);
            });
        }

        public enum ChangeType
        {
            Position,
            Rotation,
            Scale
        }
    }
}
