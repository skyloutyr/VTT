namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using VTT.Util;

    public abstract class PacketBase
    {
        public static Dictionary<uint, Type> PacketsByID { get; } = new Dictionary<uint, Type>();
        public static Dictionary<Type, uint> IDByPacketType { get; } = new Dictionary<Type, uint>();

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

            foreach (Type t in pbTypes)
            {
                PacketBase pb = (PacketBase)Activator.CreateInstance(t);
                uint idx = pb.PacketID;
                if (PacketsByID.ContainsKey(idx))
                {
                    throw new Exception($"Packet with ID {idx} is already registered by {PacketsByID[idx].Name} while attempting to register {t.Name}!");
                }   
                
                PacketsByID[idx] = t;
                IDByPacketType[t] = idx;
            }
        }

        public Guid Session { get; set; }
        public bool IsServer { get; set; }
        public abstract uint PacketID { get; }

        public Server Server { get; set; }
        public Client Client { get; set; }
        public ServerClient Sender { get; set; }

        public abstract void Encode(BinaryWriter bw);
        public abstract void Decode(BinaryReader br);
        public abstract void Act(Guid sessionID, Server server, Client client, bool isServer);

        public void Send(NetClient client = null)
        {
            client ??= Client.Instance.NetClient;
            this.Session = client.Id;
            this.IsServer = false;
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((int)0);
            bw.Write(IDByPacketType[this.GetType()]);
            this.Encode(bw);
            byte[] arr = ms.ToArray();
            Array.Copy(BitConverter.GetBytes(arr.Length - 4), 0, arr, 0, 4);
            client.Send(arr);
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
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            this.Session = sc.Id;
            this.IsServer = true;
            bw.Write((int)0);
            bw.Write(IDByPacketType[this.GetType()]);
            this.Encode(bw);
            byte[] arr = ms.ToArray();
            Array.Copy(BitConverter.GetBytes(arr.Length - 4), 0, arr, 0, 4);
            sc.Send(arr);
        }

        public Logger GetContextLogger() => this.IsServer ? Server.Instance.Logger : Client.Instance.Logger;
    }
}
