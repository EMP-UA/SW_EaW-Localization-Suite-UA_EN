// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

namespace EaWLocalizationTool.UI;

/// <summary>
/// UA: Керування локалізацією інтерфейсу самої програми.
/// EN: Management of the application's own interface localization.
/// </summary>
public static class Localizer
{
    /// <summary>
    /// UA: Перемикач мови інтерфейсу: true = Українська, false = English.
    /// EN: Interface language switcher: true = Ukrainian, false = English.
    /// </summary>
    public static bool UseUkrainian = true;

    public static string Get(string ua, string en) => UseUkrainian ? ua : en;

    public static string MenuHeader => Get("=== STAR WARS: EMPIRE AT WAR ІНСТРУМЕНТ ЛОКАЛІЗАЦІЇ ===", "=== STAR WARS: EMPIRE AT WAR LOCALIZATION TOOL ===");
    public static string MenuStep1 => Get("1. [Експорт] OG -> BeforeAPI (Створення таблиць)", "1. [Export] OG -> BeforeAPI (Create tables)");
    public static string MenuStep2 => Get("2. [Огляд] BeforeAPI + AfterAPI -> Review (Зіставлення)", "2. [Review] BeforeAPI + AfterAPI -> Review (Merge)");
    public static string MenuStep3 => Get("3. [Пакування] Review -> Final (Створення файлів гри)", "3. [Pack] Review -> Final (Generate game files)");
    public static string MenuDebug => Get("4. [Debug] Повний цикл (OG -> Final) для тесту сумісності", "4. [Debug] Full cycle (OG -> Final) for compatibility test");
    public static string MenuExit => Get("0. Вихід", "0. Exit");
    
    // UA/EN: Оновлено текст відповідно до нової логіки PathManager
    public static string InfoPaths => Get("INFO: Робочі папки створюються автоматично у папці Workspace", "INFO: Working directories are created automatically in the Workspace folder");
    
    public static string SelectOption => Get("Оберіть дію: ", "Select an option: ");
    public static string OpCompleted => Get("Операція завершена успішно!", "Operation completed successfully!");
    public static string PressAnyKey => Get("Натисніть будь-яку клавішу...", "Press any key to continue...");
    public static string Error => Get("Помилка", "Error");
}