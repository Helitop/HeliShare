using HeliShare.ViewModels.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Interop; // Нужно для MSG
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray;
using Wpf.Ui.Tray.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace HeliShare.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }
        private bool _isServiceShuttingDown = false;
        private NotifyIcon _notifyIcon;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            
            // --- ПОДПИСКА НА СООБЩЕНИЯ (ДЛЯ SINGLE INSTANCE) ---
            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;

            SetPageService(navigationViewPageProvider);
            navigationService.SetNavigationControl(RootNavigation);
        }

        // --- ОБРАБОТЧИК СООБЩЕНИЯ ОТ ВТОРОЙ КОПИИ ---
        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            // Если пришло наше уникальное сообщение "Покажись!"
            if (msg.message == HeliShare.NativeMethods.WM_SHOWME)
            {
                ShowWindow(); // Разворачиваем и активируем окно
                handled = true;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            InitializeTray();
        }

        // ... Остальной код (InitializeTray, ShowWindow, ShutdownApp и т.д.) без изменений ...
        
        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon
            {
                TooltipText = "HeliShare — Обмен файлами",
                Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/wpfui-icon-256.png"))
            };

            var trayMenu = new ContextMenu();
            var openItem = new MenuItem { Header = "Открыть HeliShare" };
            openItem.Click += (s, e) => ShowWindow();
            var exitItem = new MenuItem { Header = "Выйти полностью" };
            exitItem.Click += (s, e) => ShutdownApp();

            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(new Separator());
            trayMenu.Items.Add(exitItem);

            _notifyIcon.Menu = trayMenu;
            _notifyIcon.LeftClick += (s, e) => ShowWindow();
            _notifyIcon.Register();
        }

        public void ShowWindow()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
                this.WindowState = WindowState.Normal;

            this.Activate();
            this.Focus();
        }

        private void ShutdownApp()
        {
            _isServiceShuttingDown = true;
            _notifyIcon?.Unregister();
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isServiceShuttingDown)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnClosing(e);
        }

        #region INavigationWindow methods
        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);
        public void CloseWindow() => Close();
        #endregion

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        INavigationView INavigationWindow.GetNavigation()
        {
            return RootNavigation;
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}