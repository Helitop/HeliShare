using Microsoft.Windows.AppNotifications;
using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;

public static class NotificationCoordinator
{
    private static TaskCompletionSource<bool> _currentTransferTcs;
    private static string _pendingFileName;
    private static string _updateUrl; // Для хранения ссылки

    // ... (Метод WaitForUserConfirmation без изменений) ...
    public static async Task<bool> WaitForUserConfirmation(string fileName, string senderNickname)
    {
        // Оставляем ваш старый код этого метода как есть...
        // Я его здесь сократил для читаемости, но в файле он должен остаться
        // ...
        _currentTransferTcs = new TaskCompletionSource<bool>();
        _pendingFileName = fileName;

        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "wpfui-icon-256.png");
            string safeFileName = SecurityElement.Escape(fileName);
            string safeNickname = SecurityElement.Escape(senderNickname);
            string safeIconPath = SecurityElement.Escape(iconPath);

            string imageXml = "";
            if (File.Exists(iconPath)) imageXml = $"<image placement='appLogoOverride' src='{safeIconPath}'/>";

            string payload = $@"
        <toast launch='action=view'>
            <visual>
                <binding template='ToastGeneric'>
                    <text>Входящий файл</text>
                    <text>{safeNickname} хочет отправить: {safeFileName}</text>
                    {imageXml}
                </binding>
            </visual>
            <actions>
                <action content='Принять' arguments='action=accept' activationType='foreground'/>
                <action content='Отклонить' arguments='action=decline' activationType='background'/>
            </actions>
        </toast>";

            var notification = new AppNotification(payload);
            if (AppNotificationManager.IsSupported()) AppNotificationManager.Default.Show(notification);
            else ShowAcceptWindowDirectly();

            var completedTask = await Task.WhenAny(_currentTransferTcs.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            if (completedTask != _currentTransferTcs.Task) ShowAcceptWindowDirectly();
        }
        catch (Exception ex)
        {
            ShowAcceptWindowDirectly();
        }
        return await _currentTransferTcs.Task;
    }

    // --- НОВЫЙ МЕТОД ДЛЯ УВЕДОМЛЕНИЯ ОБ ОБНОВЛЕНИИ ---
    public static void ShowUpdateNotification(string newVersion, string downloadUrl)
    {
        _updateUrl = downloadUrl;
        string safeVer = SecurityElement.Escape(newVersion);
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "wpfui-icon-256.png");
        string safeIconPath = SecurityElement.Escape(iconPath);
        
        string imageXml = File.Exists(iconPath) ? $"<image placement='appLogoOverride' src='{safeIconPath}'/>" : "";

        // XML уведомления с кнопкой "Обновить"
        string payload = $@"
        <toast launch='action=update_view'>
            <visual>
                <binding template='ToastGeneric'>
                    <text>Доступно обновление!</text>
                    <text>Версия {safeVer} доступна для скачивания.</text>
                    {imageXml}
                </binding>
            </visual>
            <actions>
                <action content='Обновить' arguments='action=update_go' activationType='foreground'/>
                <action content='Позже' arguments='action=dismiss' activationType='background'/>
            </actions>
        </toast>";

        var notification = new AppNotification(payload);
        if (AppNotificationManager.IsSupported())
        {
            AppNotificationManager.Default.Show(notification);
        }
    }

    public static void ProcessNotificationAction(string arguments)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (arguments)
            {
                case "action=accept":
                    BringAppToFront();
                    _currentTransferTcs?.TrySetResult(true);
                    break;

                case "action=decline":
                    _currentTransferTcs?.TrySetResult(false);
                    break;

                case "action=view":
                    BringAppToFront();
                    ShowAcceptWindowDirectly(); 
                    break;

                // --- НОВЫЕ КЕЙСЫ ДЛЯ ОБНОВЛЕНИЯ ---
                case "action=update_go":
                case "action=update_view":
                    // Открываем браузер со ссылкой на скачивание
                    if (!string.IsNullOrEmpty(_updateUrl))
                    {
                        try 
                        { 
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo 
                            { 
                                FileName = _updateUrl, 
                                UseShellExecute = true 
                            }); 
                        } catch { }
                    }
                    break;
            }
        });
    }

    private static void ShowAcceptWindowDirectly()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var existingWindow = Application.Current.Windows.OfType<HeliShare.Views.Windows.FileAcceptWindow>().FirstOrDefault();
            if (existingWindow != null) { existingWindow.Activate(); return; }

            var wnd = new HeliShare.Views.Windows.FileAcceptWindow(_pendingFileName);
            wnd.Topmost = true;
            bool result = wnd.ShowDialog() == true;
            _currentTransferTcs?.TrySetResult(result);
        });
    }

    private static void BringAppToFront()
    {
        var mainWnd = Application.Current.MainWindow;
        if (mainWnd != null)
        {
            if (mainWnd.WindowState == WindowState.Minimized) mainWnd.WindowState = WindowState.Normal;
            mainWnd.Show();
            mainWnd.Activate();
        }
    }
}