using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Diagnostics;

namespace HeliShare.Services
{
    public static class UpdateService
    {
        // --- НАСТРОЙКИ РЕПОЗИТОРИЯ ---
        private const string GithubUser = "Helitop"; // Укажите свой ник
        private const string GithubRepo = "HeliShare";    // Укажите имя репозитория
        // -----------------------------

        public static async Task CheckForUpdatesAsync(bool manualCheck = false)
        {
            try
            {
                using var client = new HttpClient();
                // GitHub API требует User-Agent
                client.DefaultRequestHeaders.UserAgent.ParseAdd("HeliShare-App");

                string url = $"https://api.github.com/repos/{GithubUser}/{GithubRepo}/releases/latest";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode) 
                {
                    if (manualCheck) MessageBox.Show("Не удалось проверить обновления. Проверьте подключение.");
                    return; 
                }

                string json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                if (release == null) return;

                // Парсим версию с GitHub (обычно теги идут как v1.0.0, убираем 'v')
                string tagVersion = release.TagName.TrimStart('v');
                
                // Текущая версия сборки
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var remoteVersion = new Version(tagVersion);

                // Если на сервере версия выше
                if (remoteVersion > currentVersion)
                {
                    // Показываем уведомление
                    NotificationCoordinator.ShowUpdateNotification(release.TagName, release.HtmlUrl);
                }
                else if (manualCheck)
                {
                    MessageBox.Show("У вас установлена последняя версия!", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (manualCheck) MessageBox.Show($"Ошибка проверки: {ex.Message}");
            }
        }
    }

    // Класс для десериализации ответа GitHub
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }
    }
}