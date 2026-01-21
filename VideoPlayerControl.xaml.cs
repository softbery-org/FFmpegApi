// Version: 0.0.3.2
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using FFmpegApi.Logs;
using FFmpegApi.Utilities;
using FFmpegApi.Views;

using Media;

using Newtonsoft.Json.Linq;

using Thmd.Views;

using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace FFmpegApi
{
    public partial class VideoPlayerControl : UserControl, IPlayer, INotifyPropertyChanged
    {
        private FFmpegVideoPlayer _videoPlayer = new FFmpegVideoPlayer();
        //private FFmpegApi.FFmpegThumbnail _thumbnail = new FFmpegThumbnail();
        private TimeSpan _position;
        private TimeSpan _duration;
        private double _volume;
        private bool _paused;
        private Random _random = new Random();
        private bool _isfullscreen;
        private Visibility _playlistVisibility = Visibility.Collapsed;
        private Task<WriteableBitmap> _thumbnail;
        private DispatcherTimer _thumbnailTimer;
        private TimeSpan _thumbnailTimerTime;

        #region WinAPI

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint BLOCK_SLEEP_MODE = 2147483651u;   // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        private const uint DONT_BLOCK_SLEEP_MODE = 2147483648u; // ES_CONTINUOUS

        #endregion

        public Playlist Playlist => PlaylistView;

        public Controlbar Controlbar => ControlbarView;

        public InfoBox InfoBox => throw new NotImplementedException();

        public ProgressbarView ProgressBar => ProgressbarView;

        public KeyboardShortcutsView KeyboardShortcuts => Keyboard;

        public FrameworkElement Subtitle => throw new NotImplementedException();

        //public FrameworkElement VideoOutput => VideoImage;

        public MediaItem CurrentMedia => Playlist.Current;

        public TimeSpan Position 
        { 
            get => _position; 
            set 
            { 
                _position = _videoPlayer.Position;
                OnPropertyChanged(nameof(Position), ref _position, value);
            }
        }

        public TimeSpan Duration {
            get=> _duration;
            set
            {
                _duration = value;
                OnPropertyChanged(nameof(Duration), ref _duration, value);
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                _videoPlayer?.Volume = value;
                OnPropertyChanged(nameof(Volume), ref _volume, value);
            }
        }

        public bool isFullscreen
        {
            get => _isfullscreen;
            set
            {
                _isfullscreen = value;
                Dispatcher.InvokeAsync(() =>
                {
                    this.Fullscreen();
                });
                OnPropertyChanged(nameof(isFullscreen), ref _isfullscreen, value);
            }
        }

        public bool isPlaying { get => _videoPlayer.isPlay; }
        public bool isPaused { get => _videoPlayer.isPause; }
        public bool isMute { get => _videoPlayer.isMute; }
        public bool isStopped { get => _videoPlayer.isStop; }
        public bool isBuffering { get => throw new NotImplementedException(); }

        public Visibility PlaylistVisibility
        {
            get => _playlistVisibility; 
            set 
            { 
                _playlistVisibility = value;
                OnPropertyChanged(nameof(PlaylistVisibility), ref _playlistVisibility, value);
            }
        }

        public event EventHandler<EventArgs> TimeChanged;
        public event EventHandler<EventArgs> Playing;
        public event EventHandler<EventArgs> Paused;
        public event EventHandler<EventArgs> Stopped;
        public event PropertyChangedEventHandler PropertyChanged;

        public VideoPlayerControl()
        {
            InitializeComponent();
            
            Playlist.SetPlayer(this);
            Controlbar.SetPlayer(this);

            ControlControllerHelper.Attach(Playlist);
            ControlControllerHelper.Attach(Controlbar);
            ControlControllerHelper.Attach(KeyboardShortcuts);

            Playlist.Visibility = Visibility.Collapsed;
            KeyboardShortcuts.Visibility = Visibility.Collapsed;

            FFmpegEvent();
            ProgressbarEvent();
            ProgressbarThumbnailEvent();
            KeyboardEvents();
            ControlbarButtons();
            ControlbarSlideEvent();
        }

        private void ProgressbarThumbnailEvent()
        {
            _thumbnailTimer = new DispatcherTimer();
            _thumbnailTimer.Interval = TimeSpan.FromMilliseconds(500);
            _thumbnailTimer.Tick += _thumbnailTimer_Tick;
        }

        private void ControlbarSlideEvent()
        {
            Controlbar.PositionSlider.ValueChanged += (sender, e) =>
            {

            };
        }

        private void ControlbarButtons() 
        {
            Controlbar.PlayBtn.Click += (s, e) => TogglePlayPause();
            Controlbar.PreviouseButton.Click += (s, e) => Preview();
            Controlbar.NextButton.Click += (s, e) => Next();
            Controlbar.OpenFileButton.Click += (s, e) => OpenFile();
            Controlbar.OpenURLButton.Click += (s, e) =>
            {
                _videoPlayer.Play("https://samplelib.com/lib/preview/mp4/sample-30s.mp4");
            };
            Controlbar.OpenPlaylistButton.Click += (s, e) => TogglePlaylist();
            Controlbar.OpenSubtitlesButton.Click += (s, e) => OpenSubtitles();
            Controlbar.MuteBtn.Click += (s, e) => ToggleMute();
            Controlbar.FullscreenButton.Click += (s, e) => ToggleFullscreen();
            Controlbar.CloseButton.Click += (s, e) => ToggleControlbar();
            Controlbar.EqualizerButton.Click += (s, e) =>
            {
                
            };
            Controlbar.HelpButton.Click += (s, e) => { };
            Controlbar.KeyboardShortcutsButton.Click += (s, e) => ToggleKeyboardShortcuts();
        }

        private void ToggleFullscreen()
        {
            isFullscreen = !isFullscreen;
        }

        private void ToggleControlbar()
        {
            if (Controlbar.Visibility == Visibility.Visible)
                Controlbar.Visibility = Visibility.Visible;
            else
                Controlbar.Visibility = Visibility.Collapsed;
        }

        private void ToggleKeyboardShortcuts()
        {
            if (KeyboardShortcuts.Visibility == Visibility.Visible)
                KeyboardShortcuts.Visibility = Visibility.Collapsed;
            else
                KeyboardShortcuts.Visibility = Visibility.Visible;
        }

        private void OpenSubtitles()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Subtitle File",
                Filter = "Subtitle Files|*.txt;*.sub|All Files|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
                foreach (var f in ofd.FileNames)
                    Playlist.Current.SubtitlePath = f;
        }

        private async void OpenFile()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Media File",
                Filter = "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.mp3;*.wav;*.flac;*.ts;*.m3u8;*.hlsarc|All Files|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
                foreach (var f in ofd.FileNames)
                    await Playlist.AddAsync(new MediaItem(f));
        }

        public void Dispose()
        {
            ControlControllerHelper.Detach(Playlist);
            ControlControllerHelper.Detach(Controlbar);
            ControlControllerHelper.Detach(KeyboardShortcuts);

            _videoPlayer?.Dispose();
        }

        public void Next()
        {
            Playlist.CurrentIndex++;
            Play(Playlist.Current);
        }

        public void Preview()
        {
            Playlist.CurrentIndex--;
            Play(Playlist.Current);
        }

        public void Pause()
        {
            _videoPlayer.Pause();
            SetThreadExecutionState(DONT_BLOCK_SLEEP_MODE);
        }

        private void TogglePlaylist()
        {
            if (Playlist.Visibility == Visibility.Visible)
                Playlist.Visibility = Visibility.Collapsed;
            else
                Playlist.Visibility = Visibility.Visible;
        }

        private void FFmpegEvent()
        {
            // Zmiana ramki
            _videoPlayer?.FrameReady += bitmap =>
            {
                VideoImage.Dispatcher.Invoke(() =>
                {
                    VideoImage.Source = bitmap;
                });
            };

            // Zmiana czasu
            _videoPlayer?.TimeChanged += (s, e) =>
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    _position = e.Position;
                    _duration = e.Duration;

                    Controlbar.MediaTitle.Content = Playlist.Current?.Name ?? "";
                    Controlbar.PositionTextbox.Text = $"{_position.ToString("hh\\:mm\\:ss")} ";
                    Controlbar.DurationTextbox.Text = $"/ {_videoPlayer?.Duration.ToString("hh\\:mm\\:ss")}";

                    UpdateUIOnTimeChanged((long)_position.TotalMilliseconds);
                    TimeChanged?.Invoke(this, e);
                }));
            };

            // Osiągnięcie końca odtwarzania
            _videoPlayer?.EndReached += (s, e) =>
            {
                Dispatcher.Invoke(HandleEndReached);
            };
        }

        private void HandleRepeat(string repeat)
        {
            Dispatcher.Invoke(() =>
            {
                switch (repeat)
                {
                    case "One":
                        Stop();
                        Play(Playlist.Current);
                        break;
                    case "All":
                        Next();
                        break;
                    case "Random":
                        if (Playlist.Items.Count > 0)
                        {
                            int randomIndex = _random.Next(Playlist.Items.Count);
                            Playlist.CurrentIndex = randomIndex;
                            Play(Playlist.Current);
                        }
                        break;
                    case "None":
                        Stop();
                        break;
                }
            });
        }

        private void HandleEndReached()
        {
            Debug.WriteLine($"End has reached");

            if (Playlist.PlayNext != null)
            {
                var index = Playlist.Videos.IndexOf(Playlist.PlayNext);
                Play(Playlist.Videos[index]);
                Playlist.PlayNext = null;
            }
            
            Debug.WriteLine(Controlbar.RepeatComboBox.SelectedItem.ToString());
            HandleRepeat(Controlbar.RepeatComboBox.SelectedItem.ToString());
        }

        private void ProgressbarEvent()
        {
            ProgressBar.MouseDown += (s, e) =>
            {
                if (Playlist.Current == null)
                    return;

                if (_videoPlayer == null)
                    return;

                System.Windows.Point mousePosition = e.GetPosition(ProgressBar);
                double width = ProgressBar.ActualWidth;

                if (width <= 0) 
                    return;

                var val = (e.GetPosition(ProgressBar).X*100)/width;
                var time = (Playlist.Current.Duration.TotalSeconds*val)/100;

                ProgressBar.Value = (long)time;
                Seek(TimeSpan.FromSeconds(time));

                ProgressBar._rectangleMouseOverPoint.Margin = new Thickness(mousePosition.X - (ProgressBar._rectangleMouseOverPoint.Width / 2), 0, 0, 0);
            };

            ProgressBar.MouseMove += async (s, e) =>
            {
                if (Playlist.Current == null) 
                    return;

                if (_videoPlayer == null)
                    return;

                System.Windows.Point mousePosition = e.GetPosition(ProgressBar);
                double width = ProgressBar.ActualWidth;
                if (width <= 0) return;

                var val = (e.GetPosition(ProgressBar).X * 100) / width;
                var time = TimeSpan.FromSeconds((Playlist.Current.Duration.TotalSeconds * val) / 100);

                ProgressBar._rectangleMouseOverPoint.Margin = new Thickness(mousePosition.X - (ProgressBar._rectangleMouseOverPoint.Width / 2), 0, 0, 0);

                ProgressBar._popup.IsOpen = true;
                ProgressBar.PopupText = $"{time.Hours}:{time.Minutes:00}:{time.Seconds:00}";
                ProgressBar._popup.HorizontalOffset = mousePosition.X - (ProgressBar._popup.ActualWidth / 2);

                MouseOverProgressBar(time);

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Seek(time);
                }
            };
        }

        private void MouseOverProgressBar(TimeSpan time)
        {
            _thumbnailTimerTime = time;
            _thumbnailTimer.Start();
        }

        private async void _thumbnailTimer_Tick(object sender, EventArgs e)
        {
            _thumbnail = _videoPlayer.GetThumbnailAsync(_thumbnailTimerTime, 800, 600, default);
            ProgressBar._popupImage.Source = await _thumbnail;
            _thumbnailTimer.Stop();
        }

        private void _Play(MediaItem media = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (Playlist.Videos.Count <= 0 || Playlist == null)
                {
                    Logger.Info("Playlist is empty.");
                    MessageBox.Show("Playlist is empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (Playlist.Current == null && Playlist.Videos.Count > 0)
                {
                    Logger.Info("Current _media is not set.");
                    Playlist.Current = Playlist.Videos[0];
                    Playlist.SelectedItem = Playlist.Current;
                }

                try
                {
                    if (media != null)
                    {
                        _videoPlayer?.Stop();
                        _videoPlayer?.Play(@media.Uri.LocalPath);
                    }
                    else
                    {
                        _videoPlayer?.Play();
                    }

                    SetThreadExecutionState(BLOCK_SLEEP_MODE);
                }
                catch (Exception ex)
                {
                    this.WriteLine($"Error while playing _media: {ex.Message}");
                }
            });
        }

        public void Play(MediaItem media) => _Play(media);

        public void Play()
        {
            _Play();
        }

        public void PlayNext(MediaItem media)
        {
            if (Playlist.PlayNext == null)
            {
                Playlist.PlayNext = media;
                return;
            }
        }

        public void Seek(TimeSpan time)
        {
            _videoPlayer?.Seek(time);
        }

        public void Seek(TimeSpan time, SeekDirection seek_direction = SeekDirection.Forward)
        {
            switch (seek_direction)
            {
                case SeekDirection.Backward:
                    if(_position - time <= TimeSpan.Zero)
                        time = TimeSpan.Zero;
                    else 
                        time = _position - time;
                    _videoPlayer?.Seek(time);
                    break;
                case SeekDirection.Forward:
                    if (_position + time >= _duration)
                        time = _duration;
                    else
                        time = _position + time;
                    _videoPlayer?.Seek(time);
                    break;
            }
        }

        public void Stop()
        {
            _videoPlayer?.Stop();
            SetThreadExecutionState(DONT_BLOCK_SLEEP_MODE);
        }

        public void TogglePlayPause()
        {
            _videoPlayer.TogglePlayPause();
        }

        private void TogglePlayPause_Click(object sender, RoutedEventArgs e)
        {
            _videoPlayer.Stop();
        }

        public void ToggleMute()
        {
            _videoPlayer.ToggleMute();
        }

        public void SetVolume(double volume)
        {
            Volume = volume;
        }

        public void OpenPlaylist()
        {
            PlaylistVisibility = Visibility.Visible;
        }

        protected void OnPropertyChanged<T>(string propertyName, ref T field, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void UpdateUIOnTimeChanged(long timeMs)
        {
            if (Playlist.Current == null) return;

            _position = TimeSpan.FromMilliseconds(timeMs);

            Dispatcher.BeginInvoke(() => Playlist.Current.Position = timeMs);

            ProgressbarView.Value = (ProgressbarView.Maximum * timeMs) / Playlist.Current.Duration.TotalMilliseconds;
            ProgressbarView.ProgressText = $"{Playlist.Current.PositionFormatted} / {Playlist.Current.DurationFormatted}";

            var position_value = (100 * Playlist.Current.Position) / Playlist.Current.Duration.TotalMilliseconds;
            Controlbar.PositionSlider.Value = position_value;

            //_subtitle.PositionTime = _position;
        }

        #region === Obsługa Zmiany Rozmiaru Okna ===
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            VideoImage.Width = sizeInfo.NewSize.Width;
            VideoImage.Height = sizeInfo.NewSize.Height;
        }
        #endregion

        #region === Obsługa Klawiatury ===

        private void KeyboardEvents()
        {
            ControlControllerHelper.HandleKeyDown(this, Key.Right, new Action(() =>
            {
                _videoPlayer?.Seek(Position + TimeSpan.FromMinutes(2));
                return;
            }));
            ControlControllerHelper.HandleKeyDown(this, Key.Left, new Action(() =>
            {
                _videoPlayer?.Seek(Position - TimeSpan.FromMinutes(2));
                return;
            }));
            ControlControllerHelper.HandleKeyDown(this, Key.H, new Action(() =>
            {
                if (KeyboardShortcuts.Visibility == Visibility.Visible)
                    KeyboardShortcuts.Visibility = Visibility.Collapsed;
                else
                    KeyboardShortcuts.Visibility = Visibility.Visible;
                return;
            }));
            ControlControllerHelper.HandleKeyDown(this, Key.F, new Action(() =>
            {
                isFullscreen = !isFullscreen;
                return;
            }));
            ControlControllerHelper.HandleKeyDown(this, Key.Escape, new Action(() =>
            {
                if (isFullscreen)
                    isFullscreen = false;
                return;
            }));
            ControlControllerHelper.HandleKeyDown(this, Key.P, new Action(() =>
            {
                if (Playlist.Visibility == Visibility.Visible)
                    Playlist.Visibility = Visibility.Collapsed;
                else
                    Playlist.Visibility = Visibility.Visible;
                return;
            }));
            ControlControllerHelper.HandleKeyDown(this, Key.Space, new Action(() =>
            {
                _videoPlayer?.TogglePlayPause();
                return;
            }));
            ControlControllerHelper.HandleKeyDown(Playlist, Key.Escape, new Action(() =>
            {
                if (Playlist.Visibility == Visibility.Visible)
                    Playlist.Visibility = Visibility.Collapsed;
                return;
            }));
        }

        #endregion

        #region === Obsługa Myszy dla VideoImage ===
        
        private void VideoImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VideoImage.Focus();
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                isFullscreen = !isFullscreen;
            }
        }

        #endregion
    }
}
