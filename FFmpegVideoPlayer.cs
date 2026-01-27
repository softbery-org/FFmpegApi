// Version: 0.0.2.90
using System;
using System.Collections.Concurrent;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

using FFmpeg.AutoGen;

using FFmpegApi.Logs;

using NAudio.Gui;
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

        private AVBufferRef* _hwDeviceCtx;
        private AVPixelFormat _hwPixFmt = AVPixelFormat.AV_PIX_FMT_D3D11;
        private AVFrame* _hwFrame = ffmpeg.av_frame_alloc();
        private AVFrame* _swFrame = ffmpeg.av_frame_alloc();

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
        private int _videoBufferSize = 0;

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
        private long _audioBytesBase = 0;

        private bool _grabFrame = false;
        private TimeSpan _grabTarget;
        private WriteableBitmap _grabResult;
        private ManualResetEventSlim _grabWait = new(false);

        private double _volume = 1.0;

        private Thread _readerThread;
        private Thread _videoThread;
        private Thread _audioThread;

        private BlockingCollection<IntPtr> _videoQueue = new BlockingCollection<IntPtr>(100);
        private BlockingCollection<IntPtr> _audioQueue = new BlockingCollection<IntPtr>(100);
        private CancellationTokenSource _cts =  new CancellationTokenSource();

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

                //StartDecode();

                Debug.WriteLine($"PLAY: {_filePath}");
            }
        }
        public void Play(string path)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(path))
                return;

            _filePath = path;
            _running = false;
            _stop = false;

            Debug.WriteLine(IsUrl(path)
                ? $"PLAY URL: {path}"
                : $"PLAY FILE: {path}");

            //StartDecode();
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
            Debug.WriteLine($"SEEK({time})");
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
            if (_waveOut == null)
                return;

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

            //_videoQueue.CompleteAdding();
            //_audioQueue.CompleteAdding();

            //_cts.Cancel();
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
            // Synchronizacja audio i wideo - ulepszona
            //if (diff > 0)
            //{
            //    // video wyprzedza audio, poczekaj
            //    int waitMs = (int)(diff * 1000);
            //    if (waitMs > 0 && waitMs < 100) // max 100ms
            //        Thread.Sleep(waitMs);
            //}
            //else if (diff < -0.05)
            //{
            //    // video spóźnione, pomiń klatkę
            //    ffmpeg.av_frame_unref(_videoFrame);
            //    return;
            //}

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

        private void StartDecode()
        {
            _running = true;
            _pause = false;
            _stop = false;
            
            _cts = new CancellationTokenSource();

            _videoQueue = new BlockingCollection<IntPtr>(100);
            _audioQueue = new BlockingCollection<IntPtr>(100);

            //_readerThread = new Thread(() => PacketReaderLoop(_cts.Token));
            //_videoThread = new Thread(() => VideoDecodeLoop(_cts.Token));
            //_audioThread = new Thread(() => AudioDecodeLoop(_cts.Token));

            _readerThread.Start();
            _videoThread.Start();
            _audioThread.Start();
        }

        #endregion

        #region === DECODE LOOP ===

        //private void PacketReaderLoop(CancellationToken token)
        //{
        //    _fmt = ffmpeg.avformat_alloc_context();
        //    AVDictionary* options = null;

        //    if (IsUrl(_filePath))
        //    {
        //        ffmpeg.av_dict_set(&options, "timeout", "5000000", 0); // 5s
        //        ffmpeg.av_dict_set(&options, "buffer_size", "1048576", 0);
        //        ffmpeg.av_dict_set(&options, "reconnect", "1", 0);
        //        ffmpeg.av_dict_set(&options, "reconnect_streamed", "1", 0);
        //        ffmpeg.av_dict_set(&options, "reconnect_delay_max", "5", 0);
        //    }

        //    AVFormatContext* fmt = _fmt;
        //    if (ffmpeg.avformat_open_input(&fmt, _filePath, null, &options) != 0)
        //        return;

        //    _fmt = fmt;
        //    ffmpeg.av_dict_free(&options);

        //    ffmpeg.avformat_find_stream_info(_fmt, null);

        //    FindStreams();
        //    InitVideo();
        //    InitAudio();

        //    Duration = TimeSpan.FromSeconds(_fmt->duration / ffmpeg.AV_TIME_BASE);
        //    try
        //    {
        //        _running = true;
        //        _audioClockOffset = 0;
        //        _lastAudioClock = 0;
        //        while (!token.IsCancellationRequested)
        //        {
        //            if (_pause)
        //            {
        //                Thread.Sleep(10);
        //                continue;
        //            }

        //            AVPacket* pkt = ffmpeg.av_packet_alloc();

        //            if (ffmpeg.av_read_frame(_fmt, pkt) < 0)
        //            {
        //                ffmpeg.av_packet_free(&pkt);
        //                break;
        //            }

        //            if (pkt->stream_index == _videoStream)
        //            {
        //                if (!_videoQueue.IsAddingCompleted)
        //                    _videoQueue.Add((IntPtr)pkt, token);
        //                else
        //                    ffmpeg.av_packet_free(&pkt);
        //            }
        //            else if (pkt->stream_index == _audioStream)
        //            {
        //                if (!_audioQueue.IsAddingCompleted)
        //                    _audioQueue.Add((IntPtr)pkt, token);
        //                else
        //                    ffmpeg.av_packet_free(&pkt);
        //            }
        //            else
        //            {
        //                ffmpeg.av_packet_free(&pkt);
        //            }
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    { }
        //    catch (ObjectDisposedException) 
        //    { }
        //    finally
        //    {
        //        _stop = true;
        //        _pause = true;

        //        if (!_videoQueue.IsAddingCompleted)
        //            _videoQueue.CompleteAdding();
        //        if (!_audioQueue.IsAddingCompleted)
        //            _audioQueue.CompleteAdding();

        //        _cts.Cancel();
        //    }
        //}

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


            while (_running)
            {
                if (_seek)
                {
                    PerformSeek();
                    continue; // ⬅️ bardzo ważne
                }

                if (ffmpeg.av_read_frame(_fmt, _packet) < 0)
                    break;

                AVPacket* pkt = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(pkt, _packet);

                if (_stop)
                {
                    OnStop?.Invoke(this, EventArgs.Empty);
                    ffmpeg.av_packet_unref(pkt);
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
                    ffmpeg.av_packet_unref(pkt);
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

        private void PerformSeek()
        {
            _waveOut.Stop();
            _audioBuffer.ClearBuffer();

            // 🔥 RESET AUDIO CLOCK SOURCE
            _audioBytesBase = _waveOut.GetPosition();

            _audioClockOffset = _seekTarget.TotalSeconds;
            _lastAudioClock = _audioClockOffset;
            _lastReported = _audioClockOffset;

            _seekVideoTarget = _seekTarget.TotalSeconds;

            _afterSeek = true;
            _audioAfterSeek = true;
            _forceRenderAfterSeek = true;

            long seekTarget = (long)(_seekTarget.TotalSeconds * ffmpeg.AV_TIME_BASE);

            ffmpeg.av_seek_frame(
                _fmt,
                -1,
                seekTarget,
                ffmpeg.AVSEEK_FLAG_BACKWARD
            );

            ffmpeg.avcodec_flush_buffers(_videoCtx);
            ffmpeg.avcodec_flush_buffers(_audioCtx);

            if (!_pause)
                _waveOut.Play();

            _seek = false;

            OnSeek?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region === HW ===

        private void InitHWDevice()
        {
            var hwd = _hwDeviceCtx;
            int err = ffmpeg.av_hwdevice_ctx_create(
                &hwd,
                AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
                null,
                null,
                0);

            if (err < 0)
                throw new ApplicationException("D3D11VA not supported");
        }

        private AVPixelFormat GetHWFormat(AVCodecContext* ctx, AVPixelFormat* pix_fmts)
        {
            for (var p = pix_fmts; *p != AVPixelFormat.AV_PIX_FMT_NONE; p++)
                if (*p == _hwPixFmt)
                    return *p;

            return pix_fmts[0];
        }

        private void InitVideoHW()
        {
            var codec = ffmpeg.avcodec_find_decoder(_fmt->streams[_videoStream]->codecpar->codec_id);

            _videoCtx = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_videoCtx, _fmt->streams[_videoStream]->codecpar);

            InitHWDevice();

            _videoCtx->hw_device_ctx = ffmpeg.av_buffer_ref(_hwDeviceCtx);
            _videoCtx->get_format = (AVCodecContext_get_format_func)GetHWFormat;

            ffmpeg.avcodec_open2(_videoCtx, codec, null);
        }

        private void DecodeVideoHW(AVPacket* pkt)
        {
            ffmpeg.avcodec_send_packet(_videoCtx, pkt);

            while (ffmpeg.avcodec_receive_frame(_videoCtx, _hwFrame) == 0)
            {
                // jeśli klatka jest HW
                if (_hwFrame->format == (int)_hwPixFmt)
                {
                    RenderD3D11Frame(_hwFrame);
                }

                ffmpeg.av_frame_unref(_hwFrame);
            }
        }
        D3D11Context _d3d;
        D3DImage _d3dImage;

        public void InitRenderer(D3DImage img)
        {
            _d3d = new D3D11Context();
            _d3dImage = img;
        }

        private unsafe void RenderD3D11Frame(AVFrame* frame)
        {
            //var tex = (SharpDX.Direct3D11.Texture2D)
            //    SharpDX.Direct3D11.Texture2D.FromPointer((IntPtr)frame->data[0]);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _d3dImage.Lock();
                
                //if (_d3dImage.BackBuffer == IntPtr.Zero)
                {
                    //_d3dImage.SetBackBuffer(
                        //D3DResourceType.IDirect3DSurface9,
                        //tex.NativePointer);
                }

                _d3dImage.AddDirtyRect(
                    new Int32Rect(0, 0, frame->width, frame->height));

                _d3dImage.Unlock();
            });
        }
        #endregion

        #region === VIDEO ===

        private void InitVideo()
        {
            var par = _fmt->streams[_videoStream]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(par->codec_id);
            
            // FPS wideo
            if (_fmt->streams[_videoStream]->avg_frame_rate.den != 0)
                _fps = (double)_fmt->streams[_videoStream]->avg_frame_rate.num / (double)_fmt->streams[_videoStream]->avg_frame_rate.den;
            else
                _fps = 30;

            _videoCtx = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_videoCtx, par);
            ffmpeg.avcodec_open2(_videoCtx, codec, null);

            _width = _videoCtx->width;
            _height = _videoCtx->height;

            if (_videoBuffer != null)
            {
                ffmpeg.av_free(_videoBuffer);
                _videoBuffer = null;
            }

            // BGRA32 dla WPF
            //_videoStride = _width * 4;
            //_videoBuffer = (byte*)ffmpeg.av_malloc((ulong)(_videoStride * _height));
            _videoStride = _width * 4;
            int requiredSize = _videoStride * _height;

            if (_videoBuffer == null || _videoBufferSize != requiredSize)
            {
                if (_videoBuffer != null)
                    ffmpeg.av_free(_videoBuffer);

                _videoBuffer = (byte*)ffmpeg.av_malloc((ulong)requiredSize);
                _videoBufferSize = requiredSize;
            }

            if (_sws != null)
            {
                ffmpeg.sws_freeContext(_sws);
                _sws = null;
            }

            _sws = ffmpeg.sws_getContext(
                _width, _height, _videoCtx->pix_fmt,
                _width, _height, AVPixelFormat.AV_PIX_FMT_BGRA,
                (int)SwsFlags.SWS_FAST_BILINEAR,
                null, null, null);

            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    _wb = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
            //    FrameReady?.Invoke(_wb);
            //});

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_wb == null || _wb.PixelWidth != _videoCtx->width || _wb.PixelHeight != _videoCtx->height)
                {
                    _wb = new WriteableBitmap(_videoCtx->width, _videoCtx->height, 96, 96, PixelFormats.Bgra32, null);
                    FrameReady?.Invoke(_wb);
                }
            });

            Debug.WriteLine($"✅ Video Init: {_width}x{_height}, FPS: {_fps:F2}");
        }

        //private void VideoDecodeLoop(CancellationToken token)
        //{
        //    _videoFrame = ffmpeg.av_frame_alloc();
        //    AVPacket* pkt = null;
        //    try
        //    {
        //        foreach (var ip in _videoQueue.GetConsumingEnumerable(token))
        //        {
        //            if (_stop) break;

        //            pkt = (AVPacket*)ip;

        //            ffmpeg.avcodec_send_packet(_videoCtx, pkt);

        //            while (ffmpeg.avcodec_receive_frame(_videoCtx, _videoFrame) == 0)
        //            {
        //                if (_pause) break;

        //                double video_pts = _videoFrame->best_effort_timestamp * ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);

        //                double audioClock = GetAudioClock();
        //                double bufferedAudio = _audioBuffer.BufferedBytes / (double)_audioBuffer.WaveFormat.AverageBytesPerSecond;
        //                //double diff = video_pts - audioClock;
        //                double diff = video_pts - (audioClock - bufferedAudio);

        //                IfSeek(video_pts);

        //                if (!_grabFrame)
        //                    SyncAV(diff);

        //                RenderFrame();

        //                ffmpeg.av_frame_unref(_videoFrame);
        //            }

        //            ffmpeg.av_packet_free(&pkt);
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    { }
        //    catch (ObjectDisposedException)
        //    { }
        //    finally
        //    {
        //        if (pkt != null)
        //            ffmpeg.av_packet_free(&pkt);
        //    }
        //}
        private double _videoPtsBase = double.NaN;
        private void DecodeVideo(AVPacket* pkt)
        {
            ffmpeg.avcodec_send_packet(_videoCtx, pkt);

            while (ffmpeg.avcodec_receive_frame(_videoCtx, _videoFrame) == 0)
            {
                if (_stop) return;
                if (_pause && !_forceRenderAfterSeek) return;

                // --- PTS i synchronizacja ---
                //double video_pts = _videoFrame->best_effort_timestamp * ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);
                long pts = _videoFrame->pts;
                if (pts == ffmpeg.AV_NOPTS_VALUE)
                    pts = _videoFrame->best_effort_timestamp;

                double video_pts = pts * ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);
                
                if (_afterSeek)
                {
                    if (double.IsNaN(_videoPtsBase))
                    {
                        _videoPtsBase = video_pts;
                        ffmpeg.av_frame_unref(_videoFrame);
                        return;
                    }

                    video_pts -= _videoPtsBase;
                }

                double audioClock = GetAudioClock();
                double diff = video_pts - audioClock;

                //if (diff > 1.0) diff = 1.0;
               //if (diff < -1.0) diff = -1.0;

                if (System.Math.Abs(diff) > 1.0)
                {
                    // ignoruj duże skoki PTS
                    diff = 0;
                }

                if (diff < -0.1)
                {
                    ffmpeg.av_frame_unref(_videoFrame);
                    return;
                }

                _videoPts = video_pts;

                Debug.WriteLine($"AUDIO CLOCK:{audioClock:F3}s | VIDEO PTS:{video_pts:F3}s | DIFF:{diff:F3}");

                if (_afterSeek && video_pts < _seekVideoTarget)
                {
                    ffmpeg.av_frame_unref(_videoFrame);
                    continue; // ⬅️ NIE return
                }

                // --- SEEK / SYNC ---
                IfSeek(video_pts);

                if (_syncAfterStop)
                {
                    _audioClockOffset = video_pts;
                    _lastAudioClock = 0;
                    _lastReported = 0;
                    _syncAfterStop = false;

                    Debug.WriteLine("[SYNC AFTER STOP @ 0.0]");
                }

                if (_forceRenderAfterSeek)
                {
                    RenderFrame();
                    _forceRenderAfterSeek = false;
                    ffmpeg.av_frame_unref(_videoFrame);
                    continue;
                }

                // --- FRAME GRAB ---
                if (_grabFrame)
                {
                    if (video_pts + 0.0005 < _grabTarget.TotalSeconds)
                    {
                        ffmpeg.av_frame_unref(_videoFrame);
                        return;
                    }

                    RenderFrame();
                    _grabResult = CloneWriteableBitmap(_wb);
                    _grabFrame = false;
                    _grabWait.Set();

                    ffmpeg.av_frame_unref(_videoFrame);
                    return;
                }

                // --- SYNCHRONIZACJA ---
                if (!_grabFrame)
                    SyncAV(diff);

                // --- RENDER ---
                RenderFrame();

                ffmpeg.av_frame_unref(_videoFrame);
            }
        }

        private void RenderFrame()
        {
            // --- sprawdzenie czy klatka jest gotowa ---
            if (_videoFrame == null || _videoFrame->data[0] == null || _videoFrame->linesize[0] <= 0)
                return;

            int frameWidth = _videoFrame->width;
            int frameHeight = _videoFrame->height;

            // --- alokacja bufora i WriteableBitmap przy pierwszej klatce lub zmianie rozdzielczości ---
            if (_wb == null || _width != frameWidth || _height != frameHeight)
            {
                _width = frameWidth;
                _height = frameHeight;
                _videoStride = _width * 4; // BGRA32
                _videoBufferSize = _videoStride * _height;

                if (_videoBuffer != null)
                    ffmpeg.av_free(_videoBuffer);

                _videoBuffer = (byte*)ffmpeg.av_malloc((ulong)_videoBufferSize);

                _wb = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
                Debug.WriteLine($"[RenderFrame] Nowy bufor: {_width}x{_height}, stride: {_videoStride}, size: {_videoBufferSize}");
            }

            // --- sws_scale ---
            var dstData = new byte_ptrArray4();
            var dstLines = new int_array4();
            dstData[0] = _videoBuffer;
            dstLines[0] = _videoStride;
            if (_videoFrame == null || _videoFrame->data[0] == null || _videoFrame->linesize[0] <= 0)
            {
                Debug.WriteLine("Frame not ready, skipping sws_scale");
                return;
            }
            int res = ffmpeg.sws_scale(
                _sws,
                _videoFrame->data,
                _videoFrame->linesize,
                0,
                _videoCtx->height,
                dstData,
                dstLines);

            if (res <= 0)
            {
                Debug.WriteLine("[RenderFrame] sws_scale nie zwróciło żadnych pikseli.");
                return;
            }

            // --- WritePixels ---
            if (_videoBufferSize <= 0 || _width <= 0 || _height <= 0)
                return;

            try
            {
                _wb.Dispatcher.BeginInvoke(() =>
                {
                    if (_isClosing) return;

                    _wb.Lock();
                    Buffer.MemoryCopy(
                        _videoBuffer,
                        (void*)_wb.BackBuffer,
                        _videoBufferSize,
                        _videoBufferSize);

                    try
                    {
                        _wb.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
                    }
                    catch
                    {
                        throw new ArgumentOutOfRangeException();
                    }

                    _wb.Unlock();
                }, DispatcherPriority.Background);
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
                BufferDuration = TimeSpan.FromSeconds(1)
            };
            _swr = swr;

            //_audioBuffer.ClearBuffer();
            
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioBuffer);
            _waveOut.Play();
        }

        //private double GetAudioClock()
        //{
        //    if (_waveOut == null || _audioBuffer == null)
        //        return _lastAudioClock;

        //    // ile już zostało odtworzone
        //    long bytesPlayed = _waveOut.GetPosition();
        //    int bytesPerSecond = _audioBuffer.WaveFormat.AverageBytesPerSecond;

        //    double played = (bytesPerSecond > 0) ? (double)bytesPlayed / bytesPerSecond : 0;

        //    // dodaj offset przy seek/stop
        //    _lastAudioClock = _audioClockOffset + played;

        //    return _lastAudioClock;
        //}

        private double GetAudioClock()
        {
            //if (_waveOut == null || _audioBuffer == null)
            //    return _lastAudioClock;

            //if (_waveOut.PlaybackState != PlaybackState.Playing)
            //    return _lastAudioClock;

            //long bytesPlayed = _waveOut.GetPosition();
            //int bytesPerSecond = _audioBuffer.WaveFormat.AverageBytesPerSecond;

            //if (bytesPerSecond > 0)
            //{
            //    double played = (double)bytesPlayed / bytesPerSecond;
            //    _lastAudioClock = _audioClockOffset + played;
            //}

            //return _lastAudioClock;
            if (_waveOut == null || _audioBuffer == null)
                return _lastAudioClock;

            long bytesPlayed = _waveOut.GetPosition() - _audioBytesBase;
            if (bytesPlayed < 0) bytesPlayed = 0;

            int bytesPerSecond = _audioBuffer.WaveFormat.AverageBytesPerSecond;
            if (bytesPerSecond <= 0)
                return _lastAudioClock;

            double played = (double)bytesPlayed / bytesPerSecond;
            _lastAudioClock = _audioClockOffset + played;

            return _lastAudioClock;
        }

        //private void AudioDecodeLoop(CancellationToken token)
        //{
        //    _audioFrame = ffmpeg.av_frame_alloc();
        //    AVPacket* pkt = null;
        //    try
        //    {
        //        foreach (var ip in _audioQueue.GetConsumingEnumerable(token))
        //        {
        //            if (_stop) break;

        //            pkt = (AVPacket*)ip;

        //            ffmpeg.avcodec_send_packet(_audioCtx, pkt);

        //            while (ffmpeg.avcodec_receive_frame(_audioCtx, _audioFrame) == 0)
        //            {
        //                int outSamples = ffmpeg.swr_get_out_samples(_swr, _audioFrame->nb_samples);
        //                byte* outBuf = (byte*)ffmpeg.av_malloc((ulong)(outSamples * 4));

        //                byte** arr = stackalloc byte*[1];
        //                arr[0] = outBuf;

        //                int samples = ffmpeg.swr_convert(
        //                    _swr, arr, outSamples,
        //                    _audioFrame->extended_data,
        //                    _audioFrame->nb_samples);

        //                if (samples > 0)
        //                {
        //                    int bytes = samples * 4;
        //                    byte[] managed = new byte[bytes];

        //                    fixed (byte* dst = managed)
        //                        Buffer.MemoryCopy(outBuf, dst, bytes, managed.Length);

        //                    _audioBuffer.AddSamples(managed, 0, bytes);
        //                }

        //                // wymagane do synchronizacji seek
        //                if (_audioAfterSeek && samples > 0)
        //                {
        //                    _audioClockOffset = _seekTarget.TotalSeconds;
        //                    _lastAudioClock = _audioClockOffset;
        //                    _audioAfterSeek = false;
        //                }

        //                ffmpeg.av_free(outBuf);
        //                ffmpeg.av_frame_unref(_audioFrame);
        //            }

        //            ffmpeg.av_packet_free(&pkt);
        //        }
        //    }
        //    catch (OperationCanceledException) 
        //    { }
        //    catch (ObjectDisposedException) 
        //    { }
        //    finally
        //    {
        //        if (pkt != null)
        //            ffmpeg.av_packet_free(&pkt);
        //    }
        //}

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
                //if (_audioAfterSeek && samples > 0)
                //{
                //    _audioClockOffset = _seekTarget.TotalSeconds;
                //    _lastAudioClock = _audioClockOffset;
                //    _audioAfterSeek = false;
                //}

                if (_audioAfterSeek)
                {
                    _audioAfterSeek = false; // tylko flagę
                }

                ffmpeg.av_free(outBuf);
                ffmpeg.av_frame_unref(_audioFrame);
            }
        }

        #endregion

        #region === DISPOSE ===

        public void Dispose()
        {
            _stop = true;
            _pause = true;
            _isClosing = true;
            _running = false;

            //// Zakończ kolejki
            //if (!_videoQueue.IsAddingCompleted)
            //    _videoQueue.CompleteAdding();
            //if (!_audioQueue.IsAddingCompleted)
            //    _audioQueue.CompleteAdding();

            //_cts.Cancel();

            //// Oczekiwanie na wątki
            //_videoThread?.Join(500);
            //_audioThread?.Join(500);
            //_readerThread?.Join(500);

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
