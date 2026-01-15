using System.IO;
using System.Text.Json;

namespace LanShare.Services
{
    public static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "HeliShare", 
            "settings.json");

        public static string SavePath { get; set; } = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        public static int UploadLimitKBps { get; set; } = 0;
        public static int DownloadLimitKBps { get; set; } = 0;

        // --- НОВАЯ НАСТРОЙКА ---
        public static bool CheckUpdatesOnStartup { get; set; } = true;

        static AppSettings()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        if (Directory.Exists(data.SavePath)) SavePath = data.SavePath;
                        UploadLimitKBps = data.UploadLimitKBps;
                        DownloadLimitKBps = data.DownloadLimitKBps;
                        CheckUpdatesOnStartup = data.CheckUpdatesOnStartup; // Загружаем
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var data = new SettingsData
                {
                    SavePath = SavePath,
                    UploadLimitKBps = UploadLimitKBps,
                    DownloadLimitKBps = DownloadLimitKBps,
                    CheckUpdatesOnStartup = CheckUpdatesOnStartup // Сохраняем
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        private class SettingsData
        {
            public string SavePath { get; set; }
            public int UploadLimitKBps { get; set; }
            public int DownloadLimitKBps { get; set; }
            public bool CheckUpdatesOnStartup { get; set; } = true;
        }
    }
}