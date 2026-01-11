// Version: 0.0.1.91
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using FFmpeg.AutoGen;

using FFmpegApi.Views;

using Media;

using NAudio.Wave;

using Thmd.Views;

namespace FFmpegApi
{
    public partial class VideoPlayer : UserControl, IPlayer, INotifyPropertyChanged
    {
        private FFmpegVideoPlayer _videoPlayer = new FFmpegVideoPlayer();
        private TimeSpan _position;
        private TimeSpan _duration;
        private double _volume;

        #region WinAPI

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint BLOCK_SLEEP_MODE = 2147483651u;   // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        private const uint DONT_BLOCK_SLEEP_MODE = 2147483648u; // ES_CONTINUOUS

        #endregion

        public Playlist Playlist => PlaylistView;

        public Controlbar Controlbar => throw new NotImplementedException();

        public InfoBox InfoBox => throw new NotImplementedException();

        public ProgressbarView ProgressBar => ProgressbarView;

        public FrameworkElement Subtitle => throw new NotImplementedException();

        public TimeSpan Position 
        { 
            get => _position; 
            set 
            { 
                _position = _videoPlayer.Position;
                OnPropertyChanged(nameof(Position), ref _position, value);
            }
        }

        public TimeSpan Duration => _duration;

        public double Volume { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool isFullscreen { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool isPlaying { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool isPaused { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool isMute { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool isStopped { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event EventHandler<EventArgs> TimeChanged;
        public event EventHandler<EventArgs> Playing;
        public event EventHandler<EventArgs> Paused;
        public event EventHandler<EventArgs> Stopped;
        public event PropertyChangedEventHandler PropertyChanged;

        public VideoPlayer()
        {
            InitializeComponent();
            var path1 = @"F:\Filmy\Futurama\Futurama S07E03 PL 720p.mp4";
            var path2 = @"E:\films\Futurama\Season5-6\2 - Futurama 5-7.mp4";
            var path3 = @"E:\films\Rebel_Moon_A_Child_of_Fire_part_1_Dubbing.mp4";
            var path4 = @"E:\films\Loki.S02E06.MULTi.720p.DSNP.WEB-DL.H264.DDP5.1.Atmos-Donnek.mp4";
            _videoPlayer.FrameReady += bitmap =>
            {
                VideoImage.Dispatcher.Invoke(() =>
                {
                    VideoImage.Source = bitmap;
                });
                _textblockuraDuration.Text = _duration.ToString("hh\\:mm\\:ss");
            };
            _videoPlayer.TimeChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _textblockuraDuration.Text = _videoPlayer.Duration.ToString("hh\\:mm\\:ss");
                    _position = e.Position;
                    _textblockPosition.Text = _position.ToString("hh\\:mm\\:ss");
                });
            };
            _videoPlayer.Play(path1);
        }

        public void Dispose()
        {
            _videoPlayer?.Dispose();
        }

        public void Next()
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public void Play()
        {
            throw new NotImplementedException();
        }

        public void Play(MediaItem media)
        {
            throw new NotImplementedException();
        }

        public void PlayNext(MediaItem media)
        {
            throw new NotImplementedException();
        }

        public void Preview()
        {
            throw new NotImplementedException();
        }

        public void Seek(TimeSpan time)
        {
            throw new NotImplementedException();
        }

        public void Seek(TimeSpan time, SeekDirection seek_direction)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void TogglePlayPause()
        {
            throw new NotImplementedException();
        }

        private void TogglePlayPause_Click(object sender, RoutedEventArgs e)
        {
            _videoPlayer.TogglePlayPause();
        }

        private void _pause_Click(object sender, RoutedEventArgs e)
        {
            _videoPlayer.TogglePlayPause();
        }

        private void _resume_Click(object sender, RoutedEventArgs e)
        {
            _videoPlayer.TogglePlayPause();
        }

        private void _seek_Click(object sender, RoutedEventArgs e)
        {
            _videoPlayer.Seek(TimeSpan.FromMinutes(5));
        }

        protected void OnPropertyChanged<T>(string propertyName, ref T field, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}


//// Version: 0.0.0.1

//    /// <summary>
//    /// Logika interakcji dla klasy UserControl1.xaml
//    /// </summary>
//    using System;
//    using System.Collections.Generic;
//    using System.ComponentModel;
//    using System.IO;
//    using System.Net.Sockets;
//    using System.Runtime.InteropServices;
//    using System.Threading;
//    using System.Windows;
//    using System.Windows.Controls;
//    using System.Windows.Media;
//    using System.Windows.Media.Imaging;
//    using System.Windows.Threading;

//    using FFmpeg.AutoGen;

//using FFmpegApi.Logs;

//using NAudio.Utils;
//    using NAudio.Wave;

//namespace FFmpegApi
//{
//    public unsafe partial class Player : UserControl, IDisposable, INotifyPropertyChanged
//    {
//        #region Publiczne eventy (zdarzenia)

//        public event Action<WriteableBitmap> FrameReady;
//        public event Action<TimeSpan> PositionChanged;
//        public event PropertyChangedEventHandler PropertyChanged;

//        #endregion

//        #region Prywatne zmienne
//        // FFMpeg
//        private string _dllPath = @"ffmpeg";


//        // ZARZĄDZANIE ODTWARZANIEM
//        private Thread _decodeThread;
//        private bool _run = true, _pause = false, _seek = false;
//        private long _seekTimestamp = -1, _seekTargetPts;
//        private TimeSpan _seekTime, _currentTime;

//        // BITMAP
//        private WriteableBitmap _wb;

//        // File path
//        private string _filePath = string.Empty;

//        // VIDEO
//        private AVFormatContext* _fmt;
//        private AVCodecContext* _videoCtx;
//        private int _videoStream = -1, _width, _height, _bufferSize;
//        private SwsContext* _sws;
//        private byte* _bufferPtr;
//        private AVPacket* _packet;
//        private AVFrame* _frame;
//        private AVFrame* _rgbFrame;

//        // AUDIO
//        private int _audioStream = -1;
//        private AVCodecContext* _audioCtx;
//        private SwrContext* _swr;
//        private WaveOutEvent _waveOut;
//        private AudioFileReader _audioFileReader;
//        private BufferedWaveProvider _audioBuffer;
//        private System.Timers.Timer _syncTimer;

//        // Log
//        private bool _logger = false;

//        #endregion

//        #region Publiczne własności

//        public TimeSpan Duration { get; private set; }
//        public TimeSpan Position { get; private set; }
//        public bool isPause { get; set; }
//        public bool isPlaying { get; set; }
//        public bool isStopped { get; set; }

//        #endregion

//        #region Constructor

//        public Player()
//        {
//            ffmpeg.RootPath = $@"{_dllPath}";
//            ffmpeg.avformat_network_init();

//            // LOGS
//            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_DEBUG);

//            if (_logger)
//            {
//                Logger.InitLogs();
//            }
//        }

//        public Player(string dll_path = null) : this()
//        {
//            if (dll_path == null)
//                _dllPath = @"ffmpeg";
//        }

//        #endregion



//        #region Pętla dekodująca

//        private void DecodeLoop(string path)
//        {
//            var _fmt = ffmpeg.avformat_alloc_context();
//            if (ffmpeg.avformat_open_input(&_fmt, path, null, null) < 0) return;
//            if (ffmpeg.avformat_find_stream_info(_fmt, null) < 0) return;

//            InitializeVideo(_fmt);

//            var video_parameters = _fmt->streams[_videoStream]->codecpar;

//            var video_codec = ffmpeg.avcodec_find_decoder(video_parameters->codec_id);
//            // Przypisanie AVCodecContext jako wartość lokalną
//            var _videoCtx = ffmpeg.avcodec_alloc_context3(video_codec);
//            ffmpeg.avcodec_parameters_to_context(_videoCtx, video_parameters);

//            if (ffmpeg.avcodec_open2(_videoCtx, video_codec, null) < 0)
//                return;

//            _width = _videoCtx->width;
//            _height = _videoCtx->height;

//            var _sws = ffmpeg.sws_getContext(
//                _width, _height, _videoCtx->pix_fmt,
//                _width, _height, AVPixelFormat.AV_PIX_FMT_BGR24,
//                (int)SwsFlags.SWS_FAST_BILINEAR,
//                null, null, null);

//            var _packet = ffmpeg.av_packet_alloc();
//            var _frame = ffmpeg.av_frame_alloc();
//            var _rgbFrame = ffmpeg.av_frame_alloc();

//            _bufferSize = _width * _height * 3;
//            var _bufferPtr = (byte*)ffmpeg.av_malloc((ulong)_bufferSize);

//            var dstData = new byte_ptrArray4();
//            var dstLinesize = new int_array4();
//            dstData[0] = _bufferPtr;
//            dstLinesize[0] = _width * 3;

//            // Rysowanie bitmapy
//            CreateWriteableBitmap();


//            // ===============================================================================================================================================================



//            // Odtwarzanie dźwięku
//            _audioFileReader = new AudioFileReader(path);

//            _audioBuffer = new BufferedWaveProvider(new WaveFormat(44100, 16, 2))
//            {
//                DiscardOnBufferOverflow = true,
//            };

//            _waveOut = new WaveOutEvent();
//            _waveOut.Init(_audioBuffer);
//            _waveOut.Play();

//            // ===============================================================================================================================================================

//            //_audioFileReader = new AudioFileReader(_filePath);

//            //_waveOut = new WaveOutEvent();
//            //_waveOut.DesiredLatency = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
//            //_waveOut.Init(_audioFileReader);

//            //_audioBuffer = new BufferedWaveProvider(_waveOut.OutputWaveFormat);

//            //_waveOut.Play();

//            while (_run && ffmpeg.av_read_frame(_fmt, _packet) >= 0)
//            {

//                if (_packet->stream_index != _videoStream)
//                {
//                    ffmpeg.av_packet_unref(_packet);
//                    continue;
//                }

//                if (_packet->stream_index == _audioStream)
//                {
//                    //DecodeAudio(_packet);
//                    ffmpeg.av_packet_unref(_packet);
//                    continue;
//                }

//                VideoFrame(_fmt, _bufferPtr, dstData, dstLinesize, _sws, _videoCtx, _frame, _packet);

//                ffmpeg.avcodec_send_packet(_videoCtx, _packet);

//                ffmpeg.av_packet_unref(_packet);
//            }

//            // Flush dekodera
//            FlushVideoDecoder(_bufferPtr, dstData, dstLinesize, _sws, _videoCtx, _frame);

//            CleanUp();
//        }

//        #endregion

//        #region Metody prywatne

//        private void InitializeVideo(AVFormatContext* fmt)
//        {
//            _videoStream = -1;

//            for (int i = 0; i < fmt->nb_streams; i++)
//            {
//                if (fmt->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
//                {
//                    _videoStream = i;
//                    break;
//                }
//            }
//            if (_videoStream == -1)
//                return;
//        }

//        private void InitializeAudio(AVFormatContext* fmt)
//        {
//            _audioStream = -1;

//            for (int i = 0; i < fmt->nb_streams; i++)
//            {
//                if (fmt->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
//                {
//                    _audioStream = i;
//                    break;
//                }
//            }
//            if (_audioStream == -1)
//                return;
//        }

//        private void CreateWriteableBitmap()
//        {
//            Application.Current.Dispatcher.InvokeAsync(() =>
//            {
//                _wb = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr24, null);
//                FrameReady?.Invoke(_wb);
//            });
//        }

//        private unsafe void ReadDuration(AVFormatContext* fmt)
//        {
//            if (fmt->duration >= 0)
//                Duration = TimeSpan.FromSeconds(fmt->duration / ffmpeg.AV_TIME_BASE);
//            else
//                Duration = TimeSpan.Zero;
//        }

//        private void UpdatePosition(AVFormatContext* fmt, AVFrame* frame)
//        {
//            if (frame->pts == ffmpeg.AV_NOPTS_VALUE)
//                return;

//            AVStream* stream = fmt->streams[_videoStream];

//            double seconds = frame->pts * ffmpeg.av_q2d(stream->time_base);

//            Position = TimeSpan.FromSeconds(seconds);

//            PositionChanged?.Invoke(Position);
//        }

//        private void SyncVideoWithAudio()
//        {
//            if (_audioFileReader != null && Position != null)
//            {
//                // Oblicz różnicę między bieżącą pozycją audio a wideo
//                TimeSpan audioPos = _audioFileReader.CurrentTime;
//                TimeSpan diff = Position - audioPos;

//                if (System.Math.Abs(diff.TotalMilliseconds) > 100)  // Tolerancja 50ms, dostosuj
//                {

//                    _audioFileReader.CurrentTime = Position;  // Przeskocz audio do pozycji wideo
//                }
//            }
//        }

//        private void StartSyncTimer()
//        {
//            _syncTimer = new System.Timers.Timer(100);  // Co 100ms
//            _syncTimer.Elapsed += (s, e) => SyncVideoWithAudio();
//            _syncTimer.Start();
//        }

//        private void VideoFrame(AVFormatContext* fmt, byte* buffer_ptr, byte_ptrArray4 dst_data, int_array4 dst_linesize, SwsContext* sws, AVCodecContext* ctx, AVFrame* frame, AVPacket* packet)
//        {
//            //_syncTimer = new System.Timers.Timer((double)TimeSpan.FromMilliseconds(1).TotalMilliseconds);
//            //_syncTimer.Start();
//            //_waveOut.Play();

//            // Ramka
//            while (ffmpeg.avcodec_receive_frame(ctx, frame) == 0)
//            {
//                UpdatePosition(fmt, frame);

//                // SyncVideoWithAudio();

//                // Pauzowanie odtwarzania
//                while (_pause && _run)
//                {
//                    Thread.Sleep(50);
//                    _waveOut.Pause();
//                }

//                if (!_pause)
//                {
//                    _waveOut.Play();
//                }

//                if (_seek)
//                {
//                    _seek = false;
//                    int ret = ffmpeg.av_seek_frame(
//                        fmt,
//                        _videoStream,
//                        SeekTargetTime(fmt, _seekTime),
//                        (int)ffmpeg.AVSEEK_FLAG_BACKWARD
//                    );

//                    //_audioFileReader->WriteTimeout = ret;
//                    //_audioFileReader.CurrentTime = _seekTime;

//                    SyncVideoWithAudio();

//                    if (ret < 0)
//                        return;
//                }

//                ffmpeg.sws_scale(
//                    sws,
//                    frame->data,
//                    frame->linesize,
//                    0,
//                    _height,
//                    dst_data,
//                    dst_linesize);

//                // Aktualizacja WriteableBitmap _width wątku UI
//                if (_wb != null)
//                {
//                    _wb.Dispatcher.InvokeAsync(() =>
//                    {
//                        // Inna implementacja bez tworzenia każdej lini
//                        //_wb.Lock();
//                        //IntPtr ptr = _wb.BackBuffer;
//                        //Buffer.MemoryCopy((byte*)buffer_ptr, (void*)ptr, _bufferSize, _bufferSize);
//                        //_wb.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
//                        //_wb.Unlock();

//                        _wb.Lock();

//                        byte* dst = (byte*)_wb.BackBuffer;
//                        byte* src = buffer_ptr;

//                        int srcStride = _width * 3;
//                        int dstStride = _wb.BackBufferStride;

//                        for (int y = 0; y < _height; y++)
//                        {
//                            Buffer.MemoryCopy(
//                                src + y * srcStride,
//                                dst + y * dstStride,
//                                dstStride,
//                                srcStride);
//                        }

//                        _wb.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
//                        _wb.Unlock();
//                    });
//                }
//                ffmpeg.av_frame_unref(frame);

//                // Szybkość odświerzania:
//                // ~120 FPS = 0,
//                //  ~60 FPS = 15,
//                //  ~50 FPS = 20,
//                //  ~40 FPS = 25,
//                //  ~30 FPS = 30,
//                //  ~23 FPS = 43, - większość plików wideo
//                Thread.Sleep(43);
//            }
//        }

//        private void FlushVideoDecoder(byte* buffer_ptr, byte_ptrArray4 dst_data, int_array4 dst_linesize, SwsContext* sws, AVCodecContext* ctx, AVFrame* frame)
//        {
//            // Flush dekodera
//            ffmpeg.avcodec_send_packet(ctx, null);
//            while (ffmpeg.avcodec_receive_frame(ctx, frame) == 0)
//            {
//                ffmpeg.sws_scale(
//                      sws,
//                      frame->data,
//                      frame->linesize,
//                      0,
//                      _height,
//                      dst_data,
//                      dst_linesize);

//                _wb.Dispatcher.InvokeAsync(() =>
//                {
//                    // Inna implementacja bez tworzenia każdej lini
//                    //_wb.Lock();
//                    //IntPtr ptr = _wb.BackBuffer;
//                    //Buffer.MemoryCopy(_bufferPtr, (void*)ptr, _bufferSize, _bufferSize);
//                    //_wb.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
//                    //_wb.Unlock();

//                    _wb.Lock();

//                    byte* dst = (byte*)_wb.BackBuffer;
//                    byte* src = buffer_ptr;

//                    int srcStride = _width * 3;
//                    int dstStride = _wb.BackBufferStride;

//                    for (int y = 0; y < _height; y++)
//                    {
//                        Buffer.MemoryCopy(
//                            src + y * srcStride,
//                            dst + y * dstStride,
//                            dstStride,
//                            srcStride);
//                    }

//                    _wb.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
//                    _wb.Unlock();
//                });

//                ffmpeg.av_frame_unref(frame);
//            }
//        }

//        #endregion

//        #region Metody czyszczące

//        private void CleanUp()
//        {
//            AVFormatContext* fmt = _fmt;
//            AVCodecContext* ctx = _videoCtx;
//            AVPacket* packet = _packet;
//            AVFrame* frame = _frame;
//            AVFrame* rgbFrame = _rgbFrame;

//            _audioBuffer.ClearBuffer();
//            ffmpeg.av_free(_bufferPtr);
//            if (_swr != null)
//            {
//                SwrContext* swr = _swr;
//                ffmpeg.swr_close(_swr);
//                ffmpeg.swr_free(&swr);
//            }
//            ffmpeg.sws_freeContext(_sws);
//            ffmpeg.av_frame_free(&rgbFrame);
//            ffmpeg.av_frame_free(&frame);
//            ffmpeg.av_packet_free(&packet);
//            ffmpeg.avcodec_free_context(&ctx);
//            ffmpeg.avformat_close_input(&fmt);
//        }

//        #endregion

//        #region Metody publiczne

//        public void Stop() => _run = false;

//        public void Pause() => _pause = true;

//        public void TogglePlayPause() => _pause = !_pause;

//        public void Resume()
//        {
//            _pause = false;
//            _waveOut.Play();
//        }

//        public void Play(string file)
//        {
//            _filePath = file;
//            _run = true;
//            _decodeThread = new Thread(() => DecodeLoop(file));
//            _decodeThread.IsBackground = true;
//            _decodeThread.Start();

//            SyncVideoWithAudio();
//        }

//        public void Seek(TimeSpan time)
//        {
//            _seekTime = time;
//            _seek = true;
//        }



//        public void Seek(TimeSpan time, SeekDirection direction)
//        {
//            switch (direction)
//            {
//                case SeekDirection.Forward:
//                    var seek_time = Position + time;
//                    if (seek_time > Duration)
//                    {
//                        seek_time = Duration;
//                    }
//                    _seekTime = seek_time;
//                    //SyncVideoWithAudio();
//                    break;
//                case SeekDirection.Backward:
//                    seek_time = Position - time;
//                    if (seek_time < TimeSpan.Zero)
//                    {
//                        seek_time = TimeSpan.Zero;
//                    }
//                    _seekTime = seek_time;
//                    //SyncVideoWithAudio();
//                    break;
//            }
//            _seek = true;
//        }

//        private long SeekTargetTime(AVFormatContext* fmt, TimeSpan position)
//        {
//            long targetPts = (long)(
//                position.TotalSeconds /
//                ffmpeg.av_q2d(fmt->streams[_videoStream]->time_base)
//            );

//            _audioFileReader.CurrentTime = position;// position-TimeSpan.FromSeconds(1);
//            //SyncVideoWithAudio();
//            //_freader.Seek((long)position.TotalSeconds,SeekOrigin.Current);
//            SyncVideoWithAudio();
//            return targetPts;
//        }


//        #endregion

//        #region PropertyChanged

//        protected void OnPropertyChanged<T>(string propertyName, ref T field, T value)
//        {
//            if (!EqualityComparer<T>.Default.Equals(field, value))
//            {
//                field = value;
//                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//            }
//        }

//        protected void OnPropertyChanged(string propertyName)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        #endregion

//        #region Disposing

//        public void Dispose()
//        {
//            _audioFileReader.Dispose();
//            _waveOut?.Stop();
//            _waveOut?.Dispose();
//            Stop();
//            CleanUp();
//        }

//        public void Dispose(bool disposing)
//        {
//            if (disposing)
//            {
//                Dispose();
//            }
//        }

//        #endregion
//    }
//}

//    /*
//     Wywołanie:

//    Thmd.Ffmpeg.Player _player = new Player();

//                _player.FrameReady += (bitmap) =>
//                {
//                    _videoimage.Dispatcher.InvokeAsync(() =>
//                    {
//                        _textBox.Text = _player.Duration;
//                        _videoimage.Source = bitmap;
//                    });
//                };

//                _player.Play(@"F:\Filmy\Calineczka-WarnerBros-PL.mp4");
//     */
