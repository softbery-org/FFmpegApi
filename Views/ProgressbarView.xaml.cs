// Version: 0.0.0.1
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace FFmpegApi.Views
{
    public partial class ProgressbarView : UserControl
    {
        public ProgressbarView()
        {
            InitializeComponent();
            DataContext = this;
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

        #endregion

        #region Methods

        public void UpdateProgress(double value, string? text = null)
        {
            if (value < Minimum) value = Minimum;
            if (value > Maximum) value = Maximum;

            Value = value;
            ProgressText = text ?? $"{System.Math.Round((value - Minimum) / (Maximum - Minimum) * 100)}%";
        }

        public void ShowPopup(string text, BitmapImage? image = null)
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

        #region Mouse Events

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var pos = e.GetPosition(_progressBar);
            double ratio = pos.X / _progressBar.ActualWidth;
            double hoverValue = Minimum + ratio * (Maximum - Minimum);

            _rectangleMouseOverPoint.Margin = new Thickness(pos.X, 0, 0, 0);

            PopupText = $"{System.Math.Round(hoverValue)} / {Maximum}";
            _popup.HorizontalOffset = pos.X;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _rectangleMouseOverPoint.Margin = new Thickness(0, 0, 0, 0);
            HidePopup();
        }

        #endregion
    }
}
