namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
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
                                bool allowNormalProc = true;
                                switch (this.Type)
                                {
                                    case ChangeType.Position:
                                    {
                                        // Set the position first for the portal code
                                        mo.Position = d.Item3.Xyz();

                                        // Handle portals
                                        if (HandlePortals(server, m, mo, out bool needsMapChange, out Vector3 newPosition, out Map newMap))
                                        {
                                            if (needsMapChange)
                                            {
                                                m.RemoveObject(mo);
                                                new PacketDeleteMapObject() { DeletedObjects = new List<(Guid, Guid)>() { (m.ID, mo.ID) } }.Broadcast(); // Remove object and notify
                                                m.NeedsSave = true;

                                                mo.Position = newPosition;
                                                newMap.AddObject(mo);
                                                new PacketMapObject() { Obj = mo }.Broadcast(); // Add to new map and notify
                                                newMap.NeedsSave = true;
                                                // In this case we don't add this object to broadcastChanges or rejects, as it is gone from the map entirely.
                                            }
                                            else
                                            {
                                                mo.Position = newPosition;
                                                broadcastChanges.Add((d.Item1, d.Item2, new Vector4(newPosition, d.Item3.W)));
                                                m.NeedsSave = true;
                                            }

                                            allowNormalProc = false;
                                        }

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

                                if (allowNormalProc)
                                {
                                    broadcastChanges.Add((d.Item1, d.Item2, d.Item3));
                                    m.NeedsSave = true;
                                }
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

        public static bool HandlePortals(Server server, Map m, MapObject mo, out bool needsMapChange, out Vector3 position, out Map newMap)
        {
            foreach (MapObject o2 in m.IterateObjects(null))
            {
                if (o2 != mo && o2.IsPortal && (mo.MapLayer > 0 || o2.MapLayer <= 0) && !o2.PairedPortalID.IsEmpty()) // If our object is on the GM layer, then it can use any portals, otherwise it can only use portals on a non-gm layer
                {
                    bool isInBounds = false;
                    // The server isn't aware of the object's model bounds, that is client only, hance the portal's scale feature
                    if (m.Is2D)
                    {
                        RectangleF rect = new RectangleF(o2.Position.X + (o2.PortalSize.X * o2.Scale.X * -0.5f), o2.Position.Y + (o2.PortalSize.Y * o2.Scale.Y * -0.5f), o2.PortalSize.X * o2.Scale.X, o2.PortalSize.Y * o2.Scale.Y);
                        isInBounds = rect.Contains(mo.Position.X, mo.Position.Y);
                    }
                    else
                    {
                        AABox box = new AABox(o2.PortalSize * -0.5f, o2.PortalSize * 0.5f).Scale(o2.Scale).Offset(o2.Position);
                        isInBounds = box.Contains(mo.Position);
                    }

                    if (isInBounds) // Again, as the server isn't aware of the actual bounds, simply test for position
                    {
                        // Unfortunately have to potentially load a map here which is bad (slow)
                        // There is currently no way to set an exit portal on a different map through the UI, but it is supported by the engine regardless
                        if (server.TryGetMap(o2.PairedPortalMapID, out Map m2) && m2.GetObject(o2.PairedPortalID, out MapObject pportal) && pportal.IsPortal)
                        {
                            // Figure out the scale discrepancy and the offset - try to position the object relative to the portal
                            Vector3 portalScaleDiff = pportal.Scale * pportal.PortalSize / (o2.Scale * o2.PortalSize);
                            Vector3 correctedDeltaToCenter = (mo.Position - o2.Position) * portalScaleDiff;
                            if (m2.Is2D || m.Is2D)
                            {
                                correctedDeltaToCenter *= new Vector3(1, 1, 0);
                                correctedDeltaToCenter.Z = mo.Position.Z - o2.Position.Z;
                            }

                            needsMapChange = m != m2;
                            position = pportal.Position + correctedDeltaToCenter;
                            newMap = m2;
                            return true;
                        }
                    }
                }
            }

            needsMapChange = false;
            position = mo.Position;
            newMap = m;
            return false;
        }

        public enum ChangeType
        {
            Position,
            Rotation,
            Scale
        }
    }
}
