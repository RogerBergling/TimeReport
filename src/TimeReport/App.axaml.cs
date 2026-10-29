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
        try
        {
            session = new AppSession(AppOptions.DatabasePath, AppOptions.DataDir);
            LocalizationService.SetLanguage(session.Settings.Language);
            desktop.MainWindow = new MainWindow(session);
            desktop.Exit += (_, _) => session.Dispose();
        }
        catch (Exception exception)
        {
            session?.Dispose();
            var errorWindow = new Window
            {
                Title = "TimeReport",
                Width = 640,
                Height = 240,
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
