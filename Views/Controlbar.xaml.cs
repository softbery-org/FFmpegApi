// Version: 0.0.0.9
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

using FFmpegApi.Logs;

using Media;

namespace FFmpegApi.Views
{
    /// <summary>
    /// Logika interakcji dla klasy Controlbar.xaml
    /// </summary>
    public partial class Controlbar : UserControl
    {
        private IPlayer _player;

        private double _lastVolume;

        #region Publiczne Właściwości

        public string RepeatMode { get; set; } = "None,All,One,Random";
        public double Volume { get; set; }
        public Button PreviouseButton => PreviouseBtn;
        public Button PlayButton => PlayBtn;
        public Button NextButton => NextBtn;
        public Button OpenFileButton => OpenFileBtn;
        public Button OpenURLButton => OpenUrlBtn;
        public Button OpenPlaylistButton => OpenPlaylistBtn;
        public Button OpenSubtitlesButton => OpenSubBtn;
        public Button FullscreenButton => FullscreenBtn;
        public Button CloseButton => CloseBtn;
        public Button EqualizerButton => EqualizerBtn;
        public Button MuteButton => MuteBtn;
        public Button HelpButton => HelpBtn;
        public Button KeyboardShortcutsButton => KeyboardShortcutsBtn;
        public ComboBox RepeatListBox => RepeatComboBox;
        public Slider PositionTracker => PositionSlider;
        public Slider VolumeTracker => VolumeSlider;

        #endregion

        public Controlbar()
        {
            InitializeComponent();
        }

        public Controlbar(IPlayer player) : this()
        {
            _player = player;
        }

        public void SetPlayer(IPlayer player)
        {
            _player = player;
        }

        #region Transport Buttons

        private void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_player != null)
            {
                if (_player.isPlaying)
                {
                    PlayBtn.Content = "❚❚";   // Pause
                    _player?.Pause();
                    Logger.Info("Odtwarzanie wstrzymane.");
                }
                else
                {
                    PlayBtn.Content = "▶"; // Play
                    _player?.Play();
                    Logger.Info("Odtwarzanie rozpoczęte.");
                }
            }
            else
                Logger.Error("Nie ustawiono playera dla controlbar!");
        }

        private void PreviouseBtn_Click(object sender, RoutedEventArgs e)
        {
            _player?.Preview();
            Logger.Info("Przejście do poprzedniego utworu.");
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            _player?.Next();
            Logger.Info("Przejście do następnego utworu.");
        }

        #endregion

        #region Volume

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VolumeSlider.Value > 0)
            {
                _lastVolume = VolumeSlider.Value;
                VolumeSlider.Tag = VolumeSlider.Value; // zapamiętaj głośność
                VolumeSlider.Value = 0;
                _player?.Volume = 0;
                MuteBtn.Content = "🔇";
                Logger.Info("Dźwięk wyciszony.");
            }
            else
            {
                VolumeSlider.Value = VolumeSlider.Tag != null ? (double)VolumeSlider.Tag : _lastVolume;
                _player?.Volume = VolumeSlider.Value;
                MuteBtn.Content = "🔊";
                Logger.Info("Dźwięk przywrócony.");
            }
        }

        #endregion

        #region Equalizer Popup Slide Animation

        private void EqualizerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!EqualizerPopup.IsOpen)
            {
                EqualizerPopup.IsOpen = true;

                var anim = new DoubleAnimation
                {
                    From = -20,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                EqualizerBorder.RenderTransform.BeginAnimation(TranslateTransform.YProperty, anim);
            }
            else
            {
                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = -20,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                anim.Completed += (s, a) => EqualizerPopup.IsOpen = false;
                EqualizerBorder.RenderTransform.BeginAnimation(TranslateTransform.YProperty, anim);
            }
        }

        #endregion

        #region Close Button

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Position Slider (timeline)

        private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Przelicz na czas odtwarzania
            TimeSpan current = TimeSpan.FromSeconds(PositionSlider.Value);
            PositionTextbox.Text = current.ToString(@"mm\:ss");
        }

        #endregion

        #region ComboBox Repeat

        private void RepeatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // None / All / One / Random
            var selected = (RepeatComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            RepeatMode = selected ?? "None";
        }

        #endregion

        private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
        {
            _player?.isFullscreen = !_player.isFullscreen;
        }

        private void OpenSubBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OpenPlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            _player?.OpenPlaylist();
        }

        private void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OpenUrlBtn_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
