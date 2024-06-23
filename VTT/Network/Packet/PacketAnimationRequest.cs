namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketAnimationRequest : PacketBase
    {
        public override uint PacketID => 67;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public ActionType Action { get; set; }
        public object Data { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
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
                if (!this.MapID.Equals(client.CurrentMap.ID))
                {
                    l.Log(LogLevel.Warn, "Server asked for animation change on a different map, ignoring!");
                    return;
                }

                m = client.CurrentMap;
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

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.ObjectID = br.ReadGuid();
            this.Action = br.ReadEnumSmall<ActionType>();
            switch (this.Action)
            {
                case ActionType.TogglePause:
                {
                    this.Data = br.ReadBoolean();
                    break;
                }

                case ActionType.SetDefaultAnimation:
                case ActionType.SwitchToAnimationNow:
                {
                    this.Data = br.ReadString();
                    break;
                }

                case ActionType.SetPlayRate:
                {
                    this.Data = br.ReadSingle();
                    break;
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.ObjectID);
            bw.WriteEnumSmall(this.Action);
            switch (this.Action)
            {
                case ActionType.TogglePause:
                {
                    bw.Write((bool)this.Data);
                    break;
                }

                case ActionType.SetDefaultAnimation:
                case ActionType.SwitchToAnimationNow:
                {
                    bw.Write((string)this.Data);
                    break;
                }

                case ActionType.SetPlayRate:
                {
                    bw.Write((float)this.Data);
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
