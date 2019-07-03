using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FFmpeg_usbCam.FFmpeg
{
    public unsafe class VideoStreamDecoder : GenericVideoStreamManager
    {
        public VideoStreamDecoder(string url, int type)
        {
            ffmpeg.avdevice_register_all();

            iFormatContext = ffmpeg.avformat_alloc_context();

            var _iFormatContext = iFormatContext;

            if(type == (int)VTYPE.RTSP_RTP)
            {
                //rtsp video streaming
                ffmpeg.avformat_open_input(&_iFormatContext, url, null, null).ThrowExceptionIfError();
            }
            else if(type == (int)VTYPE.CAM)
            {
                //webcam
                AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
                ffmpeg.avformat_open_input(&_iFormatContext, url, iformat, null).ThrowExceptionIfError();
            }

            ffmpeg.avformat_find_stream_info(_iFormatContext, null).ThrowExceptionIfError();

            // find the first video stream
            AVStream* pStream = null;
            for (var i = 0; i < _iFormatContext->nb_streams; i++)
                if (_iFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    pStream = _iFormatContext->streams[i];
                    break;
                }

            if (pStream == null) throw new InvalidOperationException("Could not found video stream.");

            dec_stream_index = pStream->index;
            iCodecContext = pStream->codec;

            var codecId = iCodecContext->codec_id;
            var pCodec = ffmpeg.avcodec_find_decoder(codecId);
            if (pCodec == null) throw new InvalidOperationException("Unsupported codec.");

            ffmpeg.avcodec_open2(iCodecContext, pCodec, null).ThrowExceptionIfError();

            CodecName = ffmpeg.avcodec_get_name(codecId);
            FrameSize = new Size(iCodecContext->width, iCodecContext->height);
            PixelFormat = iCodecContext->pix_fmt;

            rawPacket = ffmpeg.av_packet_alloc();
            decodedFrame = ffmpeg.av_frame_alloc();

            iFormatContext = _iFormatContext;
        }

        public string CodecName { get; }
        public Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }

        public override void Dispose()
        {
            ffmpeg.av_frame_unref(decodedFrame);
            ffmpeg.av_free(decodedFrame);

            ffmpeg.av_packet_unref(rawPacket);
            ffmpeg.av_free(rawPacket);

            ffmpeg.avcodec_close(iCodecContext);

            var _iFormatContext = iFormatContext;
            ffmpeg.avformat_close_input(&_iFormatContext);
        }

        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(decodedFrame);
            int ret;
            do
            {
                try
                {
                    do
                    {
                        ret = ffmpeg.av_read_frame(iFormatContext, rawPacket);
                        if (ret == ffmpeg.AVERROR_EOF)
                        {
                            frame = *decodedFrame;
                            return false;
                        }

                        ret.ThrowExceptionIfError();
                    } while (rawPacket->stream_index != dec_stream_index);

                    ffmpeg.av_packet_rescale_ts(rawPacket, iFormatContext->streams[dec_stream_index]->time_base, iCodecContext->time_base);

                    /* Send the video frame stored in the temporary packet to the decoder.
                     * The input video stream decoder is used to do this. */
                    ffmpeg.avcodec_send_packet(iCodecContext, rawPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(rawPacket);
                }

                //read decoded frame from input codec context
                ret = ffmpeg.avcodec_receive_frame(iCodecContext, decodedFrame);
            } while (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            ret.ThrowExceptionIfError();
            frame = *decodedFrame;

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
    }
}
