using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using FFmpeg_usbCam.FFmpeg;


namespace FFmpeg_usbCam
{
    public unsafe class changecontainer
    {


        public void changecontainerset()
        {
            AVFormatContext* input_format_context = null;
            AVFormatContext* output_format_context = null;
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
            ffmpeg.avdevice_register_all();
            input_format_context = ffmpeg.avformat_alloc_context();

            AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            string device = "video=USB3. 0 capture:audio=디지털 오디오 인터페이스(5- USB3. 0 capture)";


            var a = ffmpeg.avformat_open_input(&input_format_context, device, iformat, null); //음수이면 파일 안열려..그런 장치 없어!! 
            var b = ffmpeg.avformat_find_stream_info(input_format_context, null);//Stream을 찾을수 없어...

            var fileName = @"C:\Users\admin\Desktop\changeContainer.avi";
            ffmpeg.avformat_alloc_output_context2(&output_format_context, null, null, fileName);
            var number_of_streams = input_format_context->nb_streams;
          
            var streams_list = new int[2];
            int stream_index = 0;


            for (int i = 0; i < input_format_context->nb_streams; i++)
            {
                AVStream* out_stream;
                AVStream* in_stream = input_format_context->streams[i];
                AVCodecParameters* in_codecpar = in_stream->codecpar;
                Console.WriteLine(in_codecpar->codec_id);
             
                if (in_codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_VIDEO &&
                    in_codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO )
                {
                    streams_list[i] = -1;
                    continue;
                }
                streams_list[i] = stream_index++;

                out_stream =ffmpeg.avformat_new_stream(output_format_context, null);

                var ret = ffmpeg.avcodec_parameters_copy(out_stream->codecpar, in_codecpar);
            }

            if (ffmpeg.avio_open(&output_format_context->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            ffmpeg.avformat_write_header(output_format_context,null);

            output_format_context->streams[0]->time_base = new AVRational {num = 1, den = 30};
            output_format_context->streams[0]-> codec->time_base = new AVRational { num = 1, den = 30 };
            output_format_context->streams[0]->codec->framerate = new AVRational { num = 30, den =1 };
            int index = 1;
            while (index<1000)
            {
                AVStream* in_stream;
                AVStream *out_stream;
                AVPacket packet;
               var ret =ffmpeg.av_read_frame(input_format_context, &packet);
                if (ret < 0)
                    break;
                in_stream = input_format_context->streams[packet.stream_index];

                if (packet.stream_index == 0)
                {
                    in_stream->codec->time_base = new AVRational { num = 1, den = 30 };
                    in_stream->codec->framerate= new AVRational {num = 30, den = 1};
                    in_stream->r_frame_rate= new AVRational { num = 30, den = 1 };
                    output_format_context->streams[0]->r_frame_rate = new AVRational { num = 30, den = 1 };
                }
                if (packet.stream_index >= number_of_streams || streams_list[packet.stream_index] < 0)
                {
                   ffmpeg.av_packet_unref(&packet);
                    continue;
                }
                packet.stream_index = streams_list[packet.stream_index];

                out_stream = output_format_context->streams[packet.stream_index];
               
                Console.WriteLine(output_format_context->streams[0]->time_base.num + "/ "+ output_format_context->streams[0]->time_base.den);
                if (packet.stream_index == 0)
                {
                    packet.pts = index;
                    packet.dts = index;
                }
                else
                {
                }

                packet.pts = ffmpeg.av_rescale_q_rnd(packet.pts, output_format_context->streams[packet.stream_index]->codec->time_base, output_format_context->streams[packet.stream_index]->time_base, AVRounding.AV_ROUND_INF | AVRounding.AV_ROUND_PASS_MINMAX);
                packet.dts = ffmpeg.av_rescale_q_rnd(packet.dts, output_format_context->streams[packet.stream_index]->codec->time_base, output_format_context->streams[packet.stream_index]->time_base, AVRounding.AV_ROUND_INF | AVRounding.AV_ROUND_PASS_MINMAX);


                Console.WriteLine(output_format_context->streams[packet.stream_index]->codec->time_base.den);
                ///* copy packet */

                Console.WriteLine($"Packet {packet.pts} / {packet.dts} ");

                //Console.WriteLine($"Packet {packet.pts} / {packet.dts} ");
                //Console.WriteLine($"Packet {packet.pts} / {packet.dts} ");
                index++;


                var ret1 =ffmpeg.av_interleaved_write_frame(output_format_context, &packet);
                if (ret < 0)
                {
                    Console.WriteLine("write error");
                }
                //av_packet_unref(&packet);
            }
            ffmpeg.av_write_trailer(output_format_context);

        }

    }
}
