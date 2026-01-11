// Version: 0.0.0.2
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using FFmpegApi.Logs;

using MediaToolkit;
using MediaToolkit.Model;

using NAudio.Wave;

namespace Media;

public class MediaItem : UIElement, INotifyPropertyChanged
{
    private static int _nextIndex = 1; // statyczne, aby ID było unikalne
    private readonly int _index;
    private Uri _uri;
    private string _name;
    private double _position = 0.0;
    private double _volume = 1.0;
    private double _duration;
    private IPlayer _player;
    private double _fps;
    private Metadata _metadataMediaToolkit;
    private string _format;
    private string _frameSize;
    private string _audioFormat;
    private string _audioSampleRate;
    private int _audioBitRate;
    private string _audioChanelOutput;
    private bool _isPlaying;
    private bool _isIndents = false;
    private string _extension;
    private bool _isPaused;

    // --------------------- Właściwości ---------------------

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }

    /// <summary>
    /// Czy wideo ma zdefiniowane indenty (znaczniki)
    /// </summary>
    public bool IsIndents
    {
        get => _isIndents;
        set
        {
            if (_isIndents != value)
            {
                _isIndents = value;
                OnPropertyChanged(nameof(IsIndents));
            }
        }
    }

    public object MediaType { get; private set; }

    public int Id => _index;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public Uri Uri => _uri;

    /// <summary>
    /// Czas trwania wideo jako TimeSpan – teraz poprawnie obsługuje >24h
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_duration);

    /// <summary>
    /// Sformatowany czas trwania do wyświetlania w UI (z dniami jeśli potrzeba)
    /// Przykład: 1.05:23:45 dla 29 godzin
    /// </summary>
    public string DurationFormatted => FormatTimeSpan(TimeSpan.FromMilliseconds(_duration));

    public IPlayer Player => _player;

    public double Fps => GetFPS();

    public string Format => GetFormat();

    public string FrameSize => GetFrameSize();

    public string AudioFormat => GetAudioFormat();

    public string AudioSampleRate => GetAudioSampleRate();

    public int AudioBitRate => GetAudioBitRate();

    public string AudioChanelOutput => GetAudioChanelOutput();

    public string SubtitlePath { get; set; }

    public double Position
    {
        get => _position;
        set
        {
            if (value < 0) value = 0;
            if (value > _duration) value = _duration;

            if (System.Math.Abs(_position - value) > 0.001)
            {
                _position = value;
                OnPropertyChanged(nameof(Position));
                OnPropertyChanged(nameof(PositionFormatted));
            }
        }
    }

    /// <summary>
    /// Sformatowana aktualna pozycja – z obsługą długich czasów
    /// </summary>
    public string PositionFormatted => FormatTimeSpan(TimeSpan.FromMilliseconds(_position));

    public string Extension => _extension;

    public double Volume
    {
        get => _volume;
        set
        {
            if (value < 0) value = 0;
            if (value > 1) value = 1;

            if (System.Math.Abs(_volume - value) > 0.001)
            {
                _volume = value;
                OnVolumeChanged(value);
                OnPropertyChanged(nameof(Volume));
            }
        }
    }

    public bool IsHlsarc => _extension.Equals(".hlsarc", StringComparison.OrdinalIgnoreCase);
    public bool isIndents
    {
        get => _isIndents;
        set
        {
            _isIndents = value;
            OnPropertyChanged(nameof(isIndents));
        }
    }

    // --------------------- Events ---------------------

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<double> VolumeChanged;
    public event EventHandler<IPlayer> PlayerChanged;

    protected virtual void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected virtual void OnVolumeChanged(double newVolume)
    {
        this.WriteLine($"Volume change: {newVolume}");
        VolumeChanged?.Invoke(this, newVolume);
    }

    protected virtual void OnPlayerChanged(IPlayer player)
    {
        this.WriteLine($"Player change event: {player}");
        PlayerChanged?.Invoke(this, player);
    }

    public MediaItem(string path)
    {
        _index = _nextIndex++;
        _uri = new Uri(path);
        var fileInfo = new FileInfo(_uri.LocalPath);
        _name = fileInfo.Name;
        _extension = fileInfo.Extension.ToLowerInvariant();

        LoadMetadata();
        AutoSetSubtitle(path);
    }

    public MediaItem(string path, IPlayer player) : this(path)
    {
        SetPlayer(player);
        //_player.TimeChanged += (s, e) => Position = e.Time;
    }

    public MediaItem(string path, bool deferMetadata)
    {
        _index = _nextIndex++;
        _uri = new Uri(path);
        var fileInfo = new FileInfo(_uri.LocalPath);
        _name = fileInfo.Name;
        _extension = fileInfo.Extension.ToLowerInvariant();

        if (!deferMetadata)
            LoadMetadata();

        AutoSetSubtitle(path);
    }

    // --------------------- Pomocnicze metody ---------------------

    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays} d. {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        if (ts.TotalDays < 1)
        {
            return ts.ToString(@"hh\:mm\:ss");
        }
        return ts.ToString(@"d\ .d hh\:mm\:ss");
    }

    private void LoadMetadata()
    {
        //if (_extension == ".hlsarc")
        //{
        //    _fps = 0.0;
        //    _duration = GetDurationFromHlsarc(_uri.LocalPath) * 1000.0;
        //    _format = "hlsarc";
        //    _frameSize = string.Empty;
        //    _audioFormat = string.Empty;
        //    _audioSampleRate = string.Empty;
        //    _audioBitRate = 0;
        //    _audioChanelOutput = string.Empty;
        //}
        //else
        //{
        _metadataMediaToolkit = GetMetadata();
        if (_metadataMediaToolkit != null)
        {
            _fps = GetFPS();
            _duration = GetDuration();
            _format = GetFormat();
            _frameSize = GetFrameSize();
            _audioFormat = GetAudioFormat();
            _audioSampleRate = GetAudioSampleRate();
            _audioBitRate = GetAudioBitRate();
        }
        else
        {
            _fps = 0.0;
            _duration = 0.0;
            _format = string.Empty;
            _frameSize = string.Empty;
            _audioFormat = string.Empty;
            _audioSampleRate = string.Empty;
            _audioBitRate = 0;
            _audioChanelOutput = string.Empty;
        }
        //}
    }

    private double GetDurationFromM3u8(string m3u8Path)
    {
        if (!File.Exists(m3u8Path))
            throw new FileNotFoundException("Nie znaleziono pliku playlisty.", m3u8Path);

        double totalSeconds = 0;
        foreach (var line in File.ReadAllLines(m3u8Path))
        {
            if (line.StartsWith("#EXTINF:"))
            {
                string val = line.Substring(8).TrimEnd(',');
                if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                    totalSeconds += dur;
            }
        }

        return totalSeconds;
    }

    //////////////////////////////////////////////////////////////
    //                                                          //
    // TODO: Dokończyć tworzenie formatu multimedialnego HLSARC //
    //                                                          //
    //////////////////////////////////////////////////////////////

    /*private double GetDurationFromHlsarc(string archivePath)
    {
        using (var reader = new HlsArchiveReader(archivePath))
        {
            reader.Open(); // wczytuje indeks i archiwum
            string playlist = reader.GetPlaylist();

            double totalSeconds = 0;
            using (var sr = new StringReader(playlist))
            {
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("#EXTINF:"))
                    {
                        string val = line.Substring(8).TrimEnd(',');
                        if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out double dur))
                            totalSeconds += dur;
                    }
                }
            }

            return totalSeconds;
        }
    }*/

    /// <summary>
    /// Asynchronously loads metadata and updates the associated properties.
    /// </summary>
    /// <remarks>This method retrieves metadata in a background task and updates the  <see cref="Duration"/>,
    /// <see cref="Format"/>, <see cref="Fps"/>, and  <see cref="FrameSize"/> properties upon completion. It is designed
    /// to  avoid blocking the calling thread.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task LoadMetadataAsync()
    {
        await Task.Run(() =>
        {
            LoadMetadata();
        });

        // Aktualizuj UI po zakończeniu
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(Format));
        OnPropertyChanged(nameof(Fps));
        OnPropertyChanged(nameof(FrameSize));
    }


    /// <summary>
    /// Dispose the media item and release resources.
    /// </summary>
    public void Dispose()
    {
        _player.Dispose();
        Logger.Info("[" + GetType().Name + "]: Disposing: " + Name);
    }

    /// <summary>
    /// Set the media _player instance for this media item.
    /// </summary>
    /// <param name="player"></param>
    public void SetPlayer(IPlayer player)
    {
        try
        {
            _player = player;
            OnPlayerChanged(player);
        }
        catch (Exception ex)
        {
            this.WriteLine($"Error with _player set in media class. In media: {Uri}. {ex.Message}");
        }
    }

    private void AutoSetSubtitle(string path)
    {
        FileInfo file = new FileInfo(path);
        var tmp = ReturnNameWithoutExtension(file);
        var subtitle = String.Empty;

        //if (File.Exists($"{tmp}.{Subtitles.SubtitleExtensions.txt}"))
        //{
        //    SubtitlePath = $"{tmp}.{Subtitles.SubtitleExtensions.txt}";
        //    this.WriteLine($"Auto set subtitle {SubtitlePath}");
        //}
        //else if (File.Exists($"{tmp}.{Subtitles.SubtitleExtensions.sub}"))
        //{
        //    SubtitlePath = $"{tmp}.{Subtitles.SubtitleExtensions.sub}";
        //    this.WriteLine($"Auto set subtitle {SubtitlePath}");
        //}
        //else if (File.Exists($"{tmp}.{Subtitles.SubtitleExtensions.srt}"))
        //{
        //SubtitlePath = $"{tmp}.{Subtitles.SubtitleExtensions.srt}";
        //    this.WriteLine($"Auto set subtitle {SubtitlePath}");
        //}
        //else
        //{
        //    SubtitlePath = String.Empty;
        //    this.WriteLine("No auto subtitle.");
        //}
    }

    private string ReturnNameWithoutExtension(FileInfo item)
    {
        return item.FullName.Remove(item.FullName.Length - item.Extension.Length, item.Extension.Length);
    }

    /// <summary>
    /// Start or resume media playback.
    /// </summary>
    public void Play()
    {
        Dispatcher.Invoke(() =>
        {
            _player.Play(this);
            _isPlaying = true;
            this.WriteLine($"[{GetType().Name}]: Playing media {Name}");
        });
    }

    /// <summary>
    /// Pause the media playback.
    /// </summary>
    public void Pause()
    {
        Dispatcher.Invoke(() =>
        {
            _player.Pause();
            _isPaused = true;
            this.WriteLine($"[{GetType().Name}]Pause media {Name}");
        });
    }

    /// <summary>
    /// Stop the media playback and reset the position to the beginning.
    /// </summary>
    public void Stop()
    {
        Dispatcher.Invoke(() =>
        {
            _player.Stop();
            //_isStopped = true;
            Position = 0.0;
            this.WriteLine($"[{GetType().Name}]Stopped media {Name}");
        });
    }

    /// <summary>
    /// Move the media position forward by a specified number of seconds.
    /// </summary>
    /// <param name="seconds">In seconds</param>
    public void Forward(double seconds)
    {
        Position += seconds;
        Logger.Info($"[{GetType().Name}]: Change position to forward with +{seconds} second(s)");
    }

    /// <summary>
    /// Rewind the media position by a specified number of seconds.
    /// </summary>
    /// <param name="seconds">in seconds</param>
    public void Rewind(double seconds)
    {
        Position -= seconds;
        Logger.Info($"[{GetType().Name}]: Rewind position by -{seconds} second(s)");
    }

    private Metadata GetMetadata()
    {
        if (_uri == null || string.IsNullOrEmpty(_uri.LocalPath) || !File.Exists(_uri.LocalPath))
        {
            Logger.Error("[" + GetType().Name + "]: Invalid media file path: " + _uri?.LocalPath);
            return null;
        }
        try
        {
            MediaFile inputFile = new MediaFile
            {
                Filename = _uri.LocalPath
            };
            using (Engine engine = new Engine())
            {
                engine.GetMetadata(inputFile);
            }
            Logger.Info("[" + GetType().Name + "]: Get metadata for media: " + _uri.LocalPath);
            return inputFile.Metadata;
        }
        catch (Exception exception)
        {
            Logger.Error("]: Error getting metadata for media: " + _uri.LocalPath, exception);
            return null;
        }
    }

    private double GetDuration()
    {
        try
        {
            double duration = _metadataMediaToolkit.Duration.TotalMilliseconds;
            Logger.Info($"[{GetType().Name}]: Get media {TimeSpan.FromMilliseconds(duration)} duration");
            if (duration == 0)
            {
                try
                {
                    // Próba pobrania czasu za pomocą alternatywnej biblioteki NAudio.Wave
                    var p = new MediaFoundationReader(this._uri.AbsoluteUri);
                    duration = p.TotalTime.TotalMilliseconds;
                }
                catch (Exception ex)
                {
                    Logger.Info($"[{GetType().Name}]: {ex.Message}");
                }
            }
            return duration;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return 0;
        }
    }

    private double GetFPS()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                double fps = _metadataMediaToolkit.VideoData.Fps;
                Logger.Info($"[{GetType().Name}]: Get media {fps} frame_size");
                return fps;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return 0;
        }
    }

    private string GetFormat()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                string format = _metadataMediaToolkit.VideoData.Format;
                Logger.Info("[" + GetType().Name + "]: Get media " + format + " format");
                return format;
            }
            return String.Empty;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return String.Empty;
        }
    }

    private string GetFrameSize()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                string frame_size = _metadataMediaToolkit.VideoData.FrameSize;
                Logger.Info("[" + GetType().Name + "]: Get media " + frame_size + " frame_size");
                return frame_size;
            }
            return String.Empty;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return String.Empty;
        }
    }

    private string GetAudioFormat()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                string format = _metadataMediaToolkit.AudioData.Format;
                Logger.Info("[" + GetType().Name + "]: Get media " + format + " audio_format");
                return format;
            }
            return String.Empty;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return String.Empty;
        }
    }

    private string GetAudioSampleRate()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                string rate = _metadataMediaToolkit.AudioData.SampleRate;
                Logger.Info("[" + GetType().Name + "]: Get media " + rate + " audio_sample_rate");
                return rate;
            }
            return String.Empty;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return String.Empty;
        }
    }

    private int GetAudioBitRate()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                int rate = _metadataMediaToolkit.AudioData.BitRateKbs;
                Logger.Info($"[{GetType().Name}]: Get media {rate} audio_bit_rate");
                return rate;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return 0;
        }
    }

    private string GetAudioChanelOutput()
    {
        try
        {
            if (_metadataMediaToolkit != null)
            {
                string chanel_output = _metadataMediaToolkit.AudioData.ChannelOutput;
                Logger.Info("[" + GetType().Name + "]: Get media " + chanel_output + " audio_channel_output");
                return chanel_output;
            }
            return String.Empty;
        }
        catch (Exception ex)
        {
            Logger.Info($"[{GetType().Name}]: {ex.Message}");
            return String.Empty;
        }
    }

    /// <summary>
    /// Override ToString method to provide a string representation of the VideoItem.
    /// </summary>
    /// <returns>string</returns>
    public override string ToString()
    {
        return $"BaseString: {Name}, Duration: {Duration}, Format: {Format}";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static MediaItem FromFile(string path)
    {
        return new MediaItem(path);
    }
}
