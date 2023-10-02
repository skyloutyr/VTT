namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketMapObjectGenericData : PacketBase
    {
        public DataType ChangeType { get; set; }
        public List<(Guid, Guid, object)> Data { get; set; } = new List<(Guid, Guid, object)>();
        public override uint PacketID => 45;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            List<(Guid, Guid, object)> changes = new List<(Guid, Guid, object)>();
            foreach ((Map, MapObject, object) d in this.ListObjects())
            {
                bool hasAccess = true;
                if (isServer)
                {
                    hasAccess = this.Sender.IsAdmin || d.Item2.CanEdit(this.Sender.ID);
                    if (this.ChangeType is DataType.MapLayer or DataType.Owner)
                    {
                        hasAccess = this.Sender.IsAdmin;
                    }
                }

                if (hasAccess)
                {
                    switch (this.ChangeType)
                    {
                        case DataType.Description:
                        {
                            d.Item2.Description = (string)d.Item3;
                            break;
                        }

                        case DataType.Notes:
                        {
                            d.Item2.Notes = (string)d.Item3;
                            break;
                        }

                        case DataType.IsNameVisible:
                        {
                            d.Item2.IsNameVisible = (bool)d.Item3;
                            break;
                        }

                        case DataType.LightsEnabled:
                        {
                            d.Item2.LightsEnabled = (bool)d.Item3;
                            break;
                        }

                        case DataType.LightsCastShadows:
                        {
                            d.Item2.LightsCastShadows = (bool)d.Item3;
                            break;
                        }

                        case DataType.MapLayer:
                        {
                            d.Item2.MapLayer = (int)d.Item3;
                            break;
                        }

                        case DataType.Name:
                        {
                            d.Item2.Name = (string)d.Item3;
                            break;
                        }

                        case DataType.Owner:
                        {
                            d.Item2.OwnerID = (Guid)d.Item3;
                            break;
                        }

                        case DataType.Properties:
                        {
                            d.Item2.CustomProperties = (DataElement)d.Item3;
                            break;
                        }

                        case DataType.SelfCastsShadow:
                        {
                            d.Item2.LightsSelfCastsShadow = (bool)d.Item3;
                            break;
                        }

                        case DataType.TintColor:
                        {
                            d.Item2.TintColor = (Color)d.Item3;
                            break;
                        }

                        case DataType.IsCrossedOut:
                        {
                            d.Item2.IsCrossedOut = (bool)d.Item3;
                            break;
                        }

                        case DataType.HasCustomNameplate:
                        {
                            d.Item2.HasCustomNameplate = (bool)d.Item3;
                            break;
                        }

                        case DataType.CastsShadow:
                        {
                            d.Item2.CastsShadow = (bool)d.Item3;
                            break;
                        }

                        case DataType.IsInfo:
                        {
                            d.Item2.IsInfoObject = (bool)d.Item3;
                            break;
                        }

                        case DataType.CustomNameplateID:
                        {
                            d.Item2.CustomNameplateID = (Guid)d.Item3;
                            break;
                        }

                        case DataType.ShaderID:
                        {
                            d.Item2.ShaderID = (Guid)d.Item3;
                            break;
                        }
                    }

                    d.Item1.NeedsSave = true;
                    changes.Add((d.Item1.ID, d.Item2.ID, d.Item3));
                }
                else // HasAccess can only be false on server-side
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for object data change without permissions");
                    PacketMapObject pmo = new PacketMapObject() { IsServer = true, Obj = d.Item2, Session = sessionID };
                    pmo.Send(this.Sender);
                }
            }

            if (isServer && changes.Count > 0)
            {
                server.Logger.Log(LogLevel.Info, "Notifying clients of generic changes");
                new PacketMapObjectGenericData() { ChangeType = this.ChangeType, Data = changes }.Broadcast();
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ChangeType = (DataType)br.ReadByte();
            int count = br.ReadInt32();
            while (count-- > 0)
            {
                Guid mID = new Guid(br.ReadBytes(16));
                Guid oID = new Guid(br.ReadBytes(16));
                object o;
                switch (this.ChangeType)
                {
                    case DataType.IsNameVisible:
                    case DataType.LightsEnabled:
                    case DataType.LightsCastShadows:
                    case DataType.SelfCastsShadow:
                    case DataType.IsCrossedOut:
                    case DataType.HasCustomNameplate:
                    case DataType.CastsShadow:
                    case DataType.IsInfo:
                    {
                        o = br.ReadBoolean();
                        break;
                    }

                    case DataType.Name:
                    case DataType.Description:
                    case DataType.Notes:
                    {
                        o = br.ReadString();
                        break;
                    }

                    case DataType.MapLayer:
                    {
                        o = br.ReadInt32();
                        break;
                    }

                    case DataType.Owner:
                    case DataType.CustomNameplateID:
                    case DataType.ShaderID:
                    {
                        o = new Guid(br.ReadBytes(16));
                        break;
                    }

                    case DataType.TintColor:
                    {
                        o = Extensions.FromArgb(br.ReadUInt32());
                        break;
                    }

                    default:
                    {
                        DataElement de = new DataElement();
                        de.Read(br);
                        o = de;
                        break;
                    }
                }

                this.Data.Add((mID, oID, o));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write((byte)this.ChangeType);
            bw.Write(this.Data.Count);
            foreach ((Guid, Guid, object) d in this.Data)
            {
                bw.Write(d.Item1.ToByteArray());
                bw.Write(d.Item2.ToByteArray());
                switch (this.ChangeType)
                {
                    case DataType.IsNameVisible:
                    case DataType.LightsEnabled:
                    case DataType.LightsCastShadows:
                    case DataType.SelfCastsShadow:
                    case DataType.IsCrossedOut:
                    case DataType.HasCustomNameplate:
                    case DataType.CastsShadow:
                    case DataType.IsInfo:
                    {
                        bw.Write((bool)d.Item3);
                        break;
                    }

                    case DataType.Name:
                    case DataType.Description:
                    case DataType.Notes:
                    {
                        bw.Write((string)d.Item3);
                        break;
                    }

                    case DataType.MapLayer:
                    {
                        bw.Write((int)d.Item3);
                        break;
                    }

                    case DataType.Owner:
                    case DataType.CustomNameplateID:
                    case DataType.ShaderID:
                    {
                        bw.Write(((Guid)d.Item3).ToByteArray());
                        break;
                    }

                    case DataType.TintColor:
                    {
                        bw.Write(((Color)d.Item3).Argb());
                        break;
                    }

                    case DataType.Properties:
                    {
                        DataElement de = (DataElement)d.Item3;
                        de.Write(bw);
                        break;
                    }
                }
            }
        }

        public IEnumerable<(Map, MapObject, object)> ListObjects()
        {
            Logger l = this.IsServer ? this.Server.Logger : this.Client.Logger;
            foreach ((Guid, Guid, object) d in this.Data)
            {
                Map m;
                if (this.IsServer)
                {
                    if (!this.Server.TryGetMap(d.Item1, out m))
                    {
                        l.Log(LogLevel.Warn, "Non-existing map specified!");
                        continue;
                    }
                }
                else
                {
                    m = this.Client.CurrentMapIfMatches(d.Item1);
                }

                if (m == null)
                {
                    continue;
                }

                if (m.GetObject(d.Item2, out MapObject mo))
                {
                }
                else
                {
                    l.Log(LogLevel.Warn, "Non-existing object specified!");
                    if (!this.IsServer)
                    {
                        PacketObjectRequest por = new PacketObjectRequest() { IsServer = false, ObjectID = d.Item2 };
                        por.Send();
                    }

                    continue;
                }

                yield return (m, mo, d.Item3);
            }

            yield break;
        }

        public enum DataType
        {
            IsNameVisible,
            Name,
            Description,
            MapLayer,
            Owner,
            LightsEnabled,
            LightsCastShadows,
            SelfCastsShadow,
            CastsShadow,
            TintColor,
            IsCrossedOut,
            IsInfo,
            HasCustomNameplate,
            CustomNameplateID,
            Properties,
            ShaderID,
            Notes
        }
    }
}
