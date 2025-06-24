namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Control;

    public class PacketObjectPathMovement : PacketBaseWithCodec
    {
        public override uint PacketID => 85;

        public Guid MapID { get; set; }
        public List<Guid> ObjectIDs { get; set; }
        public List<Vector3> Path { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (this.Path == null || this.Path.Count == 0)
            {
                return; // Noop if no path provided
            }

            if (this.ObjectIDs == null || this.ObjectIDs.Count == 0)
            {
                return; // Noop if no objects are changed
            }

            Map m = isServer ? server.GetExistingMap(this.MapID) : client.CurrentMapIfMatches(this.MapID);
            if (m == null)
            {
                this.ContextLogger.Log(isServer ? Util.LogLevel.Error : Util.LogLevel.Info, "Got path object movement packet for non-existing map, aborting!");
                return;
            }

            List<Guid> broadcastedChangesForServer = new List<Guid>();
            foreach (Guid oId in this.ObjectIDs)
            {
                if (m.GetObject(oId, out MapObject mo))
                {
                    if (isServer)
                    {
                        if (mo.CanEdit(this.Sender.ID) || this.Sender.IsAdmin)
                        {
                            mo.Position += this.Path[^1] - this.Path[0]; // If there is only 1 point the path is invalid, so the object won't move

                            // Handle portals
                            if (PacketChangeObjectModelMatrix.HandlePortals(server, m, mo, out bool needsMapChange, out Vector3 newPosition, out Map newMap))
                            {
                                if (needsMapChange) // If map was changed we ditch this packet's systems entirely
                                {
                                    m.RemoveObject(mo);
                                    new PacketDeleteMapObject() { DeletedObjects = new List<(Guid, Guid)>() { (m.ID, mo.ID) } }.Broadcast(); // Remove object and notify
                                    m.NeedsSave = true;

                                    mo.Position = newPosition;
                                    newMap.AddObject(mo);
                                    new PacketMapObject() { Obj = mo }.Broadcast(); // Add to new map and notify
                                    newMap.NeedsSave = true;
                                }
                                else // If it wasn't, we delegate to PacketChangeObjectModelMatrix for inducer, can't really path move either way
                                {
                                    mo.Position = newPosition;
                                    new PacketChangeObjectModelMatrix() { Type = PacketChangeObjectModelMatrix.ChangeType.Position, MovementInducerID = this.Sender.ID, MovedObjects = new List<(Guid, Guid, Vector4)>() { (m.ID, mo.ID, new Vector4(newPosition, 1.0f)) } }.Broadcast();
                                    m.NeedsSave = true;
                                }
                            }
                            else // If portals return false just broadcast the change
                            {
                                broadcastedChangesForServer.Add(oId);
                            }
                        }
                        else // Uh-oh
                        {
                            this.ContextLogger.Log(Util.LogLevel.Error, $"Client {this.Sender.ID} tried to change the path position of object {oId} without permission!");
                        }
                    }
                    else
                    {
                        mo.ClientSetPathMovementChanges(this.Path);
                    }
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.ObjectIDs = broadcastedChangesForServer;
                this.Broadcast(x => Guid.Equals(this.MapID, x.ClientMapID) && x != this.Sender); // Sender already changed their object's position locally
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectIDs = c.Lookup(this.ObjectIDs, c.Lookup);
            this.Path = c.Lookup(this.Path, c.Lookup);
        }
    }
}
