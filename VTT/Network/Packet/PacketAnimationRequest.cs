namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketAnimationRequest : PacketBaseWithCodec
    {
        public override uint PacketID => 67;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public ActionType Action { get; set; }
        public object Data { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            Map m;
            MapObject mo;
            if (isServer)
            {
                if (!server.TryGetMap(this.MapID, out m))
                {
                    l.Log(LogLevel.Warn, "Client asked to change animation data on a non-existing map!");
                    return;
                }

                if (!m.GetObject(this.ObjectID, out mo))
                {
                    l.Log(LogLevel.Warn, "Client asked to change animation data for a non-existing object!");
                    return;
                }

                if (!mo.CanEdit(this.Sender.ID) && !this.Sender.IsAdmin)
                {
                    l.Log(LogLevel.Warn, "Client asked to change animation data for an object without permissions!");
                    return;
                }
            }
            else
            {
                m = client.CurrentMap;
                if (!m?.ID.Equals(this.MapID) ?? false)
                {
                    l.Log(LogLevel.Warn, "Server asked for animation change on a different map, ignoring!");
                    return;
                }

                if (!m.GetObject(this.ObjectID, out mo))
                {
                    l.Log(LogLevel.Warn, "Server asked to change animation data for a non-existing object!");
                    return;
                }
            }

            switch (this.Action)
            {
                case ActionType.TogglePause:
                {
                    mo.AnimationContainer.Paused = (bool)this.Data;
                    break;
                }

                case ActionType.SetDefaultAnimation:
                {
                    mo.AnimationContainer.LoopingAnimationName = (string)this.Data;
                    goto case ActionType.SwitchToAnimationNow;
                }

                case ActionType.SwitchToAnimationNow:
                {
                    if (!isServer)
                    {
                        mo.AnimationContainer.SwitchNow(mo.LastRenderModel, (string)this.Data);
                    }

                    break;
                }

                case ActionType.SetPlayRate:
                {
                    mo.AnimationContainer.AnimationPlayRate = (float)this.Data;
                    break;
                }
            }

            // TODO animation request undo/redo memory!
            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectID = c.Lookup(this.ObjectID);
            this.Action = c.Lookup(this.Action);
            switch (this.Action)
            {
                case ActionType.TogglePause:
                {
                    this.Data = c.LookupBox<bool>(this.Data, c.Lookup);
                    break;
                }

                case ActionType.SetDefaultAnimation:
                case ActionType.SwitchToAnimationNow:
                {
                    this.Data = c.LookupBox<string>(this.Data, c.Lookup);
                    break;
                }

                case ActionType.SetPlayRate:
                {
                    this.Data = c.LookupBox<float>(this.Data, c.Lookup);
                    break;
                }
            }
        }

        public enum ActionType
        {
            TogglePause,
            SetDefaultAnimation,
            SwitchToAnimationNow,
            SetPlayRate
        }
    }
}
