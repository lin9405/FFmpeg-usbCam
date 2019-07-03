using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using FFmpeg.AutoGen;

namespace FFmpeg_usbCam.FFmpeg
{
    public class EncodingInfo
    {
        public int Width;
        public int Height;
        public AVRational Sample_aspect_ratio;
        public AVRational Timebase;
        public AVRational Framerate;
    }
}
