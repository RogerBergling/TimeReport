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
                    Console.WriteLine($"{AppInfo.Name} {AppInfo.Version} (build {AppInfo.BuildNumber})");
                    return 0;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                case "--data-dir" when index + 1 < args.Length:
                    AppOptions.DataDir = args[++index];
                    break;
                case "--database" when index + 1 < args.Length:
                    AppOptions.DatabasePath = args[++index];
                    break;
                case "--data-dir" or "--database":
                    Console.Error.WriteLine($"Missing value for {args[index]}.");
                    PrintUsage();
                    return 2;
                case "--smoke-test":
                    AppOptions.SmokeTest = true;
                    break;
            }
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            TimeReport - local-first desktop time reporting

            Usage:
              TimeReport [options]

            Options:
              --database <path>   Use a specific SQLite database file.
              --data-dir <path>   Use a data folder (timereport.db inside it).
              --smoke-test        Open the UI briefly and exit.
              --version           Print the version and exit.
              -h, --help          Print this help and exit.
            """);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
