using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq; // Важно для LINQ запросов
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;
using Wpf.Ui.Controls;

namespace HeliShare.Views.Windows
{
    public partial class QrShareWindow
    {
        // transfer теперь может быть null
        public QrShareWindow(string filePath, string ip, LanShare.Networking.TransferItem transfer)
        {
            InitializeComponent();
            LoadNetworkInterfaces(ip);
            
            // Формируем URL
            string url = $"http://{ip}:46001/";
            
            UrlText.Content = url;
            UrlText.NavigateUri = url; // Исправлено: NavigateUri требует Uri, а не string

            QrImage.Source = HeliShare.Helpers.QrCodeHelper.GenerateQrCode(url);

            // --- ИЗМЕНЕНИЯ ТУТ ---
            if (!string.IsNullOrEmpty(filePath))
            {
                this.Title = $"HeliLink: {Path.GetFileName(filePath)}";
            }
            else
            {
                this.Title = "HeliLink: Ожидание файлов";
                // Можно визуально скрыть иконку файла или поменять текст, если нужно
            }
        }

        public class NetworkInterfaceInfo
        {
            public string Name { get; set; }
            public string IpAddress { get; set; }
            public string Icon { get; set; }
            public string Description { get; set; }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn)
            {
                string url = UrlText.Content?.ToString();
                if (string.IsNullOrEmpty(url)) return;

                Clipboard.SetText(url);

                var oldContent = btn.Content;
                btn.Content = new SymbolIcon(SymbolRegular.Checkmark24);
                btn.IsEnabled = false;

                await Task.Delay(1500);

                btn.Content = oldContent;
                btn.IsEnabled = true;
            }
        }

        private void LoadNetworkInterfaces(string initialIp)
        {
            var displayList = new List<NetworkInterfaceInfo>();
            string[] junkKeywords = { "virtual", "vmware", "virtualbox", "vbox", "pseudo", "wsl", "hyper-v", "teredo" };

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    string desc = ni.Description.ToLower();
                    if (junkKeywords.Any(k => desc.Contains(k))) continue;

                    var props = ni.GetIPProperties();
                    var ipv4 = props.UnicastAddresses
                        .FirstOrDefault(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                    if (ipv4 != null)
                    {
                        string ip = ipv4.Address.ToString();
                        string name = ni.Name;
                        string icon = "NetworkCheck24";
                        string friendlyDesc = ni.Description;

                        if (desc.Contains("radmin")) { name = "Radmin VPN"; icon = "ShieldCheckmark24"; }
                        else if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || desc.Contains("wi-fi")) { name = "Wi-Fi"; icon = "WifiSettings24"; }
                        else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) { name = "Локальная сеть"; icon = "Connector24"; }

                        displayList.Add(new NetworkInterfaceInfo { Name = name, IpAddress = ip, Icon = icon, Description = friendlyDesc });
                    }
                }

                var sortedList = displayList
                    .OrderByDescending(x => x.Name.Contains("Radmin"))
                    .ThenByDescending(x => x.Name.Contains("Wi-Fi"))
                    .ToList();

                IpComboBox.ItemsSource = sortedList;
                // Если переданный IP не найден в списке (например 0.0.0.0), берем первый попавшийся
                IpComboBox.SelectedItem = sortedList.FirstOrDefault(x => x.IpAddress == initialIp) ?? sortedList.FirstOrDefault();
            }
            catch { }
        }

        private void IpComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IpComboBox.SelectedItem is NetworkInterfaceInfo selected)
            {
                UpdateQrAndUrl(selected.IpAddress);
            }
        }

        private void UpdateQrAndUrl(string ip)
        {
            string url = $"http://{ip}:46001/";
            UrlText.Content = url;
            UrlText.NavigateUri = url; // Обновляем ссылку для клика
            
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.5, 1.0, TimeSpan.FromMilliseconds(500));
            var bluranim = new System.Windows.Media.Animation.DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(500));
            
            QrImage.BeginAnimation(OpacityProperty, anim);
            QrBlur.BeginAnimation(BlurEffect.RadiusProperty, bluranim);

            QrImage.Source = HeliShare.Helpers.QrCodeHelper.GenerateQrCode(url);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); }
    }
}