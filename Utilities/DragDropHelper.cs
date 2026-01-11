// Version: 0.1.7.80
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FFmpegApi.Utilities
{
    /*public static class DragDropHelper
    {
        public static readonly DependencyProperty IsDragOverProperty = DependencyProperty.RegisterAttached(
            "IsDragOver",
            typeof(bool),
            typeof(DragDropHelper),
            new PropertyMetadata(false));

        public static bool GetIsDragOver(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragOverProperty);
        }

        public static void SetIsDragOver(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragOverProperty, value);
        }
    }*/

    public static class DragDropHelper
    {
        public static readonly DependencyProperty IsDragOverProperty =
            DependencyProperty.RegisterAttached("IsDragOver", typeof(bool), typeof(DragDropHelper),
                new PropertyMetadata(false, OnIsDragOverChanged));

        public static bool GetIsDragOver(DependencyObject obj) => (bool)obj.GetValue(IsDragOverProperty);
        public static void SetIsDragOver(DependencyObject obj, bool value) => obj.SetValue(IsDragOverProperty, value);

        private static void OnIsDragOverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                // Opcjonalnie: Dodaj eventy DragEnter/DragLeave dla automatyzacji
                element.DragEnter += (s, args) => SetIsDragOver(d, true);
                element.DragLeave += (s, args) => SetIsDragOver(d, false);
                element.DragOver += (s, args) => { /* Obs�uga w C# PlaylistView */ };
            }
        }
    }
}
