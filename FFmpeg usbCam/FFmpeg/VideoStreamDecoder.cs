using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace FFmpeg_usbCam.FFmpeg
{
    public enum VIDEO_INPUT_TYPE
    {
        RTP_RTSP = 0,
        CAM_DEVICE
    }

    public unsafe class VideoStreamDecoder : IDisposable
    {
        private readonly AVCodecContext* iCodecContext;
        private readonly AVCodecContext* audioCodeContext;
        private readonly AVFormatContext* iFormatContext;

        private readonly AVFrame* decodedFrame;
        private readonly AVFrame* receivedFrame;
        private readonly AVPacket* rawPacket;

        private readonly int videoStreamIndex;
        private readonly int audioStreamIndex;

        public string CodecName { get; }
        public Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }
        int videoIndex = -1; ///> Video Stream Index
        int audioIndex = -1; ///> Audio Stream Index

        public VideoStreamDecoder(string url, VIDEO_INPUT_TYPE inputType= VIDEO_INPUT_TYPE.RTP_RTSP, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            ffmpeg.avdevice_register_all();

            iFormatContext = ffmpeg.avformat_alloc_context();
            receivedFrame = ffmpeg.av_frame_alloc();
            var _iFormatContext = iFormatContext;

            int i;

            AVDictionary* avDict;
            ffmpeg.av_dict_set(&avDict, "reorder_queue_size", "1", 0);

            switch (inputType)
            {
                case VIDEO_INPUT_TYPE.CAM_DEVICE:
                    AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
                    ffmpeg.avformat_open_input(&_iFormatContext, url, iformat, null).ThrowExceptionIfError();
                    break;
                case VIDEO_INPUT_TYPE.RTP_RTSP:
                    ffmpeg.avformat_open_input(&_iFormatContext, @"C:\Users\admin\Desktop\out333.avi", null, null);
                    break;
                default:
                    break;
            }
            AVCodec* videoCodec = null;
            AVCodec* audioCodec = null;
            for (i = 0; i < _iFormatContext->nb_streams; i++)
            {
                if (_iFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoIndex = i;
                    Console.WriteLine("===========Video=============");
                    Console.WriteLine("bitrate   " + _iFormatContext->streams[i]->codec->bit_rate); //W * H *FPS
                    Console.WriteLine("bitrate   " + _iFormatContext->streams[i]->first_dts); //W * H *FPS
                    Console.WriteLine("bitrate   " + _iFormatContext->streams[i]->codec->pts_correction_last_dts); //W * H *FPS

                    Console.WriteLine("bitrate   " + _iFormatContext->streams[i]->codec->time_base.num + "/" + _iFormatContext->streams[i]->codec->time_base.den); //W * H *FPS
                    Console.WriteLine("frameRate   " + _iFormatContext->streams[i]->avg_frame_rate.num + "/" + _iFormatContext->streams[i]->avg_frame_rate.den);
                    Console.WriteLine("frameRate   " + _iFormatContext->streams[i]->r_frame_rate.num + "/"+ _iFormatContext->streams[i]->r_frame_rate.den);
                    Console.WriteLine("frameRate   " + _iFormatContext->streams[i]->codec_info_nb_frames);

                    Console.WriteLine("frame number  " + _iFormatContext->streams[i]->nb_frames);

                    Console.WriteLine("codecID   " + _iFormatContext->streams[i]->codec->codec_id);
                    Console.WriteLine($"Channels :  {_iFormatContext->streams[i]->codec->channels}");
                    Console.WriteLine($"타임베이스 :  {_iFormatContext->streams[i]->time_base.num}/{_iFormatContext ->streams[i]->time_base.den}");
                    videoCodec = _iFormatContext->streams[i]->codec->codec;
                }
                else if (_iFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioIndex =i;
                    Console.WriteLine("===========audio=============");
                    Console.WriteLine("bitrate   " + _iFormatContext->streams[i]->codec->bit_rate); //W * H *FPS
                    Console.WriteLine("codecID   " + _iFormatContext->streams[i]->codec->codec_id);
                    Console.WriteLine($"Channels :  {_iFormatContext->streams[i]->codec->channels}");
                }

            }
            
            var streaminfo= ffmpeg.avformat_find_stream_info(_iFormatContext, null).ThrowExceptionIfError();

            videoStreamIndex = ffmpeg.av_find_best_stream(_iFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &videoCodec, 0).ThrowExceptionIfError();
            audioStreamIndex = ffmpeg.av_find_best_stream(_iFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &audioCodec,0);

            Console.WriteLine($"VideoStreamIndex :  {videoIndex}    AudioStreamIndex :  {audioIndex}");
            Console.WriteLine($"VideoCodec  : {videoCodec->id}    AudioCodec :  {audioCodec->id}");

            iCodecContext = ffmpeg.avcodec_alloc_context3(videoCodec);
            var audioCodecContext = ffmpeg.avcodec_alloc_context3(audioCodec);

            if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                ffmpeg.av_hwdevice_ctx_create(&iCodecContext->hw_device_ctx, HWDeviceType, null, null, 0).ThrowExceptionIfError();
            }

            ffmpeg.avcodec_parameters_to_context(iCodecContext, _iFormatContext->streams[videoStreamIndex]->codecpar).ThrowExceptionIfError();   // 동영상 파일에 있는 정보가 컨텍스트에 복사되고 없는 정보는 코덱의 원래 정보가 유지된다. 간단한 코덱은 별도의 옵션이 없지만 고성능 코덱은 동작에 필요한 필수 옵션이 있다. 이 정보를 복사하지 않으면 코덱이 제대로 동작하지 않아 일부 파일이 열리지 않는다. 다음 함수는 코덱을 열어 사용할 준비를 하고 컨텍스트도 코덱에 맞게 초기화한다.
            //ffmpeg.avcodec_parameters_to_context(audioCodeContext, iFormatContext->streams[audioStreamIndex]->codecpar).ThrowExceptionIfError();

            ffmpeg.avcodec_open2(iCodecContext, videoCodec, null).ThrowExceptionIfError();  // 세번째 인수는 코덱으로 전달할 옵션값이며 필요 없으면 NULL로 지정한다.여기까지 진행하면 코덱과 컨텍스트가 모두 완비되어 패킷의 압축을 풀어 프레임 정보를 만들 준비가 되었다. 코덱을 다 사용한 후 다음 함수로 컨텍스트와 관련 메모리를 모두 해제한다.

            CodecName = ffmpeg.avcodec_get_name(videoCodec->id);
            var audioCodecName = ffmpeg.avcodec_get_name(audioCodec->id);
            FrameSize = new Size(iCodecContext->width, iCodecContext->height);
            PixelFormat = iCodecContext->pix_fmt;
            //Console.WriteLine(audioCodecName);

            rawPacket = ffmpeg.av_packet_alloc();
            decodedFrame = ffmpeg.av_frame_alloc();
        }
        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(decodedFrame);
            ffmpeg.av_frame_unref(receivedFrame);
            int error;
            
            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(iFormatContext, rawPacket);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *decodedFrame;
                            return false;
                        }
                        error.ThrowExceptionIfError();

                        if (rawPacket->stream_index == videoIndex)
                        {
                            Console.WriteLine("video");
                            Console.WriteLine(rawPacket->dts + "        :video dts");
                            Console.WriteLine(rawPacket->pts + "        :video pts");
                            // Decode Video
                            ffmpeg.avcodec_send_packet(iCodecContext, rawPacket);

                            
                        }
                        else if (rawPacket->stream_index == audioIndex)
                        {
                            Console.WriteLine("audio");
                            int bufferSize = ffmpeg.av_samples_get_buffer_size(null, iCodecContext->channels, decodedFrame->nb_samples,
                                iCodecContext->sample_fmt, 0);
                            Console.WriteLine(rawPacket->dts + "        :audio dts");
                            Console.WriteLine(rawPacket->pts + "        :audio pts");

                            //Create the audio frame
                            //    AudioFrame* frame = new AudioFrame();
                            //frame->dataSize = bufferSize;
                            //frame->data = new uint8_t[bufferSize];
                            //if (p_frame->channels == 2)
                            //{
                            //    memcpy(frame->data, p_frame->data[0], bufferSize >> 1);
                            //    memcpy(frame->data + (bufferSize >> 1), p_frame->data[1], bufferSize >> 1);
                            //}
                            //else
                            //{
                            //    memcpy(frame->data, p_frame->data, bufferSize);
                            //}
                            //double timeBase = ((double)p_audioCodecContext->time_base.num) / (double)p_audioCodecContext->time_base.den;
                            //frame->lifeTime = duration * timeBase;

                            //p_player->addAudioFrame(frame);
                            // Decode Audio
                            //ffmpeg.avcodec_decode_audio4(pACtx, pAFrame, &bGotSound, &packet);
                            //if (bGotSound)
                            //{
                            //    // Ready to Render Sound
                            //}
                        }

                        // Free the packet that was allocated by av_read_frame
                        // av_free_packet(&packet);


                    } while (rawPacket->stream_index != videoStreamIndex);

                    ffmpeg.av_packet_rescale_ts(rawPacket, iFormatContext->streams[videoStreamIndex]->time_base, iCodecContext->time_base);

                    /* Send the video frame stored in the temporary packet to the decoder.
                     * The input video stream decoder is used to do this. */
                 //  ffmpeg.avcodec_send_packet(iCodecContext, rawPacket);
                    //ffmpeg.avcodec_send_packet(iCodecContext, rawPacket).ThrowExceptionIfError();

                }
                finally
                {
                    ffmpeg.av_packet_unref(rawPacket);
                }

                //read decoded frame from input codec context
                error = ffmpeg.avcodec_receive_frame(iCodecContext, decodedFrame);

            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();

            if (iCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(receivedFrame, decodedFrame, 0).ThrowExceptionIfError();
                frame = *receivedFrame;
            }
            else
            {
                frame = *decodedFrame;
            }
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
            videoInfo.SourceFrameSize = new Size(iCodecContext->width, iCodecContext->height);
            videoInfo.DestinationFrameSize = videoInfo.SourceFrameSize;
            videoInfo.SourcePixelFormat = iCodecContext->pix_fmt;
            videoInfo.DestinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
            videoInfo.Sample_aspect_ratio = iCodecContext->sample_aspect_ratio;
            videoInfo.Timebase = iCodecContext->time_base;
            videoInfo.Framerate = iCodecContext->framerate; ;
            
            return videoInfo;
        }

        public void Dispose()
        {
            ffmpeg.av_frame_unref(decodedFrame);
            ffmpeg.av_free(decodedFrame);

            ffmpeg.av_packet_unref(rawPacket);
            ffmpeg.av_free(rawPacket);

            ffmpeg.avcodec_close(iCodecContext);

            var _iFormatContext = iFormatContext;
            ffmpeg.avformat_close_input(&_iFormatContext);
        }
    }
}
