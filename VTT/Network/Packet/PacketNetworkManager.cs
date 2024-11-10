namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;

    public class PacketNetworkManager
    {
        private const int HeaderSize = 8;
        public const int HeaderMagic = 0x56545450;

        public bool IsServer { get; set; }
        public bool IsInvalidProtocol { get; set; }

        private int packet_size = 0; // FULL SIZE
        private long packet_read = 0; // READ
        private int packet_header_read = 0;
        private byte[] packet_header; // SIZE HEADER
        private byte[] packet = Array.Empty<byte>();        // PACKET
        private bool hasPacket;

        public IEnumerable<PacketBase> Receive(byte[] buffer, long offset, long size)
        {
            if (this.IsInvalidProtocol)
            {
                yield break;
            }

            while (offset < size)
            {
                if (packet_header_read != HeaderSize && !hasPacket)
                {
                    if (packet_header == null)
                    {
                        packet_header = new byte[HeaderSize];
                    }

                    if (HeaderSize > packet_header_read && size > offset && packet_header != null)
                    {
                        long open = HeaderSize - packet_header_read;
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
                            packet_size = BitConverter.ToInt32(packet_header, 4);
                            int packetMagic = BitConverter.ToInt32(packet_header, 0);
                            if (packetMagic != HeaderMagic)
                            {
                                this.IsInvalidProtocol = true;
                                yield break;
                            }

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
                        ushort id = br.ReadUInt16();
                        PacketBase pb = (PacketBase)Activator.CreateInstance(PacketBase.PacketsByID[id]);
                        byte compressedFlag = br.ReadByte();
                        pb.IsServer = this.IsServer;
                        if ((compressedFlag & 1) == 1)
                        {
                            ms.Position = 3; // 3 is packet ID (2 bytes) + compressed flag (1 byte)
                            using DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress);
                            using BinaryReader br2 = new BinaryReader(ds);
                            pb.Decode(br2);
                        }
                        else
                        {
                            pb.Decode(br);
                        }

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
