using HeliShare.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Wpf.Ui.Appearance;

namespace LanShare.Networking
{
    public class PeerDiscovery
    {
        public event Action<NetworkPeer> PeerFound;

        private const int Port = 45000;
        private readonly string _nickname;
        private readonly byte[] _avatarBytes; 
        private readonly IPAddress _localIp;
        public bool IsSelf { get; set; }

        private CancellationTokenSource _cts;

        public PeerDiscovery(string nickname, byte[] avatarBytes)
        {
            _nickname = nickname;
            _avatarBytes = avatarBytes;
            _localIp = GetLocalIp();
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => Listen(_cts.Token));
            Task.Run(() => Broadcast(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async Task Broadcast(CancellationToken token)
        {
            // Используем порт 0 (любой свободный) для отправки, 
            // так как нам не нужно "слушать" на этом сокете
            using var client = new UdpClient();
            client.EnableBroadcast = true;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var color = ApplicationAccentColorManager.PrimaryAccent;
                    string hexColor = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

                    string avatarBase64 = "";
                    if (_avatarBytes != null && _avatarBytes.Length < 10000)
                        avatarBase64 = Convert.ToBase64String(_avatarBytes);

                    var message = $"{_nickname}|{hexColor}|{avatarBase64}";
                    var data = Encoding.UTF8.GetBytes(message);

                    await client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port));
                }
                catch { /* Ошибки отправки в пустоту игнорируем */ }

                try 
                { 
                    await Task.Delay(3000, token); 
                }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task Listen(CancellationToken token)
        {
            UdpClient client = null;
            try
            {
                // Попытка занять порт для прослушивания
                client = new UdpClient(Port);
            }
            catch (SocketException)
            {
                // Если порт занят (программа уже запущена), 
                // этот экземпляр просто не будет искать других пиров, 
                // но сможет отправить файл (т.к. вещание идет с другого сокета).
                System.Diagnostics.Debug.WriteLine("Discovery: Порт занят. Режим 'только отправка'.");
                return;
            }

            using (client)
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await client.ReceiveAsync(token);
                        var rawData = Encoding.UTF8.GetString(result.Buffer);

                        var parts = rawData.Split('|');
                        if (parts.Length < 2) continue;

                        var nick = parts[0];
                        var color = parts[1];
                        byte[] avatarBytes = null;

                        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                        {
                            try { avatarBytes = Convert.FromBase64String(parts[2]); } catch { }
                        }

                        PeerFound?.Invoke(new NetworkPeer
                        {
                            Nickname = nick,
                            AccentColorHex = color,
                            AvatarBytes = avatarBytes,
                            EndPoint = result.RemoteEndPoint,
                            NetworkType = NetworkClassifier.Detect(result.RemoteEndPoint.Address)
                        });
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Discovery Listen Error: {ex.Message}");
                    }
                }
            }
        }

        private IPAddress GetLocalIp()
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                    ?? IPAddress.Loopback;
            }
            catch { return IPAddress.Loopback; }
        }

        public async Task SendDiscoverySignal()
        {
            using var client = new UdpClient();
            client.EnableBroadcast = true;

            var color = ApplicationAccentColorManager.PrimaryAccent;
            string hexColor = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            string avatarBase64 = _avatarBytes != null ? Convert.ToBase64String(_avatarBytes) : "";

            var message = $"{_nickname}|{hexColor}|{avatarBase64}";
            var data = Encoding.UTF8.GetBytes(message);

            await client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port));
        }
    }
}
