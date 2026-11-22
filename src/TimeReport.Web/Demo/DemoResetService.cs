namespace TimeReport.Web.Demo;

/// <summary>
/// A public demo must not accumulate visitor changes forever. The process
/// stops after the configured interval; Docker restarts it, the startup reset
/// removes the tmpfs/database state and the seeder recreates the fake data.
/// </summary>
public sealed partial class DemoResetService : BackgroundService
{
    private readonly DemoInfo _demo;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DemoResetService> _logger;

    public DemoResetService(
        DemoInfo demo,
        IHostApplicationLifetime lifetime,
        ILogger<DemoResetService> logger)
    {
        _demo = demo;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_demo.Enabled)
        {
            return;
        }

        LogScheduled(_logger, _demo.ResetHours);
        try
        {
            await Task.Delay(TimeSpan.FromHours(_demo.ResetHours), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        LogResetting(_logger);
        _lifetime.StopApplication();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Demo mode is active; the process will restart and reseed in {Hours} hour(s).")]
    private static partial void LogScheduled(ILogger logger, int hours);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Demo reset interval reached; stopping the process so the container can restart with fresh fake data.")]
    private static partial void LogResetting(ILogger logger);
}
