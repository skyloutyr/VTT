namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeMapSkyboxColors : PacketBaseWithCodec
    {
        public override uint PacketID => 84;

        public Guid MapID { get; set; }
        public bool IsNightGradientColors { get; set; }
        public ActionType Action { get; set; }
        public MapSkyboxColors.ColorsPointerType ColorsType { get; set; }
        public float GradientPointKey { get; set; }
        public Vector3 GradientPointColor { get; set; }
        public float GradientPointDesination { get; set; }
        public Guid AssetID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    this.ContextLogger.Log(LogLevel.Warn, $"Client {this.Sender.ID} tried to change the map skybox colors without permissions!");
                    return;
                }
            }

            Map m = isServer ? server.GetExistingMap(this.MapID) : client.CurrentMapIfMatches(this.MapID);
            if (m == null)
            {
                this.ContextLogger.Log(LogLevel.Warn, $"Can't change map skybox colors for non-existing map {this.MapID}");
                return;
            }

            MapSkyboxColors colors = this.IsNightGradientColors ? m.NightSkyboxColors : m.DaySkyboxColors;
            switch (this.Action)
            {
                case ActionType.SwitchKind:
                {
                    colors.SwitchType(this.ColorsType);
                    break;
                }

                case ActionType.ChangeGradientPointColor:
                case ActionType.AddGradientPoint:
                {
                    colors.ColorGradient.Remove(this.GradientPointKey);
                    colors.ColorGradient.Add(this.GradientPointKey, this.GradientPointColor);
                    break;
                }

                case ActionType.RemoveGradientPoint:
                {
                    colors.ColorGradient.Remove(this.GradientPointKey);
                    break;
                }

                case ActionType.MoveGradientPoint:
                {
                    colors.ColorGradient.Remove(this.GradientPointKey);
                    colors.ColorGradient.Add(this.GradientPointDesination, this.GradientPointColor);
                    break;
                }

                case ActionType.SetSolidColor:
                {
                    colors.SolidColor = this.GradientPointColor;
                    break;
                }

                case ActionType.SetImageAssetID:
                {
                    colors.GradientAssetID = this.AssetID;
                    break;
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(m.ID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.IsNightGradientColors = c.Lookup(this.IsNightGradientColors);
            this.Action = c.Lookup(this.Action);
            switch (this.Action)
            {
                case ActionType.SwitchKind:
                {
                    this.ColorsType = c.Lookup(this.ColorsType);
                    break;
                }

                case ActionType.ChangeGradientPointColor:
                case ActionType.AddGradientPoint:
                {
                    this.GradientPointKey = c.Lookup(this.GradientPointKey);
                    this.GradientPointColor = c.Lookup(this.GradientPointColor);
                    break;
                }

                case ActionType.RemoveGradientPoint:
                {
                    this.GradientPointKey = c.Lookup(this.GradientPointKey);
                    break;
                }

                case ActionType.MoveGradientPoint:
                {
                    this.GradientPointKey = c.Lookup(this.GradientPointKey);
                    this.GradientPointDesination = c.Lookup(this.GradientPointDesination);
                    this.GradientPointColor = c.Lookup(this.GradientPointColor);
                    break;
                }

                case ActionType.SetSolidColor:
                {
                    this.GradientPointColor = c.Lookup(this.GradientPointColor);
                    break;
                }

                case ActionType.SetImageAssetID:
                {
                    this.AssetID = c.Lookup(this.AssetID);
                    break;
                }
            }
        }

        public enum ActionType
        {
            SwitchKind,
            AddGradientPoint,
            RemoveGradientPoint,
            MoveGradientPoint,
            ChangeGradientPointColor,
            SetSolidColor,
            SetImageAssetID
        }
    }
}
