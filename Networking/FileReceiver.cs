using HeliShare.Views.Windows;
using LanShare.Services; // Для AppSettings
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Diagnostics; // Для Stopwatch

namespace LanShare.Networking
{
    public class FileReceiver
    {
        private const int Port = 46000;
        private TcpListener _listener;

        // Убираем аргумент savePath из Start, будем брать актуальный из настроек
        public void Start(Action<TransferItem> onNewTransfer)
        {
            Task.Run(async () =>
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Any, Port);
                    _listener.Start();

                    while (true)
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = HandleClient(client, onNewTransfer);
                    }
                }
                catch { }
            });
        }

        private async Task<byte[]> ReadExactly(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset);
                if (read == 0) throw new EndOfStreamException("Соединение разорвано");
                offset += read;
            }
            return buffer;
        }

        private async Task HandleClient(TcpClient client, Action<TransferItem> onNewTransfer)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // 1-5. Чтение метаданных (без изменений)
                    int nickLen = BitConverter.ToInt32(await ReadExactly(stream, 4), 0);
                    string senderNickname = Encoding.UTF8.GetString(await ReadExactly(stream, nickLen));

                    int colorLen = BitConverter.ToInt32(await ReadExactly(stream, 4), 0);
                    string senderColor = Encoding.UTF8.GetString(await ReadExactly(stream, colorLen));

                    int avatarLen = BitConverter.ToInt32(await ReadExactly(stream, 4), 0);
                    byte[] avatarBytes = avatarLen > 0 ? await ReadExactly(stream, avatarLen) : null;

                    int nameLen = BitConverter.ToInt32(await ReadExactly(stream, 4), 0);
                    string rawFileName = Encoding.UTF8.GetString(await ReadExactly(stream, nameLen));
                    string safeFileName = Path.GetFileName(rawFileName);

                    long fileSize = BitConverter.ToInt64(await ReadExactly(stream, 8), 0);
                    string senderIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    var transfer = new TransferItem
                    {
                        FileName = safeFileName,
                        TotalBytes = fileSize,
                        Date = DateTime.Now.ToString("HH:mm"),
                        Status = "Ожидание...",
                        ClientName = senderIp,
                        ClientNickname = senderNickname,
                        AccentColorHex = senderColor,
                        ClientAvatar = avatarBytes,
                        PeerIdentifier = senderIp
                    };

                    Application.Current.Dispatcher.Invoke(() => onNewTransfer?.Invoke(transfer));

                    bool accepted = await NotificationCoordinator.WaitForUserConfirmation(safeFileName, senderNickname);
                    if (!accepted)
                    {
                        transfer.Status = "Отклонено";
                        return;
                    }

                    // --- БЕРЕМ ПУТЬ ИЗ НАСТРОЕК ---
                    string savePath = AppSettings.SavePath;
                    // Если папка была удалена юзером, создаем заново или кидаем в Загрузки по умолчанию
                    if (!Directory.Exists(savePath)) 
                    {
                        try { Directory.CreateDirectory(savePath); }
                        catch { savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); }
                    }

                    string fullPath = Path.Combine(savePath, safeFileName);
                    int counter = 1;
                    while (File.Exists(fullPath))
                    {
                        string fileNameOnly = Path.GetFileNameWithoutExtension(safeFileName);
                        string extension = Path.GetExtension(safeFileName);
                        fullPath = Path.Combine(savePath, $"{fileNameOnly} ({counter++}){extension}");
                    }

                    transfer.Status = "Получение...";

                    long totalRead = 0;
                    Stopwatch throttleSw = Stopwatch.StartNew();
                    long bytesSinceThrottle = 0;

                    using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    {
                        var buffer = new byte[65536];
                        while (totalRead < fileSize)
                        {
                            int toRead = (int)Math.Min(buffer.Length, fileSize - totalRead);
                            int read = await stream.ReadAsync(buffer, 0, toRead);
                            if (read == 0) break;

                            // --- ТРОТТЛИНГ ПРИЕМА ---
                            int limitKbps = AppSettings.DownloadLimitKBps;
                            if (limitKbps > 0)
                            {
                                bytesSinceThrottle += read;
                                double expectedMs = (bytesSinceThrottle * 1000.0) / (limitKbps * 1024.0);
                                double elapsedMs = throttleSw.Elapsed.TotalMilliseconds;

                                if (elapsedMs < expectedMs)
                                {
                                    int delay = (int)(expectedMs - elapsedMs);
                                    if (delay > 0) await Task.Delay(delay);
                                }

                                if (bytesSinceThrottle > 1024 * 1024)
                                {
                                    throttleSw.Restart();
                                    bytesSinceThrottle = 0;
                                }
                            }
                            // -----------------------

                            await fs.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            transfer.Progress = (double)totalRead / fileSize * 100;
                        }
                    }
                    transfer.LocalPath = fullPath;
                    transfer.Status = (totalRead == fileSize) ? "Получен" : "Прервано";
                }
            }
            catch { }
        }
    }
}