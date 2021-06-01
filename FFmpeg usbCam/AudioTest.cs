using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using FFmpeg_usbCam.FFmpeg;

namespace FFmpeg_usbCam
{
    public unsafe class AudioTest
    {
        private readonly AVFormatContext* _fmt_ctx;
        private int AudioIndex;
        private int VideoIndex;
        private  int audioStreamIndex;
        private AVCodecContext* audioCodeContext;
        private readonly AVFrame* receivedFrame;
        public SwrContext* swrCtx_Audio;

        public int out_channel_nb; 
        //Store pcm data
        public byte* out_buffer_audio;
        AVSampleFormat out_sample_fmt;

        public AudioTest()
        {
            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            _fmt_ctx = ffmpeg.avformat_alloc_context();
            receivedFrame = ffmpeg.av_frame_alloc();
        }
        public void testSet()
        {
            ffmpeg.avdevice_register_all();
            var fmt_ctx = _fmt_ctx;
            AVDictionary* avDict;
            ffmpeg.av_dict_set(&avDict, "reorder_queue_size", "1", 0);

            AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            string device = "video=USB3. 0 capture:audio=디지털 오디오 인터페이스(5- USB3. 0 capture)";
            var a = ffmpeg.avformat_open_input(&fmt_ctx, device, iformat, null); //음수이면 파일 안열려..그런 장치 없어!! 

            int videoIndex = -1;
            int audioIndex = -1;
            AVCodec* audioCodec = null;

            for (int index = 0; index < fmt_ctx->nb_streams; index++)
            {
                var avCodecContext = fmt_ctx->streams[index]->codec;
                if (avCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {

                }
                else if (avCodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioCodeContext = fmt_ctx->streams[index]->codec;
                    audioCodec = fmt_ctx->streams[index]->codec->codec;
                    audioIndex = index;
                }
            }



            var b = ffmpeg.avformat_find_stream_info(fmt_ctx, null);//Stream을 찾을수 없어...
            AVFormatContext* inputFmtCtx = fmt_ctx;
            var oCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_MP3);
            AVCodecContext* audioCodecContext;

            audioStreamIndex = ffmpeg.av_find_best_stream(fmt_ctx, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &audioCodec, 0);

            Console.WriteLine($" AudioStreamIndex :  {audioIndex}");

            audioCodecContext = ffmpeg.avcodec_alloc_context3(audioCodec);
            Console.WriteLine("!!");

            ffmpeg.avcodec_open2(audioCodeContext, audioCodec, null).ThrowExceptionIfError();  // 세번째 인수는 코덱으로 전달할 옵션값이며 필요 없으면 NULL로 지정한다.여기까지 진행하면 코덱과 컨텍스트가 모두 완비되어 패킷의 압축을 풀어 프레임 정보를 만들 준비가 되었다. 코덱을 다 사용한 후 다음 함수로 컨텍스트와 관련 메모리를 모두 해제한다.
            swrCtx_Audio = ffmpeg.swr_alloc();
            //Resampling setting options-------------------------------------------- ---------------start
            //Input sampling format
            AVSampleFormat in_sample_fmt = audioCodeContext->sample_fmt;
            //Output sampling format 16bit PCM
            out_sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            //Input sampling rate
            int in_sample_rate = audioCodeContext->sample_rate;
            //Sample rate of output
            int out_sample_rate = 8000;
            //Input channel layout
            long in_ch_layout =3;
            //Output channel layout
            int out_ch_layout = ffmpeg.AV_CH_LAYOUT_MONO;

            ffmpeg.swr_alloc_set_opts(swrCtx_Audio, out_ch_layout, out_sample_fmt, out_sample_rate, in_ch_layout, in_sample_fmt, in_sample_rate, 0, null);
            ffmpeg.swr_init(swrCtx_Audio);
            //Resampling setting options-------------------------------------------- ---------------end
            //Get the number of output channels
           out_channel_nb = ffmpeg.av_get_channel_layout_nb_channels((ulong)out_ch_layout);
            //Store pcm data
          out_buffer_audio = (byte*)ffmpeg.av_malloc(2 * 8000);
            // OpenOutputURL(@"C:\Users\admin\Desktop\audioTest.mp3");

            decodec_audio(_fmt_ctx,audioIndex,"afadf");



        }
        int out_buffer_size_audio;

        public void decodec_audio(AVFormatContext* pFmtCtx, int audioStreamIndex, string filePath)
        {
            Console.WriteLine("decodec");
            AVCodecContext* audioCodecContext = pFmtCtx->streams[audioStreamIndex]->codec;
            AVCodec* audioCodec = ffmpeg.avcodec_find_decoder(audioCodecContext->codec_id);
            Console.WriteLine(audioCodec->id);

            if (ffmpeg.avcodec_open2(audioCodecContext, audioCodec, null) < 0)
            {
                Console.WriteLine("Fail init decoder");
            }

            AVPacket pkt;
            AVFrame* audioFrame;

            int bGotDecoderFrame = 0;
            audioFrame = ffmpeg.av_frame_alloc();

            Task.Factory.StartNew(() =>
            {
                OpenOutputURL(@"C:\Users\admin\Desktop\audioTest.avi");
            });
            byte* out_audio_buffer = out_buffer_audio;

            while (ffmpeg.av_read_frame(pFmtCtx, &pkt) >= 0)
            {
                if (pkt.stream_index == audioStreamIndex)
                {
                    if (audioCodecContext->channel_layout == 0)
                    {
                        audioCodecContext->channel_layout = ffmpeg.AV_CH_FRONT_LEFT |ffmpeg.AV_CH_FRONT_RIGHT;
                    }
                    Console.WriteLine(audioCodecContext->channel_layout);
                    
                    var len= ffmpeg.swr_convert(swrCtx_Audio, &out_audio_buffer, 2 * 8000, (byte**)&audioFrame->data, audioFrame->nb_samples);
                    out_buffer_size_audio = ffmpeg.av_samples_get_buffer_size(null, out_channel_nb, audioFrame->nb_samples, out_sample_fmt, 1);
                    ffmpeg.avcodec_send_packet(audioCodecContext, &pkt);
                    ffmpeg.avcodec_receive_frame(audioCodecContext, audioFrame);
                    Console.WriteLine(len);
                    ffmpeg.av_write_frame(oFormatContext,&pkt);
                }

            }

        }
        int enc_stream_index;
        AVFormatContext* oFormatContext;
        AVCodecContext* audioCodecContext;
        AVCodec* AudioCodec;

        public void OpenOutputURL(string fileName)
        {
 
            AVStream* out_stream;
            Console.WriteLine("openoutput");
            //output file
            var _oFormatContext = oFormatContext;

            ffmpeg.avformat_alloc_output_context2(&_oFormatContext, null, null, fileName);
            Console.WriteLine("2");

            AudioCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_MP3);

            Console.WriteLine("3");
            out_stream = ffmpeg.avformat_new_stream(_oFormatContext, AudioCodec);
            Console.WriteLine("4");

            audioCodecContext = ffmpeg.avcodec_alloc_context3(AudioCodec);

            audioCodecContext->bit_rate = 128000;
            audioCodecContext->sample_rate = 48000;
            audioCodecContext->channels = 2;
            audioCodecContext->channel_layout = ffmpeg.AV_CH_LAYOUT_STEREO;
            audioCodecContext->frame_size = 1024;
            audioCodecContext->sample_fmt = AudioCodec->sample_fmts[0];
            audioCodecContext->profile = ffmpeg.FF_PROFILE_AAC_LOW;
            audioCodecContext->codec_id = AudioCodec->id;
            audioCodecContext->codec_type = AudioCodec->type;
            Console.WriteLine("5");

            ffmpeg.av_opt_set(audioCodecContext->priv_data, "profile", "baseline", 0);

            if ((_oFormatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            {
                audioCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            Console.WriteLine("6");
            //open codecd
            ffmpeg.avcodec_open2(audioCodecContext, AudioCodec, null).ThrowExceptionIfError();
            Console.WriteLine("7");

            ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, audioCodecContext);
            out_stream->time_base = audioCodecContext->time_base;

            //Show some Information
            ffmpeg.av_dump_format(_oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&_oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }
            Console.WriteLine("8");
            //Write File Header
            ffmpeg.avformat_write_header(_oFormatContext, null).ThrowExceptionIfError();
            oFormatContext = _oFormatContext;


            ////Write file trailer
            //ffmpeg.av_write_trailer(_oFormatContext);
            //ffmpeg.avformat_close_input(&_oFormatContext);

            ////메모리 해제
            //ffmpeg.avcodec_close(audioCodecContext);
            //ffmpeg.av_free(audioCodecContext);
            //ffmpeg.av_free(AudioCodec);

            //Console.WriteLine("9");

        }

    }
}
