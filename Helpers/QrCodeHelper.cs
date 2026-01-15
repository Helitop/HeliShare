using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace HeliShare.Helpers
{
    public static class QrCodeHelper
    {
        public static BitmapSource GenerateQrCode(string text)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            // ECCLevel.H (30%) — критически важно, так как в XAML поверх наложен логотип
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.H))
            using (QRCode qrCode = new QRCode(qrCodeData))
            {
                // 1. Получаем актуальный акцентный цвет из WPF UI
                var wpfAccent = ApplicationAccentColorManager.PrimaryAccent;
                
                // Конвертируем в System.Drawing.Color
                Color darkColor = Color.FromArgb(wpfAccent.R, wpfAccent.G, wpfAccent.B);

                // 2. Генерируем стандартный, четкий QR-код
                // Параметры: (размер, цвет точек, цвет фона, рисовать ли рамку)
                // Color.Transparent позволяет коду идеально лечь на Mica-эффект окна
                using (Bitmap qrBitmap = qrCode.GetGraphic(20, darkColor, Color.Transparent, true))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        qrBitmap.Save(ms, ImageFormat.Png);
                        ms.Position = 0;

                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
            }
        }
    }
}