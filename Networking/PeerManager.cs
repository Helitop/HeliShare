// Networking/PeerManager.cs
using System.IO;
using System.Text.Json;

namespace LanShare.Networking
{
    public static class PeerManager
    {
        private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanShare");
        private static readonly string FilePath = Path.Combine(FolderPath, "peers_cache.json");

        public static void Save(IEnumerable<NetworkPeer> peers)
        {
            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
                // Сохраняем всех: и онлайн, и офлайн
                string json = JsonSerializer.Serialize(peers.ToList());
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static List<NetworkPeer> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<NetworkPeer>();
                string json = File.ReadAllText(FilePath);
                var list = JsonSerializer.Deserialize<List<NetworkPeer>>(json);

                // При загрузке все по умолчанию Offline
                foreach (var peer in list) peer.Status = PeerStatus.Offline;
                return list;
            }
            catch { return new List<NetworkPeer>(); }
        }
    }
}