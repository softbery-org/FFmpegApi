// Version: 0.0.2.93
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using FFmpeg.AutoGen;

using FFmpegApi.Logs;

using NAudio.Wave;

namespace FFmpegApi;

public unsafe class FFmpegVideoPlayer : IDisposable, INotifyPropertyChanged
{
    public event Action<WriteableBitmap> FrameReady;
    public event Action<TimeSpan> PositionChanged;
    public event PropertyChangedEventHandler PropertyChanged;

    private AutoResetEvent _pauseEvent = new AutoResetEvent(true);

    private const double SyncThreshold = 0.005; // 5 ms
    private const double MaxVideoAdjust = 0.04; // max ±4% speed

    private Stopwatch _videoClock = new Stopwatch();
    private double _videoStartPts;

    private Thread _decodeThread;
    private volatile bool _running;
    private volatile bool _paused;
    private volatile bool _stop;

    private AVFormatContext* _fmt;
    private double _fps;
    private AVCodecContext* _videoCtx;
    private AVCodecContext* _audioCtx;
    private SwsContext* _sws;
    private SwrContext* _swr;

    private AVPacket* _packet;
    private AVFrame* _videoFrame;
    private AVFrame* _audioFrame;

    private int _width;
    private int _height;

    private int _videoStream = -1;
    private int _audioStream = -1;

    private WriteableBitmap _wb;
    private byte* _videoBuffer;
    private int _videoStride;

    // AUDIO
    private WaveOutEvent _waveOut;
    private BufferedWaveProvider _audioBuffer;
    private double _audioClock;

    private string _filePath;
    private double _lastAudioClock;
    private TimeSpan _seekTarget;
    private bool _seeking;
    private long _playedSamples;

    private struct VideoFrame
    {
        public double Pts;
        public IntPtr Buffer;
    }

    private readonly BlockingCollection<IntPtr> _audioQueue = new(100);
    private readonly BlockingCollection<IntPtr> _videoQueue = new(100);


    private const int MaxVideoQueue = 5;
    private readonly object _videoLock = new();

    public TimeSpan Duration { get; private set; }
    public TimeSpan Position { get; private set; }

    public FFmpegVideoPlayer()
    {
        ffmpeg.RootPath = "ffmpeg";
        ffmpeg.avformat_network_init();
    }

    #region PUBLIC

    public void Play(string path)
    {
        _filePath = path;
        _running = true;
        _stop = false;
        _videoClock.Restart();
        _videoStartPts = 0;
        _decodeThread = new Thread(DecodeLoop) { IsBackground = true };
        _decodeThread.Start();
    }

    public void Pause()
    {
        _paused = true;
        _waveOut?.Pause();
        _pauseEvent?.Set();
    }

    public void Resume()
    {
        _paused = false;
        _waveOut?.Play();
        _pauseEvent?.Reset();
    }

    public void TogglePlayPause()
    {
        if (_paused)
            Resume();
        else
            Pause();
    }

    public void Seek(TimeSpan pos)
    {
        _seekTarget = pos;
        _seeking = true;

        _waveOut.Stop();
        _audioBuffer.ClearBuffer();

        ffmpeg.avcodec_flush_buffers(_audioCtx);
        ffmpeg.avcodec_flush_buffers(_videoCtx);
    }

    public void Dispose()
    {
        _pauseEvent.Close();
        _running = false;
        _waveOut?.Stop();
        _waveOut?.Dispose();

        var fmt = _fmt;
        ffmpeg.avformat_close_input(&fmt);
    }
    #endregion

    #region Prywatne metody

    private IntPtr CopyFrameToBuffer(AVFrame* frame)
    {
        int byteCount = _width * _height * 3; // BGR24
        byte* buffer = (byte*)ffmpeg.av_malloc((ulong)byteCount);

        var dstData = new byte_ptrArray4();
        var dstLines = new int_array4();

        dstData[0] = buffer;
        dstLines[0] = _width * 3;

        ffmpeg.sws_scale(
            _sws,
            frame->data,
            frame->linesize,
            0,
            _height,
            dstData,
            dstLines
        );

        return (IntPtr)buffer;
    }

    private void SyncVideoToAudio(double pts)
    {
        var delay = pts - GetAudioClock();
        // video za późno → drop

        if (delay > 0.1 && delay < 0.5)
        {
            Thread.Sleep((int)(delay * 1000));
        }
        else if (delay < -0.05)
        {
            ffmpeg.av_frame_unref(_videoFrame);
            return; // DROP FRAME
        }
    }

    private void UpdatePosition()
    {
        Position = TimeSpan.FromSeconds(GetAudioClock());
        PositionChanged?.Invoke(Position);
    }

    private double GetAudioClock(TimeSpan time)
    {
        int bytesPerSecond =
                _audioBuffer.WaveFormat.SampleRate *
                _audioBuffer.WaveFormat.Channels *
                (_audioBuffer.WaveFormat.BitsPerSample / 8);
        _lastAudioClock = (double)time.TotalSeconds / bytesPerSecond;

        return _lastAudioClock;
    }

    private double GetAudioClock()
    {
        if (_waveOut == null)
            return 0;

        if (_waveOut.PlaybackState != PlaybackState.Playing)
            return _lastAudioClock;

        try
        {
            long bytesPlayed = _waveOut.GetPosition();

            int bytesPerSecond =
                _audioBuffer.WaveFormat.SampleRate *
                _audioBuffer.WaveFormat.Channels *
                (_audioBuffer.WaveFormat.BitsPerSample / 8);

            _lastAudioClock = (double)bytesPlayed / bytesPerSecond;
            return _lastAudioClock;
        }
        catch (NAudio.MmException)
        {
            return _lastAudioClock;
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

        while (_running && ffmpeg.av_read_frame(_fmt, _packet) >= 0)
        {
            if(_paused && _running)
            {
                //_decodeThread.Suspend();
                Thread.Sleep(50);
                continue;
            }

            

            if (_packet->stream_index == _videoStream)
                DecodeVideo(_packet);

            if (_packet->stream_index == _audioStream)
                DecodeAudio(_packet);

            ffmpeg.av_packet_unref(_packet);
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

        _swr = ffmpeg.swr_alloc();

        AVChannelLayout outLayout;
        ffmpeg.av_channel_layout_default(&outLayout, 2);

        SwrContext* swr = _swr;
        ffmpeg.swr_alloc_set_opts2(
            &swr,
            &outLayout,
            AVSampleFormat.AV_SAMPLE_FMT_S16,
            _audioCtx->sample_rate,
            &_audioCtx->ch_layout,
            _audioCtx->sample_fmt,
            _audioCtx->sample_rate,
            0, null);

        ffmpeg.swr_init(_swr);

        _audioBuffer = new BufferedWaveProvider(new WaveFormat(_audioCtx->sample_rate, 16, _audioCtx->ch_layout.nb_channels))
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true
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
            if (_paused)
            {
                ffmpeg.av_frame_unref(_videoFrame);
                return;
            }

            double videoPts =
                _videoFrame->best_effort_timestamp *
                ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);

            if (_seeking)
            {
                // Oblicz target PTS
                videoPts = _seekTarget.TotalSeconds /
                                        ffmpeg.av_q2d(_fmt->streams[_videoStream]->time_base);
                // Seek w FFmpeg
                ffmpeg.av_seek_frame(_fmt, -1, (long)videoPts, ffmpeg.AVSEEK_FLAG_BACKWARD);

                // Flush dekodery
                ffmpeg.avcodec_flush_buffers(_videoCtx);
                ffmpeg.avcodec_flush_buffers(_audioCtx);

                _lastAudioClock = GetAudioClock(_seekTarget);
                Debug.WriteLine($"_lastAudioClock={_lastAudioClock} videoPts={videoPts} GetAudioClock={GetAudioClock()} GetAudioClock(seek)={GetAudioClock(_seekTarget)}");
                UpdatePosition();
                
                _seeking = false;
                _waveOut.Play();
            }

            double diff = videoPts - GetAudioClock();

            Debug.WriteLine($"diff={diff} videoPts={videoPts} GetAudioClock={GetAudioClock()}");

            if (diff > 0.01 && diff < 0.5)
            {
                Thread.Sleep((int)(diff * 1000));
            }
            else if (diff < -0.05)
            {
                ffmpeg.av_frame_unref(_videoFrame);
                continue; // DROP FRAME
            }
            //if (diff > 0.01)
            //    Thread.Sleep((int)(diff * 1000));
            //else if (diff < -0.05)
            //{
            //    ffmpeg.av_frame_unref(_videoFrame);
            //    continue; // DROP FRAME
            //}

            UpdatePosition();

            //Debug.WriteLine($"A={_audioClock:F3} V={videoPts:F3} Δ={(videoPts - _audioClock):F3}");

            //SyncVideoToAudio(videoPts);
            RenderFrame();
            Thread.Sleep((int)(1000 / _fps));

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

        _wb.Dispatcher.Invoke(() =>
        {
            _wb.Lock();
            Buffer.MemoryCopy(
                _videoBuffer,
                (void*)_wb.BackBuffer,
                _videoStride * _videoCtx->height,
                _videoStride * _videoCtx->height);

            _wb.AddDirtyRect(
                new Int32Rect(0, 0, _videoCtx->width, _videoCtx->height));
            _wb.Unlock();
        });
    }

    #endregion

    #region AUDIO

    private void DecodeAudio(AVPacket* pkt)
    {
        ffmpeg.avcodec_send_packet(_audioCtx, pkt);

        while (ffmpeg.avcodec_receive_frame(_audioCtx, _audioFrame) == 0)
        {
            if (_paused)
            {
                ffmpeg.av_frame_unref(_audioFrame);
                return;
            }

            int outSamples = ffmpeg.swr_get_out_samples(_swr, _audioFrame->nb_samples);
            byte* outBuf = (byte*)ffmpeg.av_malloc((ulong)(outSamples * 4));

            byte** arr = stackalloc byte*[1];
            arr[0] = outBuf;

            int samples = ffmpeg.swr_convert(
                _swr, arr, outSamples,
                _audioFrame->extended_data,
                _audioFrame->nb_samples);

            int bytes = samples * 4;
            byte[] managed = new byte[bytes];
            Marshal.Copy((IntPtr)outBuf, managed, 0, bytes);

            //_playedSamples += samples;
            _audioBuffer.AddSamples(managed, 0, bytes);

            //_audioClock =
            //    _audioFrame->best_effort_timestamp *
            //    ffmpeg.av_q2d(_fmt->streams[_audioStream]->time_base);

            ffmpeg.av_free(outBuf);
            ffmpeg.av_frame_unref(_audioFrame);
        }
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
