// Version: 0.1.13.24
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Thmd.Views;
/// <summary>
/// Logika interakcji dla klasy InfoBox.xaml
/// </summary>
public partial class InfoBox : UserControl, INotifyPropertyChanged
{
    private HwndSource _hwndSource;
    private System.Windows.Media.Color _backgroundColor = Colors.LightBlue;
    private string _imagePath;
    private System.Windows.Media.FontFamily _fontFamily;
    private System.Windows.Media.Color _fontColor;
    private bool _textShadow = true;

    private Grid _container;
    private Label _fadeLabel;
    private DispatcherTimer _fadeTimer;

    public event PropertyChangedEventHandler PropertyChanged;

    public System.Windows.Media.Color BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; InvalidateVisual(); }
    }

    public string DrawText
    {
        get => _fadeLabel.Content as string;
        set
        {
            _fadeLabel.Content = value;
            _fadeLabel.Opacity = 1.0;
            _fadeLabel.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

            // ⬇ automatyczne dopasowanie rozmiaru do tekstu
            UpdateSizeToFitText(value);

            if (!string.IsNullOrEmpty(value))
                StartFadeOutTimer();
        }
    }

    public System.Windows.Media.FontFamily TextFontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            _fadeLabel.FontFamily = _fontFamily;
            OnPropertyChanged(nameof(TextFontFamily), ref _fontFamily, value);
        }
    }

    public System.Windows.Media.Color TextColor
    {
        get => _fontColor;
        set
        {
            _fontColor = value;
            _fadeLabel.Foreground = new SolidColorBrush(_fontColor);
            OnPropertyChanged(nameof(TextColor), ref _fontColor, value);
        }
    }

    public bool TextShadow
    {
        get => _textShadow;
        set
        {
            _textShadow = value;
            if (value)
            {
                _fadeLabel.Effect = _textShadow ? new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 320,
                    ShadowDepth = 2,
                    Opacity = 0.5,
                    BlurRadius = 4
                }:null;
            }
            OnPropertyChanged(nameof(TextShadow), ref _textShadow, value);
        }
    }

    public string ImagePath
    {
        get => _imagePath;
        set { _imagePath = value; InvalidateVisual(); }
    }

    public InfoBox()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ClipToBounds = true;
        Background = System.Windows.Media.Brushes.Transparent;

        // --- Utworzenie kontenera i etykiety ---
        _container = new Grid();
        _container.Name = "_grid";

        _fadeLabel = new Label
        {
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 24,
            Content = "",
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(20, 20, 0, 0),
            Visibility = Visibility.Collapsed
        };

        _container.Children.Add(_fadeLabel);
        this.Content = _container;
    }

    private void UpdateSizeToFitText(string text)
    {
        double imgWidth = 0, imgHeight = 0;

        if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
        {
            using (var img = System.Drawing.Image.FromFile(_imagePath))
            {
                imgWidth = img.Width;
                imgHeight = img.Height;
            }
        }

        if (string.IsNullOrEmpty(text))
        {
            this.Width = imgWidth + 40;
            this.Height = imgHeight + 40;
            return;
        }

        var typeface = new Typeface(_fadeLabel.FontFamily, _fadeLabel.FontStyle, _fadeLabel.FontWeight, _fadeLabel.FontStretch);

        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            _fadeLabel.FontSize,
            System.Windows.Media.Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double newWidth = System.Math.Max(formattedText.Width, imgWidth) + _fadeLabel.Margin.Left + 20;
        double newHeight = formattedText.Height + imgHeight + _fadeLabel.Margin.Top + 40;

        this.Width = newWidth;
        this.Height = newHeight;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            _hwndSource = source;
            _hwndSource.AddHook(WndProc);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(new SolidColorBrush(_backgroundColor), null, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_PAINT = 0x000F;
        const int WM_SETREDRAW = 0x000B;

        switch (msg)
        {
            case WM_SETREDRAW:
                handled = false;
                break;

            case WM_PAINT:
                OnPaint(hwnd);
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void OnPaint(IntPtr hwnd)
    {
        using (Graphics g = Graphics.FromHwnd(hwnd))
        {
            // tło
            using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(
                _backgroundColor.A, _backgroundColor.R, _backgroundColor.G, _backgroundColor.B)))
            {
                g.FillRectangle(brush, 0, 0, (int)ActualWidth, (int)ActualHeight);
            }

            // obraz PNG
            if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
            {
                using (var image = System.Drawing.Image.FromFile(_imagePath))
                    g.DrawImage(image, new System.Drawing.Rectangle(50, 70, 128, 128));
            }
        }
    }

    // --- Fade-out tekstu ---
    private void StartFadeOutTimer()
    {
        _fadeTimer?.Stop();
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5) // krótka pauza przed startem
        };
        _fadeTimer.Tick += async (s, e) =>
        {
            _fadeTimer.Stop();
            await FadeOutLabel();
        };
        _fadeTimer.Start();
    }

    private async System.Threading.Tasks.Task FadeOutLabel()
    {
        //await _fadeLabel.HideByStoryboard((Storyboard)this.FindResource("fadeOutBox"));
        _fadeLabel.Visibility = Visibility.Collapsed;
        _fadeLabel.Opacity = 1.0; // reset
    }

    private void OnPropertyChanged<T>(string propertyName, ref T field, T value)
    {
        if (field != null || value == null)
        {
            if (field == null)
            {
                return;
            }
            object obj = value;
            if (field.Equals(obj))
            {
                return;
            }
        }
        field = value;
        PropertyChanged?.Invoke(field, new PropertyChangedEventArgs(propertyName));
    }
}
