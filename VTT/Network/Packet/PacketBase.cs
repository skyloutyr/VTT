﻿namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using VTT.Util;
    using ZstdSharp;

    public abstract class PacketBase
    {
        public static Dictionary<ushort, Type> PacketsByID { get; } = new Dictionary<ushort, Type>();
        public static Dictionary<Type, ushort> IDByPacketType { get; } = new Dictionary<Type, ushort>();

        static PacketBase()
        {
            List<Type> pbTypes = new List<Type>();

            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (t.IsAssignableTo(typeof(PacketBase)) && !t.IsAbstract)
                {
                    pbTypes.Add(t);
                }
            }

            ushort maxPId = 0;
            foreach (Type t in pbTypes)
            {
                PacketBase pb = (PacketBase)Activator.CreateInstance(t);
                ushort idx = (ushort)pb.PacketID;
                if (PacketsByID.ContainsKey(idx))
                {
                    throw new Exception($"Packet with ID {idx} is already registered by {PacketsByID[idx].Name} while attempting to register {t.Name}!");
                }

                PacketsByID[idx] = t;
                IDByPacketType[t] = idx;
                if (idx > maxPId)
                {
                    maxPId = idx;
                }
            }
        }

        public Guid Session { get; set; }
        public bool IsServer { get; set; }
        public abstract uint PacketID { get; }
        public virtual bool Compressed => false;

        public Server Server { get; set; }
        public Client Client { get; set; }
        public ServerClient Sender { get; set; }

        public abstract void Encode(BinaryWriter bw);
        public abstract void Decode(BinaryReader br);
        public abstract void Act(Guid sessionID, Server server, Client client, bool isServer);

        public void Send(NetClient client = null)
        {
            client ??= Client.Instance.NetClient;
            if (client == null)
            {
                return;    
            }

            this.Session = client.Id;
            this.IsServer = false;
            client.SendAsync(this.EncodeDataToBuffer());
        }

        public void Broadcast(Predicate<ServerClient> predicate = null)
        {
            predicate ??= s => true;
            foreach (ServerClient sc in Server.Instance.ClientsByID.Values)
            {
                if (predicate(sc))
                {
                    this.Send(sc);
                }
            }
        }

        public void Send(ServerClient sc)
        {
            this.Session = sc.Id;
            this.IsServer = true;
            sc.SendAsync(this.EncodeDataToBuffer());
        }

        private byte[] EncodeDataToBuffer()
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(PacketNetworkManager.HeaderMagic);
            bw.Write((int)0); // Irrelevant cast is for clarity
            bw.Write(IDByPacketType[this.GetType()]);
            bw.Write((byte)(this.Compressed ? 1 : 0));
            if (this.Compressed)
            {
                using CompressionStream ds = new CompressionStream(ms, 3);
                using BinaryWriter bw2 = new BinaryWriter(ds);
                this.Encode(bw2);
            }
            else
            {
                this.Encode(bw);
            }

            byte[] arr = ms.ToArray();
            Array.Copy(BitConverter.GetBytes(arr.Length - 8), 0, arr, 4, 4);
            return arr;
        }

        public Logger ContextLogger => this.IsServer ? Server.Instance.Logger : Client.Instance.Logger;
    }
}
