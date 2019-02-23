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
        
        private unsafe void DecodeAllFramesToImages()
        {
            //video="웹캠 디바이스 이름"
            //string url = "video=AVerMedia GC550 Video Capture";
            string url = "rtsp://184.72.239.149/vod/mp4:BigBuckBunny_115k.mov";
            string outputFileName = "test.mp4";

            using (var vsd = new VideoStreamDecoder())
            {
                vsd.OpenInputURL(url);
                vsd.OpenOutputURL(outputFileName);

                var info = vsd.GetContextInfo();
                info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

                var sourceSize = vsd.FrameSize;
                var sourcePixelFormat = vsd.PixelFormat;
                var destinationSize = sourceSize;
                var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;

                vsd.VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);
                
                while (activeThread)
                {
                    var frame = vsd.TryDecodeNextFrame();
                    var convertedFrame = vsd.Convert(frame);

                    Bitmap bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]);
                    BitmapToImageSource(bitmap);

                    vsd.TryEncodeNextPacket(&frame);

                }

                vsd.FlushEncode();
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
