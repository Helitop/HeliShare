using LanShare.Networking;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeliShare.Helpers;

public class NetworkPeer : INotifyPropertyChanged
{
    public string Nickname { get; set; }
    public IPEndPoint EndPoint { get; set; }
    public NetworkType NetworkType { get; set; }
    public bool IsPhonePeer { get; set; } 
    public bool IsSelf { get; set; }
    private PeerStatus _status;
    public PeerStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public DateTime LastSeen { get; set; }

    private string _accentColorHex;
    public string AccentColorHex
    {
        get => _accentColorHex;
        set { _accentColorHex = value; OnPropertyChanged(); }
    }

    public byte[] AvatarBytes { get; set; }

    // Свойство для XAML (используем наш ImageHelper)
    public ImageSource AvatarSource => HeliShare.Helpers.ImageHelper.BytesToImage(AvatarBytes);

    // Свойство для цвета круга и фона
    public Color DisplayColor
    {
        get
        {
            try { return (Color)ColorConverter.ConvertFromString(AccentColorHex ?? "#808080"); }
            catch { return Colors.Gray; }
        }
    }

    public string DisplayName =>
        $"{Nickname} [{NetworkType}] ({EndPoint.Address})";

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}