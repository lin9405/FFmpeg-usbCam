using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FFmpeg.AutoGen;
using FFmpeg_usbCam.FFmpeg.Decoder;
using System.Drawing;
using System.IO;
using FFmpeg_usbCam.FFmpeg;
using System.Windows.Interop;
using System.Drawing.Imaging;

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

        private bool activeThread;      //thread 활성화 유무

        public MainWindow()
        {
            InitializeComponent();

            //비디오 프레임 디코딩 thread 생성
            ts = new ThreadStart(DecodeAllFramesToImages);
            thread = new Thread(ts);

            //FFmpeg dll 파일 참조 경로 설정
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
            //SetupLogging();

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

        private static unsafe void SetupLogging()
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
            };

            ffmpeg.av_log_set_callback(logCallback);
        }

        private unsafe void DecodeAllFramesToImages()
        {
            //video="웹캠 디바이스 이름"
            //string device = "video=AVerMedia GC550 Video Capture";
            string device = "rtsp://184.72.239.149/vod/mp4:BigBuckBunny_115k.mov";

            using (var vsd = new VideoStreamDecoder(device))
            {
                var info = vsd.GetContextInfo();
                info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

                var sourceSize = vsd.FrameSize;
                var sourcePixelFormat = vsd.PixelFormat;
                var destinationSize = sourceSize;
                var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                {
                    var frameNumber = 0;
                    while (vsd.TryDecodeNextFrame(out var frame) && activeThread)
                    {
                        var convertedFrame = vfc.Convert(frame);

                        Bitmap bitmap;

                        bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]);
                        BitmapToImageSource(bitmap);

                        frameNumber++;
                    }
                }
            }
        }

        void BitmapToImageSource(Bitmap bitmap)
        {
            //UI thread에 접근하기 위해 dispatcher 사용
            dispatcher.BeginInvoke((Action)(() =>
            {
                if (thread.IsAlive)
                {
                    using (MemoryStream memory = new MemoryStream())
                    {
                        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                        memory.Position = 0;
                        BitmapImage bitmapimage = new BitmapImage();
                        bitmapimage.BeginInit();

                        //memory.Seek(0, SeekOrigin.Begin);
                        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapimage.StreamSource = memory;
                        bitmapimage.EndInit();

                        image.Source = bitmapimage;     //image 컨트롤에 웹캠 이미지 표시

                        memory.Dispose();
                        GC.Collect();


                        //image.Source = ConvertBitmap(bitmap);

                    }
                }
                
            }));

        }

        public BitmapImage ConvertBitmap(System.Drawing.Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();

            return image;
        }


        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public System.Windows.Media.ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
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
