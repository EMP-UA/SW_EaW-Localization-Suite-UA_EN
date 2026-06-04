using System;
using System.Windows;
using System.Windows.Threading;
using EaWLocalizationTool.GUI.Services;

namespace EaWLocalizationTool.GUI;

/// <summary>
/// UA: Точка входу GUI застосунку.
///     Завантажує збережену тему/шрифт при старті, зберігає при виході.
///     Відловлює глобальні помилки для логування.
/// EN: GUI application entry point.
///     Loads saved theme/font on startup, saves on exit.
///     Catches global exceptions for logging.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UA: Підключення глобальних обробників помилок
        // EN: Connecting global exception handlers
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        SimpleLogger.Log("=== UA: Запуск програми / EN: Application Started ===");
        ThemeManager.LoadAndApply();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SimpleLogger.LogError(e.Exception, "UI Thread Crash");
        MessageBox.Show(
            $"UA: Критична помилка UI. Деталі у logs/app.log\n" +
            $"EN: Critical UI error. Details in logs/app.log\n\n{e.Exception.Message}",
            "UA: Помилка / EN: Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            SimpleLogger.LogError(ex, "Background Thread Crash");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SimpleLogger.Log("=== UA: Вихід з програми / EN: Application Exited ===");
        ThemeManager.SaveSettings();
        base.OnExit(e);
    }
}