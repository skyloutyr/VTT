﻿namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;
    using VTT.Control;

    public class PacketCameraSnap : PacketBaseWithCodec
    {
        public override uint PacketID => 12;

        public Vector3 CameraPosition { get; set; }
        public Vector3 CameraDirection { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            this.ContextLogger.Log(Util.LogLevel.Debug, "Got camera snap request");
            if (isServer)
            {
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID));
            }
            else
            {
                Map m = client.CurrentMap;
                if (m != null)
                {
                    if (m.Is2D)
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position = new Vector3(this.CameraPosition.X, this.CameraPosition.Y, m.Camera2DHeight);
                        Client.Instance.Frontend.Renderer.MapRenderer.ChangeFOVOrZoom(this.CameraPosition.Z);
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction = this.CameraDirection;
                    }
                    else
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position = this.CameraPosition;
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction = this.CameraDirection;
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                    }

                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.CameraPosition = c.Lookup(this.CameraPosition);
            this.CameraDirection = c.Lookup(this.CameraDirection);
        }
    }
}
