﻿namespace VTT.Network.Packet
{
    using System.Numerics;
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeMapData : PacketBase
    {
        public DataType Type { get; set; }
        public Guid MapID { get; set; }
        public object Data { get; set; }
        public override uint PacketID => 15;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Map m = null;
            ServerMapPointer smp = null;
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Error, "Client asked map data change without permissions!");
                    PacketMap pm = new PacketMap() { IsServer = true, Map = m, Session = sessionID };
                    pm.Send(this.Sender);
                }
                else
                {
                    server.TryGetMap(this.MapID, out m);
                    server.TryGetMapPointer(this.MapID, out smp);
                }
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
            }

            if (m != null)
            {
                switch (this.Type)
                {
                    case DataType.Name:
                    {
                        m.Name = (string)this.Data;
                        if (smp != null)
                        {
                            smp.MapName = (string)this.Data;
                        }

                        break;
                    }

                    case DataType.Folder:
                    {
                        m.Folder = (string)this.Data;
                        if (smp != null)
                        {
                            smp.MapFolder = (string)this.Data;
                        }

                        break;
                    }

                    case DataType.AmbientColor:
                    {
                        m.AmbientColor = (Color)this.Data;
                        break;
                    }

                    case DataType.GridSize:
                    {
                        m.GridSize = (float)this.Data;
                        break;
                    }

                    case DataType.GridUnits:
                    {
                        m.GridUnit = (float)this.Data;
                        break;
                    }

                    case DataType.GridColor:
                    {
                        m.GridColor = (Color)this.Data;
                        break;
                    }

                    case DataType.GridEnabled:
                    {
                        m.GridEnabled = (bool)this.Data;
                        break;
                    }

                    case DataType.GridDrawn:
                    {
                        m.GridDrawn = (bool)this.Data;
                        break;
                    }

                    case DataType.SkyColor:
                    {
                        m.BackgroundColor = (Color)this.Data;
                        break;
                    }

                    case DataType.SunColor:
                    {
                        m.SunColor = (Color)this.Data;
                        break;
                    }

                    case DataType.SunEnabled:
                    {
                        m.SunEnabled = (bool)this.Data;
                        break;
                    }

                    case DataType.EnableShadows:
                    {
                        m.EnableShadows = (bool)this.Data;
                        break;
                    }

                    case DataType.EnableDirectionalShadows:
                    {
                        m.EnableDirectionalShadows = (bool)this.Data;
                        break;
                    }

                    case DataType.SunPitch:
                    {
                        m.SunPitch = (float)this.Data;
                        break;
                    }

                    case DataType.SunYaw:
                    {
                        m.SunYaw = (float)this.Data;
                        break;
                    }

                    case DataType.SunIntensity:
                    {
                        m.SunIntensity = (float)this.Data;
                        break;
                    }

                    case DataType.AmbietIntensity:
                    {
                        m.AmbietIntensity = (float)this.Data;
                        break;
                    }

                    case DataType.CameraPosition:
                    {
                        m.DefaultCameraPosition = (Vector3)this.Data;
                        break;
                    }

                    case DataType.CameraDirection:
                    {
                        m.DefaultCameraRotation = (Vector3)this.Data;
                        break;
                    }

                    case DataType.DarkvisionEnabled:
                    {
                        m.EnableDarkvision = (bool)this.Data;
                        break;
                    }

                    case DataType.Camera2DHeight:
                    {
                        m.Camera2DHeight = (float)this.Data;
                        if (!isServer)
                        {
                            client.Frontend.Renderer.MapRenderer.Change2DMapHeight(m.Camera2DHeight);
                        }

                        break;
                    }

                    case DataType.Is2D:
                    {
                        m.Is2D = (bool)this.Data;
                        if (!isServer)
                        {
                            client.Frontend.Renderer.MapRenderer.Switch2D(m.Is2D);
                        }

                        break;
                    }

                    case DataType.EnableDrawing:
                    {
                        m.EnableDrawing = (bool)this.Data;
                        break;
                    }

                    case DataType.AmbientSoundID:
                    {
                        m.AmbientSoundID = (Guid)this.Data;
                        break;
                    }

                    case DataType.AmbientVolume:
                    {
                        m.AmbientSoundVolume = (float)this.Data;
                        if (!isServer)
                        {
                            client?.Frontend?.Sound?.NotifyOfVolumeChanges();
                        }

                        break;
                    }
                }

                if (isServer)
                {
                    m.NeedsSave = true;
                    server.Logger.Log(LogLevel.Info, "Changed map data, notifying clients");
                    new PacketChangeMapData() { Data = this.Data, MapID = this.MapID, Type = this.Type }.Broadcast(c => c.ClientMapID.Equals(this.MapID));
                    if (this.Type == DataType.Name)
                    {
                        new PacketMapPointer() { Data = new System.Collections.Generic.List<(Guid, string, string)>() { (this.MapID, m.Folder, (string)this.Data) }, Remove = false }.Broadcast(c => c.IsAdmin);
                    }

                    if (this.Type == DataType.Folder)
                    {
                        new PacketMapPointer() { Data = new System.Collections.Generic.List<(Guid, string, string)>() { (this.MapID, (string)this.Data, m.Name) }, Remove = false }.Broadcast(c => c.IsAdmin);
                    }
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Type = (DataType)br.ReadByte();
            this.MapID = new Guid(br.ReadBytes(16));
            switch (this.Type)
            {
                case DataType.Name:
                case DataType.Folder:
                {
                    this.Data = br.ReadString();
                    break;
                }

                case DataType.GridEnabled:
                case DataType.GridDrawn:
                case DataType.EnableShadows:
                case DataType.EnableDirectionalShadows:
                case DataType.SunEnabled:
                case DataType.DarkvisionEnabled:
                case DataType.Is2D:
                case DataType.EnableDrawing:
                {
                    this.Data = br.ReadBoolean();
                    break;
                }

                case DataType.GridSize:
                case DataType.GridUnits:
                case DataType.SunYaw:
                case DataType.SunPitch:
                case DataType.SunIntensity:
                case DataType.AmbietIntensity:
                case DataType.Camera2DHeight:
                case DataType.AmbientVolume:
                {
                    this.Data = br.ReadSingle();
                    break;
                }

                case DataType.GridColor:
                case DataType.SkyColor:
                case DataType.AmbientColor:
                case DataType.SunColor:
                {
                    uint v = br.ReadUInt32();
                    this.Data = Extensions.FromArgb(v);
                    break;
                }

                case DataType.CameraPosition:
                case DataType.CameraDirection:
                {
                    this.Data = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    break;
                }

                case DataType.AmbientSoundID:
                {
                    this.Data = br.ReadGuid();
                    break;
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write((byte)this.Type);
            bw.Write(this.MapID.ToByteArray());
            switch (this.Type)
            {
                case DataType.Name:
                case DataType.Folder:
                {
                    bw.Write((string)this.Data);
                    break;
                }

                case DataType.GridEnabled:
                case DataType.GridDrawn:
                case DataType.EnableShadows:
                case DataType.EnableDirectionalShadows:
                case DataType.SunEnabled:
                case DataType.DarkvisionEnabled:
                case DataType.Is2D:
                case DataType.EnableDrawing:
                {
                    bw.Write((bool)this.Data);
                    break;
                }

                case DataType.GridSize:
                case DataType.GridUnits:
                case DataType.SunYaw:
                case DataType.SunPitch:
                case DataType.SunIntensity:
                case DataType.AmbietIntensity:
                case DataType.Camera2DHeight:
                case DataType.AmbientVolume:
                {
                    bw.Write((float)this.Data);
                    break;
                }

                case DataType.GridColor:
                case DataType.SkyColor:
                case DataType.AmbientColor:
                case DataType.SunColor:
                {
                    Color c = (Color)this.Data;
                    uint v = c.Argb();
                    bw.Write(v);
                    break;
                }

                case DataType.CameraPosition:
                case DataType.CameraDirection:
                {
                    Vector3 d = (Vector3)this.Data;
                    bw.Write(d.X);
                    bw.Write(d.Y);
                    bw.Write(d.Z);
                    break;
                }

                case DataType.AmbientSoundID:
                {
                    bw.Write((Guid)this.Data);
                    break;
                }
            }
        }

        public enum DataType
        {
            Name,
            Folder,
            GridEnabled,
            GridDrawn,
            GridSize,
            GridUnits,
            GridColor,
            SkyColor,
            AmbientColor,
            SunEnabled,
            SunYaw,
            SunPitch,
            SunIntensity,
            AmbietIntensity,
            EnableShadows,
            EnableDirectionalShadows,
            CameraPosition,
            CameraDirection,
            DarkvisionEnabled,
            Is2D,
            Camera2DHeight,
            SunColor,
            EnableDrawing,
            AmbientSoundID,
            AmbientVolume
        }
    }
}
