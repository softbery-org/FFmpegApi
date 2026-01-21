// Version: 0.0.0.10
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FFmpegApi.Views
{
    /// <summary>
    /// Logika interakcji dla klasy KeyboardShortcutView.xaml
    /// </summary>
    public partial class KeyboardShortcutsView : UserControl
    {
        public static readonly DependencyProperty CloseCommandProperty =
            DependencyProperty.Register("CloseCommand", typeof(ICommand), typeof(KeyboardShortcutsView));

        public ICommand CloseCommand
        {
            get => (ICommand)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public KeyboardShortcutsView()
        {
            InitializeComponent();

            InitializeCommands();
            InitializeEvents();
        }

        private void InitializeEvents()
        {
            this.Loaded += (s, e) =>
            {
                Keyboard.Focus((IInputElement)this.Parent);
            };

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close(null);
                }
            };
        }

        private void InitializeCommands()
        {
            
        }

        private void Close(object parameter)
        {
            this.Visibility = Visibility.Collapsed;
        }

        #region === FADE ANIMATION ===

        public void FadeOut()
        {
            ((Storyboard)Resources["fadeOutControl"]).Begin();
        }

        public void FadeIn()
        {
            ((Storyboard)Resources["fadeInControl"]).Begin();
        }

        #endregion
    }
}
