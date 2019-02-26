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
using System.Windows.Interop;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FFmpeg_usbCam
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread thread;
        ThreadStart ts;
        Dispatcher dispatcher = Application.Current.Dispatcher;

        ConcurrentQueue<Bitmap> decodedFrameQueue = new ConcurrentQueue<Bitmap>();
        AutoResetEvent decodedFrameSignal = new AutoResetEvent(true);
        Bitmap queueImage;

        string outputFileName = "out.h264";
        System.Drawing.Size sourceSize;
        AVPixelFormat sourcePixelFormat;
        System.Drawing.Size destinationSize;
        AVPixelFormat destinationPixelFormat;
        
        private bool activeThread;      //thread 활성화 유무

        public MainWindow()
        {
            InitializeComponent();
            
            //FFmpeg dll 파일 참조 경로 설정
            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            Console.WriteLine("Current directory: " + Environment.CurrentDirectory);
            Console.WriteLine("Runnung in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
            Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");

            SetupLogging();
            
            // Start send queue worker
            ThreadPool.QueueUserWorkItem(
                new WaitCallback(EncodeImagesToH264));
            
            //비디오 프레임 디코딩 thread 생성
            ts = new ThreadStart(DecodeAllFramesToImages);
            thread = new Thread(ts);

            activeThread = true;
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            //thread 시작 
            if (thread.ThreadState == ThreadState.Unstarted)
            {
                thread.Start();
            }
        }

        long pts;
        int frameNumber = 0;
        private unsafe void DecodeAllFramesToImages()
        {
            //video="웹캠 디바이스 이름"
            string url = "video=AVerMedia GC550 Video Capture";
            //string url = "rtsp://184.72.239.149/vod/mp4:BigBuckBunny_115k.mov";
            
            using (var vsd = new VideoStreamDecoder(url))
            {
                using (var vse = new H264VideoStreamEncoder())
                {
                    vse.OpenOutputURL("test.mp4");

                    var info = vsd.GetContextInfo();
                    info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

                    sourceSize = vsd.FrameSize;
                    sourcePixelFormat = vsd.PixelFormat;
                    destinationSize = sourceSize;
                    destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;

                    using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                    {
                        while (vsd.TryDecodeNextFrame(out var frame) && activeThread)
                        {
                            pts = frame.pts;

                            var convertedFrame = vfc.Convert(frame);

                            Bitmap bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]);
                            

                            var enc_sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                            var enc_destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;

                            using (var vfc2 = new VideoFrameConverter(sourceSize, enc_sourcePixelFormat, destinationSize, enc_destinationPixelFormat))
                            {
                                var enc_convertedFrame = vfc2.Convert(frame);
                                enc_convertedFrame.pts = frameNumber++;

                                vse.TryEncodeNextPacket(enc_convertedFrame);
                            }

                            BitmapToImageSource(bitmap);
                        }
                    }

                    vse.FlushEncode();
                } 
            }
        }

        
        private unsafe void EncodeImagesToH264(object state)
        {
            while (activeThread)
            {
                if (decodedFrameQueue.TryDequeue(out queueImage))
                {
                    var sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                    destinationSize = sourceSize;
                    var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;

                    using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                    {
                        using (var fs = File.Open(outputFileName, FileMode.Create)) // be advise only ffmpeg based player (like ffplay or vlc) can play this file, for the others you need to go through muxing
                        {
                            using (var vse = new H264VideoStreamEncoder())
                            {
                                byte[] bitmapData;

                                bitmapData = GetBitmapData(queueImage);

                                fixed (byte* pBitmapData = bitmapData)
                                {
                                    var data = new byte_ptrArray8 { [0] = pBitmapData };
                                    var linesize = new int_array8 { [0] = bitmapData.Length / sourceSize.Height };
                                    var frame = new AVFrame
                                    {
                                        data = data,
                                        linesize = linesize,
                                        height = sourceSize.Height
                                    };

                                    var convertedFrame = vfc.Convert(frame);
                                    convertedFrame.pts = frameNumber * 25;
                                    vse.TryEncodeNextPacket(convertedFrame);
                                }

                                Console.WriteLine($"frame: {frameNumber}");
                                frameNumber++;
                            }
                        }
                    }
                    
                    decodedFrameSignal.WaitOne();
                }
                else
                {
                    // Queue is empty, sleep until signalled
                    decodedFrameSignal.WaitOne();
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
                if (thread.IsAlive)
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
                        GC.Collect();
                    }
                }

            }));
        }

        private byte[] GetBitmapData(Bitmap frameBitmap)
        {
            var bitmapData = frameBitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, frameBitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var length = bitmapData.Stride * bitmapData.Height;
                var data = new byte[length];
                Marshal.Copy(bitmapData.Scan0, data, 0, length);
                return data;
            }
            finally
            {
                frameBitmap.UnlockBits(bitmapData);
            }
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
            if (thread.IsAlive)
            {
                activeThread = false;
                thread.Join();
            }
        }
    }
}
