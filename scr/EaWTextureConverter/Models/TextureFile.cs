using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace EaWTextureConverter.Models;

// UA: Модель одного DDS файлу у списку
// EN: Model for a single DDS file in the list
public partial class TextureFile : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private string _status = "—";
    [ObservableProperty] private bool _hasError;

    public string FullPath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FullPath);
    public string RelativePath { get; init; } = string.Empty;

    // UA: Звідки файл — основна гра чи аддон
    // EN: Which mod this file belongs to
    public string ModLabel { get; init; } = string.Empty;
}