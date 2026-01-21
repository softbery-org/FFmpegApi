// Version: 0.0.0.7
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using Media;

using FFmpegApi.Logs;
using FFmpegApi.Views.Effects;
using FFmpegApi.Utilities;

namespace FFmpegApi.Views
{
    public partial class Playlist : ListView, INotifyPropertyChanged
    {
        private IPlayer _player;
        private MediaItem _next;
        private MediaItem _previous;
        private MediaItem _current;
        private int _currentIndex;

        private ScrollViewer _scrollViewer;
        private DispatcherTimer _scrollTimer;
        private double _scrollVelocity = 0;
        private const double _scrollZone = 60.0;
        private const double _maxScrollSpeed = 18.0;
        private const double _scrollDamping = 0.85;

        private DispatcherTimer _autoScrollTimer;
        private double _autoScrollVelocity = 0;
        private const double AutoScrollZone = 120.0;
        private const double MaxAutoScrollSpeed = 3.0;

        private bool _isDragging;
        private object _draggedItem;
        private AdornerLayer _adornerLayer;

        private Point _startPoint;
        private ScrollIndicatorAdorner _topIndicator;
        private ScrollIndicatorAdorner _bottomIndicator;
        private ListViewItem _draggedContainer;
        private bool _isMouseSubscribed;
        private int _draggedIndex;
        private DragShadowAdorner _dragAdorner;

        public Playlist()
        {
            InitializeComponent();
            DataContext = this;

            Videos = new ObservableCollection<MediaItem>();

            AllowDrop = true;
            PreviewDragOver += Playlist_PreviewDragOver;
            Drop += Playlist_Drop;

            InitCommands();
            CreateContextMenu();

            _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        }

        #region Context Menu Setup
        private void CreateContextMenu()
        {
            var rightClickMenu = new ContextMenu();

            MenuItem playItem = new MenuItem { Header = "Play" };
            MenuItem playNext = new MenuItem { Header = "Play As Next" };
            MenuItem removeItem = new MenuItem { Header = "Remove" };
            MenuItem moveUpItem = new MenuItem { Header = "Move Up" };
            MenuItem moveDownItem = new MenuItem { Header = "Move Down" };
            MenuItem moveTopItem = new MenuItem { Header = "Move To Top" };
            MenuItem moveEndItem = new MenuItem { Header = "Move To End" };

            playItem.Click += MenuItemPlay_Click;
            playNext.Click += MenuItemPlayNext_Click;
            removeItem.Click += MenuItemRemove_Click;
            moveUpItem.Click += MenuItemMoveUpper_Click;
            moveDownItem.Click += MenuItemMoveLower_Click;
            moveTopItem.Click += MenuItemMoveToTop_Click;
            moveEndItem.Click += MenuItemMoveToEnd_Click;

            rightClickMenu.Items.Add(playItem);
            rightClickMenu.Items.Add(playNext);
            rightClickMenu.Items.Add(removeItem);
            rightClickMenu.Items.Add(new Separator());
            rightClickMenu.Items.Add(moveUpItem);
            rightClickMenu.Items.Add(moveDownItem);
            rightClickMenu.Items.Add(new Separator());
            rightClickMenu.Items.Add(moveTopItem);
            rightClickMenu.Items.Add(moveEndItem);

            ContextMenu = rightClickMenu;
        }

        private void MenuItemPlay_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                PlayVideo(video);
                CurrentIndex = Videos.IndexOf(video);
                this.WriteLine($"Context menu play {video.Name}");
            }
        }

        private void MenuItemPlayNext_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                this.PlayNext = video;
            }
        }

        private void MenuItemRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
                Videos.Remove(video);
        }

        private void MenuItemMoveUpper_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                int index = Videos.IndexOf(video);
                if (index > 0)
                    Videos.Move(index, index - 1);
            }
        }

        private void MenuItemMoveLower_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                int index = Videos.IndexOf(video);
                if (index < Videos.Count - 1)
                    Videos.Move(index, index + 1);
            }
        }

        private void MenuItemMoveToTop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                int index = Videos.IndexOf(video);
                if (index > 0)
                    Videos.Move(index, 0);
            }
        }

        private void MenuItemMoveToEnd_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                int index = Videos.IndexOf(video);
                if (index > 0)
                    Videos.Move(index, Videos.Count);
            }
        }

        #endregion

        #region Commands
        private void InitCommands()
        {
            AddCommand = new RelayCommand(async _ => await AddCommandExecuteAsync(), _ => true);
            RemoveCommand = new RelayCommand(async _ => await RemoveCommandExecuteAsync(), _ => Count > 0);
            EditCommand = new RelayCommand(_ => Edit(null), _ => Count > 0);
            CloseCommand = new RelayCommand(_ => Close(null), _ => true);
            SavePlaylistCommand = new RelayCommand(_ => Save(null), _ => true);
            LoadPlaylistCommand = new RelayCommand(_ => Load(null), _ => true);
            ClearPlaylistCommand = new RelayCommand(async _ => await ClearTracksAsync(), _ => true);
            MoveUpCommand = new RelayCommand(_ => MoveUp(), _ => CanMoveUp());
            MoveDownCommand = new RelayCommand(_ => MoveDown(), _ => CanMoveDown());
            ShuffleCommand = new RelayCommand(_ => Shuffle(), _ => Count > 1);
            SortByNameCommand = new RelayCommand(_ => SortByName(), _ => Count > 1);
        }

        private bool CanMoveUp() => SelectedItem is MediaItem m && Videos.IndexOf(m) > 0;
        private bool CanMoveDown() => SelectedItem is MediaItem m && Videos.IndexOf(m) < Videos.Count - 1;
        #endregion

        #region === DATA ===

        public ObservableCollection<MediaItem> Videos
        {
            get => (ObservableCollection<MediaItem>)GetValue(VideosProperty);
            set => SetValue(VideosProperty, value);
        }

        public static readonly DependencyProperty VideosProperty =
            DependencyProperty.Register(nameof(Videos),
                typeof(ObservableCollection<MediaItem>),
                typeof(Playlist));

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<EventArgs> PlaylistChanged;

        public MediaItem PlayNext { get; set; }

        public MediaItem Current
        {
            get => _current;
            set
            {
                _current = value;

                OnPropertyChanged(nameof(Current));
            }
        }
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;

                    UpdateNavigation();
                    OnPropertyChanged(nameof(CurrentIndex));
                }
            }
        }
        public MediaItem Next
        {
            get => _next;
            private set { _next = value; OnPropertyChanged(); }
        }
        public MediaItem Previous
        {
            get => _previous;
            private set { _previous = value; OnPropertyChanged(nameof(Previous)); }
        }
        public int Count => Videos.Count;
        #endregion

        #region Commands

        // Commands
        public ICommand AddCommand { get; private set; }
        public ICommand RemoveCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand SavePlaylistCommand { get; private set; }
        public ICommand LoadPlaylistCommand { get; private set; }
        public ICommand ClearPlaylistCommand { get; private set; }
        public ICommand MoveUpCommand { get; private set; }
        public ICommand MoveDownCommand { get; private set; }
        public ICommand ShuffleCommand { get; private set; }
        public ICommand SortByNameCommand { get; private set; }

        #endregion

        #region Commands executes
        
        private async Task AddCommandExecuteAsync()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Media File",
                Filter = "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.mp3;*.wav;*.flac;*.ts;*.m3u8;*.hlsarc|All Files|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
                foreach (var f in ofd.FileNames)
                    await AddAsync(new MediaItem(f));
        }

        private async Task RemoveCommandExecuteAsync()
        {
            if (SelectedItem is MediaItem m)
            {
                if (m == Current) try { _player.Preview(); _player.Play(); } catch { }
                await RemoveAsync(m);
            }
            else TrySetInfoBoxText("No item selected.");
        }

        #endregion

        #region Add / Remove
        public async Task AddAsync(MediaItem media)
        {
            if (media == null || Contains(media)) return;

            TrySetPlayerOnItem(media);

            await TryLoadMetadataAsync(media);

            await Dispatcher.InvokeAsync(() =>
            {
                Videos.Add(media);
                if (CurrentIndex < 0 && Videos.Count == 1) CurrentIndex = 0;
                TrySetInfoBoxText($"Added: {media.Name}");
            });
        }

        public async Task AddAsync(MediaItem[] medias)
        {
            if (medias == null) return;
            foreach (var m in medias) await AddAsync(m);
        }

        public async Task<MediaItem> RemoveAsync(MediaItem media)
        {
            if (media == null) return null;

            int index = Videos.IndexOf(media);
            if (index < 0) return null;

            await Dispatcher.InvokeAsync(() =>
            {
                Videos.RemoveAt(index);
                if (index < CurrentIndex) CurrentIndex--;
                else if (index == CurrentIndex && Videos.Count > 0)
                    CurrentIndex = System.Math.Min(CurrentIndex, Videos.Count - 1);
                else if (Videos.Count == 0) CurrentIndex = -1;
                UpdateNavigation();
                TrySetInfoBoxText($"Removed: {media.Name}");
            });

            return media;
        }

        public void RemoveSelected() => _ = RemoveAsync(SelectedItem as MediaItem);

        public Task ClearTracksAsync() => Task.Run(() =>
        {
            Dispatcher.Invoke(() =>
            {
                Videos.Clear();
                CurrentIndex = -1;
                UpdateNavigation();
                TrySetInfoBoxText("Playlist cleared.");
            });
        });

        #endregion

        #region Buttons

        private void Edit(object p = null)
        {
            // placeholder – np. rename / metadata
            MessageBox.Show("Edit item (todo)");
        }

        private void Close(object p = null)
        {
            Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Save/Load
        public void Save(object p = null)
        {
            //var playlistConfig = Config.Instance.PlaylistConfig;

            // Czyścimy i wypełniamy listy
            //playlistConfig.MediaList.Clear();
            //playlistConfig.Subtitles.Clear();

            //foreach (var item in Videos)
            //{
            //    playlistConfig.MediaList.Add(item.Uri?.OriginalString ?? string.Empty);
            //    playlistConfig.Subtitles.Add(item.SubtitlePath ?? string.Empty);
            //}

            //// Poprawne przypisanie indeksu, nie obiektu MediaItem
            //playlistConfig.Current = CurrentIndex;

            //// Rozmiar i pozycja okna playlisty
            //playlistConfig.Width = ActualWidth;
            //playlistConfig.Height = ActualHeight;
            //playlistConfig.Left = Margin.Left;
            //playlistConfig.Top = Margin.Top;

            //// Zapisujemy TYLKO PlaylistConfig do właściwego pliku
            //Config.SaveConfig(Config.PlaylistConfigPath, playlistConfig);
            //Config.Instance.GetType().GetProperty(nameof(Config.PlaylistConfig)).SetValue(Config.Instance, playlistConfig);
            //Config.Instance.Save();

            //Logger.Info($"Playlist saved: {Videos.Count} items to {Config.PlaylistConfigPath}");
            TrySetInfoBoxText("Playlist saved.");
        }

        public async void Load(object p = null)
        {
            //var playlistConfig = Config.Instance.PlaylistConfig;
            
            //// Przywrócenie rozmiaru i pozycji okna playlisty
            //if (playlistConfig.Width > 0 && playlistConfig.Height > 0)
            //{
            //    Width = playlistConfig.Width;
            //    Height = playlistConfig.Height;
            //}

            //await ClearTracksAsync();

            //for (int i = 0; i < playlistConfig.MediaList.Count; i++)
            //{
            //    string path = playlistConfig.MediaList[i];
            //    if (string.IsNullOrWhiteSpace(path)) continue;

            //    var item = new MediaItem(path, true);
            //    if (i < playlistConfig.Subtitles.Count)
            //        item.SubtitlePath = playlistConfig.Subtitles[i];

            //    await AddAsync(item);
            //}

            
            //// Poprawne wczytanie indeksu
            //if (playlistConfig.Current >= 0 && playlistConfig.Current < Videos.Count)
            //{
            //    CurrentIndex = playlistConfig.Current;
            //    SelectedIndex = CurrentIndex;
            //}

            //Margin = new Thickness(playlistConfig.Left, playlistConfig.Top, 0, 0);

            Logger.Info("Playlist loaded successfully.");
            TrySetInfoBoxText("Playlist loaded.");
        }

        #endregion

        #region Navigation
        private void UpdateNavigation()
        {
            if (Videos.Count == 0 || CurrentIndex < 0)
            {
                Current = Next = Previous = null;
                return;
            }

            if (CurrentIndex > Videos.Count-1)
                CurrentIndex = 0;
            if (CurrentIndex < 0)
                CurrentIndex = Videos.Count - 1;

            Current = Videos[CurrentIndex];

            Next = CurrentIndex + 1 < Videos.Count ? Videos[CurrentIndex + 1] : Videos.First();

            Previous = CurrentIndex > 0 ? Videos[CurrentIndex - 1] : Videos.Last();
        }
        #endregion

        #region Sorting / Move
        public void SortByName()
        {
            var sorted = Videos.OrderBy(v => v?.Name ?? "").ToList();
            RebuildCollection(sorted, "Playlist sorted by name.");
        }

        public void Shuffle()
        {
            var rnd = new Random();
            var list = Videos.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            RebuildCollection(list, "Playlist shuffled.");
        }

        private void RebuildCollection(List<MediaItem> newList, string message)
        {
            var current = Current;
            Dispatcher.Invoke(() =>
            {
                Videos.Clear();
                foreach (var v in newList) Videos.Add(v);
                CurrentIndex = current != null ? System.Math.Max(0, Videos.IndexOf(current)) : (Videos.Count > 0 ? 0 : -1);
                UpdateNavigation();
                TrySetInfoBoxText(message);
            });
        }

        public void MoveUp() => MoveItem(-1);
        public void MoveDown() => MoveItem(1);

        private void MoveItem(int direction)
        {
            if (SelectedItem is not MediaItem sel) return;
            int idx = Videos.IndexOf(sel);
            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= Videos.Count) return;

            Dispatcher.Invoke(() =>
            {
                Videos.Move(idx, newIdx);
                SelectedItem = sel;
                if (CurrentIndex == idx) CurrentIndex = newIdx;
                else if (CurrentIndex == newIdx) CurrentIndex = idx;
                UpdateNavigation();
            });
        }
        #endregion

        #region === DRAG & DROP ===

        private void Playlist_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void Playlist_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
                Videos.Add(MediaItem.FromFile(file));
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            _startPoint = e.GetPosition(this);
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            if (SelectedItem is MediaItem video)
            {
                _player?.Stop();
                PlayVideo(video);
                CurrentIndex = Videos.IndexOf(video);
                this.WriteLine($"Double click on {video.Name}");
                TrySetInfoBoxText($"Play {video.Name}.");
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point pos = e.GetPosition(this);
                Vector diff = _startPoint - pos;

                //ControlControllerHelper.SaveConfigElement<PlaylistConfig>(this);

                if (System.Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Sprawdzanie, czy mysz znajduje się nad elementem listy
                    var sourceElement = e.OriginalSource as DependencyObject;
                    var listViewItem = FindAncestor<ListViewItem>(sourceElement);

                    if (listViewItem == null)
                        return;

                    if (SelectedItem is MediaItem item)
                    {
                        _isDragging = true;
                        _draggedItem = item;
                        _draggedIndex = Items.IndexOf(item);
                        StartDrag(item, e);
                    }
                }
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _isDragging)
            {
                _isDragging = false;
            }
            _draggedItem = null;
            _draggedIndex = -1;

            EndDrag();
        }

        private List<string> CreateMediaList()
        {
            var list = new List<string>();
            foreach (var video in Videos)
            {
                list.Add(video.Name);
            }
            return list;
        }

        // === StartDrag – subskrypcja MouseMove ===
        private void StartDrag(MediaItem item)
        {
            _adornerLayer = AdornerLayer.GetAdornerLayer(this);
            if (_adornerLayer == null) return;

            var container = ItemContainerGenerator.ContainerFromItem(item) as UIElement;
            if (container == null)
            {
                ScrollIntoView(item);
                UpdateLayout();
                container = ItemContainerGenerator.ContainerFromItem(item) as UIElement;
            }

            if (container != null)
            {
                _dragAdorner = new DragShadowAdorner(this, container);
                _adornerLayer.Add(_dragAdorner);
            }

            // KLUCZOWE: globalny MouseMove
            this.MouseMove += OnDragMouseMove;

            var data = new DataObject(typeof(MediaItem), item);
            DragDrop.DoDragDrop(this, data, DragDropEffects.Move);

            EndDrag();
        }

        private void StartDrag(MediaItem item, MouseEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => StartDrag(item, e));
                return;
            }

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                this.WriteLine(new InvalidOperationException("DragDrop need STA thread."));
            }

            _adornerLayer = AdornerLayer.GetAdornerLayer(this);
            if (_adornerLayer == null) return;

            var draggedContainer = ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
            if (draggedContainer == null)
            {
                ScrollIntoView(item);
                UpdateLayout();
                draggedContainer = ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                if (draggedContainer == null) return;
            }

            _draggedContainer = draggedContainer;

            var dataObject = new DataObject();
            dataObject.SetData(typeof(MediaItem), item);

            try
            {
                Dispatcher.Invoke(() =>
                {
                    DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Move);
                });
            }
            catch (NullReferenceException ex) when (ex.Message.Contains("OleDoDragDrop"))
            {
                this.WriteLine($"Drag-drop crash: {ex}");
            }
            finally
            {
                StopAutoScroll();
                EndDrag();
            }
        }

        //private readonly DebouncedSaveQueue<Playlist> _saveQueue = new();

        // === EndDrag – cleanup ===
        private async void EndDrag()
        {
            this.MouseMove -= OnDragMouseMove;
            _autoScrollTimer.Stop();
            _autoScrollVelocity = 0;

            if (_dragAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
            }

            _isDragging = false;
            _draggedItem = null;

            //await _saveQueue.SaveAsync(new List<Playlist>(), SaveToDiskAsync);
        }

        //private async Task SaveToDiskAsync(IReadOnlyList<Playlist> items)
        //{
        //    //await Task.Delay(500); // symulacja zapisu
        //    //File.WriteAllText("playlist.json", JsonSerializer.Serialize(items));
        //    Save();
        //}

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);

            if (_dragAdorner != null && _draggedContainer != null)
            {
                Point position = e.GetPosition(this);
                double offsetX = position.X - (_draggedContainer.ActualWidth / 2);
                double offsetY = position.Y - (_draggedContainer.ActualHeight / 2);
                _dragAdorner.Offset = new Point(offsetX, offsetY);
                _dragAdorner.InvalidateArrange();
                _dragAdorner.InvalidateVisual();
            }

            int targetIndex = GetIndexAtPosition(e.GetPosition(this));
            if (targetIndex >= 0)
            {
                var targetItem = ItemContainerGenerator.ContainerFromIndex(targetIndex) as DependencyObject;
                DragDropHelper.SetIsDragOver(targetItem, true);
            }

            CalculateAutoScroll(e.GetPosition(this));
        }


        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            StopAutoScroll();
            if (e.Data.GetDataPresent(typeof(MediaItem)))
            {
                if (e.Data.GetData(typeof(MediaItem)) is MediaItem droppedItem)
                {
                    int oldIndex = Videos.IndexOf(droppedItem);
                    Point dropPosition = e.GetPosition(this);
                    int newIndex = GetIndexAtPosition(dropPosition);

                    if (newIndex < 0) newIndex = Videos.Count - 1;
                    if (newIndex != oldIndex)
                    {
                        Videos.Move(oldIndex, newIndex);
                        CurrentIndex = newIndex;
                    }
                }
            }

            foreach (var container in ItemContainerGenerator.Items)
            {
                var itemContainer = ItemContainerGenerator.ContainerFromItem(container) as DependencyObject;
                if (itemContainer != null)
                {
                    Utilities.DragDropHelper.SetIsDragOver(itemContainer, false);
                }
            }

            EndDrag();
        }

        // === Globalny MouseMove podczas drag (subskrybuj w StartDrag) ===
        private void OnDragMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _dragAdorner == null) return;

            Point mousePos = e.GetPosition(this);

            // Aktualizacja pozycji cienia (centrowanie pod kursorem)
            _dragAdorner.UpdatePosition(mousePos);

            // Auto-scroll – działa nawet gdy mysz jest poza ListView (dzięki RelativeTo this)
            CalculateAutoScroll(mousePos);
        }

        #endregion

        #region AutoScroll Logic

        private void CalculateAutoScroll(Point mousePos)
        {
            if (_scrollViewer == null)
            {
                _scrollViewer = FindScrollViewer(this);
                if (_scrollViewer == null) return;
            }

            double distanceFromTop = mousePos.Y - BorderThickness.Top;
            double distanceFromBottom = ActualHeight - mousePos.Y;

            this.WriteLine("Distance from top" + mousePos.Y);

            if (distanceFromTop < AutoScrollZone)
            {
                // Blisko góry → scroll w górę
                _autoScrollVelocity = -MaxAutoScrollSpeed * (1.0 - distanceFromTop / AutoScrollZone);
                if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
            }
            else if (distanceFromBottom < AutoScrollZone)
            {
                // Blisko dołu → scroll w dół
                _autoScrollVelocity = MaxAutoScrollSpeed * (1.0 - distanceFromBottom / AutoScrollZone);
                if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
            }
            else
            {
                // Poza strefą → zwolnij
                _autoScrollVelocity = 0;
                _autoScrollTimer.Stop();
            }
        }

        // === Timer – płynny scroll ===
        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_scrollViewer == null || System.Math.Abs(_autoScrollVelocity) < 0.5)
            {
                _autoScrollVelocity = 0;
                _autoScrollTimer.Stop();
                return;
            }

            double newOffset = _scrollViewer.VerticalOffset + _autoScrollVelocity;

            // Ogranicz do granic
            newOffset = System.Math.Max(0, System.Math.Min(newOffset, _scrollViewer.ScrollableHeight));

            _scrollViewer.ScrollToVerticalOffset(newOffset);

            // Lekkie tłumienie dla płynności
            _autoScrollVelocity *= 0.92;
        }

        private void StopAutoScroll()
        {
            _scrollTimer?.Stop();
            _scrollVelocity = 0;
        }

        private ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        #endregion


        #region === FADE ===

        public async void FadeIn()
        {
            ((Storyboard)Resources["fadeInControl"]).Begin(this);
            //await this.ShowByStoryboard((Storyboard)this.FindResource("fadeInControl"));
        }

        public async void FadeOut()
        {
            ((Storyboard)Resources["fadeOutControl"]).Begin(this);
            //await this.ShowByStoryboard((Storyboard)this.FindResource("fadeOutControl"));
        }

        #endregion

        public void SetPlayer(IPlayer player)
        {
            _player = player;
        }

        #region Helper methods

        private void PlayVideo(MediaItem video)
        {
            if (video != null && _player != null)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    _player?.Play(video);
                }));
            }
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target)
                    return target;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void OnGlobalMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _dragAdorner != null && _draggedContainer != null)
            {
                // Fix: Pobierz pozycję globalnie via Mouse.GetPosition(this) – dokładna dla ListView
                Point position = e.GetPosition(this);

                // Centrowanie: Odejmij środek kontenera dla "podążania" za myszą
                double offsetX = position.X - (_draggedContainer.ActualWidth / 2);
                double offsetY = position.Y - (_draggedContainer.ActualHeight / 2);

                _dragAdorner.Offset = new Point(offsetX / 2, offsetY / 2);

                // Fix: Wymuś redraw dla natychmiastowego efektu (płynność w .NET 4.8)
                _dragAdorner.InvalidateArrange();
                _dragAdorner.InvalidateVisual();
            }
        }

        private int GetIndexAtPosition(Point position)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var item = (ListViewItem)ItemContainerGenerator.ContainerFromIndex(i);
                if (item == null) continue;

                Rect bounds = VisualTreeHelper.GetDescendantBounds(item);
                Point topLeft = item.TranslatePoint(new Point(0, 0), this);

                if (position.Y >= topLeft.Y && position.Y <= topLeft.Y + bounds.Height)
                    return i;
            }
            return -1;
        }

        private void ShowIndicator(bool top)
        {
            if (_adornerLayer == null)
                _adornerLayer = AdornerLayer.GetAdornerLayer(this);
            if (_adornerLayer == null)
                return;

            if (top)
            {
                if (_topIndicator == null)
                {
                    _topIndicator = new ScrollIndicatorAdorner(this, true);
                    _adornerLayer.Add(_topIndicator);
                }
                _topIndicator.FadeIn();
            }
            else
            {
                if (_bottomIndicator == null)
                {
                    _bottomIndicator = new ScrollIndicatorAdorner(this, false);
                    _adornerLayer.Add(_bottomIndicator);
                }
                _bottomIndicator.FadeIn();
            }
        }

        private void HideIndicator(bool top)
        {
            if (top)
            {
                _topIndicator?.FadeOut();
            }
            else
            {
                _bottomIndicator?.FadeOut();
            }
        }

        private void OnPlayerChanged(object sender, EventArgs e)
        {
            PlaylistChanged?.Invoke(this, e);
        }

        private async Task TryLoadMetadataAsync(MediaItem item) { try { await item.LoadMetadataAsync(); } catch { } }

        private void TrySetPlayerOnItem(MediaItem item)
        {
            try { item?.GetType().GetMethod("SetPlayer")?.Invoke(item, new object[] { _player }); }
            catch { }
        }

        private void TrySetInfoBoxText(string text)
        {
            try { _player?.InfoBox.DrawText = text; } catch { }
        }

        public bool Contains(MediaItem media) => media != null && Videos.Any(v => UriEquals(v?.Uri, media.Uri));

        private static bool UriEquals(Uri a, Uri b) => a != null && b != null && (a == b || string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase));

        private void Video_PositionChanged(object sender, double pos)
        {
            if (sender is MediaItem video)
            {
                TimeSpan position = TimeSpan.FromSeconds(pos);

                video.Position = position.TotalMilliseconds;

                this.WriteLine($"PlaylistView: Position updated {video.Name} -> {position}");
            }
        }

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion

    }
}
