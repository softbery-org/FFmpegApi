// Version: 0.1.0.29
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FFmpegApi.Views.Effects
{
    /// <summary>
    /// Displays a semi-transparent gradient overlay at the top or bottom of the ListView
    /// to indicate active auto-scroll during drag-and-drop.
    /// </summary>
    public class ScrollIndicatorAdorner : Adorner
    {
        private readonly bool _isTop;
        private double _opacity = 0.0;

        /// <summary>
        /// Initializes a new instance of the ScrollIndicatorAdorner.
        /// </summary>
        /// <param name="adornedElement">The target UIElement to adorn.</param>
        /// <param name="isTop">True for top indicator, false for bottom indicator.</param>
        public ScrollIndicatorAdorner(UIElement adornedElement, bool isTop)
            : base(adornedElement)
        {
            _isTop = isTop;
            IsHitTestVisible = false;
        }

        /// <summary>
        /// Smoothly animates opacity to make the indicator visible.
        /// </summary>
        public void FadeIn()
        {
            var anim = new DoubleAnimation(_opacity, 1.0, new Duration(TimeSpan.FromMilliseconds(150)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        /// <summary>
        /// Smoothly hides the indicator.
        /// </summary>
        public void FadeOut()
        {
            var anim = new DoubleAnimation(Opacity, 0.0, new Duration(TimeSpan.FromMilliseconds(250)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        /// <summary>
        /// Draws the top or bottom gradient overlay.
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            double height = 60;
            double width = AdornedElement.RenderSize.Width;
            Rect rect = _isTop
                ? new Rect(0, 0, width, height)
                : new Rect(0, AdornedElement.RenderSize.Height - height, width, height);

            GradientStopCollection stops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(_isTop ? (byte)200 : (byte)180, 0, 0, 0), 0.0),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0)
            };

            var brush = new LinearGradientBrush(stops, _isTop ? new Point(0, 0) : new Point(0, 1), _isTop ? new Point(0, 1) : new Point(0, 0));
            brush.Opacity = this.Opacity;

            dc.DrawRectangle(brush, null, rect);
        }
    }
}
