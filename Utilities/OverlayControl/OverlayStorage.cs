// Version: 0.0.0.3
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Media;

namespace FFmpegApi.Utilities.OverlayControl
{
    public static class OverlayStorage
    {
        private static readonly string FilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyApp",
                "overlay.json");

        public static async Task SaveAsync(OverlayLayout layout)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true });
            //await File.WriteAllTextAsync(FilePath, json);
        }

        //public static async Task<OverlayLayout?> LoadAsync()
        //{
        //    if (!File.Exists(FilePath))
        //        return null;

        //    var json = await File.ReadAllTextAsync(FilePath);
        //    return JsonSerializer.Deserialize<OverlayLayout>(json);
        //}
    }
}
