using FFmpeg.AutoGen;
using System;
using System.Drawing;
using System.IO;

namespace FFmpeg_usbCam.FFmpeg
{
    public unsafe class H264VideoStreamEncoder : GenericVideoStreamManager
    {
        public void OpenOutputURL(string fileName)
        {
            AVStream* out_stream;
            AVStream* in_stream;
            AVCodec* encoder;

            int ret;

            //output file
            var _oFormatContext = oFormatContext;
            
            ffmpeg.avformat_alloc_output_context2(&_oFormatContext, null, null, fileName);

            for (int i = 0; i < iFormatContext->nb_streams; i++)
            {
                in_stream = iFormatContext->streams[i];
                iCodecContext = in_stream->codec;

                if (iCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    encoder = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

                    out_stream = ffmpeg.avformat_new_stream(_oFormatContext, encoder);

                    oCodecContext = ffmpeg.avcodec_alloc_context3(encoder);
                    oCodecContext = out_stream->codec;

                    oCodecContext->height = iCodecContext->height;
                    oCodecContext->width = iCodecContext->width;
                    oCodecContext->sample_aspect_ratio = iCodecContext->sample_aspect_ratio;
                    oCodecContext->pix_fmt = encoder->pix_fmts[0];
                    oCodecContext->time_base = iCodecContext->time_base;
                    oCodecContext->framerate = ffmpeg.av_inv_q(iCodecContext->framerate);

                    if ((_oFormatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                    {
                        oCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                    }

                    //open codecd
                    ret = ffmpeg.avcodec_open2(oCodecContext, encoder, null).ThrowExceptionIfError();

                    ret = ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, oCodecContext);
                    out_stream->time_base = oCodecContext->time_base;
                }
            }
            
            //Show some Information
            ffmpeg.av_dump_format(_oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&_oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            //Write File Header
            int error = ffmpeg.avformat_write_header(_oFormatContext, null);
            error.ThrowExceptionIfError();

            oFormatContext = _oFormatContext;
        }

        public override void Dispose()
        {
            var _oFormatContext = oFormatContext;
            
            //Write file trailer
            ffmpeg.av_write_trailer(_oFormatContext);
            ffmpeg.avformat_close_input(&_oFormatContext);
        }

        public void TryEncodeNextPacket(AVFrame uncompressed_frame)
        {
            int ret;
            AVPacket* encoded_packet;

            encoded_packet = ffmpeg.av_packet_alloc();
            ffmpeg.av_init_packet(encoded_packet);

            ret = ffmpeg.avcodec_send_frame(oCodecContext, &uncompressed_frame);
            ret.ThrowExceptionIfError();

            while (true)
            {
                ret = ffmpeg.avcodec_receive_packet(oCodecContext, encoded_packet);

                enc_stream_index = encoded_packet->stream_index;

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
                    encoded_packet->pts = (long)(ffmpeg.av_rescale_q(encoded_packet->pts, oCodecContext->time_base, oFormatContext->streams[enc_stream_index]->time_base));
                if (encoded_packet->dts != ffmpeg.AV_NOPTS_VALUE)
                    encoded_packet->dts = ffmpeg.av_rescale_q(encoded_packet->dts, oCodecContext->time_base, oFormatContext->streams[enc_stream_index]->time_base);

                ffmpeg.av_write_frame(oFormatContext, encoded_packet);
            }
        }

        public void FlushEncode()
        {
            ffmpeg.avcodec_send_frame(oCodecContext, null);
        }


    }
}
