using HeliShare.Helpers;
using HeliShare.Services;
using HeliShare.ViewModels.Pages;
using HeliShare.ViewModels.Windows;
using HeliShare.Views.Pages;
using HeliShare.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace HeliShare
{
    public partial class App : Application
    {
        private IHost? _host;
        private const string AppId = "HeliShare.App.1";
        private const string MutexName = "Global\\HeliShare_SingleInstance_Mutex"; 
        private Mutex _mutex;

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string AppID);

        public bool WasShortcutCreated { get; private set; }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            // 1. Пробуем настроить права на порт (чтобы сервер работал без Админа)
            EnsurePortPermission();

            // 2. Singleton (один экземпляр)
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, NativeMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                Shutdown();
                return;
            }

            // 3. Регистрация AUMID и Ярлыка
            SetCurrentProcessExplicitAppUserModelID(AppId);
            try { WasShortcutCreated = ShellHelper.CreateShortcutForNotifications(AppId); } catch { }

            // 4. Запуск приложения
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)!); })
                .ConfigureServices((context, services) =>
                {
                    services.AddNavigationViewPageProvider();
                    services.AddHostedService<ApplicationHostService>();
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<ITaskBarService, TaskBarService>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<INavigationWindow, MainWindow>();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<DashboardPage>();
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<SettingsPage>();
                    services.AddSingleton<SettingsViewModel>();
                })
                .Build();

            _host.Start();
            var mainWindow = _host.Services.GetRequiredService<INavigationWindow>();
            mainWindow.ShowWindow();

            InitializeNotifications();

            // --- ЗАПУСК ПРОВЕРКИ ОБНОВЛЕНИЙ ---
            if (LanShare.Services.AppSettings.CheckUpdatesOnStartup)
            {
                // Запускаем асинхронно, чтобы не тормозить старт
                Task.Run(() => HeliShare.Services.UpdateService.CheckForUpdatesAsync());
            }
        }

        // --- НОВЫЙ МЕТОД: Автоматическая настройка порта ---
        private void EnsurePortPermission()
        {
            int port = 46001; // Ваш порт
            string args = $"http add urlacl url=http://*:{port}/ user=Everyone";

            // Если Windows русская, нужно user=Все, если английская user=Everyone.
            // Хак: используем SID "S-1-1-0" (World/Everyone), чтобы работало на любом языке.
            // Но netsh требует имя. Попробуем добавить для текущего пользователя.
            string currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            args = $"http add urlacl url=http://*:{port}/ user=\"{currentUser}\"";

            // Пытаемся запустить маленький процесс настройки
            // Если прав нет, HttpListener выбросит исключение при старте,
            // но мы попытаемся превентивно добавить правило.

            // Мы не можем проверить, есть ли правило, без прав админа.
            // Поэтому просто пробуем запустить netsh через runas (запрос UAC), если это первый запуск.
            // Чтобы не спамить UAC каждый раз, можно сохранять флаг в настройки.

            string flagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeliShare", "port_setup_done.lock");

            if (!File.Exists(flagPath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = args,
                    Verb = "runas", // Запрос Админа
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };

                try
                {
                    var p = System.Diagnostics.Process.Start(psi);
                    p.WaitForExit();

                    // Если успешно (или пользователь нажал Да), создаем файл-флаг
                    Directory.CreateDirectory(Path.GetDirectoryName(flagPath));
                    File.WriteAllText(flagPath, "done");
                }
                catch
                {
                    // Пользователь нажал "Нет" в UAC.
                    // Ничего страшного, попробуем запуститься так.
                    // Если сервер упадет - он напишет в лог.
                }
            }
        }

        private bool IsRunAsAdmin()
        {
            try
            {
                var id = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void InitializeNotifications()
        {
            try
            {
                if (AppNotificationManager.IsSupported())
                {
                    var notificationManager = AppNotificationManager.Default;
                    // Отписываемся перед подпиской, чтобы не дублировать (на всякий случай)
                    notificationManager.NotificationInvoked -= OnNotificationInvoked;
                    notificationManager.NotificationInvoked += OnNotificationInvoked;
                    notificationManager.Register();
                }
            }
            catch { }

            // Обработка клика, если приложение было закрыто
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs?.Kind == ExtendedActivationKind.AppNotification)
            {
                if (activatedArgs.Data is AppNotificationActivatedEventArgs args)
                {
                    OnNotificationInvoked(AppNotificationManager.Default, args);
                }
            }
        }

        private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            // Получаем аргументы (action=accept, action=decline или action=view)
            string argument = args.Argument;

            // Парсим словарь аргументов (для надежности, если Windows добавит что-то еще)
            // Но в нашем простом случае можно просто проверить строку

            Dispatcher.Invoke(() =>
            {
                // Передаем управление в NotificationCoordinator
                NotificationCoordinator.ProcessNotificationAction(argument);
            });
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            _mutex?.Dispose();
            try 
            { 
                if (AppNotificationManager.IsSupported())
                    AppNotificationManager.Default.Unregister(); 
            } 
            catch { }

            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try { File.WriteAllText("heli_crash.txt", e.Exception.ToString()); } catch { }
        }
    }

    public static class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_HELISHARE");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);
    }
}