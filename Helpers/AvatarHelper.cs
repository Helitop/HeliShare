using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace HeliShare.Helpers
{
    public static class AvatarHelper
    {
        public static byte[] GetCurrentUserAvatarBytes()
        {
            try
            {
                // Путь из твоего второго проекта (наиболее точный для Windows 10/11)
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "AccountPicture");

                if (!Directory.Exists(dir)) return null;

                // Ищем файлы user*.png или user*.jpg, берем самый большой (обычно лучший по качеству)
                var files = Directory.GetFiles(dir, "user*.png")
                    .Concat(Directory.GetFiles(dir, "user*.jpg"))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .ToList();

                if (!files.Any()) return null;

                string avatarPath = files.First();

                // Сжимаем изображение для сети (48x48 или 64x64 вполне достаточно)
                return ResizeAndCompress(avatarPath, 64);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ResizeAndCompress(string path, int size)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.DecodePixelWidth = 48; // 48x48 оптимально для иконок в списке
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();

                    var encoder = new JpegBitmapEncoder { QualityLevel = 60 }; // Чуть ниже качество для экономии трафика
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using (var ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }
    }
}