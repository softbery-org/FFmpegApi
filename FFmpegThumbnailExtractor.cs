// Version: 0.0.0.7
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using FFmpeg.AutoGen;

using FFmpegApi.Logs;

namespace FFmpegApi
{
    public static unsafe class FFmpegThumbnailExtractor
    {
        public static WriteableBitmap Extract(string filePath, TimeSpan time, int targetWidth, int targetHeight, CancellationToken ct)
        {
            AVFormatContext* fmt = null;
            AVCodecContext* videoCtx = null;
            AVFrame* frame = null;
            AVPacket* packet = null;
            SwsContext* sws = null;

            try
            {
                ffmpeg.avformat_open_input(&fmt, filePath, null, null);
                ffmpeg.avformat_find_stream_info(fmt, null);

                int videoStream = -1;
                for (int i = 0; i < fmt->nb_streams; i++)
                {
                    if (fmt->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                    {
                        videoStream = i;
                        break;
                    }
                }

                if (videoStream < 0)
                    throw new Exception("No video stream");

                var par = fmt->streams[videoStream]->codecpar;
                var codec = ffmpeg.avcodec_find_decoder(par->codec_id);

                videoCtx = ffmpeg.avcodec_alloc_context3(codec);
                ffmpeg.avcodec_parameters_to_context(videoCtx, par);
                ffmpeg.avcodec_open2(videoCtx, codec, null);

                long seekTarget = (long)(time.TotalSeconds * ffmpeg.AV_TIME_BASE);
                ffmpeg.av_seek_frame(fmt, -1, seekTarget,
                    ffmpeg.AVSEEK_FLAG_BACKWARD | ffmpeg.AVSEEK_FLAG_FRAME);

                ffmpeg.avcodec_flush_buffers(videoCtx);

                frame = ffmpeg.av_frame_alloc();
                packet = ffmpeg.av_packet_alloc();

                while (ffmpeg.av_read_frame(fmt, packet) >= 0)
                {
                    ct.ThrowIfCancellationRequested();

                    if (packet->stream_index != videoStream)
                    {
                        ffmpeg.av_packet_unref(packet);
                        continue;
                    }

                    ffmpeg.avcodec_send_packet(videoCtx, packet);

                    while (ffmpeg.avcodec_receive_frame(videoCtx, frame) == 0)
                    {
                        double pts = frame->best_effort_timestamp *
                                     ffmpeg.av_q2d(fmt->streams[videoStream]->time_base);

                        if (pts < time.TotalSeconds)
                        {
                            ffmpeg.av_frame_unref(frame);
                            continue;
                        }

                        return ConvertFrame(frame, videoCtx, targetWidth, targetHeight, ref sws);
                    }

                    ffmpeg.av_packet_unref(packet);
                }

                Logger.Error("No frame found at specified time {0} in file {1}", time, filePath);
                return null;
                //throw new Exception("Frame not found");
            }
            finally
            {
                if (sws != null) ffmpeg.sws_freeContext(sws);
                if (packet != null) ffmpeg.av_packet_free(&packet);
                if (frame != null) ffmpeg.av_frame_free(&frame);
                if (videoCtx != null) ffmpeg.avcodec_free_context(&videoCtx);
                if (fmt != null)
                {
                    ffmpeg.avformat_close_input(&fmt);
                    ffmpeg.avformat_free_context(fmt);
                }
            }
        }

        private static WriteableBitmap ConvertFrame(AVFrame* frame, AVCodecContext* ctx, int targetWidth, int targetHeight, ref SwsContext* sws)
        {
            int srcWidth = ctx->width;
            int srcHeight = ctx->height;

            if (targetHeight < 0)
                targetHeight = srcHeight * targetWidth / srcWidth;

            int stride = targetWidth * 3;
            byte* buffer = (byte*)ffmpeg.av_malloc((ulong)(stride * targetHeight));

            sws = ffmpeg.sws_getContext(
                srcWidth, srcHeight, ctx->pix_fmt,
                targetWidth, targetHeight, AVPixelFormat.AV_PIX_FMT_BGR24,
                (int)SwsFlags.SWS_BILINEAR,
                null, null, null);

            byte_ptrArray4 dst = new byte_ptrArray4();
            int_array4 lines = new int_array4();
            dst[0] = buffer;
            lines[0] = stride;

            ffmpeg.sws_scale(
                sws,
                frame->data,
                frame->linesize,
                0,
                srcHeight,
                dst,
                lines);

            WriteableBitmap wb = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                wb = new WriteableBitmap(
                    targetWidth,
                    targetHeight,
                    96,
                    96,
                    PixelFormats.Bgr24,
                    null);

                wb.Lock();
                Buffer.MemoryCopy(
                    buffer,
                    (void*)wb.BackBuffer,
                    stride * targetHeight,
                    stride * targetHeight);
                wb.AddDirtyRect(new Int32Rect(0, 0, targetWidth, targetHeight));
                wb.Unlock();
            });

            ffmpeg.av_free(buffer);
            return wb;
        }
    }
}
