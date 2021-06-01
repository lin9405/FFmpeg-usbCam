using FFmpeg.AutoGen;
using System;
using System.Drawing;
using System.IO;

namespace FFmpeg_usbCam.FFmpeg
{
    public unsafe class H264VideoStreamEncoder : IDisposable
    {
        int enc_stream_index;
        private AVFormatContext* oFormatContext;
        AVCodecContext* videoCodecContextoutput;
        private AVCodecContext* audioCodecContext;
        AVCodec* AudioCodec;
        AVCodec* VideoCodec;


        public void OpenOutputURL(string fileName, VideoInfo videoInfo)
        {
            AVStream* VideoStream;
            AVStream* AudioStream;

            //output file
            var _oFormatContext = oFormatContext;

            ffmpeg.avformat_alloc_output_context2(&_oFormatContext, null, null, fileName);

            VideoCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
            AudioCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC);

            VideoStream = ffmpeg.avformat_new_stream(_oFormatContext, VideoCodec);
            AudioStream = ffmpeg.avformat_new_stream(_oFormatContext, AudioCodec);
            

            videoCodecContextoutput = ffmpeg.avcodec_alloc_context3(VideoCodec);
            audioCodecContext = ffmpeg.avcodec_alloc_context3(AudioCodec);

            videoCodecContextoutput->height = videoInfo.SourceFrameSize.Height;
            videoCodecContextoutput->width = videoInfo.SourceFrameSize.Width;
            videoCodecContextoutput->sample_aspect_ratio = videoInfo.Sample_aspect_ratio;
            videoCodecContextoutput->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            videoCodecContextoutput->time_base = new AVRational { num = 1, den = 30 };
            videoCodecContextoutput->framerate = ffmpeg.av_inv_q(videoInfo.Framerate);
            videoCodecContextoutput->timecode_frame_start = 0;

            audioCodecContext->bit_rate = 128000;
            audioCodecContext->sample_rate = 48000;
            audioCodecContext->channel_layout = ffmpeg.AV_CH_LAYOUT_STEREO;
            audioCodecContext->channels = ffmpeg.av_get_channel_layout_nb_channels(audioCodecContext->channel_layout);
            audioCodecContext->frame_size =100000;
            audioCodecContext->sample_fmt = AudioCodec->sample_fmts[0];
            audioCodecContext->profile = ffmpeg.FF_PROFILE_AAC_LOW;
            audioCodecContext->codec_id = AudioCodec->id;
            audioCodecContext->codec_type = AudioCodec->type;
            audioCodecContext->time_base = new AVRational {num = 1, den = 30};
            audioCodecContext->framerate = new AVRational { num = 30, den = 1 };
            audioCodecContext->timecode_frame_start = 0;


            if ((_oFormatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            {
                videoCodecContextoutput->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                audioCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            //open codecd
            ffmpeg.avcodec_open2(videoCodecContextoutput, VideoCodec, null).ThrowExceptionIfError(); 
            ffmpeg.avcodec_open2(audioCodecContext, AudioCodec, null).ThrowExceptionIfError();

            ffmpeg.avcodec_parameters_from_context(VideoStream->codecpar, videoCodecContextoutput); 
            ffmpeg.avcodec_parameters_from_context(AudioStream->codecpar, audioCodecContext);


            VideoStream->time_base = videoCodecContextoutput->time_base;
            AudioStream->time_base = audioCodecContext->time_base;

            //Show some Information
            ffmpeg.av_dump_format(_oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&_oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            //Write File Header

            ffmpeg.avformat_write_header(_oFormatContext, null).ThrowExceptionIfError();
            oFormatContext = _oFormatContext;
        }


        public void Dispose()
        {
            var _oFormatContext = oFormatContext;

            //Write file trailer
            ffmpeg.av_write_trailer(_oFormatContext);
            ffmpeg.avformat_close_input(&_oFormatContext);

            //메모리 해제
            ffmpeg.avcodec_close(videoCodecContextoutput);
            ffmpeg.av_free(videoCodecContextoutput);
            ffmpeg.av_free(VideoCodec);

            ffmpeg.avcodec_close(audioCodecContext);
            ffmpeg.av_free(audioCodecContext);
            ffmpeg.av_free(AudioCodec);
        }
        int index = 1;

        public void TryEncodeNextPacket(AVFrame uncompressed_frame)
        {
            var encoded_packet = ffmpeg.av_packet_alloc();
            ffmpeg.av_init_packet(encoded_packet);
            
            try
            {
                int error;
                do
                {
                    if (uncompressed_frame.channels == 0)
                    {
                        ffmpeg.avcodec_send_frame(videoCodecContextoutput, &uncompressed_frame);

                        error = ffmpeg.avcodec_receive_packet(videoCodecContextoutput, encoded_packet);
                        enc_stream_index = encoded_packet->stream_index;

                        Console.WriteLine("videoPAcket");
                        encoded_packet->pts = (long)(ffmpeg.av_rescale_q(index, videoCodecContextoutput->time_base, oFormatContext->streams[enc_stream_index]->time_base));
                        encoded_packet->dts = ffmpeg.av_rescale_q(index, videoCodecContextoutput->time_base, oFormatContext->streams[enc_stream_index]->time_base);
                 
                        Console.WriteLine($"{encoded_packet->pts}   /  {encoded_packet->dts}");
                        error = 1;
                    }
                    else
                    {
                        //ffmpeg.av_audio_fifo_alloc(audioCodecContext->sample_fmt, audioCodecContext->channels, 1);
                        Console.WriteLine(audioCodecContext->time_base.num + "   //   " +audioCodecContext->time_base.den);

                       // ffmpeg.avcodec_send_frame(audioCodecContext, &uncompressed_frame);
                        error = 1;
                       
                       //  error = ffmpeg.avcodec_receive_packet(audioCodecContext, encoded_packet);
                        enc_stream_index = encoded_packet->stream_index;
                        
                        //Console.WriteLine("AudioPacket");
                        //encoded_packet->pts = (long)(ffmpeg.av_rescale_q(encoded_packet->pts, audioCodecContext->time_base, oFormatContext->streams[enc_stream_index]->time_base));
                        //encoded_packet->dts = ffmpeg.av_rescale_q(encoded_packet->dts, audioCodecContext->time_base, oFormatContext->streams[enc_stream_index]->time_base);

                    }

                    index++;
                    //Console.WriteLine("==========");
                    //Console.WriteLine(encoded_packet->pts + "/" + encoded_packet->dts);

                    //write frame in video file
                 //   ffmpeg.av_write_frame(oFormatContext, encoded_packet);
                 ffmpeg.av_interleaved_write_frame(oFormatContext, encoded_packet);
                } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN) || error == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF));
            }
            finally
            {
                ffmpeg.av_packet_unref(encoded_packet);
            }
        }

        public void FlushEncode()
        {
            ffmpeg.avcodec_send_frame(videoCodecContextoutput, null);
            ffmpeg.avcodec_send_frame(audioCodecContext, null);

        }


    }
}
