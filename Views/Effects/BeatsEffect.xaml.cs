// Version: 0.1.1.29
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Thmd.Consolas;

namespace FFmpegApi.Views.Effects
{
    /// <summary>
    /// Logika interakcji dla klasy Beats.xaml
    /// </summary>
    public partial class BeatsEffect : UserControl, INotifyPropertyChanged, IEffect
    {
        private double _time = 0.0;
        private DateTime _lastFrameTime;

        // Stałe częstotliwości
        private const double PRIMARY_BEAT_FREQUENCY = 1.8;
        private const double SECONDARY_BEAT_FREQUENCY = 0.9;

        // Event INotifyPropertyChanged (opcjonalny dla DependencyProperties)
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // DependencyProperty dla tła (ImageSource w Twoim kodzie → BackgroundImageSource)
        public static readonly DependencyProperty BackgroundImageSourceProperty =
            DependencyProperty.Register(nameof(BackgroundImageSource), typeof(ImageSource), typeof(BeatsEffect),
                new PropertyMetadata(null, OnBackgroundImageSourceChanged));

        public ImageSource BackgroundImageSource
        {
            get => (ImageSource)GetValue(BackgroundImageSourceProperty);
            set => SetValue(BackgroundImageSourceProperty, value);
        }

        private static void OnBackgroundImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BeatsEffect control)
            {
                // Opcjonalnie: logika po zmianie
            }
        }

        // DependencyProperty dla efektu (ImageSource)
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(BeatsEffect),
                new PropertyMetadata(null, OnImageSourceChanged));

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BeatsEffect control && e.NewValue == null)
            {
                // Domyślna wartość tylko jeśli null
                control.ImageSource = control.LoadImageFromResource("pack://Thmd:,,,/Image/alien_skeleton.png");
            }
        }

        public BeatsEffect()
        {
            InitializeComponent();
            Name = this.GetType().Name;
            DataContext = this;
            _lastFrameTime = DateTime.Now;
            BackgroundImageSource = LoadImageFromResource("pack://application:,,,/Image/alien_skeleton.png");
            ImageSource = LoadImageFromResource("pack://application:,,,/Image/alien_skeleton.png");
            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // Dynamiczne centrowanie transformów po załadowaniu
            Loaded += BeatsEffect_Loaded;
        }

        private void BeatsEffect_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTransformCenters();
        }

        private void UpdateTransformCenters()
        {
            double centerX = ActualWidth / 2;
            double centerY = ActualHeight / 2;
            BeatRotate.CenterX = centerX;
            BeatRotate.CenterY = centerY;
            BeatScale.CenterX = centerX;
            BeatScale.CenterY = centerY;
            BeatSkew.CenterX = centerX;
            BeatSkew.CenterY = centerY;
        }

        // Wywołaj to też w SizeChanged event, jeśli UserControl zmienia rozmiar
        /// <summary>
        /// Obsługuje zmianę rozmiaru kontrolki i aktualizuje centra transformacji.
        /// </summary>
        /// <param name="sizeInfo">nowa wielkość</param>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateTransformCenters();
        }

        // Metoda pomocnicza do ładowania ImageSource z zasobu
        private ImageSource LoadImageFromResource(string resourcePath)
        {
            try
            {
                // 1️⃣ Najpierw próbujemy jako zasób WPF (Resource)
                var uri = new Uri(resourcePath, UriKind.Absolute);
                var bitmap = new BitmapImage(uri);
                return bitmap;
            }
            catch
            {
                try
                {
                    // 2️⃣ Jeśli nie działa — spróbujmy jako Content (plik w katalogu aplikacji)
                    string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, resourcePath.Replace('/', '\\'));
                    if (System.IO.File.Exists(path))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        return bitmap;
                    }
                }
                catch (Exception ex2)
                {
                    this.WriteLine($"[{ex2.HResult}]: {ex2.Message}");
                }
            }
            return null;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            DateTime currentTime = DateTime.Now;
            double deltaTime = (currentTime - _lastFrameTime).TotalSeconds;
            _lastFrameTime = currentTime;
            _time += deltaTime;

            // 1. Pulsowanie (Scale)
            double primaryBeat = System.Math.Abs(System.Math.Sin(_time * PRIMARY_BEAT_FREQUENCY * System.Math.PI));
            double secondaryBeat = System.Math.Abs(System.Math.Sin(_time * SECONDARY_BEAT_FREQUENCY * System.Math.PI));
            double combinedBeat = (primaryBeat * 0.7 + secondaryBeat * 0.3);
            double scaleFactor = 1.0 + System.Math.Pow(combinedBeat, 1.5) * 0.08;
            BeatScale.ScaleX = scaleFactor;
            BeatScale.ScaleY = scaleFactor;

            // 2. Wibracje (Translate)
            double pulseVibration = System.Math.Pow(primaryBeat, 3) * 4;
            BeatTranslate.X = System.Math.Sin(_time * 15) * pulseVibration;
            BeatTranslate.Y = System.Math.Cos(_time * 18) * pulseVibration;

            // 3. Wyginanie (Skew)
            BeatSkew.AngleX = combinedBeat * 2.5;
            BeatSkew.AngleY = combinedBeat * 1.5;

            // 4. Opacity na elemencie UI (nie na ImageSource!)
            /*if (BeatImageElement != null)
            {
                BeatImageElement.Opacity = 0.8 + combinedBeat * 0.2;
            }*/

            // 5. Rotacja
            BeatRotate.Angle = System.Math.Sin(_time * PRIMARY_BEAT_FREQUENCY * System.Math.PI) * 5;
        }

        public void ForceUpdate(double deltaTime)
        {
            _time += deltaTime;

            double primaryBeat = System.Math.Abs(System.Math.Sin(_time * PRIMARY_BEAT_FREQUENCY * System.Math.PI));
            double secondaryBeat = System.Math.Abs(System.Math.Sin(_time * SECONDARY_BEAT_FREQUENCY * System.Math.PI));
            double combinedBeat = (primaryBeat * 0.7 + secondaryBeat * 0.3);
            double scaleFactor = 1.0 + System.Math.Pow(combinedBeat, 1.5) * 0.08;

            BeatScale.ScaleX = scaleFactor;
            BeatScale.ScaleY = scaleFactor;

            double pulseVibration = System.Math.Pow(primaryBeat, 3) * 4;
            BeatTranslate.X = System.Math.Sin(_time * 15) * pulseVibration;
            BeatTranslate.Y = System.Math.Cos(_time * 18) * pulseVibration;

            BeatSkew.AngleX = combinedBeat * 2.5;
            BeatSkew.AngleY = combinedBeat * 1.5;

            BeatRotate.Angle = System.Math.Sin(_time * PRIMARY_BEAT_FREQUENCY * System.Math.PI) * 5;
        }
    }
}
