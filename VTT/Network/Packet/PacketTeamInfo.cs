﻿namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketTeamInfo : PacketBaseWithCodec
    {
        public override uint PacketID => 57;

        public ActionType Action { get; set; }
        public string Name { get; set; }
        public int Index { get; set; }
        public Color Color { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = isServer ? server.Logger : client.Logger;
            l.Log(LogLevel.Debug, "Got team info packet");
            Map m = isServer ? server.GetExistingMap(this.Sender.ClientMapID) : client.CurrentMap;
            bool canChange = !isServer || this.Sender.IsAdmin;
            if (!canChange)
            {
                l.Log(LogLevel.Warn, "Client asked for team data change without permissions!");
                return;
            }

            if (string.IsNullOrEmpty(this.Name))
            {
                l.Log(LogLevel.Warn, "Not allowed to change the default team!");
            }

            lock (m.TurnTracker.Lock)
            {
                switch (this.Action)
                {
                    case ActionType.Add:
                    {
                        TurnTracker.Team t = new TurnTracker.Team() { Name = this.Name, Color = this.Color };
                        if (m.TurnTracker.Teams.Find(p => p.Name.Equals(this.Name)) != null)
                        {
                            l.Log(LogLevel.Error, "Asked for a team addition with an already existing name! Team names must be unique!");
                            return;
                        }

                        m.TurnTracker.Teams.Add(t);
                        break;
                    }

                    case ActionType.Delete:
                    {
                        if (!m.TurnTracker.RemoveTeam(this.Name))
                        {
                            l.Log(LogLevel.Warn, "Team could not be deleted");
                        }

                        break;
                    }

                    case ActionType.UpdateColor:
                    {
                        TurnTracker.Team t = m.TurnTracker.Teams.Find(p => p.Name.Equals(this.Name));
                        if (t == null)
                        {
                            l.Log(LogLevel.Warn, "Got change request for a non-existing team, ignoring.");
                            return;
                        }

                        t.Color = this.Color;
                        break;
                    }

                    case ActionType.UpdateName:
                    {
                        if (this.Index >= 0 && this.Index < m.TurnTracker.Teams.Count)
                        {
                            TurnTracker.Team t = m.TurnTracker.Teams[this.Index];
                            TurnTracker.Team t1 = m.TurnTracker.Teams.Find(p => p.Name.Equals(this.Name));
                            if (t1 == null || !isServer)
                            {
                                t.Name = this.Name;
                            }
                            else
                            {
                                l.Log(LogLevel.Warn, "A team with that name already exists! Team names must be unique!");
                            }
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, "Got change request for a non-existing team, ignoring.");
                        }

                        break;
                    }
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.Action = c.Lookup(this.Action);
            this.Name = c.Lookup(this.Name);
            if (this.Action is ActionType.Add or ActionType.UpdateColor)
            {
                this.Color = c.Lookup(this.Color);
            }

            if (this.Action == ActionType.UpdateName)
            {
                this.Index = c.Lookup(this.Index);
            }
        }

        public enum ActionType
        {
            Add,
            Delete,
            UpdateName,
            UpdateColor
        }
    }
}
