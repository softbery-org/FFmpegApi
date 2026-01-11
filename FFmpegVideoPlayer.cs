// Version: 0.0.1.38
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.OleDb;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using FFmpeg.AutoGen;

using NAudio.Utils;
using NAudio.Wave;

namespace FFmpegApi
{
    public class FFmpegTimeChangedEventArgs : EventArgs
    {
        public TimeSpan Position { get; set; }
    }

    public unsafe class FFmpegVideoPlayer : IDisposable, INotifyPropertyChanged
    {
        private AVFormatContext* _fmt;
        private string _filePath;
        private AVPacket* _packet;
        private AVFrame* _videoFrame;
        private AVFrame* _audioFrame;
        private volatile bool _run;
        private volatile bool _pause = false;
        private volatile bool _isClosing;
        private volatile bool _stop;
        private volatile bool _seek;
        private Thread _decodeThread;
        private TimeSpan _seekTarget;
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _audioBuffer;
        private int _videoStream = -1;
        private double _fps;
        private AVCodecContext* _videoCtx;
        private AVCodecContext* _audioCtx;
        private SwrContext* _swr;
        private int _audioStream = -1;
        private int _width;
        private int _height;
        private SwsContext* _sws;
        private int _videoStride;
        private byte* _videoBuffer;
        private WriteableBitmap _wb;
        private double _audioClock;
        private double _lastAudioClock = 0;
        private double _audioClockOffset;
        private bool _afterSeek = false;
        private bool _audioAfterSeek;
        private double _seekVideoTarget = 0;
        private double _lastReported;
        private TimeSpan _duration = TimeSpan.Zero;
        private TimeSpan _position = TimeSpan.Zero;
        private double _videoPts;

        public TimeSpan Position
        {
            get => _position;
            private set
            {
                _position = value;
                OnPropertyChanged(nameof(Position), ref _position, value);
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            private set
            {
                _duration = value;
                OnPropertyChanged(nameof(Duration), ref _duration, value);
            }
        }

        public bool isPlay
        {
            get
            {
                if (_run && !_pause)
                    return true;
                else
                    return false;
            }
        }

        public bool isPause => _pause;

        public bool isStop => _stop;

        public bool isSeek => _seek;

        public event EventHandler<AVFrame> FrameChanged;
        public event Action<AVFrame> VideoFrameChanged;
        public event Action<WriteableBitmap> FrameReady;
        public event Action<TimeSpan> PositionChanged;
        public event EventHandler<FFmpegTimeChangedEventArgs> TimeChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public FFmpegVideoPlayer()
        {
            ffmpeg.RootPath = "ffmpeg";
            ffmpeg.avformat_network_init();
        }

        public FFmpegVideoPlayer(string ffmpeg_path)
        {
            ffmpeg.RootPath = ffmpeg_path;
            ffmpeg.avformat_network_init();
        }

        public void Play()
        {
            if (_filePath!=string.Empty || _filePath !=null)
            {
                _stop = false;
                _pause = false;
                _run = true;
                _decodeThread = new Thread(DecodeLoop) { IsBackground = true };
                _decodeThread.Start();
            }
        }

        public void Play(string path)
        {
            _filePath = path;
            _run = true;
            _decodeThread = new Thread(DecodeLoop) { IsBackground = true };
            _decodeThread.Start();
        }

        public void Open(string path)
        {
            Play(path);
            Stop();
        }

        public void Seek(TimeSpan time)
        {
            _seekTarget = time;
            _seek = true;
        }

        public void TogglePlayPause()
        {
            if (_pause)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            _pause = true;
            _waveOut.Pause();
        }

        public void Resume()
        {
            _pause = false;
            _waveOut.Play();
        }

        public void Stop()
        {
            _run = false;
            _stop = true;
            Seek(TimeSpan.Zero);
            _waveOut.Stop();
        }

        #region PRIVATE
        private void SetPosition()
        {
            double seconds = GetAudioClock();

            if (seconds < 0)
                seconds = 0;

            Duration = TimeSpan.FromSeconds(_fmt->duration / ffmpeg.AV_TIME_BASE);

            Position = TimeSpan.FromSeconds(seconds);

            PositionChanged?.Invoke(Position);

            TimeChanged?.Invoke(this, new FFmpegTimeChangedEventArgs
            {
                Position = Position
            });
        }

        private void IfSeek(double video_pts)
        {
            // Przed seekiem
            if (_afterSeek)
            {
                if (video_pts < _seekVideoTarget)
                {
                    ffmpeg.av_frame_unref(_videoFrame);
                    return; // jeszcze nie jesteśmy po seek
                }

                // pierwsza poprawna klatka
                _afterSeek = false;
            }

            if (_seek)
            {
                _waveOut.Stop();
                _audioBuffer.ClearBuffer();

                // wyliczanie seeka
                long seek_target = (long)(_seekTarget.TotalSeconds * ffmpeg.AV_TIME_BASE);

                // sam seek
                ffmpeg.av_seek_frame(
                    _fmt,
                    -1,
                    seek_target,
                    ffmpeg.AVSEEK_FLAG_BACKWARD | ffmpeg.AVSEEK_FLAG_FRAME);

                // fash dekoderów
                ffmpeg.avcodec_flush_buffers(_videoCtx);
                ffmpeg.avcodec_flush_buffers(_audioCtx);

                _audioClockOffset = _seekTarget.TotalSeconds;
                _lastAudioClock = _audioClockOffset;

                _seekVideoTarget = _seekTarget.TotalSeconds;
                _afterSeek = true;
                _audioAfterSeek = true;

                _waveOut.Play();
                _seek = false;
            }
        }

        #endregion

        #region DECODE LOOP

        private void DecodeLoop()
        {
            _fmt = ffmpeg.avformat_alloc_context();
            var fmt = _fmt;
            ffmpeg.avformat_open_input(&fmt, _filePath, null, null);
            _fmt = fmt;
            ffmpeg.avformat_find_stream_info(_fmt, null);

            FindStreams();
            InitVideo();
            InitAudio();

            _packet = ffmpeg.av_packet_alloc();
            _videoFrame = ffmpeg.av_frame_alloc();
            _audioFrame = ffmpeg.av_frame_alloc();

            Duration = TimeSpan.FromSeconds(_fmt->duration / ffmpeg.AV_TIME_BASE);

            while (_run && ffmpeg.av_read_frame(_fmt, _packet) >= 0)
            {
                AVPacket* pkt = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(pkt, _packet);

                while (_pause && _run)
                {
                    Thread.Sleep(50);
                    IfSeek(_videoPts);
                    continue;
                }

                if (_packet->stream_index == _videoStream)
                    DecodeVideo(pkt);

                if (_packet->stream_index == _audioStream)
                    DecodeAudio(pkt);

                ffmpeg.av_packet_unref(pkt);
            }
        }

        #endregion

        #region INIT

        private void FindStreams()
        {
            for (int i = 0; i < _fmt->nb_streams; i++)
            {
                var type = _fmt->streams[i]->codecpar->codec_type;
                if (type == AVMediaType.AVMEDIA_TYPE_VIDEO && _videoStream < 0) _videoStream = i;
                if (type == AVMediaType.AVMEDIA_TYPE_AUDIO && _audioStream < 0) _audioStream = i;
            }
        }

        private void InitVideo()
        {
            var par = _fmt->streams[_videoStream]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(par->codec_id);

            // FPS wideo
            _fps = (double)_fmt->streams[_videoStream]->avg_frame_rate.num / (double)_fmt->streams[_videoStream]->avg_frame_rate.den;

            _videoCtx = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_videoCtx, par);
            ffmpeg.avcodec_open2(_videoCtx, codec, null);

            _width = _videoCtx->width;
            _height = _videoCtx->height;

            _videoStride = _width * 3;
            _videoBuffer = (byte*)ffmpeg.av_malloc((ulong)(_videoStride * _height));

            _sws = ffmpeg.sws_getContext(
                _width, _height, _videoCtx->pix_fmt,
                _width, _height, AVPixelFormat.AV_PIX_FMT_BGR24,
                (int)SwsFlags.SWS_FAST_BILINEAR,
                null, null, null);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _wb = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr24, null);
                FrameReady?.Invoke(_wb);
            });
        }

        private void InitAudio()
        {
            var par = _fmt->streams[_audioStream]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(par->codec_id);

            _audioCtx = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_audioCtx, par);
            ffmpeg.avcodec_open2(_audioCtx, codec, null);

            var swr = ffmpeg.swr_alloc();
            if (_swr!=null)
                swr = _swr;

            AVChannelLayout outLayout;
            ffmpeg.av_channel_layout_default(&outLayout, 2);

            ffmpeg.swr_alloc_set_opts2(
                &swr,
                &outLayout,
                AVSampleFormat.AV_SAMPLE_FMT_S16,
                _audioCtx->sample_rate,
                &_audioCtx->ch_layout,
                _audioCtx->sample_fmt,
                _audioCtx->sample_rate,
                0, null);

            ffmpeg.swr_init(swr);
            _swr = swr;

            _audioBuffer = new BufferedWaveProvider(new WaveFormat(_audioCtx->sample_rate, 16, _audioCtx->ch_layout.nb_channels))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(300)               
            };

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioBuffer);
            _waveOut.Play();
        }

        #endregion

        #region VIDEO

        private void DecodeVideo(AVPacket* pkt)
        {
            ffmpeg.avcodec_send_packet(_videoCtx, pkt);

            while (ffmpeg.avcodec_receive_frame(_videoCtx, _videoFrame) == 0)
            {
                var video_pts = _videoFrame->best_effort_timestamp * ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);
                double audioClock = GetAudioClock();
                double diff = video_pts - audioClock;

                _videoPts = video_pts;

                //Seek
                IfSeek(video_pts);

                // Renderowanie ramki
                RenderFrame();

                // 10Hz-cowa, tańsza wersja niż 60Hz samo SetPosition()
                if ((int)(GetAudioClock() * 10) != (int)(_lastReported * 10))
                {
                    SetPosition();
                    _lastReported = GetAudioClock();
                }

                Debug.WriteLine($"POS: {Position.TotalSeconds:F3}s | AUDIO: {GetAudioClock():F3}s");
                
                // Synchronizacja audio i wideo
                if (diff > 0.01 && diff < 0.5)
                {
                    Thread.Sleep((int)(diff * 1000)); // video za szybkie
                }
                else if (diff < -0.05)
                {
                    ffmpeg.av_frame_unref(_videoFrame);
                    return; // DROP FRAME – video spóźnione
                }

                Debug.WriteLine($"SEEK:{_audioClockOffset:F3} |A:{audioClock:F3}s | V:{video_pts:F3}s | diff:{diff:F3}\");");
                
                ffmpeg.av_frame_unref(_videoFrame);
            }
        }

        private void RenderFrame()
        {
            var dstData = new byte_ptrArray4();
            var dstLines = new int_array4();
            dstData[0] = _videoBuffer;
            dstLines[0] = _videoStride;

            ffmpeg.sws_scale(
                _sws,
                _videoFrame->data,
                _videoFrame->linesize,
                0,
                _videoCtx->height,
                dstData,
                dstLines);

            try
            {
                _wb.Dispatcher.BeginInvoke(() =>
                {
                    if (_isClosing) return;

                    _wb.Lock();

                    Buffer.MemoryCopy(
                        _videoBuffer,
                        (void*)_wb.BackBuffer,
                        _videoStride * _videoCtx->height,
                        _videoStride * _videoCtx->height);

                    _wb.AddDirtyRect(
                        new Int32Rect(0, 0, _videoCtx->width, _videoCtx->height));

                    _wb.Unlock();
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        #endregion

        #region AUDIO

        private double GetAudioClock()
        {
            if (_waveOut == null || _audioBuffer == null)
                return _lastAudioClock;

            if (_waveOut.PlaybackState != PlaybackState.Playing)
                return _lastAudioClock;

            long bytesPlayed = _waveOut.GetPosition();
            int bytesPerSecond = _audioBuffer.WaveFormat.AverageBytesPerSecond;

            if (bytesPerSecond > 0)
            {
                double played = (double)bytesPlayed / bytesPerSecond;
                _lastAudioClock = _audioClockOffset + played;
            }

            return _lastAudioClock;
        }

        private void DecodeAudio(AVPacket* pkt)
        {
            ffmpeg.avcodec_send_packet(_audioCtx, pkt);

            while (ffmpeg.avcodec_receive_frame(_audioCtx, _audioFrame) == 0)
            {
                int outSamples = ffmpeg.swr_get_out_samples(_swr, _audioFrame->nb_samples);
                byte* outBuf = (byte*)ffmpeg.av_malloc((ulong)(outSamples * 4));

                byte** arr = stackalloc byte*[1];
                arr[0] = outBuf;

                int samples = ffmpeg.swr_convert(
                    _swr, arr, outSamples,
                    _audioFrame->extended_data,
                    _audioFrame->nb_samples);

                
                if (samples > 0)
                {
                    int bytes = samples * 4;
                    byte[] managed = new byte[bytes];
                    
                    fixed (byte* dst = managed)
                        Buffer.MemoryCopy(outBuf, dst, bytes, managed.Length);

                    _audioBuffer.AddSamples(managed, 0, bytes);
                }

                // wymagane do synchronizacji seek
                if (_audioAfterSeek && samples > 0)
                {
                    _audioClockOffset = _seekTarget.TotalSeconds;
                    _lastAudioClock = _audioClockOffset;
                    _audioAfterSeek = false;
                }

                ffmpeg.av_free(outBuf);
                ffmpeg.av_frame_unref(_audioFrame);
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            _isClosing = true;

            _run = false;

            try
            {
                if (_decodeThread != null && _decodeThread.IsAlive)
                {
                    _decodeThread.Join(500); // czekaj max 0,5s
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error stopping decode thread: " + ex.Message);
            }

            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;
            }
            catch { }

            // 🔹 zwolnij FFmpeg audio
            if (_audioFrame != null)
            {
                var af = _audioFrame;
                ffmpeg.av_frame_free(&af);
                _audioFrame = null;
            }

            if (_audioCtx != null)
            {
                var ac = _audioCtx;
                ffmpeg.avcodec_free_context(&ac);
                _audioCtx = null;
            }

            if (_swr != null)
            {
                var s = _swr;
                ffmpeg.swr_free(&s);
                _swr = null;
            }

            if (_videoFrame != null)
            {
                var vf = _videoFrame;
                ffmpeg.av_frame_free(&vf);
                _videoFrame = null;
            }

            if (_videoCtx != null)
            {
                var vc = _videoCtx;
                ffmpeg.avcodec_free_context(&vc);
                _videoCtx = null;
            }

            if (_sws != null)
            {
                ffmpeg.sws_freeContext(_sws);
                _sws = null;
            }

            if (_videoBuffer != null)
            {
                ffmpeg.av_free(_videoBuffer);
                _videoBuffer = null;
            }

            if (_fmt != null)
            {
                var f = _fmt;
                ffmpeg.avformat_close_input(&f);
                ffmpeg.avformat_free_context(f);
                _fmt = null;
            }

            _wb = null;

            Debug.WriteLine("FFmpegVideoPlayer disposed");
        }

        #endregion

        #region PropertyChanged

        protected void OnPropertyChanged<T>(string propertyName, ref T field, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
