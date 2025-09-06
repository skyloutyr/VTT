namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;
    using VTT.Control;

    public class PacketCelestialBodyInfo : PacketBaseWithCodec
    {
        public override uint PacketID => 86;

        public Guid MapID { get; set; }
        public Guid BodyID { get; set; }
        public DataType ChangeKind { get; set; }
        public object Data { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer && !this.Sender.IsAdmin)
            {
                this.ContextLogger.Log(Util.LogLevel.Error, "Client asked for celestial body change without permissions!");
                return;
            }

            Map m = isServer ? server.GetExistingMap(this.MapID) : client.CurrentMapIfMatches(this.MapID);
            if (m == null)
            {
                this.ContextLogger.Log(Util.LogLevel.Warn, "Got celestial body change request for non-existing map!");
                return;
            }

            if (!m.CelestialBodies.TryGetBody(this.BodyID, out CelestialBody body))
            {
                this.ContextLogger.Log(Util.LogLevel.Warn, "Got celestial body change request for non-existing body!");
                return;
            }

            switch (this.ChangeKind)
            {
                case DataType.SunShadowPolicy:
                {
                    body.ShadowPolicy = (CelestialBody.ShadowCastingPolicy)this.Data;
                    break;
                }

                case DataType.PositionPolicy:
                {
                    body.PositionKind = (CelestialBody.PositionPolicy)this.Data;
                    break;
                }

                case DataType.Position:
                {
                    body.Position = (Vector3)this.Data;
                    break;
                }

                case DataType.Rotation:
                {
                    body.Rotation = (Vector3)this.Data;
                    break;
                }

                case DataType.Scale:
                {
                    body.Scale = (Vector3)this.Data;
                    break;
                }

                case DataType.Enabled:
                {
                    body.Enabled = (bool)this.Data;
                    break;
                }

                case DataType.Billboard:
                {
                    body.Billboard = (bool)this.Data;
                    break;
                }

                case DataType.UseOwnTime:
                {
                    body.UseOwnTime = (bool)this.Data;
                    break;
                }

                case DataType.RenderPolicy:
                {
                    body.RenderKind = (CelestialBody.RenderPolicy)this.Data;
                    break;
                }

                case DataType.AssetRef:
                {
                    body.AssetRef = (Guid)this.Data;
                    break;
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast();
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.BodyID = c.Lookup(this.BodyID);
            this.ChangeKind = c.Lookup(this.ChangeKind);
            switch (this.ChangeKind)
            {
                case DataType.SunShadowPolicy:
                {
                    this.Data = c.LookupBox<CelestialBody.ShadowCastingPolicy>(this.Data, c.Lookup);
                    break;
                }

                case DataType.PositionPolicy:
                {
                    this.Data = c.LookupBox<CelestialBody.PositionPolicy>(this.Data, c.Lookup);
                    break;
                }

                case DataType.Position:
                case DataType.Rotation:
                case DataType.Scale:
                {
                    this.Data = c.LookupBox<Vector3>(this.Data, c.Lookup);
                    break;
                }

                case DataType.Enabled:
                case DataType.Billboard:
                case DataType.UseOwnTime:
                {
                    this.Data = c.LookupBox<bool>(this.Data, c.Lookup);
                    break;
                }

                case DataType.RenderPolicy:
                {
                    this.Data = c.LookupBox<CelestialBody.RenderPolicy>(this.Data, c.Lookup);
                    break;
                }

                case DataType.AssetRef:
                {
                    this.Data = c.LookupBox<Guid>(this.Data, c.Lookup);
                    break;
                }
            }
        }

        public enum DataType
        {
            SunShadowPolicy,
            PositionPolicy,
            Position,
            Rotation,
            Scale,
            Enabled,
            RenderPolicy,
            Billboard,
            UseOwnTime,
            AssetRef
        }
    }

    public class PacketCreateOrDeleteCelestialBody : PacketBaseWithCodec
    {
        public override uint PacketID => 87;

        public Guid MapID { get; set; }
        public bool IsDeletion { get; set; }
        public Guid BodyIDForDeletion { get; set; }
        public CelestialBody BodyForAddition { get; set; } = new CelestialBody();

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer && !this.Sender.IsAdmin)
            {
                this.ContextLogger.Log(Util.LogLevel.Error, "Client asked for celestial body addition/removal without permissions!");
                return;
            }

            Map m = isServer ? server.GetExistingMap(this.MapID) : client.CurrentMapIfMatches(this.MapID);
            if (m == null)
            {
                this.ContextLogger.Log(Util.LogLevel.Warn, "Got celestial body addition/removal request for non-existing map!");
                return;
            }

            if (this.IsDeletion)
            {
                m.CelestialBodies.RemoveBody(this.BodyIDForDeletion);
            }
            else
            {
                m.CelestialBodies.AddBody(this.BodyForAddition);
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast();
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            if (this.IsDeletion = c.Lookup(this.IsDeletion))
            {
                this.BodyIDForDeletion = c.Lookup(this.BodyIDForDeletion);
            }
            else
            {
                c.Lookup(this.BodyForAddition);
            }
        }
    }
}
