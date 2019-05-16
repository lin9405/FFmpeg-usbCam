using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FFmpeg.AutoGen;
using System.Drawing;
using System.IO;
using FFmpeg_usbCam.FFmpeg;
using System.Drawing.Imaging;
using System.Collections.Concurrent;

namespace FFmpeg_usbCam
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;

        Thread decodingThread;
        Thread encodingThread;
        ThreadStart decodingThreadStart;
        ThreadStart encodingThreadStart;

        ManualResetEvent pauseEvent = new ManualResetEvent(false);

        ConcurrentQueue<AVFrame> decodedFrameQueue = new ConcurrentQueue<AVFrame>();
        AVFrame queueFrame;
        
        System.Drawing.Size sourceSize;
        System.Drawing.Size destinationSize;

        bool activeThread;      
        bool activeEncodingThread;

        int frameNumber = 0;

        public MainWindow()
        {
            InitializeComponent();

            //FFmpeg dll 파일 참조 경로 설정
            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            Console.WriteLine("Current directory: " + Environment.CurrentDirectory);
            Console.WriteLine("Runnung in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
            Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");

            SetupLogging();

            //비디오 프레임 디코딩 thread 생성
            decodingThreadStart = new ThreadStart(DecodeAllFramesToImages);
            decodingThread = new Thread(decodingThreadStart);

            //비디오 프레임 인코딩 thread 생성
            encodingThreadStart = new ThreadStart(EncodeImagesToH264);
            encodingThread = new Thread(encodingThreadStart);
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            //thread 시작 
            if (decodingThread.ThreadState == ThreadState.Unstarted)
            {
                activeThread = true;
                decodingThread.Start();
            }
        }
        
        
        private unsafe void DecodeAllFramesToImages()
        {
            //video="웹캠 디바이스 이름"
            //string url = "video=AVerMedia GC550 Video Capture";

            //sample rtsp source
            string url = "rtsp://184.72.239.149/vod/mp4:BigBuckBunny_115k.mov";

            using (var vsd = new VideoStreamDecoder(url))
            {
                var info = vsd.GetContextInfo();
                info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

                sourceSize = vsd.FrameSize;
                destinationSize = sourceSize;
                var sourcePixelFormat = vsd.PixelFormat;
                var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;

                using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                {
                    while (vsd.TryDecodeNextFrame(out var frame) && activeThread)
                    {
                        var convertedFrame = vfc.Convert(frame);

                        Bitmap bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]);

                        if (activeEncodingThread)
                        {
                            decodedFrameQueue.Enqueue(convertedFrame);
                        }

                        //display video image
                        BitmapToImageSource(bitmap);
                    }
                }
            }
        }

        private unsafe void EncodeImagesToH264()
        {
            while (pauseEvent.WaitOne())
            {
                if (decodedFrameQueue.TryDequeue(out queueFrame))
                {
                    var sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                    var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P; //for h.264

                    using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                    {
                        var convertedFrame = vfc.Convert(queueFrame);
                        convertedFrame.pts = frameNumber * 2;       //to do
                        h264Encoder.TryEncodeNextPacket(convertedFrame);
                    }

                    frameNumber++;
                }
            }

        }
        
        void BitmapToImageSource(Bitmap bitmap)
        {
            Bitmap b = new Bitmap(bitmap);
            bitmap.Dispose();
            bitmap = null;

            //UI thread에 접근하기 위해 dispatcher 사용
            dispatcher.BeginInvoke((Action)(() =>
            {
                if (decodingThread.IsAlive)
                {
                    using (MemoryStream memory = new MemoryStream())
                    {
                        b.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                        memory.Position = 0;
                        BitmapImage bitmapimage = new BitmapImage();
                        bitmapimage.BeginInit();
                        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapimage.StreamSource = memory;
                        bitmapimage.EndInit();

                        image.Source = bitmapimage;     //image 컨트롤에 웹캠 이미지 표시

                        memory.Dispose();
                    }
                }

            }));
        }

        private unsafe void SetupLogging()
        {
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_VERBOSE);

            // do not convert to local function
            av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
            {
                if (level > ffmpeg.av_log_get_level()) return;

                var lineSize = 1024;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(line);
                Console.ResetColor();
            };

            ffmpeg.av_log_set_callback(logCallback);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (decodingThread.IsAlive)
            {
                activeThread = false;
                decodingThread.Join();
            }

            if (encodingThread.IsAlive)
            {
                if (activeEncodingThread)
                {
                    h264Encoder.FlushEncode();
                    h264Encoder.Dispose();

                    pauseEvent.Reset();
                    activeEncodingThread = false;
                }

                encodingThread.Abort();
                pauseEvent.Dispose();
            }
        }

        H264VideoStreamEncoder h264Encoder;
        bool isFirstRecord = true;

        private void Record_Button_Checked(object sender, RoutedEventArgs e)
        {
            h264Encoder = new H264VideoStreamEncoder();

            string videoName = DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss") + ".mp4";

            //initialize output format&codec
            h264Encoder.OpenOutputURL(videoName);

            //start video recode
            activeEncodingThread = true;

            if (isFirstRecord)
            {
                encodingThread.Start();
                isFirstRecord = false;
            }

            pauseEvent.Set();
        }

        private void Record_Button_Unchecked(object sender, RoutedEventArgs e)
        {
            activeEncodingThread = false;
            pauseEvent.Reset();

            h264Encoder.FlushEncode();
            h264Encoder.Dispose();

            frameNumber = 0;
        }
    }
}
