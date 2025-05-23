﻿namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using VTT.Control;
    using VTT.Network.UndoRedo;
    using VTT.Util;

    public class PacketAura : PacketBaseWithCodec
    {
        public override uint PacketID => 11;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public int Index { get; set; } = -1;
        public Color AuraColor { get; set; }
        public float AuraRange { get; set; }
        public Action ActionType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got aura packet");
            bool allowed = true;
            Map m = null;
            MapObject mo = null;
            if (isServer)
            {
                if (server.TryGetMap(this.MapID, out m))
                {
                    if (m.GetObject(this.ObjectID, out mo))
                    {
                        allowed = this.Sender.IsAdmin || mo.CanEdit(this.Sender.ID);
                    }
                }
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
                m?.GetObject(this.ObjectID, out mo);
            }

            if (m == null)
            {
                l.Log(LogLevel.Warn, "Got aura change packet for non existing map, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Got aura change packet for non existing object, discarding.");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for aura change without permissions!");
                return;
            }

            switch (this.ActionType)
            {
                case Action.Add:
                {
                    lock (mo.Lock)
                    {
                        if (this.Index == -1)
                        {
                            mo.Auras.Add((this.AuraRange, this.AuraColor));
                        }
                        else
                        {
                            mo.Auras.Insert(this.Index, (this.AuraRange, this.AuraColor));
                        }
                    }

                    if (isServer)
                    {
                        this.Sender.ActionMemory.NewAction(new AuraAddOrDeleteAction() { IsAddition = true, AuraColor = this.AuraColor, AuraContainer = mo, AuraIndex = mo.Auras.Count - 1, AuraRange = this.AuraRange });
                    }

                    break;
                }

                case Action.Delete:
                {
                    (float, Color) data = mo.Auras[this.Index];
                    lock (mo.Lock)
                    {
                        mo.Auras.RemoveAt(this.Index);
                    }

                    if (isServer)
                    {
                        this.Sender.ActionMemory.NewAction(new AuraAddOrDeleteAction() { IsAddition = false, AuraColor = data.Item2, AuraContainer = mo, AuraIndex = this.Index, AuraRange = data.Item1 });
                    }

                    break;
                }

                case Action.Update:
                {
                    (float, Color) data = mo.Auras[this.Index];
                    mo.Auras[this.Index] = (this.AuraRange, this.AuraColor);
                    if (isServer)
                    {
                        this.Sender.ActionMemory.NewAction(new AuraChangeAction() { AuraContainer = mo, AuraIndex = this.Index, InitialAuraColor = data.Item2, InitialAuraRange = data.Item1, LastModifyTime = DateTime.Now, NewAuraColor = this.AuraColor, NewAuraRange = this.AuraRange });
                    }

                    break;
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectID = c.Lookup(this.ObjectID);
            this.ActionType = c.Lookup(this.ActionType);
            if (this.ActionType != Action.Add)
            {
                this.Index = c.Lookup(this.Index);
            }

            if (this.ActionType != Action.Delete)
            {
                this.AuraColor = c.Lookup(this.AuraColor);
                this.AuraRange = c.Lookup(this.AuraRange);
            }
        }

        public enum Action
        {
            Add,
            Delete,
            Update
        }
    }
}
