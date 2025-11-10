namespace VTT.Network.Packet
{
    using System;
    using System.IO;

    public class PacketServerSettingChange : PacketBaseWithCodec
    {
        public override uint PacketID => 89;

        public object Data { get; set; }
        public SettingType ChangeKind { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                switch (this.ChangeKind)
                {
                    case SettingType.ServerAllowsEmbeddedImages:
                    {
                        client.ServerAllowsEmbeddedImages = (bool)this.Data;
                        break;
                    }
                }
            }
            else
            {
                if (!this.Sender.IsAdmin)
                {
                    this.ContextLogger.Log(Util.LogLevel.Warn, $"Client {this.Sender.ID} asked for server settings change without permissions!");
                    return;
                }

                bool needsSave = false;
                switch (this.ChangeKind)
                {
                    case SettingType.ServerAllowsEmbeddedImages:
                    {
                        if (needsSave = (server.Settings.AllowEmbeddedImages != (bool)this.Data))
                        {
                            server.Settings.AllowEmbeddedImages = (bool)this.Data;
                        }

                        break;
                    }
                }

                if (needsSave)
                {
                    server.Settings.Save();
                    this.Broadcast();
                }
            }
        }

        public override void LookupData(Codec c) => throw new NotImplementedException();

        public enum SettingType
        {
            ServerAllowsEmbeddedImages
        }
    }
}
