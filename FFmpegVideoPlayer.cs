// Version: 0.0.2.33
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
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

using FFmpegApi.Logs;

using NAudio.Utils;
using NAudio.Wave;

namespace FFmpegApi
{
    public class FFmpegTimeChangedEventArgs : EventArgs
    {
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public bool isPlay {  get; set; }
        public bool isPause { get; set; }
    }

    public class FFmpegStopChangedEventArgs : EventArgs;
    public class FFmpegEndReachedEventArgs : EventArgs;

    public unsafe class FFmpegVideoPlayer : IDisposable, INotifyPropertyChanged
    {
        private const double END_EPSILON = 2; // 50 ms
        private const int OUT_CHANNELS = 2; // 2 - stereo

        private string _filePath = string.Empty;

        private AVFormatContext* _fmt;
        private AVPacket* _packet;
        private AVFrame* _videoFrame;
        private AVFrame* _audioFrame;
        private AVCodecContext* _videoCtx;
        private AVCodecContext* _audioCtx;
        private SwrContext* _swr;
        private SwsContext* _sws;

        private volatile bool _running = true;
        private volatile bool _pause = false;
        private volatile bool _isClosing = false;
        private volatile bool _stop = false;
        private volatile bool _seek = false;

        private Thread _decodeThread;
        
        private WaveOutEvent _waveOut;
        
        private BufferedWaveProvider _audioBuffer;
        
        private WriteableBitmap _wb;
        
        private int _videoStream = -1;
        private int _audioStream = -1;
        private int _width;
        private int _height;
        private int _videoStride;
        
        private byte* _videoBuffer;
        
        private double _fps;
        private double _audioClock;
        private double _lastAudioClock = 0;
        private double _audioClockOffset;
        private double _seekVideoTarget = 0;
        private double _lastReported;
        private double _videoPts;
        private bool _mute = false;
        private double _lastVolume;

        private TimeSpan _seekTarget = TimeSpan.Zero;
        private TimeSpan _duration = TimeSpan.Zero;
        private TimeSpan _position = TimeSpan.Zero;
        
        private bool _afterSeek = false;
        private bool _audioAfterSeek = false;
        private bool _endReached = false;
        private bool _syncAfterStop = false;
        private bool _forceRenderAfterSeek = false;

        private bool _grabFrame = false;
        private TimeSpan _grabTarget;
        private WriteableBitmap _grabResult;
        private ManualResetEventSlim _grabWait = new(false);

        private double _volume = 1.0;

        public string FilePath => _filePath;

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

        public double Volume
        {
            get
            {
                if (_waveOut != null)
                    return _waveOut.Volume;
                else
                    return 0;
            }
            set
            {
                if (_waveOut != null)
                    _waveOut.Volume = (float)value;
                _volume = value;
                OnPropertyChanged(nameof(Volume), ref _volume, value);
            }
        }

        public bool isPlay
        {
            get
            {
                if (_running && !_pause)
                    return true;
                else
                    return false;
            }
        }

        public bool isPause => _pause;

        public bool isStop => _stop;

        public bool isSeek => _seek;

        public bool isRunning => _running;

        public double Fps => _fps;

        public bool isMute => _mute;

        public event EventHandler<AVFrame> FrameChanged;
        public event Action<AVFrame> VideoFrameChanged;
        public event Action<WriteableBitmap> FrameReady;
        public event Action<TimeSpan> PositionChanged;
        public event EventHandler<FFmpegTimeChangedEventArgs> TimeChanged;
        public event EventHandler<FFmpegStopChangedEventArgs> StopChanged;
        public event EventHandler<FFmpegEndReachedEventArgs> EndReached;
        public event EventHandler<EventArgs> OnPlay;
        public event EventHandler<EventArgs> OnStop;
        public event EventHandler<EventArgs> OnSeek;
        public event EventHandler<EventArgs> OnPause;


        public event PropertyChangedEventHandler PropertyChanged;

        #region === KONSTRUKTORY ===

        public FFmpegVideoPlayer()
        {
            ffmpeg.RootPath = "ffmpeg";
            ffmpeg.avformat_network_init();
        }

        public FFmpegVideoPlayer(string library_directory_path)
        {
            ffmpeg.RootPath = library_directory_path;
            ffmpeg.avformat_network_init();
        }

        #endregion

        #region === STEROWANIE ODTWARZANIEM ===

        public void Play()
        {
            if (_running && !_pause)
            {
                Pause();
                return;
            }            

            if (_pause)
            {
                _pause = false;
                _stop = false;
                _waveOut.Play();
                Debug.WriteLine($"PLAY");
                return;
            }

            if (!_running && !string.IsNullOrEmpty(_filePath))
            {
                _running = false;
                _stop = false;
                _decodeThread = new Thread(DecodeLoop) { IsBackground = true };
                _decodeThread.Start();
                Debug.WriteLine($"PLAY: {_filePath}");
            }
        }
        public void Play(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            _filePath = path;
            _running = false;
            _stop = false;

            Debug.WriteLine(IsUrl(path)
                ? $"PLAY URL: {path}"
                : $"PLAY FILE: {path}");

            _decodeThread = new Thread(DecodeLoop)
            {
                IsBackground = true
            };
            _decodeThread.Start();
        }
        //public void Play(string path)
        //{
        //    _filePath = path;
        //    _running = false;
        //    _stop = false;
        //    _decodeThread = new Thread(DecodeLoop) { IsBackground = true };
        //    _decodeThread.Start();
        //    Debug.WriteLine($"PLAY: {_filePath}");
        //}

        public void Open(string path)
        {
            Play(path);
            Stop();
            Debug.WriteLine("OPEN");
        }

        public void Seek(TimeSpan time)
        {
            if (IsUrl(_filePath) && !_fmt->pb->seekable.Equals(1))
            {
                Debug.WriteLine("⚠ SEEK not supported for this stream");
                return;
            }

            _seekTarget = time;
            _seek = true;
            _forceRenderAfterSeek = true;
        }
        //public void Seek(TimeSpan time)
        //{
        //    _seekTarget = time;
        //    _seek = true;
        //    _forceRenderAfterSeek = true;
        //    Debug.WriteLine("SEEK");
        //}

        public void TogglePlayPause()
        {
            if (_pause)
                Resume();
            else
                Pause();
            Debug.WriteLine("TOGGLE PLAY PAUSE");
        }

        public void Pause()
        {
            _pause = true;
            _waveOut.Pause();
            Debug.WriteLine("PAUSE");
        }

        public void Resume()
        {
            _pause = false;
            _waveOut.Play();
            Debug.WriteLine("RESUME");
        }

        public void Stop()
        {
            if (!_running)
                return;

            Debug.WriteLine("STOP");

            _pause = true;
            _stop = true;

            _seekTarget = TimeSpan.Zero;
            _seek = true;
            _syncAfterStop = true;

            _endReached = false;

            _waveOut?.Stop();
            _audioBuffer?.ClearBuffer();

            _audioClockOffset = 0;
            _lastAudioClock = 0;
            _lastReported = 0;

            Position = TimeSpan.Zero;

            TimeChanged?.Invoke(this, new FFmpegTimeChangedEventArgs
            {
                Position = Position,
                Duration = Duration,
                isPlay = false,
                isPause = true
            });

            ClearWriteableBitmap();
            StopChanged?.Invoke(this, new FFmpegStopChangedEventArgs());

            OnStop?.Invoke(this, EventArgs.Empty);
            _running = false;
        }

        public void ToggleMute()
        {
            _lastVolume = Volume;
            if (_mute)
            {
                Volume = 1.0;
                _mute = false;
            }
            else
            {
                Volume = 0.0;
                _mute = true;
            }
            OnPropertyChanged(nameof(isMute), ref _mute, _mute);
        }
        #endregion

        #region === PUBLICZNE ===

        public void SaveBitmap(WriteableBitmap bmp, string path)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using var fs = new FileStream(path, FileMode.Create);
            encoder.Save(fs);
        }

        public WriteableBitmap GrabFrame(TimeSpan time, int timeoutMs = 2000)
        {
            if (_fmt == null || _videoCtx == null)
                throw new InvalidOperationException("Player not initialized");

            _pause = true;
            _stop = false;

            _grabTarget = time;
            _grabFrame = true;
            _grabResult = null;
            _grabWait.Reset();

            Seek(time);

            if (!_grabWait.Wait(timeoutMs))
                throw new TimeoutException("Frame grab timeout");

            return _grabResult;
        }

        #endregion

        #region === PRYWATNE ===

        private WriteableBitmap CloneWriteableBitmap(WriteableBitmap src)
        {
            WriteableBitmap clone = null;

            src.Dispatcher.Invoke(() =>
            {
                clone = new WriteableBitmap(
                    src.PixelWidth,
                    src.PixelHeight,
                    src.DpiX,
                    src.DpiY,
                    src.Format,
                    null);

                src.Lock();
                clone.Lock();

                Buffer.MemoryCopy(
                    (void*)src.BackBuffer,
                    (void*)clone.BackBuffer,
                    src.BackBufferStride * src.PixelHeight,
                    src.BackBufferStride * src.PixelHeight);

                clone.AddDirtyRect(
                    new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight));

                clone.Unlock();
                src.Unlock();
            });

            return clone;
        }

        private void ClearWriteableBitmap()
        {
            if (_wb == null)
                return;

            if (_wb.Dispatcher.HasShutdownStarted ||
                _wb.Dispatcher.HasShutdownFinished)
                return;

            _wb.Dispatcher.Invoke(() =>
            {
                _wb.Lock();
                Buffer.MemoryCopy(
                    null,
                    (void*)_wb.BackBuffer,
                    _wb.BackBufferStride * _wb.PixelHeight,
                    0); // zerowanie

                _wb.AddDirtyRect(
                    new Int32Rect(0, 0, _wb.PixelWidth, _wb.PixelHeight));

                _wb.Unlock();
            });
        }

        private void UpdatePausedPosition()
        {
            if (_pause)
            {
                PositionChanged?.Invoke(Position);
                TimeChanged?.Invoke(this, new FFmpegTimeChangedEventArgs
                {
                    Position = Position
                });
            }
        }

        private void RefreshPausedFrame()
        {
            if (_pause && !_isClosing && _videoFrame != null)
            {
                RenderFrame();
            }
        }

        private void CheckEndReached()
        {
            if (_endReached)
                return;

            if (!_running || _pause || _seek)
                return;

            double audioClock = GetAudioClock();
            double duration = Duration.TotalSeconds;

            if (duration <= 0)
                return;

            if (audioClock >= duration - END_EPSILON)
            {
                _endReached = true;

                Debug.WriteLine("END REACHED");

                EndReached?.Invoke(this, new FFmpegEndReachedEventArgs());

                Stop();
            }
        }

        private void ResetEndReached()
        {
            _endReached = false;
        }

        private void SetPosition()
        {
            double seconds = GetAudioClock();

            if (seconds < 0)
                seconds = 0;

            if (Duration != TimeSpan.Zero && seconds > Duration.TotalSeconds)
                seconds = Duration.TotalSeconds;

            Position = TimeSpan.FromSeconds(seconds);

            PositionChanged?.Invoke(Position);

            TimeChanged?.Invoke(this, new FFmpegTimeChangedEventArgs
            {
                Position = Position
            });

            CheckEndReached();
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

                _forceRenderAfterSeek = true;

                if (!_pause)
                    _waveOut.Play();
                else
                    _waveOut.Pause();

                _seek = false;
                
                OnSeek?.Invoke(this, EventArgs.Empty);

                UpdatePausedPosition();
                RefreshPausedFrame();
            }

            if (_stop)
            {
                _pause = true;
                _waveOut.Stop();
                OnStop?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SyncAV(double diff)
        {
            // 10Hz-cowa, tańsza wersja niż 60Hz czyli samo SetPosition()
            if ((int)(GetAudioClock() * 10) != (int)(_lastReported * 10))
            {
                SetPosition();
                _lastReported = GetAudioClock();
            }

            Debug.WriteLine($"POS: {Position.TotalSeconds:F3}s | AUDIO: {GetAudioClock():F3}s | DIFF: {diff:F3}");

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

            Debug.WriteLine($"SEEK:{_audioClockOffset:F3} | diff:{diff:F3}\");");
        }

        public Task<WriteableBitmap> GetThumbnailAsync(TimeSpan time, int width = 320, int height = -1, CancellationToken ct = default)
        {
            string path = _filePath;

            if (IsUrl(_filePath))
                Logger.Error(new NotSupportedException("Thumbnail from URL not supported").Message);

            if (string.IsNullOrEmpty(path))
                Logger.Error(new InvalidOperationException("No file loaded").Message);

            return Task.Run(() => FFmpegThumbnailExtractor.Extract(path, time, width, height, ct), ct);
        }

        private static bool IsUrl(string path)
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                return false;

            string scheme = uri.Scheme.ToLowerInvariant();

            return scheme == Uri.UriSchemeHttp
                || scheme == Uri.UriSchemeHttps
                || scheme == "rtsp"
                || scheme == "rtmp"
                || scheme == "rtmps"
                || scheme == "udp"
                || scheme == "tcp";
        }

        private static bool IsLiveStream(string path)
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == "rtsp"
                || uri.Scheme == "rtmp"
                || uri.Scheme == "udp";
        }

        #endregion

        #region === INIT ===

        private void FindStreams()
        {
            for (int i = 0; i < _fmt->nb_streams; i++)
            {
                var type = _fmt->streams[i]->codecpar->codec_type;
                if (type == AVMediaType.AVMEDIA_TYPE_VIDEO && _videoStream < 0) _videoStream = i;
                if (type == AVMediaType.AVMEDIA_TYPE_AUDIO && _audioStream < 0) _audioStream = i;
            }
        }

        #endregion

        #region === DECODE LOOP ===

        private CancellationToken _ct = new CancellationToken();

        private void DecodeLoop()
        {
            _fmt = ffmpeg.avformat_alloc_context();
            
            AVDictionary* options = null;

            if (IsUrl(_filePath))
            {
                ffmpeg.av_dict_set(&options, "timeout", "5000000", 0); // 5s
                ffmpeg.av_dict_set(&options, "buffer_size", "1048576", 0);
                ffmpeg.av_dict_set(&options, "reconnect", "1", 0);
                ffmpeg.av_dict_set(&options, "reconnect_streamed", "1", 0);
                ffmpeg.av_dict_set(&options, "reconnect_delay_max", "5", 0);
            }

            var fmt = _fmt;
            //ffmpeg.avformat_open_input(&fmt, @_filePath, null, null);
            int ret = ffmpeg.avformat_open_input(&fmt, _filePath, null, &options);
            _fmt = fmt;
            ffmpeg.av_dict_free(&options);

            if (ret != 0)
            {
                Debug.WriteLine("❌ Cannot open input");
                return;
            }
            ffmpeg.avformat_find_stream_info(_fmt, null);

            FindStreams();
            InitVideo();
            InitAudio();

            _packet = ffmpeg.av_packet_alloc();
            _videoFrame = ffmpeg.av_frame_alloc();
            _audioFrame = ffmpeg.av_frame_alloc();

            Duration = TimeSpan.FromSeconds(_fmt->duration / ffmpeg.AV_TIME_BASE);

            if (!_running)
            {
                _running = true;
                _pause = false;
            }
            if (_stop)
            {
                _pause = true;
                _waveOut.Stop();
            }
            

            while (_running && ffmpeg.av_read_frame(_fmt, _packet) >= 0)
            {
                _ct.ThrowIfCancellationRequested();

                AVPacket* pkt = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(pkt, _packet);

                if (_stop)
                {
                    OnStop?.Invoke(this, EventArgs.Empty);
                    ffmpeg.av_packet_unref(_packet);
                    ffmpeg.av_packet_free(&pkt);
                    continue;
                }

                if (_pause)
                {
                    OnPause?.Invoke(this, EventArgs.Empty);
                    UpdatePausedPosition();
                    RefreshPausedFrame();
                }

                if (_seek)
                {
                    OnSeek?.Invoke(this, EventArgs.Empty);
                    UpdatePausedPosition();
                    RefreshPausedFrame();
                }

                if (_isClosing)
                {
                    ffmpeg.av_packet_unref(_packet);
                    ffmpeg.av_packet_free(&pkt);
                    break;
                }

                if (!_pause && _running)
                {
                    OnPlay?.Invoke(this, EventArgs.Empty);
                }

                _packet = pkt;

                if (_packet->stream_index == _videoStream)
                    DecodeVideo(_packet);

                if (_packet->stream_index == _audioStream)
                    DecodeAudio(_packet);

                while (_pause && _running)
                {
                    if (_seek)
                    {
                        double video_pts = _videoFrame != null
                            ? _videoFrame->best_effort_timestamp * ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base)
                            : _seekTarget.TotalSeconds;

                        IfSeek(video_pts);

                        // wymuszenie render dla jednej ramki po seeku
                        if (_forceRenderAfterSeek && _videoFrame != null)
                        {
                            RenderFrame();
                            _forceRenderAfterSeek = false;
                        }
                    }

                    Thread.Sleep(50);
                    //continue;
                    ffmpeg.av_packet_unref(_packet);
                }
                ffmpeg.av_packet_unref(_packet);
            }
            ffmpeg.av_packet_unref(_packet);
        }

        #endregion

        #region === VIDEO ===

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

        private void DecodeVideo(AVPacket* pkt)
        {
            ffmpeg.avcodec_send_packet(_videoCtx, pkt);

            while (ffmpeg.avcodec_receive_frame(_videoCtx, _videoFrame) == 0)
            {
                if (_stop)
                    return;

                if (_pause && !_forceRenderAfterSeek)
                    return;

                double video_pts = _videoFrame->best_effort_timestamp * ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);
                double audioClock = GetAudioClock();
                double diff = video_pts - audioClock;

                // === FRAME GRAB ===
                if (_grabFrame)
                {
                    if (video_pts + 0.0005 < _grabTarget.TotalSeconds)
                    {
                        ffmpeg.av_frame_unref(_videoFrame);
                        return;
                    }

                    // MAMY KLATKĘ
                    RenderFrame();

                    _grabResult = CloneWriteableBitmap(_wb);
                    _grabFrame = false;
                    _grabWait.Set();

                    ffmpeg.av_frame_unref(_videoFrame);
                    return;
                }

                _videoPts = video_pts;

                Debug.WriteLine($"AUDIO CLOCK:{audioClock:F3}s | VIDEO PTS:{video_pts:F3}s | DIFF: {diff}");

                // === SEEK ===
                IfSeek(video_pts);

                if (_syncAfterStop)
                {
                    _audioClockOffset = video_pts; // = 0
                    _lastAudioClock = 0;
                    _lastReported = 0;
                    _syncAfterStop = false;

                    Debug.WriteLine("[SYNC AFTER STOP @ 0.0]");
                }

                if (_forceRenderAfterSeek)
                {
                    RenderFrame();
                    _forceRenderAfterSeek = false;
                    return;
                }

                // === SYNCHRONIZACJA ===
                if (!_grabFrame)
                    SyncAV(diff);

                // === RENDER FRAME ===
                RenderFrame();

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

        #region === AUDIO ===

        private void InitAudio()
        {
            var par = _fmt->streams[_audioStream]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(par->codec_id);

            _audioCtx = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_audioCtx, par);
            ffmpeg.avcodec_open2(_audioCtx, codec, null);

            var swr = ffmpeg.swr_alloc();
            if (_swr != null)
                swr = _swr;

            AVChannelLayout outLayout;
            ffmpeg.av_channel_layout_default(&outLayout, OUT_CHANNELS);

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

            _audioBuffer = new BufferedWaveProvider(new WaveFormat(_audioCtx->sample_rate, 16, OUT_CHANNELS))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(300)
            };
            _swr = swr;

            //_audioBuffer.ClearBuffer();
            
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioBuffer);
            _waveOut.Play();
        }

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

        #region === DISPOSE ===

        public void Dispose()
        {
            _isClosing = true;

            _running = false;

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

            if (_videoBuffer != null)
            {
                ffmpeg.av_free(_videoBuffer);
                _videoBuffer = null;
            }

            if (_swr != null)
            {
                var s = _swr;
                ffmpeg.swr_close(s);
                ffmpeg.swr_free(&s);
                _swr = null;
            }

            if (_sws != null)
            {
                ffmpeg.sws_freeContext(_sws);
                _sws = null;
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

        #region === PROPERTYCHANGED ===

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
