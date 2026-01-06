// Version: 0.0.0.1
using FFmpeg.AutoGen;

using static FFmpeg.AutoGen.ffmpeg;

namespace FFmpegApi
{
    public static unsafe class FFmpegHelper
    {
        public static void Register()
        {
            av_log_set_level(AV_LOG_DEBUG);
            avformat_network_init();
        }
    }
}
