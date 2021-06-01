using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpeg_usbCam.FFmpeg
{
    public unsafe class MirroringHelper
    { 
        private string deviceName = "video=USB3. 0 capture:audio=디지털 오디오 인터페이스(5- USB3. 0 capture)";

        public MirroringHelper()
        {
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
        }

        public void InitVideoAndAudio(string video, string audio)
        {
            FFmpegBinariesHelper.RegisterFFmpegBinaries();

        }












        public void GetVideoAndAudio()
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = @"C:\Users\admin\Desktop\ffmpeg.exe",
                Arguments = @"-list_devices true -f dshow -i dummy",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            var p = new Process();
            p.StartInfo = startInfo;
            p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                //  Console.WriteLine(e);
                //   Console.WriteLine(e.Data);
            });
            p.Start();

            string output1 = "";
            if (startInfo.RedirectStandardOutput)
            {
                // using (var reader = p.StandardOutput) 
                //  output1 = reader.BaseStream.ToString();
            }

            if (startInfo.RedirectStandardError)
            {

                var stream = p.StandardError.BaseStream;
                int read = 0;
                bool startDevice = false;
                do
                {
                    byte[] buf = new byte[1024];
                    read = stream.Read(buf, 0, buf.Length);

                    int i = 0;
                    for (i = 0; i < buf.Length; i++)
                    {
                        if (buf[i] == 0x00)
                        {
                            break;
                        }
                    }

                    byte[] dest = new byte[i];
                    Array.Copy(buf, dest, dest.Length);

                    if (startDevice == false && Encoding.UTF8.GetString(dest).StartsWith("[dshow"))
                    {
                        startDevice = true;
                    }

                    if (startDevice)
                    {
                        output1 += Encoding.UTF8.GetString(dest);
                    }


                } while (read > 0);

            }

            var outputsplit = output1.Split('\n');

            foreach (var VARIABLE in outputsplit)
            {
                var splitt = VARIABLE.Split(']');
                if (splitt.Length == 2)
                {
                    if (splitt[1].StartsWith("  \""))
                    {
                        Console.WriteLine(splitt[1]);
                    }
                }
            }
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            Console.WriteLine(output);
        }
    }
}
