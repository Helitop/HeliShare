using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Microsoft.Win32;
using LanShare.Services;
using HeliShare.Services; // Для UpdateService

namespace HeliShare.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty] private string _appVersion = String.Empty;
        [ObservableProperty] private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;
        [ObservableProperty] private string _currentSavePath;
        [ObservableProperty] private double _uploadLimitValue; 
        [ObservableProperty] private double _downloadLimitValue;
        [ObservableProperty] private string _uploadLimitText;
        [ObservableProperty] private string _downloadLimitText;

        // --- НОВОЕ СВОЙСТВО ---
        [ObservableProperty] 
        private bool _isAutoUpdateEnabled;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized) InitializeViewModel();
            return Task.CompletedTask;
        }
        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"HeliShare - {GetAssemblyVersion()}";
            CurrentSavePath = AppSettings.SavePath;
            UploadLimitValue = AppSettings.UploadLimitKBps / 1024.0;
            DownloadLimitValue = AppSettings.DownloadLimitKBps / 1024.0;

            // Загрузка состояния чекбокса
            IsAutoUpdateEnabled = AppSettings.CheckUpdatesOnStartup;

            UpdateLimitTexts();
            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light) break;
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;
                    break;
                default:
                    if (CurrentTheme == ApplicationTheme.Dark) break;
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;
                    break;
            }
        }

        [RelayCommand]
        private void OnBrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку для сохранения",
                InitialDirectory = CurrentSavePath,
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentSavePath = dialog.FolderName;
                AppSettings.SavePath = CurrentSavePath;
                AppSettings.Save();
            }
        }

        // --- НОВАЯ КОМАНДА ДЛЯ РУЧНОЙ ПРОВЕРКИ ---
        [RelayCommand]
        private async Task OnCheckUpdates()
        {
            await UpdateService.CheckForUpdatesAsync(manualCheck: true);
        }

        // --- РЕАКЦИЯ НА ИЗМЕНЕНИЕ ЧЕКБОКСА ---
        partial void OnIsAutoUpdateEnabledChanged(bool value)
        {
            AppSettings.CheckUpdatesOnStartup = value;
            AppSettings.Save();
        }

        partial void OnUploadLimitValueChanged(double value)
        {
            int kbps = (int)(value * 1024);
            AppSettings.UploadLimitKBps = kbps;
            AppSettings.Save();
            UpdateLimitTexts();
        }

        partial void OnDownloadLimitValueChanged(double value)
        {
            int kbps = (int)(value * 1024);
            AppSettings.DownloadLimitKBps = kbps;
            AppSettings.Save();
            UpdateLimitTexts();
        }

        private void UpdateLimitTexts()
        {
            UploadLimitText = UploadLimitValue <= 0.1 ? "Неограничено" : $"{UploadLimitValue:F1} MB/s";
            DownloadLimitText = DownloadLimitValue <= 0.1 ? "Неограничено" : $"{DownloadLimitValue:F1} MB/s";
        }
    }
}