using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FFmpeg.AutoGen;
using FFmpeg_usbCam.FFmpeg;

namespace FFmpeg_usbCam
{
    public enum VTYPE
    {
        RTSP_RTP = 0,
        CAM
    }

    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;

        EasyFFmpegManager easyFFmpeg;

        public MainWindow()
        {
            InitializeComponent();
            //var test = new test();
            //Task.Factory.StartNew(() =>
            //{
            //    test.testSet();

            //});
              easyFFmpeg = new EasyFFmpegManager();
        }
        private void Play_Button_Click1(object sender, RoutedEventArgs e)
        {
            easyFFmpeg.setAudio();
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            string url = "video=USB3. 0 capture:audio=디지털 오디오 인터페이스(5- USB3. 0 capture)";
            int type = VType_ComboBox.SelectedIndex;

            easyFFmpeg.InitializeFFmpeg(url, (VIDEO_INPUT_TYPE)type);

            easyFFmpeg.PlayVideo();
            easyFFmpeg.VideoFrameReceived += VideoFrameReceived;
        }

        private void VideoFrameReceived(BitmapImage frame)
        {
            dispatcher.BeginInvoke((Action)(() =>
            {
                image.Source = frame;
            }));
        }

        private void Record_Button_Checked(object sender, RoutedEventArgs e)
        {
            string fileName =DateTime.Now.ToString("yyMMdd_hh.mm.ss") + ".mp4";
            fileName = @"C:\Users\admin\Desktop\1.avi";
            easyFFmpeg.RecordVideo(fileName);
        }

        private void Record_Button_Unchecked(object sender, RoutedEventArgs e)
        {
            easyFFmpeg.StopRecord();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            easyFFmpeg.DisposeFFmpeg();
        }

        private void Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            Record_Button.IsChecked = false;
            easyFFmpeg.DisposeFFmpeg();
        }
    }
}
