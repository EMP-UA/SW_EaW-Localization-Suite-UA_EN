// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.IO;

namespace EaWLocalizer.Core;

public enum ModelTier { FreeLite, PaidFlash }

public static class Config
{
    // ── PATHS (UA: Відносні шляхи / EN: Relative paths) ──
    private static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string WorkspaceDir = Path.Combine(BasePath, "Workspace");
    
    public static readonly string OriginalDir = Path.Combine(WorkspaceDir, "OG");
    public static readonly string BeforeApiDir = Path.Combine(WorkspaceDir, "BeforeAPI");
    public static readonly string AfterApiDir = Path.Combine(WorkspaceDir, "AfterAPI");

    public static readonly string ApiKeyPath = Path.Combine(BasePath, "api_key.txt");

    public const string MasterTxtBefore = "!MASTER_BEFORE_API_TXT.tsv";
    public const string MasterTxtAfter = "!MASTER_AFTER_API_TXT.tsv";

    public static readonly string LogPath = Path.Combine(AfterApiDir, "_errors.txt");
    public static readonly string ReviewDir = Path.Combine(AfterApiDir, "_review");
    public static readonly string GlobalCachePath = Path.Combine(AfterApiDir, "_translation_cache.txt");
    public static readonly string RpdCounterPath = Path.Combine(AfterApiDir, "_rpd_counter.txt");
    public static readonly string RetryCountsPath = Path.Combine(AfterApiDir, "_retry_counts.txt");
    public static readonly string RpmLogPath = Path.Combine(AfterApiDir, "_rpm_log.txt");

    // ── DYNAMIC SETTINGS ──
    public static ModelTier SelectedTier { get; private set; } = ModelTier.FreeLite;
    public static string Model { get; private set; } = "gemini-3.1-flash-lite-preview"; 
    public static double DesiredRpm { get; private set; } = 12.0;
    public static double SafetyFactor { get; private set; } = 0.90;
    public static int MaxRpd { get; private set; } = 490;
    public static int BatchSize { get; private set; } = 5;

    public static double EmergencyRpmCeiling => DesiredRpm;
    public static readonly TimeSpan EmergencyBackoff = TimeSpan.FromSeconds(45);
    public const int MaxRetries = 5;
    public const int MaxTextLength = 2000;
    public const int MaxAutoRetries = 2;
    public const int MaxCacheEntries = 200_000;

    public static void ApplyProfile(ModelTier tier)
    {
        SelectedTier = tier;
        if (tier == ModelTier.PaidFlash)
        {
            Model = "gemini-2.5-flash";
            DesiredRpm = 120.0;
            SafetyFactor = 0.95;
            MaxRpd = 50000;
            BatchSize = 25;
        }
        else
        {
            Model = "gemini-3.1-flash-lite-preview";
            DesiredRpm = 12.0;
            SafetyFactor = 0.90;
            MaxRpd = 490;
            BatchSize = 5;
        }
    }

    public static string GetApiKey()
    {
        if (!File.Exists(ApiKeyPath))
        {
            throw new FileNotFoundException($"API Key file not found! Please create '{ApiKeyPath}' and paste your Gemini API key inside.");
        }
        return File.ReadAllText(ApiKeyPath).Trim();
    }

    public const string Glossary =
        "Empire=Імперія, Rebels=Повстанці, Millennium Falcon=Тисячолітній Сокіл, " +
        "Darth=Дарт, Sith=Сітхи, Jedi=Джедаї, Star Destroyer=Зоряний руйнівник, " +
        "Credits=Кредити, Stormtroopers=Штурмовики, Hyperdrive=Гіпердвигун, " +
        "Squadron=Ескадрилья, Frigate=Фрегат, Cruiser=Крейсер, Capital Ship=Капітальний корабель, " +
        "Shield Generator=Генератор щитів, Death Star=Зірка Смерті, Underworld=Синдикат, " +
        "Looter=Збирач"; 
}