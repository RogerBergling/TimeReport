using Avalonia;

namespace TimeReport;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--version":
                    Console.WriteLine($"{AppInfo.Name} {AppInfo.Version}");
                    return 0;
                case "--data-dir" when index + 1 < args.Length:
                    AppOptions.DataDir = args[++index];
                    break;
                case "--database" when index + 1 < args.Length:
                    AppOptions.DatabasePath = args[++index];
                    break;
                case "--smoke-test":
                    AppOptions.SmokeTest = true;
                    break;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
