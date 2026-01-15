using System.Text.Json;
using System.IO;

namespace LanShare.Networking
{
    public static class HistoryManager
    {
        private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeliShare");
        private static readonly string FilePath = Path.Combine(FolderPath, "history.json");

        public static void Save(List<TransferItem> history) // Принимаем List
        {
            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);

                // Сохраняем только последние 100 записей
                var itemsToSave = history.Take(100).ToList();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(itemsToSave, options);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения истории: {ex.Message}");
            }
        }

        public static List<TransferItem> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<TransferItem>();

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<TransferItem>>(json) ?? new List<TransferItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки истории: {ex.Message}");
                return new List<TransferItem>();
            }
        }
    }
}