using LanShare.Services; 
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LanShare.Networking
{
    public class WebShareServer
    {
        public event Func<string, string, long, Task<bool>> OnUploadConfirmationRequired;
        public event Action<TransferItem> OnTransferStarted;

        private string _senderNickname;
        private string _senderColor;
        private byte[] _avatarBytes;
        private HttpListener _listener;
        private string _filePath; 
        private int _port = 46001;
        private TransferItem _currentOutgoingTransfer;

        // --- ЛОГГЕР ---
        private void Log(string message)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "heli_server_log.txt");
                // Ограничим размер лога, чтобы он не раздувался
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 1024 * 1024) 
                    File.WriteAllText(logPath, ""); // Очистка если > 1МБ
                    
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        public void Start(string localIp, string nickname, string color, byte[] avatar)
        {
            _senderNickname = nickname;
            _senderColor = color;
            _avatarBytes = avatar;

            Stop();

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{_port}/");
                _listener.IgnoreWriteExceptions = true; 
                _listener.Start();
                Log($"СЕРВЕР ЗАПУЩЕН на порту {_port}");
            }
            catch (Exception ex)
            {
                Log($"КРИТИЧЕСКАЯ ОШИБКА ЗАПУСКА: {ex.Message}");
                return;
            }

            Task.Run(async () =>
            {
                while (_listener != null && _listener.IsListening)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch { /* Игнорируем ошибки остановки */ }
                }
            });
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url.AbsolutePath;

                // --- ФИЛЬТРАЦИЯ FAVICON (Это важно!) ---
                if (path == "/favicon.ico")
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                string deviceName = GetDeviceFriendlyName(request.UserAgent);
                Log($"Запрос {path} от {request.RemoteEndPoint.Address}");

                // CORS заголовки
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.Close();
                    return;
                }

                // --- МАРШРУТИЗАЦИЯ ---
                if (path == "/download" && request.HttpMethod == "GET")
                {
                    await HandleDownload(response, deviceName);
                    return;
                }

                if (path == "/upload" && request.HttpMethod == "POST")
                {
                    await HandleIncomingUpload(request, response, deviceName);
                    return;
                }

                if (path == "/avatar")
                {
                    if (_avatarBytes != null)
                    {
                        response.ContentType = "image/jpeg";
                        response.ContentLength64 = _avatarBytes.Length;
                        await response.OutputStream.WriteAsync(_avatarBytes, 0, _avatarBytes.Length);
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }
                    response.Close();
                    return;
                }

                // --- ГЛАВНАЯ СТРАНИЦА ---
                // Оборачиваем генерацию HTML в try-catch, чтобы страница открывалась даже при ошибках с файлом
                string html = "";
                try
                {
                    html = GetHtmlPage();
                }
                catch (Exception ex)
                {
                    Log($"Ошибка генерации HTML: {ex.Message}");
                    html = "<h1>Ошибка отображения интерфейса</h1><p>Попробуйте другой файл.</p>";
                }

                response.ContentType = "text/html; charset=utf-8";
                byte[] pageBytes = Encoding.UTF8.GetBytes(html);
                response.ContentLength64 = pageBytes.Length;
                await response.OutputStream.WriteAsync(pageBytes, 0, pageBytes.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Log($"Глобальная ошибка запроса: {ex.Message}");
                try { response.Close(); } catch { }
            }
        }

        private string GetHtmlPage()
        {
            // БЕЗОПАСНОЕ ПОЛУЧЕНИЕ ИМЕНИ ФАЙЛА
            string fileName = "Нет файла";
            long fileSize = 0;
            bool hasFile = false;

            try
            {
                if (!string.IsNullOrEmpty(_filePath))
                {
                    fileName = Path.GetFileName(_filePath);
                    // Проверка существования и доступа
                    if (File.Exists(_filePath))
                    {
                        fileSize = new FileInfo(_filePath).Length;
                        hasFile = true;
                    }
                    else
                    {
                        fileName += " (Не найден)";
                    }
                }
            }
            catch 
            {
                fileName = "Ошибка доступа к файлу";
                hasFile = false;
            }

            bool hasAvatar = _avatarBytes != null;

            // БЕЗОПАСНАЯ ОБРАБОТКА ЦВЕТА
            string rawColor = "808080"; // Серый по умолчанию
            string r="128", g="128", b="128";
            
            try
            {
                if (!string.IsNullOrEmpty(_senderColor))
                {
                    string tempColor = _senderColor.Replace("#", "");
                    if (tempColor.Length == 8) tempColor = tempColor.Substring(2);
                    if (tempColor.Length == 6)
                    {
                        rawColor = tempColor;
                        r = Convert.ToInt32(rawColor.Substring(0, 2), 16).ToString();
                        g = Convert.ToInt32(rawColor.Substring(2, 2), 16).ToString();
                        b = Convert.ToInt32(rawColor.Substring(4, 2), 16).ToString();
                    }
                }
            }
            catch { /* Игнорируем ошибки цвета */ }

            return $@"
<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1, maximum-scale=1'>
    <title>HeliShare</title>
    <style>
        :root {{
            --accent: #{rawColor};
            --accent-low: rgba({r}, {g}, {b}, 0.15);
            --bg: #f3f3f3;
            --mica-bg: rgba(255, 255, 255, 0.7);
            --text: #1a1a1a;
            --text-sec: #5d5d5d;
            --border: rgba(0, 0, 0, 0.05);
        }}
        @media (prefers-color-scheme: dark) {{
            :root {{
                --bg: #202020;
                --mica-bg: rgba(32, 32, 32, 0.75);
                --text: #ffffff;
                --text-sec: #a0a0a0;
                --border: rgba(255, 255, 255, 0.08);
                --accent-low: rgba({r}, {g}, {b}, 0.25);
            }}
        }}
        body {{
            font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
            background: var(--bg);
            color: var(--text);
            display: flex; justify-content: center; align-items: center;
            min-height: 100vh; margin: 0;
        }}
        .card {{
            background: var(--mica-bg);
            backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px);
            width: 85%; max-width: 360px; padding: 32px;
            border-radius: 12px; border: 1px solid var(--border);
            text-align: center;
            box-shadow: 0 4px 24px rgba(0,0,0,0.1);
        }}
        .avatar {{
            width: 48px; height: 48px; border-radius: 50%; 
            background: var(--accent); color: white;
            display: flex; align-items: center; justify-content: center;
            font-size: 18px; font-weight: 600; margin: 0 auto 15px auto;
        }}
        .avatar img {{ width: 100%; height: 100%; border-radius: 50%; object-fit: cover; }}
        .file-box {{
            background: rgba(128,128,128,0.1);
            padding: 12px; border-radius: 8px; margin: 15px 0;
            word-break: break-all;
        }}
        .btn {{
            background: var(--accent); color: white;
            padding: 12px; border-radius: 8px; text-decoration: none;
            display: block; font-weight: 600; margin-top: 10px;
        }}
        .btn-outline {{ background: transparent; color: var(--text); border: 1px solid var(--border); }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='avatar'>
            {(hasAvatar ? "<img src='/avatar'>" : _senderNickname.Substring(0,1))}
        </div>
        <h3>{_senderNickname}</h3>
        
        <div style='opacity:0.6; font-size:12px; margin-top:20px'>ФАЙЛ ДЛЯ СКАЧИВАНИЯ</div>
        {(hasFile ? $@"
        <div class='file-box'>
            <b>{fileName}</b><br>
            <span style='font-size:12px; opacity:0.7'>{(fileSize / 1024.0 / 1024.0):F2} MB</span>
        </div>
        <a href='/download' class='btn'>Скачать</a>
        " : "<div class='file-box'>Нет файла</div>")}

        <div style='margin: 30px 0; height:1px; background:var(--border)'></div>
        
        <div style='opacity:0.6; font-size:12px'>ОТПРАВИТЬ ФАЙЛ</div>
        <input type='file' id='fIn' style='display:none' onchange='startUp()'>
        <button class='btn btn-outline' onclick=""document.getElementById('fIn').click()"" style='width:100%; border:1px solid gray'>Выбрать файл</button>
        <div id='stat' style='margin-top:10px; font-size:12px'></div>
    </div>

    <script>
    function startUp() {{
        const f = document.getElementById('fIn').files[0];
        if(!f) return;
        const s = document.getElementById('stat');
        s.innerText = 'Ожидание подтверждения...';
        
        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/upload', true);
        xhr.setRequestHeader('X-File-Name', window.btoa(unescape(encodeURIComponent(f.name))));
        
        xhr.upload.onprogress = e => {{
            if(e.lengthComputable) s.innerText = 'Загрузка: ' + Math.round((e.loaded/e.total)*100) + '%';
        }};
        
        xhr.onload = () => {{
            s.innerText = xhr.status == 200 ? 'Готово!' : 'Ошибка/Отказ';
        }};
        xhr.onerror = () => s.innerText = 'Ошибка сети';
        xhr.send(f);
    }}
    </script>
</body>
</html>";
        }

        private async Task HandleDownload(HttpListenerResponse response, string deviceName)
        {
            FileStream fs = null;
            try
            {
                if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                {
                    response.StatusCode = 404; response.Close(); return;
                }

                Application.Current.Dispatcher.Invoke(() => {
                    if (_currentOutgoingTransfer != null) {
                        _currentOutgoingTransfer.ClientNickname = deviceName;
                        _currentOutgoingTransfer.Status = "Отправка...";
                    }
                });

                response.ContentType = "application/octet-stream";
                string safeFileName = Uri.EscapeDataString(Path.GetFileName(_filePath));
                response.AddHeader("Content-Disposition", $"attachment; filename=\"{safeFileName}\"");

                fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long len = fs.Length;
                response.ContentLength64 = len;

                byte[] buffer = new byte[65536];
                int read;
                long total = 0;
                
                // Простая передача без сложного троттлинга для стабильности
                while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await response.OutputStream.WriteAsync(buffer, 0, read);
                    total += read;
                    // Обновляем UI реже (каждый 1 МБ), чтобы не тормозить поток
                    if (total % (1024 * 1024) == 0) 
                    {
                         Application.Current.Dispatcher.Invoke(() => {
                            if (_currentOutgoingTransfer != null) _currentOutgoingTransfer.Progress = (double)total / len * 100;
                        });
                    }
                }
                
                Application.Current.Dispatcher.Invoke(() => {
                    if (_currentOutgoingTransfer != null) {
                         _currentOutgoingTransfer.Status = "Завершено";
                         _currentOutgoingTransfer.Progress = 100;
                    }
                });
                response.Close();
            }
            catch (Exception ex)
            {
                Log($"Download Error: {ex.Message}");
                try { response.Abort(); } catch { }
            }
            finally
            {
                if (fs != null) await fs.DisposeAsync();
            }
        }

        private async Task HandleIncomingUpload(HttpListenerRequest request, HttpListenerResponse response, string deviceName)
        {
            // Упрощенная логика загрузки для стабильности
            try
            {
                string encodedName = request.Headers["X-File-Name"];
                string name = "file.bin";
                try { name = Encoding.UTF8.GetString(Convert.FromBase64String(encodedName)); } catch { }

                long size = request.ContentLength64;
                
                bool allowed = false;
                if (OnUploadConfirmationRequired != null)
                     allowed = await OnUploadConfirmationRequired(name, deviceName, size);

                if (!allowed) { response.StatusCode = 403; response.Close(); return; }

                var transfer = new TransferItem { FileName = name, TotalBytes = size, Status = "Получение...", ClientNickname = deviceName, PeerIdentifier = "Phone" };
                Application.Current.Dispatcher.Invoke(() => OnTransferStarted?.Invoke(transfer));

                string path = Path.Combine(AppSettings.SavePath, name);
                
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var stream = request.InputStream)
                {
                    await stream.CopyToAsync(fs);
                }
                
                Application.Current.Dispatcher.Invoke(() => { transfer.Status = "Получен"; transfer.Progress = 100; });
                response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                Log($"Upload Error: {ex.Message}");
                response.StatusCode = 500;
            }
            finally { try { response.Close(); } catch { } }
        }

        private string GetDeviceFriendlyName(string ua) => ua.Contains("Android") ? "Android" : (ua.Contains("iPhone") ? "iPhone" : "Browser");
        public void ShareFile(string path, TransferItem item) { _filePath = path; _currentOutgoingTransfer = item; }
        public void Stop() { try { _listener?.Stop(); _listener?.Close(); } catch { } }
    }
}