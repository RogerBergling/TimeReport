namespace TimeReport.Models;

public sealed record Totals(
    int WorkedMinutes = 0,
    int LunchMinutes = 0,
    int AwayMinutes = 0,
    int XPauseMinutes = 0,
    int DaysWorked = 0,
    int DaysAway = 0)
{
    public static Totals operator +(Totals left, Totals right) => new(
        left.WorkedMinutes + right.WorkedMinutes,
        left.LunchMinutes + right.LunchMinutes,
        left.AwayMinutes + right.AwayMinutes,
        left.XPauseMinutes + right.XPauseMinutes,
        left.DaysWorked + right.DaysWorked,
        left.DaysAway + right.DaysAway);
}
