namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class PacketMapObjectGenericData : PacketBaseWithCodec
    {
        public override uint PacketID => 45;

        public DataType ChangeType { get; set; }
        public List<(Guid, Guid, object)> Data { get; set; } = new List<(Guid, Guid, object)>();

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            List<(Guid, Guid, object)> changes = new List<(Guid, Guid, object)>();
            this.ContextLogger.Log(LogLevel.Debug, $"Got object data change packet for {this.Data.Count} objects, of type {this.ChangeType}");
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

                        case DataType.NameColor:
                        {
                            d.Item2.NameColor = (Color)d.Item3;
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

                        case DataType.DoNotDraw:
                        {
                            d.Item2.DoNotRender = (bool)d.Item3;
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

                        case DataType.DescriptionIsMarkdown:
                        {
                            d.Item2.UseMarkdownForDescription = (bool)d.Item3;
                            break;
                        }

                        case DataType.IsShadow2DViewport:
                        {
                            d.Item2.IsShadow2DViewpoint = (bool)d.Item3;
                            break;
                        }

                        case DataType.Shadow2DViewportData:
                        {
                            d.Item2.Shadow2DViewpointData = (Vector2)d.Item3;
                            break;
                        }

                        case DataType.HideSelection:
                        {
                            d.Item2.HideFromSelection = (bool)d.Item3;
                            break;
                        }

                        case DataType.IsShadow2DLightSource:
                        {
                            d.Item2.IsShadow2DLightSource = (bool)d.Item3;
                            break;
                        }

                        case DataType.Shadow2DLightSourceData:
                        {
                            d.Item2.Shadow2DLightSourceData = (Vector2)d.Item3;
                            break;
                        }

                        case DataType.DisableNameplateBackground:
                        {
                            d.Item2.DisableNameplateBackground = (bool)d.Item3;
                            break;
                        }

                        case DataType.IsPortal:
                        {
                            d.Item2.IsPortal = (bool)d.Item3;
                            break;
                        }

                        case DataType.LinkedPortalID:
                        {
                            d.Item2.PairedPortalID = (Guid)d.Item3;
                            break;
                        }

                        case DataType.LinkedPortalMapID:
                        {
                            d.Item2.PairedPortalMapID = (Guid)d.Item3;
                            break;
                        }

                        case DataType.PortalSize:
                        {
                            d.Item2.PortalSize = (Vector3)d.Item3;
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

        public override void LookupData(Codec c)
        {
            this.ChangeType = c.Lookup(this.ChangeType);
            this.Data = c.Lookup(this.Data, x => 
            {
                Guid i1 = c.Lookup(x.Item1);
                Guid i2 = c.Lookup(x.Item2);
                object i3;
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
                    case DataType.DoNotDraw:
                    case DataType.DescriptionIsMarkdown:
                    case DataType.HideSelection:
                    case DataType.IsShadow2DViewport:
                    case DataType.IsShadow2DLightSource:
                    case DataType.DisableNameplateBackground:
                    case DataType.IsPortal:
                    {
                        i3 = c.LookupBox<bool>(x.Item3, c.Lookup);
                        break;
                    }

                    case DataType.Name:
                    case DataType.Description:
                    case DataType.Notes:
                    {
                        i3 = c.LookupBox<string>(x.Item3, c.Lookup);
                        break;
                    }

                    case DataType.MapLayer:
                    {
                        i3 = c.LookupBox<int>(x.Item3, c.Lookup);
                        break;
                    }

                    case DataType.Owner:
                    case DataType.CustomNameplateID:
                    case DataType.ShaderID:
                    case DataType.LinkedPortalID:
                    case DataType.LinkedPortalMapID:
                    {
                        i3 = c.LookupBox<Guid>(x.Item3, c.Lookup);
                        break;
                    }

                    case DataType.TintColor:
                    case DataType.NameColor:
                    {
                        i3 = c.LookupBox<Color>(x.Item3, c.Lookup);
                        break;
                    }

                    case DataType.Properties:
                    {
                        i3 = c.Lookup(x.Item3 as DataElement);
                        break;
                    }

                    case DataType.Shadow2DViewportData:
                    case DataType.Shadow2DLightSourceData:
                    {
                        i3 = c.LookupBox<Vector2>(x.Item3, c.Lookup);
                        break;
                    }

                    case DataType.PortalSize:
                    {
                        i3 = c.LookupBox<Vector3>(x.Item3, c.Lookup);
                        break;
                    }

                    default:
                    {
                        throw new NotSupportedException($"The specified DataType {this.ChangeType} doesn't have a conversion defined!");
                    }
                }

                return x = (i1, i2, i3);
            });
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
            NameColor,
            IsCrossedOut,
            IsInfo,
            DoNotDraw,
            HasCustomNameplate,
            CustomNameplateID,
            Properties,
            ShaderID,
            Notes,
            DescriptionIsMarkdown,
            HideSelection,
            IsShadow2DViewport,
            Shadow2DViewportData,
            IsShadow2DLightSource,
            Shadow2DLightSourceData,
            DisableNameplateBackground,
            IsPortal,
            PortalSize,
            LinkedPortalID,
            LinkedPortalMapID
        }
    }
}
