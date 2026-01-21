// Version: 0.0.0.9
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

using FFmpegApi.Logs;

using Media;

namespace FFmpegApi.Views
{
    /// <summary>
    /// Logika interakcji dla klasy UserControlbar.xaml
    /// </summary>
    public partial class UserControlbar : UserControl, IDisposable
    {

        private IPlayer _player;

        #region ==== Controlbar ====

        public TextBlock TextBlockMediaTitle => TextblockMediaTitle;
        public TextBlock TextBlockPosition => TextblockPosition;
        public TextBlock TextBlockDuration => TextblockDuration;

        public Button ButtonOpenMedia => BtnOpenMedia;
        public Button ButtonOpenStream => BtnOpenStream;
        public Button ButtonOpenSubtitle => BtnOpenSubtitle;
        public Button ButtonOpenPlaylist => BtnOpenPlaylist;
        public Button ButtonPreview => BtnPreview;
        public Button ButtonPlayPause => BtnPlayPause;
        public Button ButtonStop => BtnStop;
        public Button ButtonNext => BtnNext;
        public Button ButtonMute => BtnMute;
        public Button ButtonRepeat => BtnRepeat;
        public Button ButtonEqualizer => BtnEqualizer;
        public Button ButtonFullscreen => BtnFullscreen;

        public Slider SliderVolume => SliderVolume;
        public Slider Slider_Bass => SliderBass;
        public Slider Slider_Mid => SliderMid;
        public Slider Slider_Treble => SliderTreble;

        public Popup PopUpVolume => PopupVolume;
        //public Popup PopUpTextVolume => PopupTextVolume;
        //public Popup PopUpVolume => PopupVolume;
        //public Popup PopUpTextblock => PopupTextblockVolume;
        public Popup PopUpPlaylist => PopupPlaylist;
        public Popup PopUpEqualizer => PopupEqualizer;
        public Popup PopUpRepeat => PopupRepeat;

        public ProgressbarView ProgressBarVolume => ProgressbarVolume;

        public ListBox ListBoxPlaylist => ListboxPlaylist;

        public ComboBox ComboBoxRepeat => ComboboxRepeat;

        #endregion

        public UserControlbar()
        {
            InitializeComponent();
            DataContext = this;

            RepeatModes = new ObservableCollection<string>
            {
                "None",
                "One",
                "All",
                "Random"
            };

            Volume = 50;
            //RepeatMode = Config.Instance.PlaylistConfig.Repeat;
        }

        #region === MEDIA INFO ===

        public static readonly DependencyProperty MediaTitleProperty =
            DependencyProperty.Register(nameof(MediaTitle), typeof(string), typeof(UserControlbar));

        public string MediaTitle
        {
            get => (string)GetValue(MediaTitleProperty);
            set => SetValue(MediaTitleProperty, value);
        }

        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register(nameof(Position), typeof(string), typeof(UserControlbar));

        public string Position
        {
            get => (string)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(string), typeof(UserControlbar));

        public string Duration
        {
            get => (string)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        #endregion

        #region === VOLUME ===

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register(nameof(Volume), typeof(double), typeof(UserControlbar),
                new PropertyMetadata(50d, OnVolumeChanged));

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UserControlbar)d;
            
            ctrl.PopupTextVolume.Text = $"{(int)ctrl.Volume}%";
        }

        
        public static readonly DependencyProperty VolumeProgressTextProperty =
            DependencyProperty.Register(nameof(VolumeProgressText), typeof(string), typeof(UserControlbar),
                new PropertyMetadata("100"));

        public string VolumeProgressText
        {
            get => (string)GetValue(VolumeProgressTextProperty);
            set => SetValue(VolumeProgressTextProperty, value);
        }

        public static readonly DependencyProperty IsMutedProperty =
            DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(UserControlbar),
                new PropertyMetadata(false, OnIsMutedChanged));
        public bool IsMuted
        {
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }

        public static readonly DependencyProperty IsPlayProperty =
            DependencyProperty.Register(nameof(IsPlay), typeof(bool), typeof(UserControlbar),
                new PropertyMetadata(false, OnIsPlayPauseChanged));
        public bool IsPlay
        {
            get => (bool)GetValue(IsPlayProperty);
            set => SetValue(IsPlayProperty, value);
        }

        private bool _isDraggingVolume;

        private void VolumeProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingVolume = true;
            UpdateVolumeFromMouse(e);
            PopupVolume.IsOpen = true;
        }

        private void VolumeProgressBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingVolume)
                UpdateVolumeFromMouse(e);
        }

        private void VolumeProgressBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingVolume = false;
            PopupVolume.IsOpen = false;
        }

        private void VolumeProgressBar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDraggingVolume)
                PopupVolume.IsOpen = false;
        }

        private void UpdateVolumeFromMouse(MouseEventArgs e)
        {
            var pos = e.GetPosition(ProgressbarVolume);
            var percent = pos.X / ProgressbarVolume.ActualWidth;
            Volume = Math.Clamp(percent * 100, 0, 100);

            UpdateVolumeText();
        }

        private void ShowVolumePopup(Point mousePos)
        {
            if (ProgressbarVolume.ActualWidth <= 0) return;

            double percentage = Math.Clamp(mousePos.X / ProgressbarVolume.ActualWidth, 0.0, 1.0);
            int vol = (int)System.Math.Round(percentage * 100);

            PopupTextVolume.Text = $"{vol}%";

            double popupWidth = PopupTextVolume.ActualWidth > 0 ? PopupTextVolume.ActualWidth + 20 : 60;
            double offset = mousePos.X - (popupWidth / 2);
            offset = Math.Clamp(offset, 0, ProgressbarVolume.ActualWidth - popupWidth);

            PopupVolume.HorizontalOffset = offset;
            PopupVolume.VerticalOffset = -40;
            PopupVolume.IsOpen = true;
        }

        private void UpdateVolumeText()
        {
            VolumeProgressText = $"{(int)Volume}";
        }


        #endregion

        #region === PLAYLIST ===

        public static DependencyProperty PlaylistItemsProperty =
            DependencyProperty.Register(nameof(PlaylistItems),
                typeof(ObservableCollection<string>), typeof(UserControlbar));

        public ObservableCollection<string> PlaylistItems
        {
            get => (ObservableCollection<string>)GetValue(PlaylistItemsProperty);
            set => SetValue(PlaylistItemsProperty, value);
        }

        public static DependencyProperty SelectedPlaylistIndexProperty =
            DependencyProperty.Register(nameof(SelectedPlaylistIndex),
                typeof(int), typeof(UserControlbar));

        public int SelectedPlaylistIndex
        {
            get => (int)GetValue(SelectedPlaylistIndexProperty);
            set => SetValue(SelectedPlaylistIndexProperty, value);
        }

        public bool PlaylistPopupVisibility
        {
            get => PopupPlaylist.IsOpen;
            set => PopupPlaylist.IsOpen = value;
        }

        #endregion

        #region === EQUALIZER ===

        public double BassLevel
        {
            get => (double)GetValue(BassLevelProperty);
            set => SetValue(BassLevelProperty, value);
        }

        public static DependencyProperty BassLevelProperty =
            DependencyProperty.Register(nameof(BassLevel), typeof(double), typeof(UserControlbar));

        public double MidLevel
        {
            get => (double)GetValue(MidLevelProperty);
            set => SetValue(MidLevelProperty, value);
        }

        public static DependencyProperty MidLevelProperty =
            DependencyProperty.Register(nameof(MidLevel), typeof(double), typeof(UserControlbar));

        public double TrebleLevel
        {
            get => (double)GetValue(TrebleLevelProperty);
            set => SetValue(TrebleLevelProperty, value);
        }

        public static DependencyProperty TrebleLevelProperty =
            DependencyProperty.Register(nameof(TrebleLevel), typeof(double), typeof(UserControlbar));

        public bool EqualizerPopupVisibility
        {
            get => PopupEqualizer.IsOpen;
            set => PopupEqualizer.IsOpen = value;
        }

        #endregion

        #region === REPEAT ===

        public ObservableCollection<string> RepeatModes { get; }

        public static DependencyProperty RepeatModeProperty =
            DependencyProperty.Register(nameof(RepeatMode), typeof(string), typeof(UserControlbar));

        public string RepeatMode
        {
            get => (string)GetValue(RepeatModeProperty);
            set => SetValue(RepeatModeProperty, value);
        }

        public bool PopupRepeatVisibility
        {
            get => PopupRepeat.IsOpen;
            set => PopupRepeat.IsOpen = value;
        }

        #endregion

        #region === BUTTON HANDLERS ===

        private void TogglePopup(Popup popup)
        {
            popup.IsOpen = !popup.IsOpen;
        }

        private void BtnPlaylistOpen_Click(object sender, RoutedEventArgs e)
            => TogglePopup(PopupPlaylist);

        private void BtnEqualizer_Click(object sender, RoutedEventArgs e)
            => TogglePopup(PopupEqualizer);

        private void BtnRepeat_Click(object sender, RoutedEventArgs e)
            => TogglePopup(PopupRepeat);

        #endregion

        #region === DEPENDENCY CALLBACKS ===

        private static void OnIsPlayPauseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UserControlbar cb && e.NewValue is bool play)
            {
                //cb._player?.isPlaying = play;
                cb.UpdatePlayButtonIcon();
            }
        }

        private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UserControlbar cb && e.NewValue is bool muted)
            {
                //cb._player.isMute = muted;
                cb.UpdateMuteButtonIcon();
                //Config.Instance.ControlbarConfig.IsMuted = muted;
                //Config.Instance.Save();
            }
        }

        private static void OnRepeatModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UserControlbar cb && e.NewValue is string mode)
            {
                cb.UpdateRepeatButtonIcon();
                //Config.Instance.PlaylistConfig.Repeat = mode;
                //Config.Instance.Save();
            }
        }

        #endregion

        #region === FADE ANIMATION ===

        public void FadeOut()
        {
            Task.Run(async () =>
            {
                ((Storyboard)Resources["fadeOutControl"]).Begin();
                //await this.ShowByStoryboard((Storyboard)this.FindResource("fadeOutControl"));
            });
        }

        public void FadeIn()
        {
            Task.Run(async() =>
            {
                ((Storyboard)Resources["fadeInControl"]).Begin();
                //await this.ShowByStoryboard((Storyboard)this.FindResource("fadeInControl"));
            });
        }

        #endregion

        #region === UI UPDATES ===

        private void UpdateAllIcons()
        {
            UpdatePlayButtonIcon();
            UpdateMuteButtonIcon();
            UpdateRepeatButtonIcon();
        }

        private void UpdatePlayButtonIcon(bool isPlaying = false)
        {
            BtnPlayPause.Content = IsPlay ? "⏸️" : "▶";
            BtnPlayPause.ToolTip = IsPlay ? "Pause" : "Play";
        }

        private void UpdateMuteButtonIcon()
        {
            BtnMute.Content = IsMuted ? "🔇" : "🔊";
            BtnMute.ToolTip = IsMuted ? "Unmute" : "Mute";
        }

        private void UpdateRepeatButtonIcon()
        {
            switch (RepeatMode)
            {
                case "All": BtnRepeat.Content = "🔁"; BtnRepeat.ToolTip = "Repeat All"; break;
                case "One": BtnRepeat.Content = "🔂"; BtnRepeat.ToolTip = "Repeat One"; break;
                case "Random": BtnRepeat.Content = "🔀"; BtnRepeat.ToolTip = "Shuffle"; break;
                default: BtnRepeat.Content = "🔁"; BtnRepeat.ToolTip = "No Repeat"; break;
            }
        }

        //private void OnPlayerTimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        //{
        //    if (_player?.Playlist.Current != null)
        //    {
        //        Position = FormatTime(e.Time);
        //        Duration = FormatTime((long)_player.Playlist.Current.Duration.TotalMilliseconds);
        //        MediaTitle = _player.Playlist.Current.Name ?? "No Media";
        //    }
        //}

        private string FormatTime(long milliseconds)
        {
            var time = TimeSpan.FromMilliseconds(milliseconds);
            return time.TotalHours >= 1
                ? $"{time:hh\\:mm\\:ss}"
                : $"{time:mm\\:ss}";
        }

        #endregion

        #region === SAVE AND LOAD CONFIGURATION ===
        public void Save()
        {
            //var config = Config.Instance;

            //config.ControlbarConfig.Left = Margin.Left;
            //config.ControlbarConfig.Top = Margin.Top;
            //config.ControlbarConfig.Width = ActualWidth;
            //config.ControlbarConfig.Height = ActualHeight;
            //config.ControlbarConfig.IsMuted = IsMuted;

            //config.PlayerVolume = Volume;
            //config.PlaylistConfig.Repeat = RepeatMode;

            //Config.SaveConfig(Config.ControlbarConfigPath, config.ControlbarConfig); // lub osobny plik, jeśli chcesz
            //Config.Instance.GetType().GetProperty(nameof(Config.Instance.ControlbarConfig)).SetValue(Config.Instance, config.ControlbarConfig);
            //Config.Instance.Save();

            Logger.Info("UserControlbar configuration saved.");
        }

        public void Load()
        {
            //var config = Config.Instance;

            //Margin = new Thickness(config.ControlbarConfig.Left, config.ControlbarConfig.Top, 0, 0);
            //Width = config.ControlbarConfig.Width > 0 ? config.ControlbarConfig.Width : 710;
            //Height = config.ControlbarConfig.Height > 0 ? config.ControlbarConfig.Height : 105;
            //Volume = config.PlayerVolume;
            //_player?.Volume = Volume;
            //IsMuted = config.ControlbarConfig.IsMuted;
            //_player?.isMute = IsMuted;
            //RepeatMode = config.PlaylistConfig.Repeat ?? "None";
            //try
            //{
            //    ComboboxRepeat.SelectedItem = RepeatMode;
            //}
            //catch
            //{
            //    Logger.Error($"ComboboxRepeat does not accept the values ​​assigned to it: saved configuration value = {config.PlaylistConfig.Repeat}");
            //}
            

            //UpdateVolumeText();
            //UpdateAllIcons();

            Logger.Info("UserControlbar configuration loaded.");
        }

        #endregion

        public void SetPlayer(IPlayer player)
        {
            _player = player;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private Point _lastPosition = new Point();

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //ControlControllerHelper.SaveConfigElement<ControlbarConfig>(this);
                Logger.Info($"Save controlbar position [{Margin.Left}:{Margin.Top}] and size [{ActualWidth}X{ActualHeight}].");
            }
        }
    }
}
