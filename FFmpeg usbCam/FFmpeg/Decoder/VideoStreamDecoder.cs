using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace FFmpeg_usbCam.FFmpeg.Decoder
{
    public sealed unsafe class VideoStreamDecoder : IDisposable
    {
        AVCodecContext* _pCodecContext;
        AVCodecContext* _oCodecContext;

        AVFormatContext* _pFormatContext;
        AVFormatContext* _oFormatContext;

        AVFrame* _pFrame;
        AVPacket* packet;

        int _streamIndex;

        IntPtr _convertedFrameBufferPtr;
        Size _destinationSize;
        byte_ptrArray4 _dstData;
        int_array4 _dstLinesize;
        SwsContext* _pConvertContext;

        public string CodecName { get; set; }
        public System.Windows.Size FrameSize { get; set; }
        public AVPixelFormat PixelFormat { get; set; }


        public VideoStreamDecoder()
        {
            ffmpeg.avdevice_register_all();
        }

        public void OpenInputURL(string url)
        {
            _pFormatContext = ffmpeg.avformat_alloc_context();
            var pFormatContext = _pFormatContext;

            //webcam
            //AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            //ffmpeg.avformat_open_input(&pFormatContext, url, iformat, null).ThrowExceptionIfError();

            //미디어 파일 열기 url주소 또는 파일 이름 필요            
            ffmpeg.avformat_open_input(&pFormatContext, url, null, null).ThrowExceptionIfError();

            ////미디어 정보 가져옴, blocking 함수라서 network protocol으로 가져올 시, 블락될수도 있슴
            ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();

            // find the first video stream
            AVStream* pStream = null;

            for (var i = 0; i < _pFormatContext->nb_streams; i++)
            {
                if (_pFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    pStream = _pFormatContext->streams[i];
                    break;
                }
            }


            if (pStream == null) throw new InvalidOperationException("Could not found video stream.");

            _streamIndex = pStream->index;
            _pCodecContext = pStream->codec;

            var codecId = _pCodecContext->codec_id;
            var pCodec = ffmpeg.avcodec_find_decoder(codecId);  //H264
            if (pCodec == null) throw new InvalidOperationException("Unsupported codec.");

            //open codec
            ffmpeg.avcodec_open2(_pCodecContext, pCodec, null).ThrowExceptionIfError();
            ffmpeg.avcodec_parameters_to_context(_pCodecContext, pStream->codecpar);

            CodecName = ffmpeg.avcodec_get_name(codecId);
            FrameSize = new System.Windows.Size(_pCodecContext->width, _pCodecContext->height);
            PixelFormat = _pCodecContext->pix_fmt;

            packet = ffmpeg.av_packet_alloc();
            _pFrame = ffmpeg.av_frame_alloc();

            _pFormatContext = pFormatContext;
        }

        public void OpenOutputURL(string fileName)
        {
            AVStream* out_stream;
            AVStream* in_stream;
            AVCodec* encoder;

            int ret;

            //output file
            var oFormatContext = _oFormatContext;

            ffmpeg.avformat_alloc_output_context2(&oFormatContext, null, null, fileName);

            for (int i = 0; i < _pFormatContext->nb_streams; i++)
            {
                in_stream = _pFormatContext->streams[i];
                _pCodecContext = in_stream->codec;

                if (_pCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    encoder = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

                    out_stream = ffmpeg.avformat_new_stream(oFormatContext, encoder);

                    _oCodecContext = ffmpeg.avcodec_alloc_context3(encoder);
                    _oCodecContext = out_stream->codec;

                    _oCodecContext->height = _pCodecContext->height;
                    _oCodecContext->width = _pCodecContext->width;
                    _oCodecContext->sample_aspect_ratio = _pCodecContext->sample_aspect_ratio;
                    _oCodecContext->pix_fmt = encoder->pix_fmts[0];
                    _oCodecContext->time_base = _pCodecContext->time_base;
                    _oCodecContext->framerate = ffmpeg.av_inv_q(_pCodecContext->framerate);

                    if ((oFormatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                    {
                        _oCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                    }

                    //open codec
                    ret = ffmpeg.avcodec_open2(_oCodecContext, encoder, null).ThrowExceptionIfError();

                    ret = ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, _oCodecContext);
                    out_stream->time_base = _oCodecContext->time_base;
                }
            }

            //Show some Information
            ffmpeg.av_dump_format(oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            //Write File Header
            int error = ffmpeg.avformat_write_header(oFormatContext, null);
            error.ThrowExceptionIfError();

            _oFormatContext = oFormatContext;
        }

        int stream_index;

        public AVFrame TryDecodeNextFrame()
        {
            ffmpeg.av_frame_unref(_pFrame);
            int ret;
            do
            {
                try
                {
                    do
                    {
                        ret = ffmpeg.av_read_frame(_pFormatContext, packet);

                        /* If we are at the end of the file, flush the decoder below. */
                        if (ret == ffmpeg.AVERROR_EOF)
                        {
                            continue;
                        }

                        ret.ThrowExceptionIfError();


                    } while (packet->stream_index != _streamIndex);

                    stream_index = packet->stream_index;

                    ffmpeg.av_packet_rescale_ts(packet, _pFormatContext->streams[_streamIndex]->time_base, _pCodecContext->time_base);

                    /* Send the video frame stored in the temporary packet to the decoder.
                     * The input video stream decoder is used to do this. */
                    ret = ffmpeg.avcodec_send_packet(_pCodecContext, packet);
                    ret.ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(packet);
                }

                ret = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            } while (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            ret.ThrowExceptionIfError();

            return *_pFrame;
        }

        public void TryEncodeNextPacket(AVFrame* uncompressed_frame)
        {
            int ret;
            AVPacket* encoded_packet;

            encoded_packet = ffmpeg.av_packet_alloc();
            ffmpeg.av_init_packet(encoded_packet);

            ret = ffmpeg.avcodec_send_frame(_oCodecContext, uncompressed_frame);
            ret.ThrowExceptionIfError();

            while (true)
            {
                ret = ffmpeg.avcodec_receive_packet(_oCodecContext, encoded_packet);

                stream_index = encoded_packet->stream_index;

                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF))
                {
                    break;
                }
                else if (ret < 0)
                {
                    Console.WriteLine("Error during encoding \n");
                    break;
                }

                if (encoded_packet->pts != ffmpeg.AV_NOPTS_VALUE)
                    encoded_packet->pts = (long)(ffmpeg.av_rescale_q(encoded_packet->pts, _oCodecContext->time_base, _oFormatContext->streams[stream_index]->time_base));
                if (encoded_packet->dts != ffmpeg.AV_NOPTS_VALUE)
                    encoded_packet->dts = ffmpeg.av_rescale_q(encoded_packet->dts, _oCodecContext->time_base, _oFormatContext->streams[stream_index]->time_base);

                ffmpeg.av_write_frame(_oFormatContext, encoded_packet);
            }
        }

        public AVFrame Convert(AVFrame sourceFrame)
        {
            ffmpeg.sws_scale(_pConvertContext, sourceFrame.data, sourceFrame.linesize, 0, sourceFrame.height, _dstData, _dstLinesize);

            var data = new byte_ptrArray8();
            data.UpdateFrom(_dstData);
            var linesize = new int_array8();
            linesize.UpdateFrom(_dstLinesize);

            return new AVFrame
            {
                data = data,
                linesize = linesize,
                width = (int)_destinationSize.Width,
                height = (int)_destinationSize.Height
            };
        }

        public void FlushEncode()
        {
            ffmpeg.avcodec_send_frame(_oCodecContext, null);
        }

        public void VideoFrameConverter(Size sourceSize, AVPixelFormat sourcePixelFormat,
            Size destinationSize, AVPixelFormat destinationPixelFormat)
        {
            _destinationSize = destinationSize;

            _pConvertContext = ffmpeg.sws_getContext((int)sourceSize.Width, (int)sourceSize.Height, sourcePixelFormat, (int)destinationSize.Width, (int)destinationSize.Height, destinationPixelFormat, ffmpeg.SWS_FAST_BILINEAR, null, null, null);
            if (_pConvertContext == null) throw new ApplicationException("Could not initialize the conversion context.");

            var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(destinationPixelFormat, (int)destinationSize.Width, (int)destinationSize.Height, 1);
            _convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
            _dstData = new byte_ptrArray4();
            _dstLinesize = new int_array4();

            ffmpeg.av_image_fill_arrays(ref _dstData, ref _dstLinesize, (byte*)_convertedFrameBufferPtr, destinationPixelFormat, (int)destinationSize.Width, (int)destinationSize.Height, 1);
        }


        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();
            while ((tag = ffmpeg.av_dict_get(_oFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }

        public void Dispose()
        {
            var oFormatContext = _oFormatContext;

            //Write file trailer
            ffmpeg.av_write_trailer(oFormatContext);

            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_free(_pFrame);

            ffmpeg.av_packet_unref(packet);
            ffmpeg.av_free(packet);

            ffmpeg.avcodec_close(_pCodecContext);
            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);
            ffmpeg.avformat_close_input(&oFormatContext);

            Marshal.FreeHGlobal(_convertedFrameBufferPtr);
            ffmpeg.sws_freeContext(_pConvertContext);
        }
    }
}