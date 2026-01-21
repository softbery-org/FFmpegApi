// Version: 0.0.0.1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using FFmpegApi.Logs;

namespace FFmpegApi.Utilities;

public class WindowLastStance
{
    public WindowState State { get; set; }

    public ResizeMode Mode { get; set; }

    public WindowStyle Style { get; set; }
}


public static class FullscreenHelper
{
    private static WindowLastStance _lastWindowStance;

    private static bool _fullscreen;

    public static bool IsFullscreen => _fullscreen;

    public static WindowLastStance LastWindowStance => _lastWindowStance;

    public static Size GetWindowSize(this object element)
    {
        Window window = Window.GetWindow(element as DependencyObject);
        var windowSize = new Size();
        if (window != null)
        {
            windowSize.Width = window.ActualWidth;
            windowSize.Height = window.ActualHeight;
        }
        return windowSize;
    }

    public static void Fullscreen(this object sender)
    {
        FullscreenOnOff(sender);
    }

    public static void Fullscreen(this object sender, bool fullscreen)
    {
        if (fullscreen == IsFullscreen)
        {
            FullscreenOnOff(sender);
        }
    }

    private static void FullscreenOnOff(object sender)
    {
        Window window = Window.GetWindow(sender as DependencyObject);
        if (window != null)
        {
            if (window.WindowStyle == WindowStyle.None)
            {
                if (_lastWindowStance != null)
                {
                    window.ResizeMode = _lastWindowStance.Mode;
                    window.WindowStyle = _lastWindowStance.Style;
                    window.WindowState = _lastWindowStance.State;
                    _fullscreen = false;
                    Logger.Info($"Exit fullscreen: Change video screen from fullscreen to last stance {_lastWindowStance.State}");
                    return;
                }
                window.ResizeMode = ResizeMode.CanResize;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.WindowState = WindowState.Normal;
                _fullscreen = false;
                Logger.Info($"Exit fullscreen: Change video screen from fullscreen to default stance {_lastWindowStance.State}");
            }
            _lastWindowStance = new WindowLastStance
            {
                Mode = window.ResizeMode,
                State = window.WindowState,
                Style = window.WindowStyle
            };
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Normal;
            window.WindowState = WindowState.Maximized;
            _fullscreen = true;
            Logger.Info($"Enter fullscreen: Change video screen to fullscreen from last stance {_lastWindowStance.State}");
        }
        else
        {
            Logger.Error("Fullscreen: Window is null");
        }
    }
}
