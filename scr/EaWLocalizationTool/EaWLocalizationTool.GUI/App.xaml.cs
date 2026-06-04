using System.Windows;

namespace EaWLocalizationTool.GUI;

/// <summary>
/// UA: Точка входу GUI застосунку.
///     Завантажує збережену тему/шрифт при старті, зберігає при виході.
/// EN: GUI application entry point.
///     Loads saved theme/font on startup, saves on exit.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.LoadAndApply();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.SaveSettings();
        base.OnExit(e);
    }
}
