using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Size = System.Drawing.Size;

namespace FFmpeg_usbCam.FFmpeg
{
    public enum VIDEO_INPUT_TYPE
    {
        RTP_RTSP = 0,
        CAM_DEVICE
    }

    public unsafe class VideoStreamDecoder : IDisposable
    {
        private readonly AVCodecContext* videoCodecContext;
        private readonly AVCodecContext* audioCodeContext;
        private readonly AVFormatContext* iFormatContext;

        private readonly AVFrame* decodedFrame;
        private readonly AVFrame* receivedFrame;
        private readonly AVPacket* rawPacket;
        int out_buffer_size_audio;
        int out_channel_nb;
        private readonly int videoStreamIndex;
        private readonly int audioStreamIndex;

        public string CodecName { get; }
        SwrContext* swrCtx_Audio;
        private AVSampleFormat out_sample_fmt;
        public string AudioCodecName { get; }
        public Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }
        int videoIndex = -1; ///> Video Stream Index
        int audioIndex = -1;
        byte* out_buffer_audio;
        ///> Audio Stream Index
        private SwrContext* swrCtx;


        public VideoStreamDecoder(string url, VIDEO_INPUT_TYPE inputType = VIDEO_INPUT_TYPE.RTP_RTSP, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            ffmpeg.avdevice_register_all();
            AVFormatContext* pFormatCtx = ffmpeg.avformat_alloc_context();
            AVDictionary* options = null;

            ffmpeg.av_dict_set(&options, "list_devices", "true", 0);
            AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            Console.WriteLine("========Device Info=============\n");
            ffmpeg.avformat_open_input(&pFormatCtx, null, iformat, &options);
            Console.WriteLine("===============================\n");

            AVDeviceInfoList* device_list = null;
            int result =ffmpeg.avdevice_list_input_sources(iformat,null, options, &device_list);
            Console.WriteLine(result);

            //iFormatContext = ffmpeg.avform at_alloc_context();
            //receivedFrame = ffmpeg.av_frame_alloc();
            //var _iFormatContext = iFormatContext;

            //int i;

            //AVDictionary* avDict;
            //ffmpeg.av_dict_set(&avDict, "reorder_queue_size", "1", 0);

            //switch (inputType)
            //{
            //    case VIDEO_INPUT_TYPE.CAM_DEVICE:
            //        AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            //        AVDeviceInfoList* listdevice = null;
            //        ffmpeg.avdevice_list_devices(_iFormatContext, (AVDeviceInfoList**)listdevice);


            //        Console.WriteLine(listdevice->devices[0]->ToString());


            //        //ffmpeg.avformat_open_input(&_iFormatContext, url, iformat, null).ThrowExceptionIfError();
            //        break;
            //    case VIDEO_INPUT_TYPE.RTP_RTSP:
            //        ffmpeg.avformat_open_input(&_iFormatContext, @"C:\Users\admin\Desktop\result1.avi", null, null);
            //        break;
            //    default:
            //        break;
            //}

            Console.ReadLine();
            //_iFormatContext->streams[0]->time_base = new AVRational { num = 1, den = 30 };
            //_iFormatContext->streams[0]->avg_frame_rate = new AVRational { num = 30, den = 1 };
            //AVCodec* videoCodec = null;
            //AVCodec* audioCodec = null;

            //for (i = 0; i < _iFormatContext->nb_streams; i++)
            //{
            //    if (_iFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            //    {
            //        videoIndex = i;
            //        videoCodecContext = _iFormatContext->streams[i]->codec;
            //        videoCodec = ffmpeg.avcodec_find_decoder(videoCodecContext->codec_id);
            //    }
            //    else if (_iFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            //    {
            //        audioCodeContext = _iFormatContext->streams[i]->codec;
            //        audioCodec = ffmpeg.avcodec_find_decoder(audioCodeContext->codec_id);
            //        audioIndex = i;
            //    }
            //}

            //ffmpeg.avformat_find_stream_info(_iFormatContext, null).ThrowExceptionIfError(); //Stream에 접근하기 위해서는 미디어로부터 데이터 읽어야함. 

            //videoStreamIndex = ffmpeg.av_find_best_stream(_iFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &videoCodec, 0).ThrowExceptionIfError();
            //audioStreamIndex = ffmpeg.av_find_best_stream(_iFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &audioCodec, 0);

            //Console.WriteLine($"VideoStreamIndex :  {videoIndex}    AudioStreamIndex :  {audioIndex}");
            //Console.WriteLine($"VideoCodec  : {videoCodec->id}    AudioCodec :  {audioCodec->id}");

            //videoCodecContext = ffmpeg.avcodec_alloc_context3(videoCodec);
            //audioCodeContext = ffmpeg.avcodec_alloc_context3(audioCodec);


            //if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            //{
            //    ffmpeg.av_hwdevice_ctx_create(&videoCodecContext->hw_device_ctx, HWDeviceType, null, null, 0).ThrowExceptionIfError();
            //}

            //ffmpeg.avcodec_parameters_to_context(videoCodecContext, _iFormatContext->streams[videoStreamIndex]->codecpar).ThrowExceptionIfError();   // 동영상 파일에 있는 정보가 컨텍스트에 복사되고 없는 정보는 코덱의 원래 정보가 유지된다. 간단한 코덱은 별도의 옵션이 없지만 고성능 코덱은 동작에 필요한 필수 옵션이 있다. 이 정보를 복사하지 않으면 코덱이 제대로 동작하지 않아 일부 파일이 열리지 않는다. 다음 함수는 코덱을 열어 사용할 준비를 하고 컨텍스트도 코덱에 맞게 초기화한다.
            //ffmpeg.avcodec_parameters_to_context(audioCodeContext, _iFormatContext->streams[audioStreamIndex]->codecpar).ThrowExceptionIfError();

            //ffmpeg.avcodec_open2(videoCodecContext, videoCodec, null).ThrowExceptionIfError();  // 세번째 인수는 코덱으로 전달할 옵션값이며 필요 없으면 NULL로 지정한다.여기까지 진행하면 코덱과 컨텍스트가 모두 완비되어 패킷의 압축을 풀어 프레임 정보를 만들 준비가 되었다. 코덱을 다 사용한 후 다음 함수로 컨텍스트와 관련 메모리를 모두 해제한다.
            //ffmpeg.avcodec_open2(audioCodeContext, audioCodec, null).ThrowExceptionIfError();  // 세번째 인수는 코덱으로 전달할 옵션값이며 필요 없으면 NULL로 지정한다.여기까지 진행하면 코덱과 컨텍스트가 모두 완비되어 패킷의 압축을 풀어 프레임 정보를 만들 준비가 되었다. 코덱을 다 사용한 후 다음 함수로 컨텍스트와 관련 메모리를 모두 해제한다.

            //CodecName = ffmpeg.avcodec_get_name(videoCodec->id);
            //AudioCodecName = ffmpeg.avcodec_get_name(audioCodec->id);
            //swrCtx = ffmpeg.swr_alloc();
            //FrameSize = new Size(videoCodecContext->width, videoCodecContext->height);
            //PixelFormat = videoCodecContext->pix_fmt;
            ////Console.WriteLine(audioCodecName);

            //swrCtx_Audio = ffmpeg.swr_alloc();

            //AVSampleFormat in_sample_fmt = audioCodeContext->sample_fmt;
            //int in_sample_rate = audioCodeContext->sample_rate;
            //long in_ch_layout = (long)audioCodeContext->channel_layout;

            //out_sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            //int out_sample_rate = 44100;
            //int out_ch_layout = ffmpeg.AV_CH_LAYOUT_MONO;

            //ffmpeg.swr_alloc_set_opts(swrCtx_Audio, out_ch_layout, out_sample_fmt, out_sample_rate, in_ch_layout, in_sample_fmt, in_sample_rate, 0, null);
            //ffmpeg.swr_init(swrCtx_Audio);
            ////Resampling setting options-------------------------------------------- ---------------end
            ////Get the number of output channels
            //out_channel_nb = ffmpeg.av_get_channel_layout_nb_channels((ulong)out_ch_layout);
            ////Store pcm data
            //out_buffer_audio = (byte*)ffmpeg.av_malloc(2 * 8000);

            //rawPacket = ffmpeg.av_packet_alloc();
            //decodedFrame = ffmpeg.av_frame_alloc();
        }
       
        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            byte* out_audio_buffer = out_buffer_audio;

            ffmpeg.av_frame_unref(decodedFrame);
            ffmpeg.av_frame_unref(receivedFrame);
            int error;
            frame = default;
            do
            {
                try
                {
                    int dtspts = 0;
                    do
                    {
                        error = ffmpeg.av_read_frame(iFormatContext, rawPacket);
                   //     Console.WriteLine(iFormatContext->streams[0]->time_base.num + " / " + iFormatContext->streams[0]->time_base.den);
                   //     Console.WriteLine(rawPacket->pts + "       " + rawPacket->dts);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *decodedFrame;
                            return false;
                        }
                        error.ThrowExceptionIfError();
                        if (rawPacket->stream_index == videoIndex)
                        {
                            videoCodecContext->time_base = new AVRational { num = 1, den = 30 };
                            videoCodecContext->framerate = new AVRational { num = 30, den = 1 };
                            ffmpeg.avcodec_send_packet(videoCodecContext, rawPacket);
                            ffmpeg.av_packet_rescale_ts(rawPacket, iFormatContext->streams[videoStreamIndex]->time_base, videoCodecContext->time_base);
                            error = ffmpeg.avcodec_receive_frame(videoCodecContext, decodedFrame);
                            frame = *decodedFrame;
                  //          Console.WriteLine(frame.channels + "   video");
                        }
                        else if (rawPacket->stream_index == audioIndex)
                        {
                            if (audioCodeContext->channel_layout == 0)
                            {
                                audioCodeContext->channel_layout = ffmpeg.AV_CH_FRONT_LEFT | ffmpeg.AV_CH_FRONT_RIGHT;
                            }

                            ffmpeg.avcodec_send_packet(audioCodeContext, rawPacket);

                            //error = ffmpeg.avcodec_receive_frame(audioCodeContext, decodedFrame);
                            //audioCodeContext->time_base = new AVRational { num = 1, den = 30 };
                            //audioCodeContext->framerate = new AVRational { num = 30, den = 1 };
                            //ffmpeg.swr_convert(swrCtx_Audio, &out_audio_buffer, 2 * 8000, (byte**)&decodedFrame->data, decodedFrame->nb_samples);
                            //out_buffer_size_audio = ffmpeg.av_samples_get_buffer_size(null, out_channel_nb, decodedFrame->nb_samples, out_sample_fmt, 1);
                            //Console.WriteLine($"channel : {decodedFrame->channels}, format : {decodedFrame->format}, rate : {decodedFrame->sample_rate}");
                            //frame = *decodedFrame;
                            //Console.WriteLine(frame.channels + "   audio");

                        }

                    } while (rawPacket->stream_index !=videoStreamIndex);

                }
                finally
                {
                    ffmpeg.av_packet_unref(rawPacket);
                }

            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            //error.ThrowExceptionIfError();

            //if (videoCodecContext->hw_device_ctx != null)
            //{
            //    ffmpeg.av_hwframe_transfer_data(receivedFrame, decodedFrame, 0).ThrowExceptionIfError();
            //    frame = *receivedFrame;
            //    //frame = *decodedFrame;
            //}
            //else
            //{
            //    frame = *decodedFrame;
            //}

            return true;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();
            while ((tag = ffmpeg.av_dict_get(iFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }

        public VideoInfo GetVideoInfo()
        {
            VideoInfo videoInfo = new VideoInfo();
            videoInfo.SourceFrameSize = new Size(videoCodecContext->width, videoCodecContext->height);
            videoInfo.DestinationFrameSize = videoInfo.SourceFrameSize;
            videoInfo.SourcePixelFormat = videoCodecContext->pix_fmt;
            videoInfo.DestinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
            videoInfo.Sample_aspect_ratio = videoCodecContext->sample_aspect_ratio;
            videoInfo.Timebase = videoCodecContext->time_base;
            videoInfo.Framerate = videoCodecContext->framerate; ;
            videoInfo.audioCodec = AudioCodecName;
            videoInfo.audioSampleRate = audioCodeContext->sample_rate;
            return videoInfo;
        }

        public void Dispose()
        {
            ffmpeg.av_frame_unref(decodedFrame);
            ffmpeg.av_free(decodedFrame);

            ffmpeg.av_packet_unref(rawPacket);
            ffmpeg.av_free(rawPacket);

            ffmpeg.avcodec_close(videoCodecContext);

            var _iFormatContext = iFormatContext;
            ffmpeg.avformat_close_input(&_iFormatContext);
        }
    }
}
