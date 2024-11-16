namespace VTT.Util
{
    using FFmpeg.AutoGen;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using VTT.Network;

    public class FFmpegWrapper
    {
        public bool IsInitialized { get; set; }

        private AVHWDeviceType hwAccelType;

        public bool Init()
        {
            string path = Path.Combine(IOVTT.ClientDir, "FFmpeg");
            if (Directory.Exists(path))
            {
                if (this.CheckLibs(path))
                {
                    ffmpeg.RootPath = path;
                    this.IsInitialized = true;
                    this.hwAccelType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                }
            }

            return false;
        }

        private bool ConfigureDecoder()
        {
            if (this.IsInitialized)
            {
                List<AVHWDeviceType> avails = new List<AVHWDeviceType>();
                AVHWDeviceType type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                bool Prefer(AVHWDeviceType dev)
                {
                    if (avails.Contains(dev))
                    {
                        type = dev;
                        return true;
                    }

                    return false;
                }

                AVHWDeviceType PreferAll(params AVHWDeviceType[] types)
                {
                    foreach (AVHWDeviceType t in types)
                    {
                        if (Prefer(t))
                        {
                            return type;
                        }
                    }

                    return AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                }

                while ((type = ffmpeg.av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    avails.Add(type);
                }

                this.hwAccelType = PreferAll(
                    AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_DRM,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_QSV,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX
                );

                return true;
            }

            return false;
        }


        private unsafe bool encode(Logger l, AVCodecContext* ctx, AVPacket* pkt, AVFrame* frame, Stream ms, List<long> offsets, out int ec)
        {
            ec = ffmpeg.avcodec_send_frame(ctx, frame);
            if (ec < 0)
            {
                l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - error sending frame to the encoder with error code {ec}!");
                return false;
            }

            while (ec >= 0)
            {
                ec = ffmpeg.avcodec_receive_packet(ctx, pkt);
                if (ec == ffmpeg.AVERROR_EOF || ec == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                {
                    return true;
                }
                else
                {
                    if (ec < 0)
                    {
                        l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - error encoding audio frame with error code {ec}!");
                        return false;
                    }
                }

                offsets.Add(ms.Position);
                ms.Write(new ReadOnlySpan<byte>(pkt->data, pkt->size));
                ffmpeg.av_packet_unref(pkt);
            }

            return true;
        }

        public unsafe byte[] EncodeMpegAudio(ushort* pcm, int amt, int nChannels, int bitrate, int sampleRate, out long[] dataPacketOffsets)
        {
            short* pcmPtr = (short*)pcm;
            dataPacketOffsets = null;
            Logger l = Client.Instance.Logger;
            AVCodec* codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_MP3);
            if (codec == null)
            {
                l.Log(LogLevel.Error, "FFMpeg could not encode mp3 - codec creation failed!");
                return null;
            }

            AVCodecContext* ctx = ffmpeg.avcodec_alloc_context3(codec);
            if (ctx == null)
            {
                l.Log(LogLevel.Error, "FFMpeg could not encode mp3 - context creation failed!");
                return null;
            }

            ctx->bit_rate = bitrate;
            ctx->sample_rate = sampleRate;
            ctx->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16P;
            ctx->rc_min_rate = bitrate;
            ctx->rc_max_rate = bitrate;
            int layoutIndex = 0;
            while (true)
            {
                AVChannelLayout ch = codec->ch_layouts[layoutIndex];

                if (ch.nb_channels == nChannels)
                {
                    ctx->ch_layout = ch;
                    break;
                }

                if (ch.nb_channels == 0)
                {
                    break;
                }

                layoutIndex += 1;
            }

            if (codec->supported_samplerates == null)
            {
                ctx->sample_rate = sampleRate;
            }
            else
            {
                int best = 0;
                int* p = codec->supported_samplerates;
                while (*p != 0)
                {
                    if (best == 0 || Math.Abs(sampleRate - *p) < Math.Abs(sampleRate - best))
                    {
                        best = *p;
                    }

                    ++p;
                }

                ctx->sample_rate = best;
            }

            int result = ffmpeg.avcodec_open2(ctx, codec, null);
            if (result < 0)
            {
                l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - codec open failed with error code {result}!");
                ffmpeg.avcodec_free_context(&ctx);
                return null;
            }

            AVPacket* pkt = ffmpeg.av_packet_alloc();
            if (pkt == null)
            {
                l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - packet allocation failed!");
                ffmpeg.avcodec_free_context(&ctx);
                return null;
            }

            AVFrame* frame = ffmpeg.av_frame_alloc();
            if (frame == null)
            {
                l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - frame allocation failed!");
                ffmpeg.av_packet_free(&pkt);
                ffmpeg.avcodec_free_context(&ctx);
                return null;
            }

            frame->nb_samples = ctx->frame_size;
            frame->format = (int)AVSampleFormat.AV_SAMPLE_FMT_S16P;
            result = ffmpeg.av_channel_layout_copy(&frame->ch_layout, &ctx->ch_layout);
            if (result < 0)
            {
                l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - channel layout copy failed with error code {result}!");
                ffmpeg.av_frame_free(&frame);
                ffmpeg.av_packet_free(&pkt);
                ffmpeg.avcodec_free_context(&ctx);
                return null;
            }

            result = ffmpeg.av_frame_get_buffer(frame, 0);
            if (result < 0)
            {
                l.Log(LogLevel.Error, $"FFMpeg could not encode mp3 - audio data buffers allocation failed with error code {result}!");
                ffmpeg.av_frame_free(&frame);
                ffmpeg.av_packet_free(&pkt);
                ffmpeg.avcodec_free_context(&ctx);
                return null;
            }

            using MemoryStream ms = new MemoryStream();
            List<long> packetOffsets = new List<long>();
            int offset = 0;
            while (true)
            {
                result = ffmpeg.av_frame_make_writable(frame);
                if (result < 0)
                {
                    break;
                }

                if (nChannels == 1)
                {
                    short* samples = (short*)frame->extended_data[0];
                    for (int j = 0; j < ctx->frame_size; ++j)
                    {
                        short value = offset >= amt ? (short)0 : pcmPtr[offset++];
                        samples[j] = value;
                    }
                }
                else
                {
                    short* ch0 = (short*)frame->extended_data[0];
                    short* ch1 = (short*)frame->extended_data[1];
                    for (int j = 0; j < ctx->frame_size; ++j)
                    {
                        short valuec0 = offset >= amt ? (short)0 : pcmPtr[offset++];
                        short valuec1 = offset >= amt ? (short)0 : pcmPtr[offset++];
                        ch0[j] = valuec0;
                        ch1[j] = valuec1;
                    }
                }

                if (!encode(l, ctx, pkt, frame, ms, packetOffsets, out int ec1))
                {
                    l.Log(LogLevel.Error, $"MP3 encoding error - failed writing packet with error code {ec1}!");
                    break;
                }

                if (offset >= amt)
                {
                    break;
                }
            }

            encode(l, ctx, pkt, null, ms, packetOffsets, out _);
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_packet_free(&pkt);
            ffmpeg.avcodec_free_context(&ctx);
            if (packetOffsets.Count < 2)
            {
                dataPacketOffsets = new long[1] { 0 };
            }
            else
            {
                int targetNumPackets = 256;
                int frameSize = (int)(packetOffsets[1] - packetOffsets[0]);
                int assumedTotalSize = frameSize * targetNumPackets;
                while (assumedTotalSize > 176128) // 172kb
                {
                    targetNumPackets -= 16;
                    assumedTotalSize = frameSize * targetNumPackets;
                }

                List<long> combinedPacketOffsets = new List<long>();
                int i = 0;
                int nRead = 0;
                while (true)
                {
                    if (i + 1 >= packetOffsets.Count)
                    {
                        break; // No need to do anything, last packet offset is good
                    }
                    
                    if (nRead % targetNumPackets == 0)
                    {
                        combinedPacketOffsets.Add(packetOffsets[i]);
                    }

                    ++nRead;
                    ++i;
                }

                dataPacketOffsets = combinedPacketOffsets.ToArray();
            }

            byte[] ret = ms.ToArray();
            return ret;
        }

        public IEnumerable<Image<Rgba32>> DecodeAllFrames(string url)
        {
            using VideoStreamDecoder vsd = new VideoStreamDecoder(url, this.hwAccelType);
            using VideoFrameConverter vfc = new VideoFrameConverter(vsd.FrameSize, this.hwAccelType == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE ? vsd.PixelFormat : GetHWPixelFormat(this.hwAccelType), vsd.FrameSize, AVPixelFormat.AV_PIX_FMT_RGBA);
            while (vsd.TryDecodeNextFrame(out AVFrame frame))
            {
                AVFrame cFrame = vfc.Convert(frame);
                yield return this.ConvertFrameToImage(cFrame);
            }
        }

        private unsafe Image<Rgba32> ConvertFrameToImage(AVFrame cFrame)
        {
            Image<Rgba32> image = new Image<Rgba32>(cFrame.width, cFrame.height);
            Span<uint> s = new Span<uint>(cFrame.data[0], cFrame.width * cFrame.height);
            for (int i = 0; i < s.Length; ++i)
            {
                int x = i % image.Width;
                int y = i / image.Width;
                Rgba32 rgba = new Rgba32(s[i]);
                image[x, y] = rgba;
            }

            return image;
        }

        private static AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
        {
            return hWDevice switch
            {
                AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
                AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
                AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
                AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
                AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
                AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
                _ => AVPixelFormat.AV_PIX_FMT_NONE
            };
        }

        private bool CheckLibs(string path)
        {
            bool CheckFile(string f)
            {
                DirectoryInfo di = new DirectoryInfo(path);
                return di.GetFiles(f + ".*").Length > 0;
            }

            return CheckFile("avcodec-59") && CheckFile("avdevice-59") && CheckFile("avfilter-8") && CheckFile("avformat-59") && CheckFile("avutil-57") && CheckFile("postproc-56");
        }

        unsafe sealed class VideoStreamDecoder : IDisposable
        {
            private readonly AVCodecContext* _pCodecContext;
            private readonly AVFormatContext* _pFormatContext;
            private readonly AVFrame* _pFrame;
            private readonly AVPacket* _pPacket;
            private readonly AVFrame* _receivedFrame;
            private readonly int _streamIndex;

            public VideoStreamDecoder(string url, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                _pFormatContext = ffmpeg.avformat_alloc_context();
                AVFormatContext* pFormatContext = _pFormatContext;
                AVCodec* codec = ffmpeg.avcodec_find_decoder_by_name("libvpx-vp9");
                pFormatContext->video_codec_id = AVCodecID.AV_CODEC_ID_VP9;
                pFormatContext->video_codec = codec;
                _receivedFrame = ffmpeg.av_frame_alloc();
                ThrowExceptionIfError(ffmpeg.avformat_open_input(&pFormatContext, url, null, null));
                ThrowExceptionIfError(ffmpeg.avformat_find_stream_info(_pFormatContext, null));
                _streamIndex = ThrowExceptionIfError(ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0));
                _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);
                if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    ThrowExceptionIfError(ffmpeg.av_hwdevice_ctx_create(&_pCodecContext->hw_device_ctx, HWDeviceType, null, null, 0));
                }

                ThrowExceptionIfError(ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamIndex]->codecpar));
                ThrowExceptionIfError(ffmpeg.avcodec_open2(_pCodecContext, codec, null));

                CodecName = ffmpeg.avcodec_get_name(codec->id);
                FrameSize = new Size(_pCodecContext->width, _pCodecContext->height);
                PixelFormat = _pCodecContext->pix_fmt;

                _pPacket = ffmpeg.av_packet_alloc();
                _pFrame = ffmpeg.av_frame_alloc();
            }

            public string CodecName { get; }
            public Size FrameSize { get; }
            public AVPixelFormat PixelFormat { get; }

            public void Dispose()
            {
                AVFrame* pFrame = _pFrame;
                ffmpeg.av_frame_free(&pFrame);

                AVPacket* pPacket = _pPacket;
                ffmpeg.av_packet_free(&pPacket);

                ffmpeg.avcodec_close(_pCodecContext);
                AVFormatContext* pFormatContext = _pFormatContext;
                ffmpeg.avformat_close_input(&pFormatContext);
            }

            public bool TryDecodeNextFrame(out AVFrame frame)
            {
                ffmpeg.av_frame_unref(_pFrame);
                ffmpeg.av_frame_unref(_receivedFrame);
                int error;

                do
                {
                    try
                    {
                        do
                        {
                            ffmpeg.av_packet_unref(_pPacket);
                            error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);

                            if (error == ffmpeg.AVERROR_EOF)
                            {
                                frame = *_pFrame;
                                return false;
                            }

                            ThrowExceptionIfError(error);
                        } while (_pPacket->stream_index != _streamIndex);

                        ThrowExceptionIfError(ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket));
                    }
                    finally
                    {
                        ffmpeg.av_packet_unref(_pPacket);
                    }

                    error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
                } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

                ThrowExceptionIfError(error);

                if (_pCodecContext->hw_device_ctx != null)
                {
                    ThrowExceptionIfError(ffmpeg.av_hwframe_transfer_data(_receivedFrame, _pFrame, 0));
                    frame = *_receivedFrame;
                }
                else
                {
                    frame = *_pFrame;
                }

                return true;
            }

            public IReadOnlyDictionary<string, string> GetContextInfo()
            {
                AVDictionaryEntry* tag = null;
                Dictionary<string, string> result = new Dictionary<string, string>();
                while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
                {
                    string key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                    string value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                    result.Add(key, value);
                }

                return result;
            }
        }

        sealed unsafe class VideoFrameConverter : IDisposable
        {
            private readonly IntPtr _convertedFrameBufferPtr;
            private readonly Size _destinationSize;
            private readonly byte_ptrArray4 _dstData;
            private readonly int_array4 _dstLinesize;
            private readonly SwsContext* _pConvertContext;

            public VideoFrameConverter(Size sourceSize, AVPixelFormat sourcePixelFormat, Size destinationSize, AVPixelFormat destinationPixelFormat)
            {
                this._destinationSize = destinationSize;

                this._pConvertContext = ffmpeg.sws_getContext(
                    sourceSize.Width,
                    sourceSize.Height,
                    sourcePixelFormat,
                    destinationSize.Width,
                    destinationSize.Height,
                    destinationPixelFormat,
                    ffmpeg.SWS_FAST_BILINEAR,
                    null,
                    null,
                    null);

                if (this._pConvertContext == null)
                {
                    throw new ApplicationException("Could not initialize the conversion context.");
                }

                int convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(destinationPixelFormat, destinationSize.Width, destinationSize.Height, 1);
                this._convertedFrameBufferPtr = (IntPtr)MemoryHelper.AllocateBytes((nuint)convertedFrameBufferSize);
                this._dstData = new byte_ptrArray4();
                this._dstLinesize = new int_array4();

                ffmpeg.av_image_fill_arrays(ref _dstData,
                    ref _dstLinesize,
                    (byte*)_convertedFrameBufferPtr,
                    destinationPixelFormat,
                    destinationSize.Width,
                    destinationSize.Height,
                    1);
            }

            public void Dispose()
            {
                unsafe
                {
                    MemoryHelper.Free((void*)this._convertedFrameBufferPtr);
                }

                ffmpeg.sws_freeContext(this._pConvertContext);
            }

            public AVFrame Convert(AVFrame sourceFrame)
            {
                ffmpeg.sws_scale(this._pConvertContext,
                    sourceFrame.data,
                    sourceFrame.linesize,
                    0,
                    sourceFrame.height,
                    _dstData,
                    _dstLinesize);

                byte_ptrArray8 data = new byte_ptrArray8();
                data.UpdateFrom(_dstData);
                int_array8 linesize = new int_array8();
                linesize.UpdateFrom(_dstLinesize);

                return new AVFrame
                {
                    data = data,
                    linesize = linesize,
                    width = _destinationSize.Width,
                    height = _destinationSize.Height
                };
            }
        }

        public static unsafe string GetAVError(int error)
        {
            int bufferSize = 1024;
            byte* buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
            string message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        public static int ThrowExceptionIfError(int error) => error < 0 ? throw new ApplicationException(GetAVError(error)) : error;
    }
}
