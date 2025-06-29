﻿namespace VTT.Sound
{
    using NLayer;
    using NVorbis;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Text;
    using VTT.Network;
    using VTT.Util;

    public class WaveAudio : ISoundProvider
    {
        public int NumChannels { get; set; }
        public int SampleRate { get; set; }
        public int ByteRate { get; set; } // SampleRate * NumChannels * BitsPerSample / 8
        public int BlockAlign { get; set; } // NumChannels * BitsPerSample / 8
        public int BitsPerSample { get; set; }
        public double Duration { get; set; }
        public bool IsReady { get; set; }

        private unsafe ushort* _hdata;

        public unsafe IntPtr DataPtr => (IntPtr)this._hdata;
        public int DataLength { get; private set; }

        public byte[] GetManagedDataCopy()
        {
            byte[] data = new byte[this.DataLength * sizeof(ushort)];
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    Buffer.MemoryCopy(this._hdata, ptr, data.LongLength, data.LongLength);
                }
            }

            return data;
        }

        public void GetRawDataFull(out IntPtr dataPtr, out int dataLength)
        {
            dataPtr = this.DataPtr;
            dataLength = this.DataLength;
        }

        // Note that this destructively frees the data pointer passed!
        public bool TryGetMpegEncodedData(out byte[] data, out long[] packetOffsets)
        {
            if (Client.Instance.Frontend.FFmpegWrapper.IsInitialized)
            {
                unsafe
                {
                    data = Client.Instance.Frontend.FFmpegWrapper.EncodeMpegAudio((ushort*)this.DataPtr, this.DataLength, this.NumChannels, 112640, this.SampleRate, out packetOffsets);
                    if (data != null)
                    {
                        return true;
                    }
                }
            }

            data = null;
            packetOffsets = null;
            return false;
        }

        public WaveAudio()
        {
        }

        public WaveAudio(byte[] raw, int sr, int nCh)
        {
            using MemoryStream ms = new MemoryStream(raw);
            this.NumChannels = nCh;
            this.SampleRate = sr;
            unsafe
            {
                this._hdata = MemoryHelper.Allocate<ushort>((nuint)(raw.Length / sizeof(ushort)));
                this.DataLength = raw.Length / 2;
                fixed (byte* ptr = raw)
                {
                    Buffer.MemoryCopy(ptr, this._hdata, raw.LongLength, raw.LongLength);
                }

                this.Duration = (double)raw.LongLength / (nCh * sr * 2);
                this.IsReady = true;
            }

        }

        public WaveAudio(VorbisReader vorbis)
        {
            this.NumChannels = vorbis.Channels;
            this.SampleRate = vorbis.SampleRate;
            this.DataLength = LoadDataFromStream((buffer, offset, size) => vorbis.ReadSamples(buffer, offset, size), this.NumChannels * this.SampleRate);
            this.Duration = vorbis.TotalTime.TotalSeconds;
            this.IsReady = true;
        }

        public WaveAudio(MpegFile mpeg)
        {
            this.NumChannels = mpeg.Channels;
            this.SampleRate = mpeg.SampleRate;
            this.DataLength = LoadDataFromStream((buffer, offset, size) => mpeg.ReadSamples(buffer, offset, size), this.NumChannels * this.SampleRate);
            this.Duration = mpeg.Duration.TotalSeconds;
            this.IsReady = true;
        }

        public static bool ValidateMPEGFrame(Stream s)
        {
            Span<byte> header = stackalloc byte[4];
            int nRead;
            if ((nRead = s.Read(header)) != 4)
            {
                s.Seek(-nRead, SeekOrigin.Current);
                return false;
            }

            s.Seek(-4, SeekOrigin.Current); // Revert stream reading
            // First we check for ID3 header. This is NOT up to spec (ID3 SHOULD be placed at the end of the file (https://id3.org/ID3v1)
            // But it is known that audacity export places ID3v2 at the beginning of the file for some reason messing up the validator
            if (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33) // ID3 header - metadata info. Do not bother determining if this is ID3v1 or ID3v2 and skipping forward, ID3 is most likely mp3, so pass here
            {
                return true;
            }

            MpegFrameHeader headerNfo = new MpegFrameHeader(BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(header)));
            return headerNfo.Validate();
        }

        private int LoadDataFromStream(Func<float[], int, int, int> reader, int bufferSize)
        {
            float[] sBuffer = new float[bufferSize];
            int currentElementSize = 1024;
            int currentElementAmount = 0;
            unsafe
            {
                this._hdata = MemoryHelper.Allocate<ushort>((nuint)currentElementSize);
                int samplesRead;
                while ((samplesRead = reader(sBuffer, 0, bufferSize)) > 0)
                {
                    if (currentElementAmount + samplesRead > currentElementSize)
                    {
                        int nextElementAmount = currentElementSize * 2;
                        while (nextElementAmount <= samplesRead + currentElementAmount)
                        {
                            nextElementAmount *= 2;
                        }

                        this._hdata = MemoryHelper.Reallocate(this._hdata, (nuint)nextElementAmount);
                        currentElementSize = nextElementAmount;
                    }

                    for (int i = 0; i < samplesRead; ++i)
                    {
                        float f = sBuffer[i];
                        int hr_i = (int)(float.IsNegative(f) ? f * 0x8000 : f * 0x7fff);
                        short s = (short)Math.Clamp(hr_i, short.MinValue, short.MaxValue);
                        ushort us = *(ushort*)&s;
                        this._hdata[currentElementAmount++] = us;
                    }
                }
            }

            return currentElementAmount;
        }

        public Image<Rgba32> GenWaveForm(int w, int h)
        {
            Image<Rgba32> ret = new Image<Rgba32>(w, h);
            Rgba32 clrBak = new Rgba32(0xff121212);
            Rgba32 clrWave = new Rgba32(0xff007ced);
            float fw = this.DataLength / (float)w;
            for (int pixel = 0; pixel < w; ++pixel)
            {
                int start = (int)(pixel * fw);
                int end = (int)((pixel + 1) * fw);
                float vals = 0;
                int nSamples = end - start;
                int samplingStep = 1;
                if (nSamples > ushort.MaxValue) // Compromise on sample rate for large audio files
                {
                    samplingStep = nSamples / short.MaxValue;
                    nSamples /= samplingStep;
                }

                for (int i = start; i < end; i += samplingStep)
                {
                    unsafe
                    {
                        ushort us = this._hdata[i];
                        float fv = MathF.Abs(((float)us / short.MaxValue) - 1f);
                        vals += fv;
                    }
                }

                vals = (1f - (vals / nSamples)) * 2f;
                for (int y = 0; y < h; ++y)
                {
                    float hfv = (y / (float)h * 2f) - 1f;
                    Rgba32 c = MathF.Abs(hfv) <= vals ? clrWave : clrBak;
                    ret[pixel, y] = c;
                }
            }

            return ret;
        }

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
                        Client.Instance.Logger.Log(LogLevel.Warn, $"Unknown chunk {id} in riff/wave audio file, skipping!");
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
                this._hdata = MemoryHelper.Allocate<ushort>((nuint)nTotalSamples);
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
                            MemoryHelper.Free(this._hdata);
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
                    MemoryHelper.Free(this._hdata);
                    this.IsReady = false;
                }
            }
        }
    }

    public readonly struct MpegFrameHeader
    {
        // AAAAAAAA AAABBCCD EEEEFFGH IIJJKLMM 
        public readonly ushort frameSync;
        public readonly byte version;
        public readonly byte layerDescription;
        public readonly bool protectionBit;
        public readonly byte bitrateIndex;
        public readonly byte samplingRateFrequencyIndex;
        public readonly bool paddingBit;
        public readonly bool privateBit;
        public readonly byte channelMode;
        public readonly byte modeExtension;
        public readonly bool copyright;
        public readonly bool original;
        public readonly byte emphasis;

        public MpegFrameHeader(uint ui)
        {
            this.frameSync = (ushort)((ui >> 21) & 0b11111111111);
            this.version = (byte)((ui >> 19) & 0b11);
            this.layerDescription = (byte)((ui >> 17) & 0b11);
            this.protectionBit = ((ui >> 16) & 0b1) == 0b1;
            this.bitrateIndex = (byte)((ui >> 12) & 0b1111);
            this.samplingRateFrequencyIndex = (byte)((ui >> 10) & 0b11);
            this.paddingBit = ((ui >> 9) & 0b1) == 0b1;
            this.privateBit = ((ui >> 8) & 0b1) == 0b1;
            this.channelMode = (byte)((ui >> 6) & 0b11);
            this.modeExtension = (byte)((ui >> 4) & 0b11);
            this.copyright = ((ui >> 3) & 0b1) == 0b1;
            this.original = ((ui >> 2) & 0b1) == 0b1;
            this.emphasis = (byte)(ui & 0b11);
        }

        public readonly bool Validate()
        {
            return
                this.frameSync == 0b11111111111 &&
                this.version >= 0b01 && // Mpeg Version 2.5, 2, or 1,
                this.layerDescription != 0 && // Layer I, II or III
                this.bitrateIndex != 0b1111; // 1111 is not an allowed value in any spec. Layer 2 is complex and has an allowed table but for quick valididty we don't care
            // Technically could also validate channel mode + mode extension validity but w/e, this is enough for basic sanity check
        }
    }
}
