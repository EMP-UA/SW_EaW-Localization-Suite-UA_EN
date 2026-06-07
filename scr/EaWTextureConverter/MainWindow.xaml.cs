using System.Windows;
using System.Windows.Controls;
using EaWTextureConverter.ViewModels;

namespace EaWTextureConverter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // UA: Застосовуємо темну тему за замовчуванням
        // EN: Apply dark theme by default
        ApplyTheme(isDark: true);

        // UA: Слухаємо зміну теми у ViewModel
        // EN: Listen for theme change in ViewModel
        if (DataContext is MainViewModel vm)
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
                    ApplyTheme(vm.IsDarkTheme);
            };
    }

    // UA: Клік по заголовку колонки — передаємо у ViewModel для сортування
    // EN: Column header click — forward to ViewModel for sorting
    private void FileListView_HeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header
            && header.Column is not null
            && DataContext is MainViewModel vm)
        {
            string? tag = header.Column.Header as string;
            if (tag is null) return;

            // UA: Визначаємо колонку по тексту заголовку
            // EN: Identify column by header text
            string column = tag switch
            {
                var s when s.Contains("Мод") || s.Contains("Mod") => "Mod",
                var s when s.Contains("Файл") || s.Contains("File") => "File",
                var s when s.Contains("Шлях") || s.Contains("Path") => "Path",
                var s when s.Contains("Статус") || s.Contains("Status") => "Status",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(column))
                vm.SortByCommand.Execute(column);
        }
    }

    // UA: Перемикає між темною і світлою темою через DynamicResource
    // EN: Switches between dark and light theme via DynamicResource
    private void ApplyTheme(bool isDark)
    {
        var dict = isDark
            ? (ResourceDictionary)Resources["DarkTheme"]
            : (ResourceDictionary)Resources["LightTheme"];

        // UA: Копіюємо всі ресурси теми у кореневий словник вікна
        // EN: Copy all theme resources into the window's root dictionary
        foreach (var key in dict.Keys)
            Resources[key] = dict[key];
    }
}