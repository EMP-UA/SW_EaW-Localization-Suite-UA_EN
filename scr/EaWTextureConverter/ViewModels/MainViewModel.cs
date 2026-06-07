using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaWTextureConverter.Models;
using EaWTextureConverter.Services;
using Microsoft.Win32;

namespace EaWTextureConverter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DdsConverterService _converter = new();

    // ── Стан / State ────────────────────────────────────────────────────────

    [ObservableProperty] private string _rootPath = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isUkrainian = true;
    [ObservableProperty] private string _outputPath = string.Empty;
    [ObservableProperty] private bool _useExeOutput = true;

    // UA: Прапорець теми — true = темна, false = світла
    // EN: Theme flag — true = dark, false = light
    [ObservableProperty] private bool _isDarkTheme = true;

    private string _sortColumn = string.Empty;
    private bool _sortAscending = true;

    // UA: Стан чекбоксу "вибрати всі" — при зміні застосовується до всіх файлів
    // EN: State of "select all" checkbox — change propagates to all files
    private bool _allSelected = true;
    public bool AllSelected
    {
        get => _allSelected;
        set
        {
            if (SetProperty(ref _allSelected, value))
                foreach (var f in Files)
                    f.IsSelected = value;
        }
    }

    public ObservableCollection<TextureFile> Files { get; } = [];

    public MainViewModel()
    {
        // UA: Встановлюємо дефолтну папку виводу біля exe при старті
        // EN: Set default output folder next to exe on startup
        OutputPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "output");
    }

    // ── Локалізація / Localization ──────────────────────────────────────────

    public string LabelRootPath => IsUkrainian ? "Папка моду:" : "Mod folder:";
    public string LabelBrowse => IsUkrainian ? "Огляд..." : "Browse...";
    public string LabelScanAll => IsUkrainian ? "Сканувати всі DDS" : "Scan all DDS";
    public string LabelAddManual => IsUkrainian ? "Додати файл вручну" : "Add file manually";
    public string LabelToPng => IsUkrainian ? "→ PNG (для редагування)" : "→ PNG (for editing)";
    public string LabelToDds => IsUkrainian ? "→ DDS (назад у гру)" : "→ DDS (back to game)";
    public string LabelOutputPath => IsUkrainian ? "Папка виводу:" : "Output folder:";
    public string LabelUseExe => IsUkrainian ? "Біля exe" : "Next to exe";
    public string LabelChooseOutput => IsUkrainian ? "Обрати..." : "Choose...";
    public string LabelColMod => IsUkrainian ? "Мод ↕" : "Mod ↕";
    public string LabelColFile => IsUkrainian ? "Файл ↕" : "File ↕";
    public string LabelColPath => IsUkrainian ? "Шлях ↕" : "Path ↕";
    public string LabelColStatus => IsUkrainian ? "Статус ↕" : "Status ↕";

    // UA: Підпис чекбоксу "вибрати всі" у тулбарі — прив'язаний у XAML як LabelSelectAll
    // EN: Label for "select all" checkbox in toolbar — bound in XAML as LabelSelectAll
    public string LabelSelectAll => IsUkrainian ? "Вибрати всі" : "Select all";

    public string WindowTitle => "EaW Texture Converter — EMP_UA";

    partial void OnIsUkrainianChanged(bool value)
    {
        RefreshLabels();

        // UA: Перечитуємо статуси — вони теж залежать від мови
        // EN: Re-read statuses — they are also language-dependent
        foreach (var f in Files)
            RefreshPngStatus(f);
    }

    partial void OnUseExeOutputChanged(bool value)
    {
        if (value)
        {
            // UA: Повертаємо стандартний шлях виводу біля exe
            // EN: Restore default output path next to exe
            OutputPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "output");
        }
    }

    partial void OnOutputPathChanged(string value)
    {
        // UA: При зміні папки виводу — оновлюємо статуси всіх файлів
        // EN: When output folder changes — refresh statuses for all files
        foreach (var f in Files)
            RefreshPngStatus(f);
    }

    // UA: Сповіщає UI про зміну всіх label-properties при перемиканні мови
    // EN: Notifies UI about all label-property changes when language is toggled
    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(LabelRootPath));
        OnPropertyChanged(nameof(LabelBrowse));
        OnPropertyChanged(nameof(LabelScanAll));
        OnPropertyChanged(nameof(LabelAddManual));
        OnPropertyChanged(nameof(LabelToPng));
        OnPropertyChanged(nameof(LabelToDds));
        OnPropertyChanged(nameof(LabelOutputPath));
        OnPropertyChanged(nameof(LabelUseExe));
        OnPropertyChanged(nameof(LabelChooseOutput));
        OnPropertyChanged(nameof(LabelColMod));
        OnPropertyChanged(nameof(LabelColFile));
        OnPropertyChanged(nameof(LabelColPath));
        OnPropertyChanged(nameof(LabelColStatus));
        OnPropertyChanged(nameof(LabelSelectAll));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(AllSelected));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    // UA: Перевіряє чи є вже готовий PNG у папці виводу для даного DDS
    // EN: Checks if a ready PNG already exists in output folder for this DDS
    private void RefreshPngStatus(TextureFile file)
    {
        string pngPath = OutputPathResolver.Resolve(file.FullPath, OutputPath, ".png");
        if (File.Exists(pngPath))
        {
            file.Status = IsUkrainian ? "PNG готовий" : "PNG ready";
            file.HasError = false;
        }
        else
        {
            file.Status = "—";
            file.HasError = false;
        }
    }

    // ── Команди / Commands ─────────────────────────────────────────────────

    [RelayCommand]
    private void BrowseFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = IsUkrainian ? "Виберіть папку моду" : "Select mod folder"
        };
        if (dlg.ShowDialog() == true)
        {
            RootPath = dlg.FolderName;
            ScanAllDds();
        }
    }

    [RelayCommand]
    private void ChooseOutputFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = IsUkrainian ? "Виберіть папку для збереження" : "Select output folder"
        };
        if (dlg.ShowDialog() == true)
        {
            OutputPath = dlg.FolderName;
            UseExeOutput = false;
        }
    }

    [RelayCommand]
    private void ScanAllDds()
    {
        if (string.IsNullOrWhiteSpace(RootPath)) return;

        Files.Clear();
        var found = FileScanner.ScanDirectory(RootPath);
        foreach (var f in found)
        {
            Files.Add(f);
            // UA: Одразу перевіряємо чи є PNG у папці виводу
            // EN: Immediately check if PNG exists in output folder
            RefreshPngStatus(f);
        }

        StatusMessage = IsUkrainian
            ? $"Знайдено {Files.Count} DDS файлів"
            : $"Found {Files.Count} DDS files";
    }

    [RelayCommand]
    private void AddManualFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = IsUkrainian ? "Виберіть DDS файл" : "Select DDS file",
            Filter = "DDS files (*.dds)|*.dds|All files (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;

        foreach (string path in dlg.FileNames)
        {
            // UA: Пропускаємо дублікати — порівняння без урахування регістру
            // EN: Skip duplicates — case-insensitive comparison
            if (Files.Any(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var file = new TextureFile
            {
                FullPath = path,
                RelativePath = path,
                ModLabel = OutputPathResolver.DetectModLabel(path),
            };
            Files.Add(file);
            RefreshPngStatus(file);
        }
    }

    // UA: Сортування по колонці — повторний клік змінює напрям
    // EN: Sort by column — repeated click toggles direction
    [RelayCommand]
    private void SortBy(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        var sorted = (_sortAscending
            ? Files.OrderBy(f => GetSortKey(f, column))
            : Files.OrderByDescending(f => GetSortKey(f, column)))
            .ToList();

        Files.Clear();
        foreach (var f in sorted)
            Files.Add(f);
    }

    private static string GetSortKey(TextureFile f, string column) => column switch
    {
        "Mod" => f.ModLabel,
        "File" => f.FileName,
        "Path" => f.RelativePath,
        "Status" => f.Status,
        _ => f.FileName,
    };

    // UA: Пакетна конвертація DDS → PNG зі збереженням структури папок
    // EN: Batch DDS → PNG preserving folder structure
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ConvertToPngAsync()
    {
        var selected = Files.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (!ConfirmOutput(selected.Count)) return;

        IsBusy = true;
        int done = 0;

        foreach (var file in selected)
        {
            if (!File.Exists(file.FullPath))
            {
                SetError(file, IsUkrainian ? "Файл не знайдено" : "File not found");
                done++; continue;
            }

            try
            {
                string outPng = OutputPathResolver.Resolve(file.FullPath, OutputPath, ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(outPng)!);

                await Task.Run(() => _converter.DdsToPng(file.FullPath, outPng));

                RefreshPngStatus(file);
            }
            catch (Exception ex)
            {
                SetError(file, ex.Message);
            }

            done++;
            Progress = (double)done / selected.Count * 100;
            StatusMessage = IsUkrainian
                ? $"Конвертовано {done}/{selected.Count}"
                : $"Converted {done}/{selected.Count}";
        }

        IsBusy = false;
        StatusMessage = IsUkrainian
            ? $"Готово! PNG збережено у: {OutputPath}"
            : $"Done! PNG saved to: {OutputPath}";
    }

    // UA: Пакетна конвертація PNG → DDS (шукає PNG у папці виводу)
    // EN: Batch PNG → DDS (looks for PNG in output folder)
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ConvertToDdsAsync()
    {
        var selected = Files.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (!ConfirmOutput(selected.Count)) return;

        IsBusy = true;
        int done = 0;

        foreach (var file in selected)
        {
            string pngPath = OutputPathResolver.Resolve(file.FullPath, OutputPath, ".png");

            if (!File.Exists(pngPath))
            {
                SetError(file, IsUkrainian
                    ? "PNG не знайдено — спочатку конвертуйте у PNG"
                    : "PNG not found — convert to PNG first");
                done++; continue;
            }

            try
            {
                string outDds = OutputPathResolver.Resolve(file.FullPath, OutputPath, ".dds");
                Directory.CreateDirectory(Path.GetDirectoryName(outDds)!);

                await _converter.PngToDdsAsync(pngPath, outDds, ct: CancellationToken.None);

                file.Status = IsUkrainian ? "DDS збережено" : "DDS saved";
                file.HasError = false;
            }
            catch (Exception ex)
            {
                SetError(file, ex.Message);
            }

            done++;
            Progress = (double)done / selected.Count * 100;
            StatusMessage = IsUkrainian
                ? $"Оброблено {done}/{selected.Count}"
                : $"Processed {done}/{selected.Count}";
        }

        IsBusy = false;
        StatusMessage = IsUkrainian
            ? $"Готово! DDS збережено у: {OutputPath}"
            : $"Done! DDS saved to: {OutputPath}";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private bool ConfirmOutput(int count)
    {
        string msg = IsUkrainian
            ? $"Буде оброблено файлів: {count}\n\nВихідна папка:\n{OutputPath}\n\nСтруктура підпапок збережеться автоматично.\n\nПродовжити?"
            : $"Files to process: {count}\n\nOutput folder:\n{OutputPath}\n\nSubfolder structure will be preserved.\n\nContinue?";

        return MessageBox.Show(
            msg,
            IsUkrainian ? "Підтвердження" : "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static void SetError(TextureFile file, string message)
    {
        file.Status = message;
        file.HasError = true;
    }

    // UA: Команди конвертації доступні тільки коли не йде інша операція
    // EN: Conversion commands are only available when no other operation is running
    private bool CanRun() => !IsBusy;
}