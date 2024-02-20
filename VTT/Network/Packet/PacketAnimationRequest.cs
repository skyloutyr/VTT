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
            if (isServer)
            {
                if (!server.TryGetMap(this.MapID, out Map m))
                {
                    l.Log(LogLevel.Warn, "Client asked to change animation data on a non-existing map!");
                    return;
                }

                if (!m.GetObject(this.ObjectID, out MapObject mo))
                {
                    l.Log(LogLevel.Warn, "Client asked to change animation data for a non-existing object!");
                    return;
                }

                if (!mo.CanEdit(this.Sender.ID))
                {
                    l.Log(LogLevel.Warn, "Client asked to change animation data for an object without permissions!");
                    return;
                }

                switch (this.Action)
                {
                    case ActionType.SetLooping:
                    {
                        mo.AnimationContainer.Looping = (bool)this.Data;
                        break;
                    }

                    case ActionType.Pause:
                    {
                        mo.AnimationContainer.Paused = true;
                        mo.AnimationContainer.TimeRaw = mo.AnimationContainer.TimeSwitchTo = (float)this.Data;
                        break;
                    }

                    case ActionType.Resume:
                    {
                        mo.AnimationContainer.Paused = false;
                        mo.AnimationContainer.TimeRaw = mo.AnimationContainer.TimeSwitchTo = (float)this.Data;
                        break;
                    }

                    case ActionType.SetNextAnimation:
                    {
                        mo.AnimationContainer.AnimationSwitchTo = (string)this.Data;
                        mo.AnimationContainer.TimeSwitchTo = 0;
                        break;
                    }

                    case ActionType.SwitchToAnimationNow:
                    {
                        mo.AnimationContainer.AnimationSwitchTo = (string)this.Data;
                        mo.AnimationContainer.TimeSwitchTo = 0;
                        break;
                    }
                }

                // TODO animation request undo/redo memory!
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
            else
            {
                if (!Guid.Equals(client.CurrentMap?.ID, this.MapID))
                {
                    l.Log(LogLevel.Warn, "Server asked for animation change for a non-existing map.");
                    return;
                }

                if (!client.CurrentMap.GetObject(this.ObjectID, out MapObject mo))
                {
                    l.Log(LogLevel.Warn, "Server asked for animation change for a non-existing object!");
                    return;
                }

                switch (this.Action)
                {
                    case ActionType.SetLooping:
                    {
                        mo.AnimationContainer.Looping = (bool)this.Data;
                        break;
                    }

                    case ActionType.Pause:
                    {
                        mo.AnimationContainer.Paused = true;
                        mo.AnimationContainer.TimeRaw = (float)this.Data;
                        break;
                    }

                    case ActionType.Resume:
                    {
                        mo.AnimationContainer.Paused = false;
                        mo.AnimationContainer.TimeRaw = (float)this.Data;
                        break;
                    }

                    case ActionType.SetNextAnimation:
                    {
                        mo.AnimationContainer.AnimationSwitchTo = (string)this.Data;
                        mo.AnimationContainer.TimeSwitchTo = 0;
                        break;
                    }

                    case ActionType.SwitchToAnimationNow:
                    {
                        if (mo.LastRenderModel != null)
                        {
                            mo.AnimationContainer.SwitchNow(mo.LastRenderModel, (string)this.Data);
                        }
                        else
                        {
                            mo.AnimationContainer.AnimationSwitchTo = (string)this.Data;
                            mo.AnimationContainer.TimeSwitchTo = 0;
                        }

                        break;
                    }
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.ObjectID = br.ReadGuid();
            this.Action = br.ReadEnumSmall<ActionType>();
            switch (this.Action)
            {
                case ActionType.SetLooping:
                {
                    this.Data = br.ReadBoolean();
                    break;
                }

                case ActionType.Pause:
                case ActionType.Resume:
                {
                    this.Data = br.ReadSingle();
                    break;
                }

                case ActionType.SetNextAnimation:
                case ActionType.SwitchToAnimationNow:
                {
                    this.Data = br.ReadString();
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
                case ActionType.SetLooping:
                {
                    bw.Write((bool)this.Data);
                    break;
                }

                case ActionType.Pause:
                case ActionType.Resume:
                {
                    bw.Write((float)this.Data);
                    break;
                }

                case ActionType.SetNextAnimation:
                case ActionType.SwitchToAnimationNow:
                {
                    bw.Write((string)this.Data);
                    break;
                }
            }
        }

        public enum ActionType
        {
            SetLooping,
            Pause,
            Resume,
            SetNextAnimation,
            SwitchToAnimationNow
        }
    }
}
