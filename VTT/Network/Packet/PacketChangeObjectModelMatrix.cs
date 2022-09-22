namespace VTT.Network.Packet
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeObjectModelMatrix : PacketBase
    {
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
                                        mo.Position = d.Item3.Xyz;
                                        break;
                                    }

                                    case ChangeType.Rotation:
                                    {
                                        mo.Rotation = new Quaternion(d.Item3.Xyz, d.Item3.W);
                                        break;
                                    }

                                    case ChangeType.Scale:
                                    {
                                        mo.Scale = d.Item3.Xyz;
                                        break;
                                    }
                                }

                                m.NeedsSave = true;
                            }
                            else
                            {
                                server.Logger.Log(LogLevel.Error, "Client requested a position/scale change for object without sufficient permissions to do so!");
                                senderChanges.Add((d.Item1, d.Item2, this.Type == ChangeType.Scale ? new Vector4(mo.Scale, 1.0f) : this.Type == ChangeType.Position ? new Vector4(mo.Position, 1.0f) : new Vector4(mo.Rotation.Xyz, mo.Rotation.W)));
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
                                        mo.Position = d.Item3.Xyz;
                                        break;
                                    }

                                    case ChangeType.Rotation:
                                    {
                                        mo.Rotation = new Quaternion(d.Item3.Xyz, d.Item3.W);
                                        break;
                                    }

                                    case ChangeType.Scale:
                                    {
                                        mo.Scale = d.Item3.Xyz;
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
                                        mo.ClientDragMoveInitialPosition = mo.Position;
                                        mo.ClientDragMoveServerInducedNewPosition = d.Item3.Xyz;
                                        break;
                                    }

                                    case ChangeType.Rotation:
                                    {
                                        Quaternion q = new Quaternion(d.Item3.Xyz, d.Item3.W);
                                        mo.ClientDragMoveServerInducedRotationChangeProgress = 1;
                                        mo.ClientDragMoveInitialRotation = mo.Rotation;
                                        mo.ClientDragMoveServerInducedNewRotation = q;
                                        break;
                                    }

                                    case ChangeType.Scale:
                                    {
                                        mo.ClientDragMoveServerInducedScaleChangeProgress = 1;
                                        mo.ClientDragMoveInitialScale = mo.Scale;
                                        mo.ClientDragMoveServerInducedNewScale = d.Item3.Xyz;
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

        public override void Decode(BinaryReader br)
        {
            this.MovementInducerID = new Guid(br.ReadBytes(16));
            this.Type = (ChangeType)br.ReadByte();
            int amt = br.ReadInt32();
            while (amt-- > 0)
            {
                Guid mId = new Guid(br.ReadBytes(16));
                Guid oId = new Guid(br.ReadBytes(16));
                Vector4 p = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                this.MovedObjects.Add((mId, oId, p));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MovementInducerID.ToByteArray());
            bw.Write((byte)this.Type);
            bw.Write(MovedObjects.Count);
            foreach ((Guid, Guid, Vector4) d in this.MovedObjects)
            {
                bw.Write(d.Item1.ToByteArray());
                bw.Write(d.Item2.ToByteArray());
                bw.Write(d.Item3.X);
                bw.Write(d.Item3.Y);
                bw.Write(d.Item3.Z);
                bw.Write(d.Item3.W);
            }
        }

        public enum ChangeType
        { 
            Position,
            Rotation,
            Scale
        }
    }
}
