using System.Globalization;
using System.Text.RegularExpressions;

namespace TimeReport.Services;

public static class TimeUtil
{
    public const int MinutesPerDay = 24 * 60;
    public const int XPauseWeeklyLimit = 60;
    public const int XPauseStep = 15;

    public static readonly string[] WeekdayNames =
    [
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
    ];

    public static readonly string[] WeekdayShort =
    [
        "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat",
    ];

    public static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    private static readonly Regex TimeRegex = new(@"^(\d{1,2}):(\d{2})$", RegexOptions.Compiled);

    public static int ParseTime(string value, bool allowEndOfDay = true)
    {
        var text = (value ?? string.Empty).Trim();
        var match = TimeRegex.Match(text);
        if (!match.Success)
        {
            throw new ValidationException("Enter a time as HH:MM using the 24-hour clock.");
        }

        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        if (hour == 24 && minute == 0)
        {
            if (!allowEndOfDay)
            {
                throw new ValidationException("24:00 can only be used as an end time.");
            }
            return MinutesPerDay;
        }

        if (hour > 23 || minute > 59)
        {
            throw new ValidationException("Enter a time as HH:MM using the 24-hour clock.");
        }

        return hour * 60 + minute;
    }

    public static string FormatTime(int minutes)
    {
        if (minutes == MinutesPerDay)
        {
            return "24:00";
        }
        if (minutes is < 0 or > MinutesPerDay)
        {
            throw new ValidationException("Time must be between 00:00 and 24:00.");
        }
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    public static string FormatDuration(int minutes)
    {
        if (minutes < 0)
        {
            throw new ValidationException("Duration cannot be negative.");
        }
        return $"{minutes / 60}:{minutes % 60:00}";
    }

    public static DateOnly ParseDate(string value)
    {
        if (!DateOnly.TryParseExact(
                (value ?? string.Empty).Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var day))
        {
            throw new ValidationException("Enter a date as YYYY-MM-DD.");
        }
        return day;
    }

    public static DateOnly WeekStart(DateOnly day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-offset);
    }

    public static DateOnly WeekEnd(DateOnly day) => WeekStart(day).AddDays(6);

    public static int WeekNumber(DateOnly day) => ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue));

    public static (DateOnly Start, DateOnly End) MonthBounds(DateOnly day)
    {
        var start = new DateOnly(day.Year, day.Month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    public static (DateOnly Start, DateOnly End) YearBounds(DateOnly day) =>
        (new DateOnly(day.Year, 1, 1), new DateOnly(day.Year, 12, 31));

    public static DateOnly AddMonths(DateOnly day, int months) => day.AddMonths(months);

    public static IEnumerable<DateOnly> DateRangeDays(DateOnly start, DateOnly end)
    {
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    public static string FormatDateLong(DateOnly day) =>
        $"{WeekdayNames[(int)day.DayOfWeek]} {day.Day} {MonthNames[day.Month - 1]} {day.Year}";

    public static string FormatDayShort(DateOnly day) =>
        $"{WeekdayShort[(int)day.DayOfWeek]} {day.Day} {MonthNames[day.Month - 1][..3]}";

    public static string FormatMonthName(DateOnly day) => MonthNames[day.Month - 1];

    public static string FormatWeekRange(DateOnly start, DateOnly end) =>
        $"{start.Day} {MonthNames[start.Month - 1][..3]} – {end.Day} {MonthNames[end.Month - 1][..3]} {end.Year}";

    public static List<(int Start, int End)> MergeIntervals(IEnumerable<(int Start, int End)> intervals)
    {
        var ordered = intervals.Where(item => item.End > item.Start).OrderBy(item => item.Start).ToList();
        if (ordered.Count == 0)
        {
            return [];
        }

        var merged = new List<(int Start, int End)> { ordered[0] };
        foreach (var (start, end) in ordered.Skip(1))
        {
            var current = merged[^1];
            if (start <= current.End)
            {
                merged[^1] = (current.Start, Math.Max(current.End, end));
            }
            else
            {
                merged.Add((start, end));
            }
        }
        return merged;
    }

    public static List<(int Start, int End)> SubtractIntervals(
        IEnumerable<(int Start, int End)> baseIntervals,
        IEnumerable<(int Start, int End)> cuts)
    {
        var remaining = MergeIntervals(baseIntervals);
        foreach (var (cutStart, cutEnd) in MergeIntervals(cuts))
        {
            var next = new List<(int Start, int End)>();
            foreach (var (start, end) in remaining)
            {
                if (cutEnd <= start || cutStart >= end)
                {
                    next.Add((start, end));
                    continue;
                }
                if (start < cutStart)
                {
                    next.Add((start, cutStart));
                }
                if (cutEnd < end)
                {
                    next.Add((cutEnd, end));
                }
            }
            remaining = next;
            if (remaining.Count == 0)
            {
                break;
            }
        }
        return remaining;
    }

    public static int TotalCovered(IEnumerable<(int Start, int End)> intervals) =>
        MergeIntervals(intervals).Sum(item => item.End - item.Start);

    public static bool IntervalsOverlap(int aStart, int aEnd, int bStart, int bEnd) =>
        aStart < bEnd && bStart < aEnd;

    public static bool StrictlyInside(int innerStart, int innerEnd, int outerStart, int outerEnd) =>
        outerStart < innerStart && innerEnd < outerEnd;
}
