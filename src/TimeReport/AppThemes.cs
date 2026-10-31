using Avalonia;
using Avalonia.Styling;

namespace TimeReport;

public static class AppThemes
{
    public static readonly ThemeVariant HighContrast = new("HighContrast", ThemeVariant.Dark);

    public static void Apply(string? theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = theme?.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            "highcontrast" => HighContrast,
            _ => ThemeVariant.Default,
        };
    }
}
