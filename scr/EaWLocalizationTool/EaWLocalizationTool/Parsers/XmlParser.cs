// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using EaWLocalizationTool.Models;

namespace EaWLocalizationTool.Parsers;

/// <summary>
/// UA: Парсер для XML. Використовує XPath для точної ідентифікації тегів.
/// EN: Parser for XML. Uses XPath for precise tag identification.
/// </summary>
public class XmlParser
{
    public List<TextEntry> Parse(string absolutePath, string relativePath)
    {
        var entries = new List<TextEntry>();
        try
        {
            var doc = XDocument.Load(absolutePath);
            foreach (var element in doc.Descendants().Where(e => !e.HasElements && !string.IsNullOrWhiteSpace(e.Value)))
            {
                string val = element.Value.Trim();
                // UA: Пропускаємо числа та логічні значення / EN: Skip numbers and booleans
                if (IsTechnical(val)) continue;

                entries.Add(new TextEntry
                {
                    FilePath = relativePath,
                    FileType = "XML",
                    Key = GetXPath(element),
                    OriginalText = val
                });
            }
        }
        catch { /* UA: Ігноруємо помилки структури / EN: Ignore structure errors */ }
        return entries;
    }

    private string GetXPath(XElement element)
    {
        var ancestors = element.Ancestors().Select(e => $"{e.Name.LocalName}[{e.ElementsBeforeSelf(e.Name).Count() + 1}]").Reverse();
        return "/" + string.Join("/", ancestors) + $"/{element.Name.LocalName}[{element.ElementsBeforeSelf(element.Name).Count() + 1}]";
    }

    private bool IsTechnical(string val) =>
        double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _) ||
        bool.TryParse(val, out _);
}