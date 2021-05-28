using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using FFmpeg_usbCam.FFmpeg;

namespace FFmpeg_usbCam
{
    public unsafe class test
    {
        struct FileContext
        {
            AVFormatContext* _fmt_ctx;
            int AudioIndex;
            int VideoIndex;
        }

        private FileContext inputFileContext;

        public AVFormatContext* _fmt_ctx;
        public AVFormatContext* _output_fmt_cts;
        private int AudioIndex;
        private int VideoIndex;

        public int create_output(string fileName)
        {
            AVFormatContext* outputFmtCtx = null;
            AudioIndex = -1;
            VideoIndex = -1;
            string filename = @"C:\Users\admin\Desktop\output223423423.avi";
            AVFormatContext* inputFmtCtx = _fmt_ctx;


            if (ffmpeg.avformat_alloc_output_context2(&outputFmtCtx, null, null, filename) < 0) //음수가 나오면 에러인거야...
            {
                Console.WriteLine("파일 생성 못해!!!");
            }

            var out_index = 0;
            // this copy video/audio streams from input video.
            for (int index = 0; index < outputFmtCtx->nb_streams; index++)
            {
                AVStream* in_stream = _fmt_ctx->streams[index];
                AVCodecContext* in_codec_ctx = in_stream->codec;

                AVStream* out_stream = ffmpeg.avformat_new_stream(outputFmtCtx, in_codec_ctx->codec);

                if (out_stream == null)
                {
                    return -2;
                }

                AVCodecContext* outCodecContext = out_stream->codec;
                if (ffmpeg.avcodec_copy_context(outCodecContext, in_codec_ctx) < 0)
                {
                    return -3;
                }


                out_stream->time_base = in_stream->time_base;
                // Remove codec tag info for compatibility with ffmpeg.
                outCodecContext->codec_tag = 0;

             
                outCodecContext->flags |=ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                
            }


    //           if(index == inputFile.v_index)
    //{
    //  outputFile.v_index = out_index++;
    //}
    //else
    //{
    //  outputFile.a_index = out_index++;
    //}







            return 0;
        }

        public void CreateOutput()
        {
            AVFormatContext* outputFmtCtx;
            AudioIndex = -1;
            VideoIndex = -1;
            string filename = @"C:\Users\admin\Desktop\output223423423.avi";
            AVFormatContext* inputFmtCtx = _fmt_ctx;

            if (ffmpeg.avformat_alloc_output_context2(&outputFmtCtx, null, null, filename) <0) //음수가 나오면 에러인거야...
            {
                Console.WriteLine("파일 생성 못해!!!");
            }
            var  oCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

            for (int index = 0; index < inputFmtCtx->nb_streams; index++)
            {

                AVStream* in_stream = inputFmtCtx->streams[index];
                AVCodecContext* in_codec_ctx = in_stream->codec;
                in_codec_ctx = ffmpeg.avcodec_alloc_context3(inputFmtCtx->data_codec);
                AVStream* out_stream = ffmpeg.avformat_new_stream(outputFmtCtx, null);

                if (out_stream == null)
                {
                    Console.WriteLine("OUTPUT 스트림 NULL");
                }

                //
                AVCodecContext* outCodecContext = out_stream->codec;
                outCodecContext->codec = oCodec;
                outCodecContext = ffmpeg.avcodec_alloc_context3(oCodec);

                outCodecContext->height = 500;
                outCodecContext->width = 600;
                //  outCodecContext->sample_aspect_ratio = videoInfo.Sample_aspect_ratio;
                outCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
                outCodecContext->time_base = new AVRational { num = 1, den = 15 };
                //   outCodecContext->framerate = ffmpeg.av_inv_q(videoInfo.Framerate);

                //context를 설정해야 뭔가 쓸수잇오.....


                if (ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, outCodecContext) < 0)
                {
                    Console.WriteLine("copy 못해에!!!");
                }

                out_stream->time_base = in_stream->time_base;

                outCodecContext->codec_tag = 0;


                if ((outputFmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) == 0)
                {
                    outCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                }
                //  ffmpeg.avcodec_open2(outCodecContext, oCodec, null).ThrowExceptionIfError();

                VideoIndex = 0;
                AudioIndex = 1;
                ffmpeg.av_dump_format(outputFmtCtx, 0, filename, 1);

                if ((outputFmtCtx->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
                {
                    // This actually open the file
                    if (ffmpeg.avio_open(&outputFmtCtx->pb, filename, ffmpeg.AVIO_FLAG_WRITE) < 0)
                    {
                        Console.WriteLine("못만들오...");
                    }
                }
                if (ffmpeg.avformat_write_header(outputFmtCtx, null) < 0)
                {
                    Console.WriteLine("헤더를 못써...\n");
                }
            }
            ffmpeg.av_write_trailer(outputFmtCtx);
            ffmpeg.avio_closep(&outputFmtCtx->pb);
            ffmpeg.avformat_free_context(outputFmtCtx);
        }


        public void testSet()
        {
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
            ffmpeg.avdevice_register_all();
            var fmt_ctx = _fmt_ctx;
            fmt_ctx = ffmpeg.avformat_alloc_context();

            AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            string device = "video=USB3. 0 capture:audio=디지털 오디오 인터페이스(5- USB3. 0 capture)";
            //string file = @"C:\Users\admin\Desktop\out2.avi";
            //var a = ffmpeg.avformat_open_input(&fmt_ctx,file, null, null); //음수이면 파일 안열려..그런 장치 없어!! 

            var a = ffmpeg.avformat_open_input(&fmt_ctx, device, iformat, null); //음수이면 파일 안열려..그런 장치 없어!! 
            var b= ffmpeg.avformat_find_stream_info(fmt_ctx, null);//Stream을 찾을수 없어...
            int videoIndex = -1;
            int audioIndex = -1;
            _fmt_ctx = fmt_ctx;
            AVFormatContext* outputFmtCtx;

                AudioIndex = -1;
                VideoIndex = -1;
                string filename = @"C:\Users\admin\Desktop\output223423423.avi";
                AVFormatContext* inputFmtCtx = _fmt_ctx;

                if (ffmpeg.avformat_alloc_output_context2(&outputFmtCtx, null, null, filename) < 0) //음수가 나오면 에러인거야...
                {
                    Console.WriteLine("파일 생성 못해!!!");
                }
                var oCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_MPEG4);

                for (int index = 0; index < inputFmtCtx->nb_streams; index++)
                {

                    AVStream* in_stream = inputFmtCtx->streams[index];
                    AVCodecContext* in_codec_ctx = in_stream->codec;
                    in_codec_ctx = ffmpeg.avcodec_alloc_context3(inputFmtCtx->data_codec);
                    AVStream* out_stream = ffmpeg.avformat_new_stream(outputFmtCtx, null);

                    if (out_stream == null)
                    {
                        Console.WriteLine("OUTPUT 스트림 NULL");
                    }

                    //
                    AVCodecContext* outCodecContext = out_stream->codec;
                    outCodecContext->codec = oCodec;
                    outCodecContext = ffmpeg.avcodec_alloc_context3(oCodec);

                    outCodecContext->height = 500;
                    outCodecContext->width = 600;
                    //  outCodecContext->sample_aspect_ratio = videoInfo.Sample_aspect_ratio;
                    outCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
                    outCodecContext->time_base = new AVRational { num = 1, den = 15 };
                    //   outCodecContext->framerate = ffmpeg.av_inv_q(videoInfo.Framerate);

                    //context를 설정해야 뭔가 쓸수잇오.....


                    if (ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, outCodecContext) < 0)
                    {
                        Console.WriteLine("copy 못해에!!!");
                    }

                    out_stream->time_base = in_stream->time_base;

                    outCodecContext->codec_tag = 0;


                    if ((outputFmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) == 0)
                    {
                        outCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                    }
                    //  ffmpeg.avcodec_open2(outCodecContext, oCodec, null).ThrowExceptionIfError();

                    VideoIndex = 0;
                    AudioIndex = 1;
                    ffmpeg.av_dump_format(outputFmtCtx, 0, filename, 1);

                    if ((outputFmtCtx->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
                    {
                        // This actually open the file
                        if (ffmpeg.avio_open(&outputFmtCtx->pb, filename, ffmpeg.AVIO_FLAG_WRITE) < 0)
                        {
                            Console.WriteLine("못만들오...");
                        }
                    }
                    if (ffmpeg.avformat_write_header(outputFmtCtx, null) < 0)
                    {
                        Console.WriteLine("헤더를 못써...\n");
                    }
                }
                //ffmpeg.av_write_trailer(outputFmtCtx);
                //ffmpeg.avio_closep(&outputFmtCtx->pb);
                //ffmpeg.avformat_free_context(outputFmtCtx);
        
            //nb_streams : 요소 몇갠지..!!! 내가 찾은거에서 뭐있는지 
            for (int index = 0; index < fmt_ctx->nb_streams; index++)
            {
                var avCodecContext = fmt_ctx->streams[index]->codec;
                if (avCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoIndex = index;
                }
                else if (avCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioIndex = index;
                    Console.WriteLine(audioIndex + "***");

                }
                if (avCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoIndex = index;
                    Console.WriteLine($"====================={avCodecContext->codec_type}======================");
                    //Console.WriteLine(avCodecContext->bit_rate); //W * H *FPS
                    //Console.WriteLine(avCodecContext->codec_id);
                    //Console.WriteLine(avCodecContext->width);
                    //Console.WriteLine(avCodecContext->coded_width);
                    //Console.WriteLine(avCodecContext->height);
                    //Console.WriteLine(avCodecContext->coded_height);
                    //Console.WriteLine(avCodecContext->pts_correction_num_faulty_pts);
                    //Console.WriteLine(avCodecContext->pts_correction_last_dts);
                    //Console.WriteLine(avCodecContext->pts_correction_last_pts);
                    Console.WriteLine();
                }
                else if (avCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioIndex = index;
                    Console.WriteLine($"====================={avCodecContext->codec_type}======================");
                    //Console.WriteLine(avCodecContext->bit_rate); //W * H *FPS
                    //Console.WriteLine(avCodecContext->codec_id);
                    //Console.WriteLine($"Channels :  {avCodecContext->channels}");
                    //Console.WriteLine(avCodecContext->width);
                    //Console.WriteLine(avCodecContext->coded_width);
                    //Console.WriteLine(avCodecContext->height);
                    //Console.WriteLine(avCodecContext->coded_height);
                    //Console.WriteLine(avCodecContext->pts_correction_num_faulty_pts);
                    //Console.WriteLine(avCodecContext->pts_correction_last_dts);
                    //Console.WriteLine(avCodecContext->pts_correction_last_pts);
                }
            }

            int ret;
            AVPacket pkt;
            int out_stream_index;
            while (true)
            {
                ret = ffmpeg.av_read_frame(fmt_ctx, &pkt); //ret == 0 이면 

                if (ret == ffmpeg.AVERROR_EOF)
                {
                    Console.WriteLine("frame end");
                    break;
                }

                if (pkt.stream_index == videoIndex)
                {
                    Console.WriteLine("Video Packet");
                }
                else if (pkt.stream_index == audioIndex)
                {
                    Console.WriteLine("Audio Packet");
                }
                
                AVStream* in_stream = fmt_ctx->streams[pkt.stream_index];
                out_stream_index = (pkt.stream_index == videoIndex) ? videoIndex : audioIndex;
                AVStream* out_stream = outputFmtCtx->streams[out_stream_index];

                ffmpeg.av_packet_rescale_ts(&pkt, in_stream->time_base, out_stream->time_base);


                pkt.stream_index = out_stream_index;

                if (ffmpeg.av_interleaved_write_frame(outputFmtCtx, &pkt) < 0)
                {
                    Console.WriteLine("!!!!!!!!@#####!@#!@#!");
                    break;
                }

                ffmpeg.av_packet_unref(&pkt); //옛날엔 av_free_packet()

            }

            ffmpeg.av_write_trailer(outputFmtCtx);

        }

    }
}
