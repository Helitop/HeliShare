using HeliShare.Helpers;
using HeliShare.ViewModels.Pages;
using HeliShare.Views.Windows;
using LanShare.Networking;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace HeliShare.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }
        public ObservableCollection<NetworkPeer> Peers { get; } = new();

        // Сервер для раздачи и приема через браузер
        private readonly WebShareServer _heliLinkServer = new();

        private NetworkPeer _selectedPeer;
        public NetworkPeer SelectedPeer
        {
            get => _selectedPeer;
            set
            {
                _selectedPeer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSendEnabled));

                if (_selectedPeer != null)
                {
                    // ОПРЕДЕЛЯЕМ ID
                    string filterId = _selectedPeer.IsPhonePeer ? "Phone" : _selectedPeer.EndPoint.Address.ToString();

                    // ИСПРАВЛЕНИЕ ЗАВИСАНИЯ:
                    // Используем DispatcherPriority.Background. Это позволяет UI сначала отрисовать
                    // выделение (клик) в списке, а уже потом запустить тяжелую фильтрацию истории.
                    Dispatcher.InvokeAsync(() =>
                    {
                        ViewModel.FilterHistory(filterId);
                    }, DispatcherPriority.Background);
                }
            }
        }

        public bool IsSendEnabled => SelectedPeer != null;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly PeerDiscovery _discovery;
        private readonly FileReceiver _receiver;
        private readonly DispatcherTimer _cleanupTimer;
        private byte[] _myAvatarBytes;

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();

            // 1. Загружаем кеш пиров
            var cachedPeers = PeerManager.Load();
            foreach (var p in cachedPeers) Peers.Add(p);

            // 2. Создаем "СЕБЯ" и принудительно ставим на 0-е место
            var localIp = GetLocalIp(); 
            var self = Peers.FirstOrDefault(p => p.IsSelf);
            if (self == null)
            {
                self = new NetworkPeer
                {
                    Nickname = Environment.UserName + " (Вы)",
                    IsSelf = true,
                    EndPoint = new IPEndPoint(localIp, 0),
                    Status = PeerStatus.Online
                };
                Peers.Insert(0, self); 
            }

            // Получаем аватаку для вещания
            _myAvatarBytes = HeliShare.Helpers.AvatarHelper.GetCurrentUserAvatarBytes();

            var accentColor = ApplicationAccentColorManager.PrimaryAccent;
            string myHexColor = $"#{accentColor.A:X2}{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}";

            // --- ВЕБ СЕРВЕР ---
            
            // 1. Сначала подписка на подтверждение (безопасность)
            _heliLinkServer.OnUploadConfirmationRequired += OnWebUploadConfirmation;
            
            // 2. Потом подписка на начало передачи (для обновления UI)
            _heliLinkServer.OnTransferStarted += OnWebFileStarted;

            // Запускаем сервер (раздача + прием)
            _heliLinkServer.Start("0.0.0.0", Environment.UserName, myHexColor, _myAvatarBytes);

            // Добавляем пир HeliLink в список
            Peers.Add(new NetworkPeer
            {
                Nickname = "HeliLink (Веб-обмен)",
                IsPhonePeer = true,
                Status = PeerStatus.Online,
                AccentColorHex = "#FF800080",
                EndPoint = new IPEndPoint(IPAddress.Any, 0)
            });
            self.AvatarBytes = _myAvatarBytes;

            // --- ОБНАРУЖЕНИЕ ---
            _discovery = new PeerDiscovery(Environment.UserName, _myAvatarBytes);
            _discovery.PeerFound += OnPeerFound;
            _discovery.Start();

            // --- ПРИЕМ ФАЙЛОВ ПО TCP (ДЕСКТОП) ---
            _receiver = new FileReceiver();
            _receiver.Start((newTransfer) =>
            {
                Dispatcher.Invoke(() => {
                    ViewModel.AddTransfer(newTransfer);
                    // Сохранение может быть долгим, но AddTransfer меняет коллекцию.
                    // Оставляем синхронным для безопасности коллекции, но учтите, что лучше сохранять реже.
                    ViewModel.SaveHistory();
                });
            });

            // --- ОЧИСТКА ---
            _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _cleanupTimer.Tick += Cleanup;
            _cleanupTimer.Start();
            CheckAndShowShortcutHint();
        }

        // Обработчик подтверждения загрузки с веба
        private async Task<bool> OnWebUploadConfirmation(string fileName, string deviceName, long size)
        {
            // Вызываем то же окно/уведомление, что и для десктопного клиента
            return await NotificationCoordinator.WaitForUserConfirmation(fileName, deviceName);
        }

        // Обработчик начала загрузки (добавляем в UI)
        private void OnWebFileStarted(TransferItem item)
        {
            Dispatcher.Invoke(() =>
            {
                ViewModel.AddTransfer(item);
                ViewModel.SaveHistory();
            });
        }

        private async void CheckAndShowShortcutHint()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeliShare");
            string flagPath = Path.Combine(appData, "notification_hint_shown.lock");

            if (Application.Current is App myApp && myApp.WasShortcutCreated && !File.Exists(flagPath))
            {
                await ShowShortcutHint();

                try
                {
                    if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                    File.WriteAllText(flagPath, "shown");
                }
                catch { }
            }
        }

        private IPAddress GetWifiIp()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        string desc = ni.Description.ToLower();
                        if (desc.Contains("radmin") || desc.Contains("virtual") ||
                            desc.Contains("vpn") || desc.Contains("hamachi") ||
                            desc.Contains("vmware") || desc.Contains("pseudo"))
                            continue;

                        var props = ni.GetIPProperties();
                        foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                if (props.GatewayAddresses.Count > 0)
                                {
                                    return ip.Address;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return GetLocalIp();
        }

        private async Task ShowShortcutHint()
        {
            await Task.Delay(1000); 

            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Уведомления настроены",
                Content = "Ярлык HeliShare создан в меню 'Пуск'. Для того чтобы уведомления Windows работали корректно, рекомендуется один раз запустить приложение через этот ярлык.",
                CloseButtonText = "Хорошо",
                MaxWidth = 450
            };

            await messageBox.ShowDialogAsync();
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPeer == null)
            {
                System.Windows.MessageBox.Show("Выберите пользователя, чтобы очистить историю переписки с ним.");
                return;
            }

            string peerName = SelectedPeer.Nickname ?? SelectedPeer.EndPoint.Address.ToString();
            string peerId = SelectedPeer.IsPhonePeer ? "Phone" : SelectedPeer.EndPoint.Address.ToString();

            var result = System.Windows.MessageBox.Show(
                $"Очистить историю переписки с {peerName}?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ViewModel.ClearHistoryForPeer(peerId);
            }
        }

        private async Task ExecuteSend(string filePath, string ip)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // ЛОГИКА ДЛЯ HELILINK (Отправка на телефон через QR)
            if (SelectedPeer != null && SelectedPeer.IsPhonePeer)
            {
                var linkTransfer = new TransferItem
                {
                    FileName = Path.GetFileName(filePath),
                    LocalPath = filePath,
                    TotalBytes = new FileInfo(filePath).Length,
                    Date = DateTime.Now.ToString("HH:mm"),
                    Status = "Раздаётся...",
                    PeerIdentifier = "Phone",
                    ClientNickname = "HeliLink",
                    AccentColorHex = "#FF800080",
                    Progress = 0
                };

                ViewModel.AddTransfer(linkTransfer);

                // Выставляем файл на сервер
                _heliLinkServer.ShareFile(filePath, linkTransfer);

                // Открываем окно с QR
                string myIp = GetWifiIp().ToString();
                var qrWindow = new HeliShare.Views.Windows.QrShareWindow(filePath, myIp, linkTransfer);
                qrWindow.Show();

                return;
            }

            // --- ЛОГИКА ДЛЯ ОБЫЧНЫХ ПК ---
            var accentColor = ApplicationAccentColorManager.PrimaryAccent;
            string myHexColor = $"#{accentColor.A:X2}{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}";

            var item = new TransferItem
            {
                FileName = Path.GetFileName(filePath),
                LocalPath = filePath,
                TotalBytes = new FileInfo(filePath).Length,
                Date = DateTime.Now.ToString("HH:mm"),
                Status = "Подключение...",
                PeerIdentifier = ip,
                ClientName = ip,
                ClientNickname = SelectedPeer?.Nickname,
                AccentColorHex = SelectedPeer?.AccentColorHex ?? "#808080",
                ClientAvatar = SelectedPeer?.AvatarBytes,
                Progress = 0
            };

            ViewModel.AddTransfer(item);
            ViewModel.SaveHistory();

            try
            {
                await FileSender.Send(
                    filePath,
                    ip,
                    item,
                    Environment.UserName,
                    myHexColor,
                    _myAvatarBytes
                );
                ViewModel.SaveHistory();
            }
            catch (Exception ex)
            {
                item.Status = "Ошибка";
                ViewModel.SaveHistory();
            }
        }

        private void OpenFileFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is TransferItem item)
            {
                if (File.Exists(item.LocalPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.LocalPath}\"");
                }
                else
                {
                    System.Windows.MessageBox.Show("Файл не найден. Возможно, он был удален или перемещен.");
                }
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPeer == null) return;

            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                await ExecuteSend(dlg.FileName, SelectedPeer.EndPoint.Address.ToString());
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (SelectedPeer == null)
            {
                System.Windows.MessageBox.Show("Сначала выберите пользователя в списке слева!");
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    await ExecuteSend(files[0], SelectedPeer.EndPoint.Address.ToString());
                }
            }
        }

        private async void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPeer == null)
            {
                System.Windows.MessageBox.Show("Сначала выберите пользователя из списка!");
                return;
            }

            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                await ExecuteSend(dlg.FileName, SelectedPeer.EndPoint.Address.ToString());
            }
        }

        private void ForgetPeer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is NetworkPeer peer)
            {
                if (peer.Status == PeerStatus.Online)
                {
                    System.Windows.MessageBox.Show("Нельзя забыть пользователя, который сейчас в сети.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Peers.Remove(peer);
                PeerManager.Save(Peers);
            }
        }

        private bool IsLocalIp(IPAddress ip)
        {
            var localIPs = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            return IPAddress.IsLoopback(ip) || localIPs.Any(x => x.Equals(ip));
        }

        private IPAddress GetLocalIp()
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?? IPAddress.Loopback;
            }
            catch
            {
                return IPAddress.Loopback;
            }
        }

        private void OnPeerFound(NetworkPeer peer)
        {
            Dispatcher.Invoke(() =>
            {
                bool isMe = IsLocalIp(peer.EndPoint.Address);

                var existing = Peers.FirstOrDefault(p =>
                    (isMe && p.IsSelf) || (!isMe && p.EndPoint.Address.Equals(peer.EndPoint.Address)));

                if (existing == null)
                {
                    peer.Status = PeerStatus.Online;
                    peer.LastSeen = DateTime.Now;
                    Peers.Add(peer);
                }
                else
                {
                    existing.Status = PeerStatus.Online;
                    existing.LastSeen = DateTime.Now;
                    if (!existing.IsSelf) existing.Nickname = peer.Nickname;
                    existing.AccentColorHex = peer.AccentColorHex;
                    if (peer.AvatarBytes != null) existing.AvatarBytes = peer.AvatarBytes;
                }
            });
        }

        private void Cleanup(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            bool changed = false;

            // Работаем с копией списка для проверки статусов
            foreach (var peer in Peers.ToList())
            {
                if (peer.IsSelf) continue;

                if (peer.Status == PeerStatus.Online && (now - peer.LastSeen).TotalSeconds > 8)
                {
                    peer.Status = PeerStatus.Offline;
                    changed = true;
                }
            }
            
            if (changed) 
            {
                // ИСПРАВЛЕНИЕ: Сохранение списка пиров на диск (JSON) - дорогая операция.
                // Выполняем её в фоновом потоке, создав моментальный снимок списка.
                var snapshot = Peers.ToList();
                Task.Run(() => PeerManager.Save(snapshot));
            }
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void UpdatePeers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var peer in Peers)
            {
                peer.Status = PeerStatus.Offline;
            }

            if (_discovery != null)
            {
                await _discovery.SendDiscoverySignal();
            }
        }
        private void OpenQrOnly_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем раздачу (чтобы на сайте было написано "Нет файлов для скачивания")
            _heliLinkServer.ShareFile(null, null);

            // Определяем IP для QR кода
            string myIp = GetWifiIp().ToString();

            // Если IP не определился корректно, fallback на Loopback
            if (myIp == "127.0.0.1")
            {
                // Попытка взять первый не-loopback
                myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))?.ToString() ?? "127.0.0.1";
            }

            // Открываем окно без файла
            var qrWindow = new HeliShare.Views.Windows.QrShareWindow(null, myIp, null);
            qrWindow.Show();
        }
        private void PeerMore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}