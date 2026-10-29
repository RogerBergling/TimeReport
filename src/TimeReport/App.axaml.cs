using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TimeReport.Data;
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

        var dataDir = AppPaths.ResolveDataDir(AppOptions.DataDir);
        var database = new Database(AppPaths.DatabasePath(dataDir));

        try
        {
            AppPaths.EnsureDataDir(dataDir);
            database.Open();
            var service = new TimeReportService(database);
            desktop.MainWindow = new MainWindow(service, dataDir);
            desktop.Exit += (_, _) => database.Dispose();
        }
        catch (Exception exception)
        {
            var errorWindow = new Window
            {
                Title = "TimeReport",
                Width = 640,
                Height = 220,
                Content = new TextBlock
                {
                    Text = $"TimeReport could not open its database.\n\n{exception.Message}",
                    Margin = new Thickness(24),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
            };
            desktop.MainWindow = errorWindow;
            if (AppOptions.SmokeTest)
            {
                desktop.Shutdown(1);
            }
        }

        if (AppOptions.SmokeTest && desktop.MainWindow is not null)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                desktop.Shutdown();
            };
            timer.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
