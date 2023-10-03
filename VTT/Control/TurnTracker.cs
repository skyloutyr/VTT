namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using VTT.Network;
    using VTT.Util;

    public class TurnTracker : ISerializable
    {
        public object Lock = new object();
        public int EntryIndex { get; set; } = 0;
        public List<Entry> Entries { get; set; } = new List<Entry>();
        public List<Team> Teams { get; set; } = new List<Team>();

        public string EntryName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public Color CurrentColor { get; set; } = Color.White;

        public bool Visible { get; set; } = false;

        public Map Container { get; set; }

        public TurnTracker(Map container)
        {
            this.Container = container;
            this.Teams.Add(new Team() { Name = string.Empty, Color = Color.White });
        }

        public void Sort() => this.Entries.Sort((l, r) => r.NumericValue.CompareTo(l.NumericValue));
        public void Add(Entry e, int idx)
        {
            if (idx <= this.EntryIndex)
            {
                this.EntryIndex += 1;
            }

            if (idx >= this.Entries.Count)
            {
                this.Entries.Add(e);
            }
            else
            {
                this.Entries.Insert(idx, e);
            }
        }

        public void Remove(int idx)
        {
            if (idx < this.EntryIndex)
            {
                this.EntryIndex -= 1;
            }
            else
            {
                if (this.EntryIndex == idx)
                {
                    this.MoveBack();
                }
            }

            this.Entries.RemoveAt(idx);
        }

        public bool RemoveTeam(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                Team t = this.Teams.Find(p => p.Name.Equals(name));
                if (t != null)
                {
                    this.Teams.Remove(t);
                    foreach (Entry e in this.Entries)
                    {
                        if (e.Team == t)
                        {
                            e.Team = this.Teams[0];
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public Entry GetAt(int idx)
        {
            if (idx < 0)
            {
                idx = this.Entries.Count - (Math.Abs(idx) % this.Entries.Count);
            }

            return this.Entries[idx % this.Entries.Count];
        }

        public Entry GetContextAwareEntry(int idx, Guid refID)
        {
            Entry e = this.GetAt(idx);
            if (e.ObjectID.Equals(refID))
            {
                return e;
            }

            e = this.Entries.Find(p => p.ObjectID.Equals(refID));
            return e;
        }

        public void MoveNext() => this.MoveTo(this.EntryIndex + 1);

        public void MoveBack() => this.MoveTo(this.EntryIndex - 1);

        public void MoveTo(int idx)
        {
            if (this.Entries.Count == 0)
            {
                return;
            }

            if (idx < 0)
            {
                idx = this.Entries.Count - (Math.Abs(idx) % this.Entries.Count);
            }

            idx %= this.Entries.Count;
            this.EntryIndex = idx;
            if (!this.Container.IsServer && Client.Instance != null && Client.Instance.Settings.EnableSoundTurnTracker && !Client.Instance.IsAdmin) // Client side only
            {
                Entry e = this.GetAt(idx);
                if (this.Container.GetObject(e.ObjectID, out MapObject mo))
                {
                    if (mo.OwnerID.Equals(Client.Instance.ID))
                    {
                        Client.Instance.Frontend.Sound.PlaySound(Client.Instance.Frontend.Sound.YourTurn, Sound.SoundCategory.UI);
                    }
                }
            }
        }

        public bool GetEntryInfo(Entry e, out Color teamColor, out string teamName, out string entryName)
        {
            if (!this.Container.GetObject(e.ObjectID, out MapObject mo))
            {
                entryName = Client.Instance.Lang.Translate("turntracker.unknown");
                teamName = Client.Instance.Lang.Translate("turntracker.unknown");
                teamColor = Color.Gray;
                return false;
            }
            else
            {
                entryName = mo.Name;
                if (mo.MapLayer > 0)
                {
                    entryName = Client.Instance.Lang.Translate("turntracker.hidden");
                    if (Client.Instance.IsAdmin)
                    {
                        entryName += " (" + mo.Name + "), L " + mo.MapLayer;
                    }

                    if (mo.MapLayer == 1)
                    {
                        teamName = e.Team.Name;
                        teamColor = e.Team.Color;
                    }
                    else
                    {
                        teamName = Client.Instance.Lang.Translate("turntracker.hidden");
                        if (Client.Instance.IsAdmin)
                        {
                            teamName += " (" + e.Team.Name + ")";
                        }

                        teamColor = Color.Gray;
                    }
                }
                else
                {
                    if (!mo.IsNameVisible && !Client.Instance.IsAdmin && !mo.CanEdit(Client.Instance.ID))
                    {
                        entryName = Client.Instance.Lang.Translate("turntracker.hidden");
                    }

                    teamName = e.Team.Name;
                    teamColor = e.Team.Color;
                }

                return true;
            }
        }

        public void Pulse()
        {
            if (this.Entries.Count <= 0)
            {
                return;
            }

            Entry e = this.GetAt(this.EntryIndex);
            if (!this.Container.GetObject(e.ObjectID, out MapObject mo))
            {
                this.EntryName = Client.Instance.Lang.Translate("turntracker.unknown");
            }
            else
            {
                this.EntryName = mo.Name;
                if (mo.MapLayer > 0)
                {
                    this.EntryName = Client.Instance.Lang.Translate("turntracker.hidden");
                    if (Client.Instance.IsAdmin)
                    {
                        this.EntryName += " (" + mo.Name + "), L " + mo.MapLayer;
                    }
                }
                else
                {
                    if (!mo.IsNameVisible && !Client.Instance.IsAdmin && !mo.CanEdit(Client.Instance.ID))
                    {
                        this.EntryName = Client.Instance.Lang.Translate("turntracker.hidden");
                    }
                }

                this.TeamName = e.Team.Name;
                this.CurrentColor = e.Team.Color;
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.Set("Index", this.EntryIndex);
            ret.Set("Visible", this.Visible);
            ret.SetArray("Entries", this.Entries.ToArray(), (n, c, v) =>
            {
                DataElement d = v.Serialize();
                c.Set(n, d);
            });

            ret.SetArray("Teams", this.Teams.ToArray(), (n, c, v) =>
            {
                DataElement d = v.Serialize();
                c.Set(n, d);
            });

            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.Teams.Clear();
            this.Entries.Clear();
            this.EntryIndex = e.Get<int>("Index");
            this.Visible = e.Get<bool>("Visible");
            this.Teams.AddRange(e.GetArray("Teams", (n, c) =>
            {
                DataElement d = c.Get<DataElement>(n);
                Team t = new Team();
                t.Deserialize(d);
                return t;
            }, Array.Empty<Team>()));

            this.Entries.AddRange(e.GetArray("Entries", (n, c) =>
            {
                DataElement d = c.Get<DataElement>(n);
                Entry e = new Entry();
                e.Deserialize(d);
                e.Team = this.Teams.Find(p => p.Name.Equals(e.readTeamName)) ?? this.Teams[0];
                e.readTeamName = null;
                return e;
            }, Array.Empty<Entry>()));

            if (this.Teams.Count == 0)
            {
                this.Teams.Add(new Team() { Name = string.Empty, Color = Color.White });
            }
        }

        public class Entry : IComparable<Entry>, IEquatable<Entry>, ISerializable
        {
            public Guid ObjectID { get; set; }
            public float NumericValue { get; set; }
            public Team Team { get; set; }

            internal string readTeamName;

            public int CompareTo(Entry other) => this.NumericValue.CompareTo(other.NumericValue);
            public bool Equals(Entry other) => other != null && other.ObjectID.Equals(this.ObjectID) && other.NumericValue.Equals(this.NumericValue);

            public void Deserialize(DataElement e)
            {
                this.ObjectID = e.GetGuid("ID");
                this.NumericValue = e.Get<float>("Value");
                this.readTeamName = e.Get<string>("Team");
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetGuid("ID", this.ObjectID);
                ret.Set("Value", this.NumericValue);
                ret.Set("Team", this.Team.Name);
                return ret;
            }

            public override bool Equals(object obj) => ReferenceEquals(this, obj) || (obj is null ? false : throw new NotImplementedException());

            public override int GetHashCode() => HashCode.Combine(this.ObjectID.GetHashCode(), this.NumericValue.GetHashCode(), this.Team.GetHashCode());

            public static bool operator ==(Entry left, Entry right) => left is null ? right is null : left.Equals(right);

            public static bool operator !=(Entry left, Entry right) => !(left == right);

            public static bool operator <(Entry left, Entry right) => left is null ? right is not null : left.CompareTo(right) < 0;

            public static bool operator <=(Entry left, Entry right) => left is null || left.CompareTo(right) <= 0;

            public static bool operator >(Entry left, Entry right) => left is not null && left.CompareTo(right) > 0;

            public static bool operator >=(Entry left, Entry right) => left is null ? right is null : left.CompareTo(right) >= 0;
        }

        public class Team : ISerializable
        {
            public string Name { get; set; }
            public Color Color { get; set; }

            public void Deserialize(DataElement e)
            {
                this.Name = e.Get<string>("Name");
                this.Color = e.GetColor("Color");
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.Set("Name", this.Name);
                ret.SetColor("Color", this.Color);
                return ret;
            }
        }
    }
}
