using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using FFmpeg.AutoGen;

namespace FFmpeg_usbCam.FFmpeg
{
    public unsafe abstract class GenericVideoStreamManager : IDisposable
    {
        public abstract void Dispose();
        
        /// <summary>
        /// for Video Stream Decoding
        /// </summary>
        protected static AVCodecContext* iCodecContext;
        protected static AVFormatContext* iFormatContext;
        protected int dec_stream_index;
        protected AVFrame* decodedFrame;
        protected AVPacket* rawPacket;

        /// <summary>
        /// for Video Stream Encoding
        /// </summary>
        protected int enc_stream_index;
        protected AVFormatContext* oFormatContext;
        protected AVCodecContext* oCodecContext;
    }
}
