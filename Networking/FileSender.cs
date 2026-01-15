using LanShare.Services; // Для AppSettings
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LanShare.Networking
{
    public static class FileSender
    {
        public static async Task Send(string filePath, string ip, TransferItem transfer, string myNickname, string myColor, byte[] myAvatar)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, 46000);
            using var stream = client.GetStream();
            var fileInfo = new FileInfo(filePath);

            // 1-5. Метаданные (код тот же)
            byte[] nickBytes = Encoding.UTF8.GetBytes(myNickname);
            await stream.WriteAsync(BitConverter.GetBytes(nickBytes.Length), 0, 4);
            await stream.WriteAsync(nickBytes, 0, nickBytes.Length);

            byte[] colorBytes = Encoding.UTF8.GetBytes(myColor);
            await stream.WriteAsync(BitConverter.GetBytes(colorBytes.Length), 0, 4);
            await stream.WriteAsync(colorBytes, 0, colorBytes.Length);

            int avatarLen = myAvatar?.Length ?? 0;
            await stream.WriteAsync(BitConverter.GetBytes(avatarLen), 0, 4);
            if (avatarLen > 0) await stream.WriteAsync(myAvatar, 0, avatarLen);

            byte[] nameBytes = Encoding.UTF8.GetBytes(fileInfo.Name);
            await stream.WriteAsync(BitConverter.GetBytes(nameBytes.Length), 0, 4);
            await stream.WriteAsync(nameBytes, 0, nameBytes.Length);

            await stream.WriteAsync(BitConverter.GetBytes(fileInfo.Length), 0, 8);

            // --- ОТПРАВКА ФАЙЛА С ОГРАНИЧЕНИЕМ ---
            using var fs = File.OpenRead(filePath);
            byte[] buffer = new byte[65536]; // 64 KB
            long totalRead = 0;
            int read;

            Stopwatch sw = Stopwatch.StartNew();
            Stopwatch throttleSw = Stopwatch.StartNew(); 
            long bytesSinceThrottle = 0;

            while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, read);
                totalRead += read;
                bytesSinceThrottle += read;

                // --- ЛОГИКА ОГРАНИЧЕНИЯ СКОРОСТИ ---
                int limitKbps = AppSettings.UploadLimitKBps;
                if (limitKbps > 0)
                {
                    // Сколько времени должно было пройти для передачи этих байтов
                    // (bytes * 1000) / (kbps * 1024) = ms
                    double expectedMs = (bytesSinceThrottle * 1000.0) / (limitKbps * 1024.0);
                    double elapsedMs = throttleSw.Elapsed.TotalMilliseconds;

                    if (elapsedMs < expectedMs)
                    {
                        int delay = (int)(expectedMs - elapsedMs);
                        if (delay > 0) await Task.Delay(delay);
                    }

                    // Сбрасываем таймер каждые ~1 МБ, чтобы не накапливать погрешности
                    if (bytesSinceThrottle > 1024 * 1024)
                    {
                        throttleSw.Restart();
                        bytesSinceThrottle = 0;
                    }
                }

                // Обновляем UI (старый код)
                transfer.Progress = (double)totalRead / fileInfo.Length * 100;

                if (sw.ElapsedMilliseconds > 700)
                {
                    double secondsElapsed = sw.Elapsed.TotalSeconds;
                    double bytesPerSecond = totalRead / (secondsElapsed + 0.001); // +0.001 защита от NaN

                    if (bytesPerSecond > 1024 * 1024)
                        transfer.SpeedDisplay = $"{(bytesPerSecond / 1024 / 1024):F1} MB/s";
                    else
                        transfer.SpeedDisplay = $"{(bytesPerSecond / 1024):F0} KB/s";

                    long bytesRemaining = fileInfo.Length - totalRead;
                    double secondsRemaining = bytesRemaining / (bytesPerSecond + 1);
                    transfer.TimeRemainingDisplay = "осталось: " + TransferItem.FormatTimeSpan(secondsRemaining);
                }
            }

            transfer.Status = "Завершено";
            transfer.SpeedDisplay = "";
            transfer.TimeRemainingDisplay = "";
        }
    }
}