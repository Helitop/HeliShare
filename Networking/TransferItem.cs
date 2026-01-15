// Файл: Networking/TransferItem.cs
using HeliShare.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Text.Json.Serialization;

namespace LanShare.Networking
{
    public class TransferItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public string LocalPath { get; set; } 
        public string Date { get; set; }
        public long TotalBytes { get; set; }
        public string PeerIdentifier { get; set; } // IP отправителя/получателя

        private string _clientName;
        public string ClientName
        {
            get => _clientName;
            set 
            { 
                _clientName = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(DisplayName)); 
            }
        }

        private string _clientNickname;
        public string ClientNickname
        {
            get => _clientNickname;
            set 
            { 
                _clientNickname = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(DisplayName)); 
            }
        }

        public string AccentColorHex { get; set; }
        public byte[] ClientAvatar { get; set; }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private string _status;
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private string _speedDisplay = "";
        [JsonIgnore] 
        public string SpeedDisplay
        {
            get => _speedDisplay;
            set { _speedDisplay = value; OnPropertyChanged(); }
        }

        private string _timeRemainingDisplay = "";
        [JsonIgnore]
        public string TimeRemainingDisplay
        {
            get => _timeRemainingDisplay;
            set { _timeRemainingDisplay = value; OnPropertyChanged(); }
        }

        public static string FormatTimeSpan(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds)) return "ожидание...";
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        [JsonIgnore]
        public ImageSource AvatarSource => HeliShare.Helpers.ImageHelper.BytesToImage(ClientAvatar);

        [JsonIgnore]
        public Color DisplayColor => HeliShare.Helpers.ImageHelper.HexToColor(AccentColorHex);

        [JsonIgnore]
        public string DisplayName 
        {
            get
            {
                bool hasNick = !string.IsNullOrWhiteSpace(ClientNickname);
                // Игнорируем ClientName, если он пустой или содержит техническую заглушку "Phone"
                bool hasName = !string.IsNullOrWhiteSpace(ClientName) && ClientName != "Phone";

                if (hasNick && hasName) return $"{ClientNickname} ({ClientName})";
                if (hasNick) return ClientNickname;
                if (hasName) return ClientName;
                
                return "Устройство"; // Если вообще ничего нет
            }
        }

        [JsonIgnore]
        public string SizeDisplay => FormatSize(TotalBytes);

        public TransferItem() { }

        public void OpenInExplorer()
        {
            if (!string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{LocalPath}\"");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1) { number /= 1024; counter++; }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}