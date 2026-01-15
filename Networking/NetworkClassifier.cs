// Networking/NetworkClassifier.cs
using System.Net;

namespace LanShare.Networking
{
    public static class NetworkClassifier
    {
        public static NetworkType Detect(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();

            // Radmin VPN: 172.16.0.0 – 172.31.255.255
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return NetworkType.RadminVpn;

            // LAN (частные сети)
            if (bytes[0] == 192 && bytes[1] == 168)
                return NetworkType.Lan;

            if (bytes[0] == 10)
                return NetworkType.Lan;

            return NetworkType.Unknown;
        }
    }
}
