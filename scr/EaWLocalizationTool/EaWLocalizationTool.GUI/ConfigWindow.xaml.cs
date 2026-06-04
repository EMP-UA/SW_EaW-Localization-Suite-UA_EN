using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace EaWLocalizationTool.GUI;

/// <summary>
/// UA: Вікно конфігурації — нотатки шляхів до файлів.
///     Зберігає/завантажує налаштування як JSON файл.
/// EN: Configuration window — file path notes.
///     Saves/loads settings as a JSON file.
/// </summary>
public partial class ConfigWindow : Window
{
    public ConfigWindow() => InitializeComponent();

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter   = "JSON files (*.json)|*.json",
            FileName = "eaw_localizer_config.json",
            Title    = "Зберегти конфіг / Save config"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var cfg = new ConfigData
            {
                OriginalDat     = OrigPathBox.Text,
                TranslationFile = TransPathBox.Text,
                OutputDir       = OutputPathBox.Text,
                Notes           = NotesBox.Text,
                SavedAt         = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            string json = JsonSerializer.Serialize(cfg,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json, Encoding.UTF8);
            MessageBox.Show(
                "Конфіг збережено / Config saved",
                "✓", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Помилка / Error:\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title  = "Завантажити конфіг / Load config"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string json = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            var cfg = JsonSerializer.Deserialize<ConfigData>(json);
            if (cfg is null) return;

            OrigPathBox.Text    = cfg.OriginalDat     ?? "";
            TransPathBox.Text   = cfg.TranslationFile ?? "";
            OutputPathBox.Text  = cfg.OutputDir       ?? "";
            NotesBox.Text       = cfg.Notes           ?? "";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Помилка / Error:\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // UA: Модель для JSON серіалізації / EN: JSON serialization model
    private sealed class ConfigData
    {
        public string? OriginalDat     { get; set; }
        public string? TranslationFile { get; set; }
        public string? OutputDir       { get; set; }
        public string? Notes           { get; set; }
        public string? SavedAt         { get; set; }
    }
}
