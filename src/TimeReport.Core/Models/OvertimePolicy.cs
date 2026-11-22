namespace TimeReport.Models;

public sealed record OvertimePolicy(
    int DailyThresholdMinutes = 0,
    int WeeklyThresholdMinutes = 0)
{
    public static OvertimePolicy Disabled { get; } = new();

    public bool IsEnabled =>
        DailyThresholdMinutes > 0 || WeeklyThresholdMinutes > 0;
}
