﻿namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using VTT.Util;

    public class PacketChangeClientPermissions : PacketBaseWithCodec
    {
        public override uint PacketID => 66;

        public Guid ChangeeID { get; set; } // Not a typo!
        public PermissionType ChangeType { get; set; }
        public bool ChangeValue { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} ({this.Sender.Name}) asked for a permission change without admin permissions!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.IllegalOperation }.Send(this.Sender);
                    this.Sender.Disconnect();
                    return;
                }

                if (!server.ClientInfos.TryGetValue(this.ChangeeID, out ClientInfo ci))
                {
                    l.Log(LogLevel.Error, $"Client {this.Sender.ID} asked for a permission change for a non-existing client!");
                    return;
                }

                if (this.ChangeeID.Equals(this.Sender.ID) && (this.ChangeType is PermissionType.IsAdmin or PermissionType.IsObserver or PermissionType.IsBanned))
                {
                    l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked for a special permission change for themselves, not allowed for administrators.");
                    return;
                }

                if (!this.ChangeeID.Equals(this.Sender.ID) && ci.IsAdmin && (this.ChangeType is PermissionType.IsAdmin or PermissionType.IsObserver or PermissionType.IsBanned))
                {
                    l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked for a special permission change for another administrator, permission level ambiguous, please manualy resolve permissions in the server folder on the server machine.");
                    l.Log(LogLevel.Warn, $"Offender was {this.Sender.ID}, changee was {this.ChangeeID}, permission request was {this.ChangeType} and value was {this.ChangeValue}.");
                    return;
                }

                switch (this.ChangeType)
                {
                    case PermissionType.IsAdmin:
                    {
                        l.Log(LogLevel.Error, "Administrator privilege change not allowed through the UI, please use the server's data folder on the server machine.");
                        return;
                    }

                    case PermissionType.IsObserver:
                    {
                        ci.IsObserver = this.ChangeValue;
                        break;
                    }

                    case PermissionType.CanDraw:
                    {
                        ci.CanDraw = this.ChangeValue;
                        break;
                    }

                    case PermissionType.IsBanned:
                    {
                        ci.IsBanned = this.ChangeValue;
                        l.Log(LogLevel.Warn, $"{this.ChangeeID} was banned!");
                        if (ci.IsLoggedOn)
                        {
                            if (server.ClientsByID.TryGetValue(ci.ID, out ServerClient sc))
                            {
                                new PacketDisconnectReason() { DCR = DisconnectReason.Banned }.Send(sc);
                                sc.Disconnect();
                            }
                        }

                        break;
                    }
                }

                l.Log(LogLevel.Info, $"{this.Sender.ID} changed {this.ChangeType} permission of {this.ChangeeID} to {this.ChangeValue}!");
                List<ClientInfo> infos = new List<ClientInfo>
                {
                    ci
                };

                new PacketClientData() { InfosToUpdate = infos }.Broadcast();
            }
        }

        public override void LookupData(Codec c)
        {
            this.ChangeeID = c.Lookup(this.ChangeeID);
            this.ChangeType = c.Lookup(this.ChangeType);
            this.ChangeValue = c.Lookup(this.ChangeValue);
        }

        public enum PermissionType
        {
            CanDraw,
            IsAdmin,
            IsObserver,
            IsBanned
        }
    }
}
