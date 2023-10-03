namespace VTT.Sound
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using VTT.Network;

    public class WaveAudio
    {
        public int NumChannels { get; set; }
        public int SampleRate { get; set; }
        public int ByteRate { get; set; } // SampleRate * NumChannels * BitsPerSample / 8
        public int BlockAlign { get; set; } // NumChannels * BitsPerSample / 8
        public int BitsPerSample { get; set; }
        public bool IsReady { get; set; }

        private unsafe ushort* _hdata;

        public unsafe IntPtr DataPtr => (IntPtr)this._hdata;
        public int DataLength { get; private set; }

        public void Load(Stream s)
        {
            long start = s.Position;
            BinaryReader br = new BinaryReader(s);
            uint riffSize;
            string riffMagic = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (riffMagic[0] != 'R' || riffMagic[1] != 'I' || riffMagic[2] != 'F' || riffMagic[3] != 'F')
            {
                throw new Exception("Stream not in riff/wave format!");
            }

            riffSize = br.ReadUInt32();
            riffMagic = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (riffMagic[0] != 'W' || riffMagic[1] != 'A' || riffMagic[2] != 'V' || riffMagic[3] != 'E')
            {
                throw new Exception("Stream not in riff/wave format!");
            }

            bool lastChunk = false;
            while (!lastChunk)
            {
                long current = s.Position;
                string id = Encoding.ASCII.GetString(br.ReadBytes(4));
                uint size = br.ReadUInt32();
                lastChunk = (size + s.Position == start + riffSize + 8); // 2 magics
                if (size + s.Position > start + riffSize + 8)
                {
                    throw new Exception("Chunk size specified as greater than full size!");
                }

                if (id[0] == 'f' && id[1] == 'm' && id[2] == 't' && id[3] == ' ') // fmt  chunk
                {
                    this.ReadHeader(br, size);
                }
                else
                {
                    if (id[0] == 'd' && id[1] == 'a' && id[2] == 't' && id[3] == 'a') // data chunk
                    {
                        this.ReadData(br, s, size);
                    }
                    else
                    {
                        Client.Instance.Logger.Log(Util.LogLevel.Warn, $"Unknown chunk {id} in riff/wave audio file, skipping!");
                    }
                }

                s.Seek(current + 8 + size - s.Position, SeekOrigin.Current);
            }
        }

        private void ReadHeader(BinaryReader br, uint size)
        {
            ushort fmt = br.ReadUInt16();
            if (fmt != 1)
            {
                throw new NotSupportedException("Compressed riff/wave file not supported!");
            }

            this.NumChannels = br.ReadUInt16();
            this.SampleRate = (int)br.ReadUInt32();
            this.ByteRate = (int)br.ReadUInt32();
            this.BlockAlign = br.ReadUInt16();
            this.BitsPerSample = br.ReadUInt16();

            uint headerLeft = size - 16;
            if (headerLeft == 0)
            {
                return;
            }

            ushort leftSpecified = br.ReadUInt16();
            if (leftSpecified != headerLeft - 2)
            {
                throw new Exception($"Malformed riff/wave header! Specified {leftSpecified} bytes left, actual {headerLeft - 2} bytes left!");
            }

            // No need to parse futher, operating with PCM and don't care
        }

        private void ReadData(BinaryReader br, Stream s, uint size)
        {
            int bps = this.BitsPerSample / 8;
            int nTotalSamples = (int)(size / bps);
            unsafe
            {
                this._hdata = (ushort*)Marshal.AllocHGlobal(nTotalSamples * sizeof(ushort));
                this.DataLength = nTotalSamples;
                Span<byte> localData = stackalloc byte[bps];
                Span<byte> uis = stackalloc byte[4];
                for (int i = 0; i < nTotalSamples; ++i)
                {
                    br.Read(localData);
                    switch (bps)
                    {
                        case 1:
                        {
                            ushort usv = localData[0];
                            usv *= 255; // simply up the precision
                            this._hdata[i] = usv;
                            break;
                        }

                        case 2:
                        {
                            this._hdata[i] = BitConverter.ToUInt16(localData);
                            break;
                        }

                        case 3:
                        {
                            // 24 bpm
                            localData.CopyTo(uis);
                            uint ui = BitConverter.ToUInt32(uis);
                            ui = ui >> 8;
                            this._hdata[i] = (ushort)ui;
                            break;
                        }

                        case 4:
                        {
                            uint ui = BitConverter.ToUInt32(localData);
                            ui = ui >> 16;
                            this._hdata[i] = (ushort)ui;
                            break;
                        }

                        default:
                        {
                            Marshal.FreeHGlobal((IntPtr)this._hdata);
                            throw new NotSupportedException($"Unsupported bps {bps} for riff/wave audio!");
                        }
                    }
                }
            }

            this.IsReady = true;
        }

        public void Free()
        {
            if (this.IsReady)
            {
                unsafe
                {
                    Marshal.FreeHGlobal((IntPtr)this._hdata);
                    this.IsReady = false;
                }
            }
        }
    }
}
