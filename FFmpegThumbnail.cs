// Version: 0.0.0.2
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;

using FFmpegApi.Logs;
using FFmpegApi.Views;

using Media;

using MediaToolkit.Options;

namespace FFmpegApi
{
    public class FFmpegThumbnail : FFmpegApi.VideoPlayerControl
    {
        private CancellationTokenSource _thumbCts;
        private bool _isThumbnailLoading;
        private MediaToolkit.Engine _engine;
        private MediaToolkit.Model.MediaFile _media;
        private string _tempPath;

        #region === Tworzenie Thumbnail ===

        public async Task<Image> GetThumbnailAsync(TimeSpan time)
        {
            _thumbCts?.Cancel();
            _thumbCts = new CancellationTokenSource();
            var token = _thumbCts.Token;

            _tempPath = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid():N}.png");

            try
            {
                _engine = new MediaToolkit.Engine("ffmpeg");
                await Task.Run(() =>
                {                    
                    token.ThrowIfCancellationRequested();

                    var options = new ConversionOptions
                    {
                        Seek = time,
                        CustomWidth = 320,
                        CustomHeight = 180
                    };
                    //MediaFile video = MediaFile.Open(base.Playlist.Current.Uri.AbsoluteUri);
                    _media = new MediaToolkit.Model.MediaFile(base.Playlist.Current.Uri.AbsoluteUri);

                    _engine.GetThumbnail(_media,new MediaToolkit.Model.MediaFile(_tempPath),options);
                    //var frame = video.Video.GetFrame(time);
                    var bitmap = Bitmap.FromFile(_tempPath);

                    //bitmap.Save("thumb.jpg");
                }, token).ConfigureAwait(false);

                    using var bmp = Image.FromFile(_tempPath);
                return bmp;
            }
            catch
            {
                return null;
            }
            finally
            {
                try { File.Delete(_tempPath); }
                catch { }
            }
        }

        private async Task UpdateThumbnailAsync(TimeSpan time)
        {
            if (base.Playlist?.Current == null || _isThumbnailLoading) return;

            _isThumbnailLoading = true;

            await Task.Run(async () =>
            {
                var bmp = await GetThumbnailAsync(time);
                if (bmp != null)
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        //base.ProgressBar._popupImage.Source =
                    });
                }
            });

            _isThumbnailLoading = false;
        }

        #endregion
    }
}
