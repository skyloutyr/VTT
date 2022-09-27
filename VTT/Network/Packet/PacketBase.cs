namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading;
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

#if OLD_CODE

        private static ThreadLocal<byte[]> fullMessageBuffer = new ThreadLocal<byte[]>(() => new byte[1024]);
        private static ThreadLocal<int> messageLength = new ThreadLocal<int>(() => 0);
        private static ThreadLocal<int> messageLengthLeft = new ThreadLocal<int>(() => 0);

        public static IEnumerable<PacketBase> Receive(byte[] binary, int offset, int length, bool server)
        {
            /*
            int cumulativeReadThisFrame = 0;
            while (true)
            {
                byte[] buffer = fullMessageBuffer.Value;
                if (messageLength.Value == 0) // Have no message
                {
                    int l = BitConverter.ToInt32(binary, offset + cumulativeReadThisFrame) + 4; // Get message length
                    messageLength.Value = messageLengthLeft.Value = l;
                    while (buffer.Length < l)
                    {
                        buffer = new byte[buffer.Length * 2];
                        fullMessageBuffer.Value = buffer;
                    }
                }

                int r = Math.Min(messageLengthLeft.Value, length);
                Array.Copy(binary, offset + cumulativeReadThisFrame, buffer, messageLength.Value - messageLengthLeft.Value, r);
                fullMessageBuffer.Value = buffer;
                cumulativeReadThisFrame += r;
                messageLengthLeft.Value -= r;
                if (messageLengthLeft.Value > 0) // Didn't read full packet! Wait for the next chunk
                {
                    break;
                }

                messageLength.Value = messageLengthLeft.Value = 0;
                using MemoryStream ms = new MemoryStream(buffer);
                using BinaryReader br = new BinaryReader(ms);
                br.ReadInt32();
                uint id = br.ReadUInt32();
                PacketBase pb = (PacketBase)Activator.CreateInstance(PacketsByID[id]);
                pb.IsServer = server;
                pb.Decode(br);
                if (server)
                {
                    pb.Server = Server.Instance;
                }
                else
                {
                    pb.Client = Client.Instance;
                }

                yield return pb;
                if (cumulativeReadThisFrame == length)
                {
                    break;
                }
            }
            */
        }
#endif
    }
}
