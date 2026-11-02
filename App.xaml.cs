using System.Threading;
using System.Windows;

namespace VideoAutoWpf;

/// <summary>
/// Interaction logic for App.xaml
/// Quản lý Single Instance Mutex và xử lý ngoại lệ toàn cục an toàn.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\VideoAutoWpf_SingleInstance_Mutex_2026";

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Kiểm tra Single Instance Mutex
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                "VideoAutoWpf đã đang chạy trong một phiên khác trên máy này.\nChỉ cho phép duy nhất một phiên ứng dụng hoạt động để bảo vệ dữ liệu SQLite và hàng đợi tự động.",
                "Ứng Dụng Đang Chạy",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // 2. Đăng ký xử lý ngoại lệ toàn cục - Bảo vệ app không bao giờ bị tắt đột ngột
        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                System.IO.File.AppendAllText(logPath, $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] Dispatcher Error: {args.Exception}\n\n");
            }
            catch { }

            // Đánh dấu đã xử lý để WPF không tắt app
            args.Handled = true;

            try
            {
                MessageBox.Show(
                    $"Đã xảy ra lỗi:\n{args.Exception?.Message}\n\nChi tiết đã được ghi vào crash_log.txt.",
                    "Thông Báo Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                System.IO.File.AppendAllText(logPath, $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] AppDomain Error: {args.ExceptionObject}\n\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                System.IO.File.AppendAllText(logPath, $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] TaskScheduler Error: {args.Exception}\n\n");
            }
            catch { }
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}
