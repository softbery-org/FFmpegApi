// Version: 0.1.0.31
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FFmpegApi.Views.Effects
{
    /// <summary>
    /// Custom Adorner for visualizing a dragged element with a shadow effect (DropShadowEffect).
    /// Renders a semi-transparent copy of a UIElement (e.g., ListViewItem) with a shadow for drag-and-drop UX.
    /// Compatible with AdornerLayer in ScrollViewer/ListView in .NET 4.8.
    /// Połączona wersja z obsługą Offset (Point) dla dynamicznego pozycjonowania (np. pod kursorem myszy).
    /// </summary>
    public class DragShadowAdorner : Adorner
    {
        /// <summary>
        /// The source element for visual copying (e.g., the dragged ListViewItem).
        /// </summary>
        private readonly UIElement _child;

        /// <summary>
        /// The shadow effect applied to the element copy.
        /// </summary>
        private readonly DropShadowEffect _shadowEffect;

        /// <summary>
        /// Transform used to move the adorner along with the mouse.
        /// </summary>
        private readonly TranslateTransform _translateTransform;

        /// <summary>
        /// Offset for positioning the shadow relative to the adorner (e.g., mouse position adjustment).
        /// </summary>
        private Point _offset = new Point(0, 0);

        /// <summary>
        /// Throttling: prevents invalidate spam (~60 FPS)
        /// </summary>
        private long _lastInvalidateTicks = 0;

        /// <summary>
        /// Gets or sets the offset for the adorner's child positioning (Point dla kompatybilności z Rect).
        /// Invalidates Arrange to update the visual position.
        /// </summary>
        public Point Offset
        {
            get => _offset;
            set
            {
                if (_offset == value) return;  // Unikaj niepotrzebnych update'ów

                _offset = value;
                _translateTransform.X = _offset.X;
                _translateTransform.Y = _offset.Y;

                // Throttling: Invalidate co ~16ms (60 FPS) – poprawia płynność w .NET 4.8
                long nowTicks = DateTime.Now.Ticks;
                if (nowTicks - _lastInvalidateTicks > 166666)  // 16ms w ticks
                {
                    InvalidateArrange();
                    InvalidateVisual();
                    _lastInvalidateTicks = nowTicks;
                }
            }
        }

        /// <summary>
        /// Initializes a new Adorner with a shadow effect.
        /// </summary>
        /// <param name="adornedElement">The element on which the Adorner is overlaid (e.g., ScrollViewer).</param>
        /// <param name="child">The element to visualize (e.g., a copy of ListViewItem).</param>
        public DragShadowAdorner(UIElement adornedElement, UIElement child)
            : base(adornedElement)
        {
            _child = child;
            _offset = new Point(5, 5);  // Domyślny offset (lekki shift dla cienia)

            // Configure DropShadowEffect for the shadow (black, blur 10, offset 5px)
            _shadowEffect = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 315,
                ShadowDepth = 5,
                BlurRadius = 10,
                Opacity = 0.5
            };

            _child.Effect = _shadowEffect;
            _translateTransform = new TranslateTransform(_offset.X, _offset.Y);  // Fix: Użyj Point
            _child.RenderTransform = _translateTransform;

            AddVisualChild(_child);
            IsHitTestVisible = false; // Adorner nie powinien przechwytywać zdarzeń myszy
            SetValue(Panel.ZIndexProperty, 999);  // Wyższa priorytet renderowania (na wierzchu)
        }

        /// <summary>
        /// Updates the position of the adorner so it follows the cursor during drag.
        /// </summary>
        /// <param name="position">The current mouse position relative to the adorned element.</param>
        public void UpdatePosition(Point position)
        {
            Offset = new Point(position.X + 5, position.Y + 5);  // Użyj Point dla spójności
            InvalidateVisual();
        }

        /// <summary>
        /// Renders the Adorner – visually copies _child with shadow.
        /// </summary>
        /// <param name="drawingContext">The drawing context.</param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            // Semi-transparent floating look is handled by RenderTransform (TranslateTransform)
            drawingContext.PushOpacity(0.8);
            base.OnRender(drawingContext);
            drawingContext.Pop();
        }

        /// <summary>
        /// Calculates the Adorner size – matched to the adornedElement.
        /// </summary>
        /// <param name="constraint">The available size constraint.</param>
        /// <returns>The desired size of the Adorner.</returns>
        protected override Size MeasureOverride(Size constraint)
        {
            _child.Measure(constraint);
            return _child.DesiredSize;  // Zwróć DesiredSize child (dopasowane do kopii elementu)
        }

        /// <summary>
        /// Sets the visual size of the Adorner with offset positioning.
        /// </summary>
        /// <param name="finalSize">The final size allocated to the Adorner.</param>
        /// <returns>The arranged size of the Adorner.</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Arrange child z offsetem (pozycjonowanie względem Offset jako Point)
            _child.Arrange(new Rect(Offset, _child.DesiredSize));  // Fix: Rect(Point, Size) – teraz pasuje
            return finalSize;  // Adorner zajmuje pełny finalSize (overlay)
        }

        /// <summary>
        /// Returns the visual children of the Adorner (copy with shadow).
        /// </summary>
        protected override Visual GetVisualChild(int index) => _child;

        /// <summary>
        /// Number of visual children (1 – element copy).
        /// </summary>
        protected override int VisualChildrenCount => 1;
    }
}
