// Version: 0.0.0.15
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FFmpegApi.Views
{
    public partial class ProgressbarView : UserControl
    {
        public int MOUSE_LEAVE_TIME = 5000; // Czas w milisekundach przed ukryciem wskaźnika myszy

        private DispatcherTimer _mouseLeaveTimer;

        public ProgressbarView()
        {
            InitializeComponent();
            DataContext = this;

            _popup.Closed += Popup_Closed;
            _popup.Opened += Popup_Opened;
            _popup.MouseEnter += Popup_MouseEnter;
            _popup.MouseLeave += Popup_MouseLeave;

            _rectangleMouseOverPoint.Visibility = Visibility.Hidden;

            _mouseLeaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _mouseLeaveTimer.Tick += MouseLeaveTimer_Tick;
        }

        #region Dependency Properties

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ProgressbarView), new PropertyMetadata(0.0));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ProgressbarView), new PropertyMetadata(100.0));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(ProgressbarView), new PropertyMetadata(0.0));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ProgressTextProperty =
            DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(ProgressbarView), new PropertyMetadata("0%"));

        public string ProgressText
        {
            get => (string)GetValue(ProgressTextProperty);
            set => SetValue(ProgressTextProperty, value);
        }

        public static readonly DependencyProperty PopupTextProperty =
            DependencyProperty.Register(nameof(PopupText), typeof(string), typeof(ProgressbarView), new PropertyMetadata(""));

        public string PopupText
        {
            get => (string)GetValue(PopupTextProperty);
            set => SetValue(PopupTextProperty, value);
        }

        public static readonly DependencyProperty PopupImageProperty =
            DependencyProperty.Register(nameof(PopupImage), typeof(BitmapImage), typeof(ProgressbarView), new PropertyMetadata(null));

        public BitmapImage PopupImage
        {
            get => (BitmapImage)GetValue(PopupImageProperty);
            set => SetValue(PopupImageProperty, value);
        }

        public static readonly DependencyProperty BufferBarProperty =
            DependencyProperty.Register(nameof(BufferBar), typeof(ProgressBar), typeof(ProgressbarView), new PropertyMetadata(null));

        public ProgressBar BufferBar
        {
            get => (ProgressBar)GetValue(BufferBarProperty);
            set => SetValue(BufferBarProperty, value);
        }

        public static readonly DependencyProperty BufferValueProperty =
            DependencyProperty.Register(nameof(BufferValue), typeof(double), typeof(ProgressbarView), new PropertyMetadata(0.0));

        public double BufferValue
        {
            get => (double)GetValue(BufferBarProperty);
            set => SetValue(BufferValueProperty, value);
        }

        #endregion

        #region === Metody Publiczne ===

        public void UpdateProgress(double value, string? text = null)
        {
            if (value < Minimum) value = Minimum;
            if (value > Maximum) value = Maximum;

            Value = value;
            ProgressText = text ?? $"{System.Math.Round((value - Minimum) / (Maximum - Minimum) * 100)}%";
        }

        public void ShowPopup(string text, BitmapImage image = null)
        {
            PopupText = text;
            if (image != null)
                PopupImage = image;

            _popup.IsOpen = true;
        }

        public void HidePopup()
        {
            _popup.IsOpen = false;
        }

        #endregion

        #region === Obsługa Myszy ===

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _rectangleMouseOverPoint.Visibility = Visibility.Visible;
            _mouseLeaveTimer.Stop();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var pos = e.GetPosition(_progressBar);
            double ratio = pos.X / _progressBar.ActualWidth;
            double hoverValue = Minimum + ratio * (Maximum - Minimum);

            Debug.WriteLine("Mouse Move - PosX: " + pos.X + " Ratio: " + ratio + " HoverValue: " + hoverValue);

            if (pos.X <= (_rectangleMouseOverPoint.Width / 2))
                _rectangleMouseOverPoint.Visibility = Visibility.Collapsed;
            else if(pos.X>= (_rectangleMouseOverPoint.Width / 2))
                _rectangleMouseOverPoint.Visibility = Visibility.Visible;
            if (pos.X >= _progressBar.ActualWidth - (_rectangleMouseOverPoint.Width / 2))
                _rectangleMouseOverPoint.Visibility = Visibility.Collapsed;

            _rectangleMouseOverPoint.Margin = new Thickness(pos.X-(_rectangleMouseOverPoint.Width/2), 0, 0, 0);

            PopupText = $"{System.Math.Round(hoverValue)} / {Maximum}";
            _popup.HorizontalOffset = pos.X-(_popup.Width/2);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _rectangleMouseOverPoint.Margin = new Thickness(0, 0, 0, 0);
            _rectangleMouseOverPoint.Visibility = Visibility.Hidden;
            _mouseLeaveTimer.Start();
        }

        #endregion

        #region === Timer dla myszy ===

        private void MouseLeaveTimer_Tick(object sender, EventArgs e)
        {
            Task.Delay(MOUSE_LEAVE_TIME);
            _rectangleMouseOverPoint.Margin = new Thickness(0, 0, 0, 0);
            _mouseLeaveTimer.Stop();
            HidePopup();
        }

        #endregion

        #region === Obsługa zdarzeń dla Popup ===

        private void Popup_MouseEnter(object sender, MouseEventArgs e)
        {
            // Zapobiega zamknięciu popupu podczas najechania myszki na popup
            e.Handled = true;
        }

        private void Popup_MouseLeave(object sender, MouseEventArgs e)
        {
            // Ukrywa popup po opuszczeniu myszki z popupu
            HidePopup();
        }

        private void Popup_Closed(object sender, EventArgs e)
        {
            var sb = (Storyboard)FindResource("fadeOutControl");
            sb.Begin();
        }

        private void Popup_Opened(object sender, EventArgs e)
        {
            var sb = (Storyboard)FindResource("fadeInControl");
            sb.Begin();
        }

        #endregion

        #region === Animacje ===
        public void ShowProgressBar()
        {
            var sb = (Storyboard)FindResource("fadeInControl");
            sb.Begin();
        }

        public void HideProgressBar()
        {
            var sb = (Storyboard)FindResource("fadeOutControl");
            sb.Begin();
        }

        #endregion
    }
}
