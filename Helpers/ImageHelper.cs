// Файл: Helpers/ImageHelper.cs
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HeliShare.Helpers
{
    public static class ImageHelper
    {
        public static ImageSource BytesToImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        // ДОБАВЬТЕ ЭТОТ МЕТОД:
        public static Color HexToColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex ?? "#808080");
            }
            catch
            {
                return Colors.Gray;
            }
        }
    }
}