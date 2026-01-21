// Version: 0.2.0.26
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FFmpegApi.Utilities;

/// <summary>
/// Static utility enabling movement, resizing, ZIndex boosting
/// and fade animations for any FrameworkElement.
/// 
/// Supports:
/// • Dragging inside parent container (Canvas, Grid, Panel)
/// • Resizing from all 8 edges/corners
/// • Preventing moving outside parent
/// • Auto-bring-to-front by ZIndex
/// • Animated opacity while dragging
/// • Per-element state pooling
/// </summary>
public static class ControlControllerHelper
{
    /// <summary>
    /// Resize zones around the control.
    /// </summary>
    private enum ResizeDirection
    {
        None,
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Moving
    }

    /// <summary>
    /// Per-element state storage.
    /// </summary>
    private class ElementState
    {
        public bool IsResizing;
        public bool IsMoving;
        public ResizeDirection Direction;
        public bool IsCaptured;
        public Point LastMousePosition;
        public Rect OriginalBounds;
        public double OriginalOpacity = 1.0;
        public double DragOpacity = 0.6;
        public double ResizeBorderWidth = 3.0;
    }

    /// <summary>
    /// State for each tracked element.
    /// </summary>
    private static readonly Dictionary<FrameworkElement, ElementState> _states = new();

    // ============================
    // PUBLIC API
    // ============================
    #region Public API
    /// <summary>
    /// Attaches dragging/resizing behavior to a FrameworkElement.
    /// </summary>
    /// <param name="element">Target control.</param>
    /// <param name="config">Target configuration.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Attach(FrameworkElement element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        if (_states.ContainsKey(element))
            return; // already attached

        _states[element] = new ElementState();

        element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
        element.MouseMove += Element_MouseMove;
        element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
        element.Cursor = Cursors.Arrow;
    }

    /// <summary>
    /// Removes controller behavior from element.
    /// </summary>
    /// <param name="element">Target element</param>
    /// <param name="config">Target configruration</param>
    public static void Detach(FrameworkElement element)
    {
        if (!_states.ContainsKey(element))
            return;

        element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
        element.MouseMove -= Element_MouseMove;
        element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;

        _states.Remove(element);
    }

    /// <summary>
    /// Returns highest ZIndex among siblings of element.
    /// </summary>
    public static int GetTopZ(FrameworkElement element)
    {
        Panel? panel = GetParentPanel(element);
        if (panel == null)
            return 0;

        int topZ = 0;

        foreach (UIElement child in panel.Children)
        {
            int z = Panel.GetZIndex(child);
            if (z > topZ)
                topZ = z;
        }

        return topZ;
    }
    #endregion
    // ============================
    // EVENT HANDLERS
    // ============================
    #region Event Handlers
    private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var st = _states[element];

        Point pos = e.GetPosition(element);
        ResizeDirection dir = GetResizeDirection(element, st, pos);

        if (dir == ResizeDirection.None)
            return;

        st.Direction = dir;
        st.IsResizing = dir != ResizeDirection.Moving;
        st.IsMoving = dir == ResizeDirection.Moving;

        st.LastMousePosition = e.GetPosition(GetRootContainer(element));
        st.OriginalBounds = new Rect(element.Margin.Left, element.Margin.Top, element.ActualWidth, element.ActualHeight);

        st.IsCaptured = true;
        element.CaptureMouse();

        // fade + bring to top
        if (st.IsMoving)
        {
            st.OriginalOpacity = element.Opacity;
            AnimateOpacity(element, st.DragOpacity);

            int z = GetTopZ(element);
            Panel.SetZIndex(element, z + 1);
        }

        e.Handled = true;
    }

    private static void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var st = _states[element];

        if (st.IsMoving)
        {
            MoveElement(element, st, e);
            return;
        }

        if (st.IsResizing)
        {
            ResizeElement(element, st, e);

            int z = GetTopZ(element);
            Panel.SetZIndex(element, z + 1);
            return;
        }

        // Update cursor only if idle
        ResizeDirection dir = GetResizeDirection(element, st, e.GetPosition(element));
        UpdateCursor(element, dir);
    }

    private static void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var st = _states[element];

        if (!st.IsResizing && !st.IsMoving)
            return;

        if (st.IsMoving)
            AnimateOpacity(element, st.OriginalOpacity);

        st.IsMoving = false;
        st.IsResizing = false;
        st.Direction = ResizeDirection.None;
        st.IsCaptured = false;

        element.ReleaseMouseCapture();
        e.Handled = true;
    }
    #endregion
    // ============================
    // MOVING
    // ============================
    #region Moving
    private static void MoveElement(FrameworkElement element, ElementState state, MouseEventArgs e)
    {
        FrameworkElement? container = GetRootContainer(element);
        if (container == null)
            return;

        Point curr = e.GetPosition(container);

        double dx = curr.X - state.LastMousePosition.X;
        double dy = curr.Y - state.LastMousePosition.Y;

        Thickness margin = element.Margin;

        margin.Left += dx;
        margin.Top += dy;

        // clamp to bounds
        double maxLeft = container.ActualWidth - element.ActualWidth;
        double maxTop = container.ActualHeight - element.ActualHeight;

        margin.Left = System.Math.Max(0, System.Math.Min(maxLeft, margin.Left));
        margin.Top = System.Math.Max(0, System.Math.Min(maxTop, margin.Top));

        element.Margin = margin;

        _margin = margin;

        state.LastMousePosition = curr;
        e.Handled = true;
    }
    #endregion
    // ============================
    // RESIZING
    // ============================
    #region Resizing
    private static void ResizeElement(FrameworkElement element, ElementState st, MouseEventArgs e)
    {
        FrameworkElement? container = GetRootContainer(element);
        if (container == null)
            return;

        Point curr = e.GetPosition(container);

        double dx = curr.X - st.LastMousePosition.X;
        double dy = curr.Y - st.LastMousePosition.Y;

        Thickness m = element.Margin;
        double w = element.ActualWidth;
        double h = element.ActualHeight;

        switch (st.Direction)
        {
            case ResizeDirection.Left:
                m.Left += dx; w -= dx; break;
            case ResizeDirection.Right:
                w += dx; break;
            case ResizeDirection.Top:
                m.Top += dy; h -= dy; break;
            case ResizeDirection.Bottom:
                h += dy; break;
            case ResizeDirection.TopLeft:
                m.Left += dx; w -= dx; m.Top += dy; h -= dy; break;
            case ResizeDirection.TopRight:
                w += dx; m.Top += dy; h -= dy; break;
            case ResizeDirection.BottomLeft:
                m.Left += dx; w -= dx; h += dy; break;
            case ResizeDirection.BottomRight:
                w += dx; h += dy; break;
        }

        // min size
        w = System.Math.Max(w, element.MinWidth);
        h = System.Math.Max(h, element.MinHeight);

        // clamp so margins never negative
        if (m.Left < 0)
        {
            w += m.Left;
            m.Left = 0;
        }
        if (m.Top < 0)
        {
            h += m.Top;
            m.Top = 0;
        }

        // clamp to parent bounds
        double maxW = container.ActualWidth - m.Left;
        double maxH = container.ActualHeight - m.Top;

        w = System.Math.Min(w, maxW);
        h = System.Math.Min(h, maxH);

        element.Margin = m;
        element.Width = w;
        element.Height = h;

        _size = new Size(w, h);

        st.LastMousePosition = curr;
        e.Handled = true;
    }
    #endregion
    // ============================
    // CURSOR AND RESIZE ZONES
    // ============================
    #region Cursor and Resize Zones
    private static ResizeDirection GetResizeDirection(FrameworkElement element, ElementState st, Point p)
    {
        double b = st.ResizeBorderWidth;

        bool left = p.X < b;
        bool right = p.X > element.ActualWidth - b;
        bool top = p.Y < b;
        bool bottom = p.Y > element.ActualHeight - b;

        bool center = !left && !right && !top && !bottom;

        if (top && left) return ResizeDirection.TopLeft;
        if (top && right) return ResizeDirection.TopRight;
        if (bottom && left) return ResizeDirection.BottomLeft;
        if (bottom && right) return ResizeDirection.BottomRight;

        if (left) return ResizeDirection.Left;
        if (right) return ResizeDirection.Right;
        if (top) return ResizeDirection.Top;
        if (bottom) return ResizeDirection.Bottom;

        if (center) return ResizeDirection.Moving;
        return ResizeDirection.None;
    }

    private static void UpdateCursor(FrameworkElement element, ResizeDirection d)
    {
        element.Cursor = d switch
        {
            ResizeDirection.TopLeft => Cursors.SizeNWSE,
            ResizeDirection.BottomRight => Cursors.SizeNWSE,
            ResizeDirection.TopRight => Cursors.SizeNESW,
            ResizeDirection.BottomLeft => Cursors.SizeNESW,
            ResizeDirection.Left => Cursors.SizeWE,
            ResizeDirection.Right => Cursors.SizeWE,
            ResizeDirection.Top => Cursors.SizeNS,
            ResizeDirection.Bottom => Cursors.SizeNS,
            _ => Cursors.Arrow,
        };
    }
    #endregion
    // ============================
    // HELPERS
    // ============================
    #region Helpers
    private static Thickness _margin;
    private static Size _size;

    //public static void SaveConfigElement<T>(FrameworkElement element) where T : IConfigElement,new()
    //{
    //    var c = new T();
    //    c.Left = _margin.Left; c.Top = _margin.Top;
    //    c.Width = _size.Width; c.Height = _size.Height;

    //    Config.Instance.Save();
    //    Console.WriteLine("Saved position and size");
    //}

    private static FrameworkElement GetRootContainer(FrameworkElement element)
    {
        DependencyObject parent = element;

        while (parent != null)
        {
            if (parent is Canvas or Grid or Border or Panel)
                return parent as FrameworkElement;

            parent = VisualTreeHelper.GetParent(parent);
        }

        return Window.GetWindow(element);
    }

    private static Panel GetParentPanel(FrameworkElement element)
    {
        DependencyObject p = element;

        while (p != null && p is not Panel)
            p = VisualTreeHelper.GetParent(p);

        return p as Panel;
    }

    private static void AnimateOpacity(FrameworkElement element, double target)
    {
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(120),
            FillBehavior = FillBehavior.Stop
        };

        anim.Completed += (s, a) => element.Opacity = target;

        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    internal static void HandleKeyDown(UIElement element, Key e, Action action)
    {
        if (Key.None == e)
            return;
        
        element.KeyDown += (s, ev) =>
        {
            var args_key_down = new KeyControlEventArgs(KeyControlEventArgs.KeyDownEvent, element)
            {
                Key = e,
                Action = action
            };
            if (e == ev.Key)
            {
                element.RaiseEvent(args_key_down);

                action?.Invoke();

                Debug.WriteLine($"In \"{element.GetType().Name}\", key down: \"{e.ToString()}\" and run action with method name: \"{action?.Method.Name}\"");
            }
        };
    }

    internal static void HandleKeyHold(FrameworkElement element, Key e, Action action)
    {
        if (Key.None == e)
            return;

        element.KeyDown += (s, ev) =>
        {
            var args_key_hold = new KeyControlEventArgs(KeyControlEventArgs.KeyHoldEvent, element)
            {
                Key = e,
                Action = action
            };
            if (e == ev.Key && ev.IsDown && ev.Timestamp >= 5000)
            {
                element.RaiseEvent(args_key_hold);

                action?.Invoke();

                Debug.WriteLine($"In \"{element.GetType().Name}\", key down: \"{e.ToString()}\" and run action with method name: \"{action?.Method.Name}\"");
            }
        };
    }
    #endregion
}

public class KeyControlEventArgs : KeyEventArgs 
{ 
    public static readonly RoutedEvent KeyDownEvent = 
        EventManager.RegisterRoutedEvent(
            "ControlKeyDown", 
            RoutingStrategy.Bubble, 
            typeof(KeyEventHandler), 
            typeof(KeyControlEventArgs)
        );

    public static readonly RoutedEvent KeyUpEvent = 
        EventManager.RegisterRoutedEvent(
            "ControlKeyUp", 
            RoutingStrategy.Bubble, 
            typeof(KeyEventHandler), 
            typeof(KeyControlEventArgs)
        );

    public static readonly RoutedEvent KeyPressEvent = 
        EventManager.RegisterRoutedEvent(
            "ControlKeyPress", 
            RoutingStrategy.Bubble, 
            typeof(KeyEventHandler), 
            typeof(KeyControlEventArgs)
        );

    public static readonly RoutedEvent KeyReleaseEvent = 
        EventManager.RegisterRoutedEvent(
            "ControlKeyRelease", 
            RoutingStrategy.Bubble, 
            typeof(KeyEventHandler), 
            typeof(KeyControlEventArgs)
        );

    public static readonly RoutedEvent KeyHoldEvent =
        EventManager.RegisterRoutedEvent(
            "ControlKeyHold",
            RoutingStrategy.Bubble,
            typeof(KeyEventHandler),
            typeof(KeyControlEventArgs)
        );

    public Key Key { get; set; }

    public Action Action { get; set; }

    public KeyControlEventArgs(RoutedEvent routedEvent, object source) 
        : base(Keyboard.PrimaryDevice, PresentationSource.FromVisual((Visual)source), 0, Key.None) 
    { 
        RoutedEvent = routedEvent; 
        Source = source;
    }
}
