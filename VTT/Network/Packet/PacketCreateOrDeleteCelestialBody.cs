namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;

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
