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

            pbTypes.Sort((l, r) => StringComparer.InvariantCulture.Compare(l.Name, r.Name));
            uint idx = 0;
            foreach (Type t in pbTypes)
            {
                PacketsByID[idx] = t;
                IDByPacketType[t] = idx;
                ++idx;
            }
        }

        public Guid Session { get; set; }
        public bool IsServer { get; set; }

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

    public class PacketNetworkManager
    {
        public bool IsServer { get; set; }

        private int packet_size = 0; // FULL SIZE
        private long packet_read = 0; // READ
        private int packet_header_read = 0;
        private byte[] packet_header; // SIZE HEADER
        private byte[] packet = new byte[0];        // PACKET
        private bool hasPacket;

        public IEnumerable<PacketBase> Receive(byte[] buffer, long offset, long size)
        {
            while (offset < size)
            {
                if (packet_header_read != 4 && !hasPacket)
                {
                    if (packet_header == null)
                    {
                        packet_header = new byte[4];
                    }

                    if (4 > packet_header_read && size > offset && packet_header != null)
                    {
                        long open = 4 - packet_header_read;
                        // Not full header in packet
                        if (offset + open > size)
                        {
                            Buffer.BlockCopy(buffer, (int)offset, packet_header, (int)packet_header_read, (int)(size - offset));
                            packet_header_read += (int)size - (int)offset;
                            offset = size;
                        }
                        // Buffer long enough
                        else
                        {
                            Buffer.BlockCopy(buffer, (int)offset, packet_header, (int)packet_header_read, (int)open);
                            offset += open;
                            packet_size = BitConverter.ToInt32(packet_header, 0);
                            packet_read = 0;
                            hasPacket = true;
                            if (packet_size > packet.Length)
                            {
                                packet = new byte[packet_size];
                            }
                        }
                    }
                }

                if (packet_size > packet_read && size > offset)
                {
                    var packet_open = packet_size - packet_read;

                    if (offset + packet_open > size)
                    {
                        Buffer.BlockCopy(buffer, (int)offset, packet, (int)packet_read, (int)(size - offset));
                        packet_read += size - offset;
                        offset = size;
                    }
                    else
                    {
                        // Copy to the packet
                        Buffer.BlockCopy(buffer, (int)offset, packet, (int)packet_read, (int)packet_open);
                        offset += packet_open;

                        // This method reports how much of the full message has been received. E.g. use it to update an status.

                        // The total message has been received and will be processed within HandlePacket(byte[] data)... 
                        using MemoryStream ms = new MemoryStream(packet);
                        using BinaryReader br = new BinaryReader(ms);
                        uint id = br.ReadUInt32();
                        PacketBase pb = (PacketBase)Activator.CreateInstance(PacketBase.PacketsByID[id]);
                        pb.IsServer = this.IsServer;
                        pb.Decode(br);
                        if (this.IsServer)
                        {
                            pb.Server = Server.Instance;
                        }
                        else
                        {
                            pb.Client = Client.Instance;
                        }

                        yield return pb;

                        hasPacket = false;
                        packet_size = 0;
                        packet_read = 0;
                        packet_header_read = 0;
                    }
                }
            }
        }
    }
}
