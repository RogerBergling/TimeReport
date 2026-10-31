using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TimeReport.Services;
using TimeReport.Views;

namespace TimeReport;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        AppSession? session = null;
        var startupFailed = false;
        try
        {
            LocalizationService.SetLanguage(AppSettings.Load(AppPaths.SettingsPath()).Language);
            session = new AppSession(AppOptions.DatabasePath, AppOptions.DataDir);
            AppLog.Initialize(session.DataDirectory);
            RegisterGlobalExceptionLogging();
            AppLog.Info($"Starting {AppInfo.Name} {AppInfo.Version}; database={session.Database.Path}");
            LocalizationService.SetLanguage(session.Settings.Language);
            AppThemes.Apply(session.Settings.Theme);
            desktop.MainWindow = new MainWindow(session);
            desktop.Exit += (_, _) =>
            {
                AppLog.Info("Exiting");
                session.Dispose();
            };
        }
        catch (Exception exception)
        {
            startupFailed = true;
            session?.Dispose();
            try
            {
                var logDirectory = string.IsNullOrWhiteSpace(AppOptions.DatabasePath)
                    ? AppPaths.ResolveDataDir(AppOptions.DataDir)
                    : Path.GetDirectoryName(Path.GetFullPath(AppOptions.DatabasePath))
                      ?? AppPaths.ResolveDataDir(AppOptions.DataDir);
                AppLog.Initialize(logDirectory);
                RegisterGlobalExceptionLogging();
            }
            catch (Exception)
            {
            }
            AppLog.Error("TimeReport could not open its database.", exception);
            var alreadyRunning = exception is SingleInstanceException;
            var errorWindow = new Window
            {
                Title = "TimeReport",
                Width = 640,
                Height = 240,
                Content = new TextBlock
                {
                    Text = alreadyRunning
                        ? LocalizationService.T("MessageAlreadyRunning")
                        : $"TimeReport could not open its database.\n\n{exception.Message}",
                    Margin = new Thickness(24),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
            };
            desktop.MainWindow = errorWindow;
        }

        if (AppOptions.SmokeTest && desktop.MainWindow is not null)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                desktop.Shutdown(startupFailed ? 1 : 0);
            };
            timer.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool _globalHandlersRegistered;

    private static void RegisterGlobalExceptionLogging()
    {
        if (_globalHandlersRegistered)
        {
            return;
        }
        _globalHandlersRegistered = true;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled application exception.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
