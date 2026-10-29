using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void EnglishAndSwedishHaveTheSameKeys()
    {
        foreach (var key in LocalizationService.Keys)
        {
            Assert.True(
                LocalizationService.HasTranslation(key, "en"),
                $"Missing English translation: {key}");
            Assert.True(
                LocalizationService.HasTranslation(key, "sv"),
                $"Missing Swedish translation: {key}");
        }
    }

    [Fact]
    public void SwedishCalendarFormatting()
    {
        try
        {
            LocalizationService.SetLanguage("sv");
            Assert.Equal("måndag 26 oktober 2026", TimeUtil.FormatDateLong(new DateOnly(2026, 10, 26)));
            Assert.Equal("mån 26 okt", TimeUtil.FormatDayShort(new DateOnly(2026, 10, 26)));
            Assert.Equal("oktober", TimeUtil.FormatMonthName(new DateOnly(2026, 10, 26)));
            Assert.Equal("26 okt – 1 nov 2026", TimeUtil.FormatWeekRange(
                new DateOnly(2026, 10, 26),
                new DateOnly(2026, 11, 1)));
        }
        finally
        {
            LocalizationService.SetLanguage("en");
        }
    }

    [Fact]
    public void LanguageSwitchUpdatesStrings()
    {
        try
        {
            LocalizationService.SetLanguage("sv");
            Assert.Equal("Vecka", LocalizationService.T("LabelWeekPrefix"));
            Assert.Equal("Arbetat", LocalizationService.T("LabelWorked"));
            Assert.Equal("X-Paus", LocalizationService.T("LabelXPause"));
            Assert.Equal("Idag", LocalizationService.T("LabelToday"));

            LocalizationService.SetLanguage("en");
            Assert.Equal("Week", LocalizationService.T("LabelWeekPrefix"));
            Assert.Equal("Worked", LocalizationService.T("LabelWorked"));
            Assert.Equal("X-Pause", LocalizationService.T("LabelXPause"));
            Assert.Equal("Today", LocalizationService.T("LabelToday"));
        }
        finally
        {
            LocalizationService.SetLanguage("en");
        }
    }

    [Fact]
    public void UnknownLanguageFallsBackToEnglish()
    {
        try
        {
            LocalizationService.SetLanguage("de");
            Assert.Equal("en", LocalizationService.CurrentLanguage);
            Assert.Equal("Worked", LocalizationService.T("LabelWorked"));
        }
        finally
        {
            LocalizationService.SetLanguage("en");
        }
    }
}
