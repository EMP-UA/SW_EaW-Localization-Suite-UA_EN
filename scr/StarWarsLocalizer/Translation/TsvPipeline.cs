// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ============================================================
//  Translation/TsvPipeline.cs
//  UA: Головний оркестратор. Розбитий на дві незалежні фази:
//
//    Фаза 1 — ScanAsync() [БЕЗ API, БЕЗ КЛЮЧА]:
//      • Виявляє TSV-файли у BeforeAPI (DAT → TXT → XML).
//      • Відновлює прогрес із AfterAPI якщо файли вже існують.
//      • Застосовує кеш та фільтрує технічні рядки.
//      • Файли де нічого перекладати — зберігає одразу.
//      • Повертає ScanResult із чергою рядків що потребують API.
//
//    Фаза 2 — TranslateAsync(ScanResult) [ПОТРЕБУЄ API-КЛЮЧА]:
//      • Викликається лише якщо ScanResult.NeedsApi == true.
//      • Надсилає пакети у Gemini API та зберігає результати.
//
//  EN: Main orchestrator. Split into two independent phases:
//
//    Phase 1 — ScanAsync() [NO API, NO KEY]:
//      • Discovers TSV files in BeforeAPI (DAT → TXT → XML).
//      • Restores progress from AfterAPI if files already exist.
//      • Applies cache and filters technical strings.
//      • Files with nothing to translate are saved immediately.
//      • Returns ScanResult with the queue of rows needing API.
//
//    Phase 2 — TranslateAsync(ScanResult) [REQUIRES API KEY]:
//      • Called only if ScanResult.NeedsApi == true.
//      • Sends batches to the Gemini API and saves results.
// ============================================================

using System.Diagnostics;
using System.Text;
using EaWLocalizer.Core;
using EaWLocalizer.Model;
using EaWLocalizer.UI;

namespace EaWLocalizer.Translation;

// ════════════════════════════════════════════════════════════
//  SCAN RESULT + FILE ENTRY
// ════════════════════════════════════════════════════════════

/// <summary>
/// UA: Результат локальної фази сканування (ScanAsync).
///     Містить повністю підготовлену чергу файлів для API.
///     Якщо NeedsApi == false — API-ключ не потрібен взагалі.
/// EN: Result of the local scan phase (ScanAsync).
///     Contains the fully prepared file queue for the API.
///     If NeedsApi == false — no API key is needed at all.
/// </summary>
public class ScanResult
{
    /// <summary>UA: Файлів знайдено загалом. EN: Total files found.</summary>
    public int TotalFiles { get; init; }

    /// <summary>UA: Рядків покрито кешем. EN: Rows covered by cache.</summary>
    public int CacheHits { get; init; }

    /// <summary>UA: Технічних рядків пропущено. EN: Technical rows skipped.</summary>
    public int Technical { get; init; }

    /// <summary>UA: Унікальних рядків у черзі на API. EN: Unique strings queued for the API.</summary>
    public int UniqueStringsForApi { get; init; }

    /// <summary>UA: Файлів у черзі на API. EN: Files queued for the API.</summary>
    public int FilesNeedingApi => Queue.Count;

    /// <summary>UA: Чи потрібен API-ключ? EN: Is an API key needed?</summary>
    public bool NeedsApi => Queue.Count > 0;

    // UA: Внутрішня черга — видима лише в межах сборки Translation.
    // EN: Internal queue — visible only within the Translation assembly.
    internal List<FileEntry> Queue { get; init; } = new();
}

/// <summary>
/// UA: Один елемент черги на переклад.
///     Містить вже підготовлені рядки (resume + кеш застосовано),
///     та uniqueMap для dedup під час API-запитів.
/// EN: One translation queue entry.
///     Contains already-prepared rows (resume + cache applied),
///     and the uniqueMap for deduplication during API requests.
/// </summary>
internal class FileEntry
{
    public FileInfo File { get; init; } = null!;
    public string OutputPath { get; init; } = "";
    public List<TsvRow> Rows { get; init; } = new();

    // UA: uniqueMap: нормалізований текст → список рядків з цим текстом.
    //     Деdup: однаковий рядок перекладається один раз → результат копіюється в усі.
    // EN: uniqueMap: normalised text → list of rows with that text.
    //     Dedup: identical string is translated once → result copied to all.
    public Dictionary<string, List<TsvRow>> UniqueMap { get; init; } = new();
}

// ════════════════════════════════════════════════════════════
//  PIPELINE
// ════════════════════════════════════════════════════════════

/// <summary>
/// UA: Клас пайплайну. Один екземпляр на запуск програми.
///     Зберігає лічильники повторів між сесіями (persisted на диск).
/// EN: Pipeline class. One instance per program launch.
///     Retry counters are persisted to disk between sessions.
/// </summary>
public class TsvPipeline
{
    readonly Stopwatch _sw = Stopwatch.StartNew();
    readonly TranslationCache _cache = TranslationCache.Instance;
    readonly GeminiClient _api = GeminiClient.Instance;

    // UA: Лічильники повторів: ім'я файлу → кількість спроб.
    //     Захищають від нескінченного перекладу незворотно битих файлів.
    // EN: Retry counters: filename → number of attempts.
    //     Protect against infinite re-translation of irreparably broken files.
    readonly Dictionary<string, int> _retries = new(StringComparer.OrdinalIgnoreCase);

    // ── STATE PERSISTENCE ─────────────────────────────────────────────────

    /// <summary>UA: Завантажує лічильники повторів з диску. EN: Loads retry counters from disk.</summary>
    public void LoadState()
    {
        if (!File.Exists(Config.RetryCountsPath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(Config.RetryCountsPath))
            {
                var p = line.Split('|', 2);
                if (p.Length == 2 && int.TryParse(p[1], out int c))
                    _retries[p[0]] = c;
            }
        }
        catch { /* non-fatal */ }
    }

    /// <summary>UA: Зберігає лічильники повторів на диск. EN: Saves retry counters to disk.</summary>
    public void SaveState()
    {
        try
        {
            File.WriteAllLines(Config.RetryCountsPath,
                _retries.Select(kv => $"{kv.Key}|{kv.Value}"));
        }
        catch { /* non-fatal */ }
    }

    // ════════════════════════════════════════════════════════
    //  PHASE 1 — LOCAL SCAN  (no network, no API key)
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Фаза 1 — повністю локальна, мережа не потрібна.
    ///     Для кожного TSV-файлу:
    ///       1. Читає BeforeAPI рядки.
    ///       2. Відновлює вже збережений прогрес з AfterAPI.
    ///       3. Класифікує: технічний (→ копія оригіналу) /
    ///                      кеш (→ застосовує негайно) /
    ///                      потребує API (→ додає до черги).
    ///       4. Якщо нічого для API — записує AfterAPI одразу.
    ///     Після завершення: якщо ScanResult.NeedsApi == false,
    ///     програма може завершитись БЕЗ запиту ключа.
    ///
    /// EN: Phase 1 — fully local, no network needed.
    ///     For each TSV file:
    ///       1. Reads BeforeAPI rows.
    ///       2. Restores already-saved progress from AfterAPI.
    ///       3. Classifies: technical (→ copy original) /
    ///                      cache   (→ applies immediately) /
    ///                      needs API (→ adds to queue).
    ///       4. If nothing for API — writes AfterAPI immediately.
    ///     After completion: if ScanResult.NeedsApi == false,
    ///     the program can exit WITHOUT requesting the API key.
    /// </summary>
    // ── FILE LABEL HELPER ────────────────────────────────────────────────────

    /// <summary>
    /// UA: Формує читабельний ярлик файлу для виводу в консоль.
    ///     Показує першу значущу папку (GameData / corruption / тощо)
    ///     та ім'я файлу: "GameData » mastertextfile_english.dat.tsv"
    ///     Це дозволяє швидко зрозуміти — файл основної гри чи аддону.
    /// EN: Builds a readable file label for console output.
    ///     Shows the first meaningful folder (GameData / corruption / etc.)
    ///     and the filename: "GameData » mastertextfile_english.dat.tsv"
    ///     This lets you instantly tell — main game or expansion file.
    /// </summary>
    static string FormatFileLabel(FileInfo fi)
    {
        string rel = Path.GetRelativePath(Config.BeforeApiDir, fi.FullName);
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // UA: Якщо файл у підпапці — показуємо "RootFolder » filename".
        //     Якщо файл у корені BeforeAPI (наприклад !MASTER_TXT.tsv) — тільки ім'я.
        // EN: If file is in a subfolder — show "RootFolder » filename".
        //     If file is in BeforeAPI root (e.g. !MASTER_TXT.tsv) — name only.
        return parts.Length > 1
            ? $"{parts[0]} » {fi.Name}"
            : fi.Name;
    }

    public ScanResult ScanAsync()
    {
        Directory.CreateDirectory(Config.AfterApiDir);
        Directory.CreateDirectory(Config.ReviewDir);

        // ── Виявлення файлів / File discovery ────────────────────────────
        var allTsv = Directory.GetFiles(Config.BeforeApiDir, "*.tsv",
                                        SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .ToList();

        if (allTsv.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(string.Format(Locale.S("no_tsv"), Config.BeforeApiDir));
            Console.ResetColor();
            return new ScanResult();
        }

        // ── Упорядкування: DAT → TXT → XML ───────────────────────────────
        // UA: XML йде останнім: найбільше технічного тексту, найменше для перекладу.
        // EN: XML last: most technical text, fewest strings to translate.
        var orderedQueue = allTsv
            .Where(f => f.Name.EndsWith(".dat.tsv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Length)
            .Concat(allTsv.Where(f =>
                f.Name.Equals(Config.MasterTxtBefore, StringComparison.OrdinalIgnoreCase)))
            .Concat(allTsv
                .Where(f => f.Name.EndsWith(".xml.tsv", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Length))
            .ToList();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Locale.S("scan_phase"));
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(Locale.S("order_info"));
        Console.WriteLine(string.Format(Locale.S("counts"),
            orderedQueue.Count(f => f.Name.EndsWith(".dat.tsv", StringComparison.OrdinalIgnoreCase)),
            orderedQueue.Count(f => f.Name.Equals(Config.MasterTxtBefore, StringComparison.OrdinalIgnoreCase)),
            orderedQueue.Count(f => f.Name.EndsWith(".xml.tsv", StringComparison.OrdinalIgnoreCase))));
        Console.ResetColor();

        int totalCacheHits = 0, totalTechnical = 0, totalUniqueForApi = 0;
        var apiQueue = new List<FileEntry>();
        int total = orderedQueue.Count;

        for (int qi = 0; qi < total; qi++)
        {
            var fi = orderedQueue[qi];
            // UA: Зберігаємо відносний шлях з BeforeAPI щоб дзеркалювати структуру папок.
            //     Наприклад: BeforeAPI\GameData\Text\file.dat.tsv
            //            →  AfterAPI\GameData\Text\file.dat.tsv
            //     Це критично: GameData і corruption мають файли з однаковими іменами.
            // EN: Preserve the relative path from BeforeAPI to mirror the folder structure.
            //     Example: BeforeAPI\GameData\Text\file.dat.tsv
            //          →  AfterAPI\GameData\Text\file.dat.tsv
            //     Critical: GameData and corruption have files with identical names.
            string relPath = Path.GetRelativePath(Config.BeforeApiDir, fi.FullName);
            string outName = Path.GetFileName(relPath).Replace(
                Config.MasterTxtBefore, Config.MasterTxtAfter,
                StringComparison.OrdinalIgnoreCase);
            string outRelDir = Path.GetDirectoryName(relPath) ?? "";
            string outPath = Path.Combine(Config.AfterApiDir, outRelDir, outName);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(string.Format(Locale.S("file_header"), qi + 1, total, FormatFileLabel(fi)));
            Console.ResetColor();

            // ── Читання / Read ────────────────────────────────────────────
            var rows = TsvIo.ReadFile(fi.FullName);
            if (rows.Count == 0) { Console.WriteLine("  (empty)"); continue; }

            // ── Відновлення прогресу / Resume ─────────────────────────────
            // UA: Ключ: FilePath + "\x00" + Key — гарантовано унікальна пара.
            //     Нуль-символ обрано тому що він ніколи не зустрічається у шляхах.
            // EN: Key: FilePath + "\x00" + Key — guaranteed unique pair.
            //     Null char chosen because it never appears in file paths.
            if (File.Exists(outPath))
            {
                // UA: Використовуємо цикл замість ToDictionary —
                //     ToDictionary кидає ArgumentException якщо є рядки з порожнім
                //     або однаковим ключем (наприклад порожній FilePath або Key).
                //     Тут: останній запис перемагає (last-write-wins), без падінь.
                // EN: Use a loop instead of ToDictionary —
                //     ToDictionary throws ArgumentException on duplicate or empty keys
                //     (e.g. empty FilePath or Key in a malformed row).
                //     Here: last-write-wins, no exceptions.
                var saved = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var r in TsvIo.ReadFile(outPath))
                    if (!r.NeedsTranslation)
                        saved[$"{r.FilePath}\x00{r.Key}"] = r.TranslatedText;

                foreach (var row in rows.Where(r => r.NeedsTranslation))
                    if (saved.TryGetValue($"{row.FilePath}\x00{row.Key}", out var prev))
                        row.TranslatedText = prev;
            }

            // ── Класифікація / Classification ─────────────────────────────
            // UA: Рахуємо скільки рядків потребували перекладу ДО класифікації —
            //     щоб після розрізнити "суто технічний файл" від "вже перекладено".
            // EN: Count rows that needed translation BEFORE classification —
            //     so we can tell "purely technical file" from "already translated".
            int rowsNeedingTranslation = rows.Count(r => r.NeedsTranslation);
            int fileCacheHits = 0, fileTechnical = 0;
            var uniqueMap = new Dictionary<string, List<TsvRow>>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (!row.NeedsTranslation) continue;

                if (!Validator.IsTranslatable(row.OriginalText))
                {
                    // UA: Технічний рядок — копіюємо оригінал щоб пакувальник
                    //     отримав повний вміст файлу без порожніх комірок.
                    // EN: Technical string — copy original so the re-packer
                    //     receives complete file content with no empty cells.
                    row.TranslatedText = row.OriginalText;
                    fileTechnical++;
                    continue;
                }

                if (_cache.TryGet(row.OriginalText, out var cached))
                {
                    row.TranslatedText = cached;
                    fileCacheHits++;
                    continue;
                }

                // UA: Потребує API — групуємо для dedup (один запит на унікальний рядок).
                // EN: Needs API — group for dedup (one request per unique string).
                string normKey = _cache.NormaliseKey(row.OriginalText);
                if (!uniqueMap.TryGetValue(normKey, out var group))
                    uniqueMap[normKey] = group = new List<TsvRow>();
                group.Add(row);
            }

            totalCacheHits += fileCacheHits;
            totalTechnical += fileTechnical;

            Console.WriteLine(string.Format(Locale.S("rows_info"),
                rows.Count, uniqueMap.Count, fileTechnical, fileCacheHits));

            if (uniqueMap.Count == 0)
            {
                // UA: Все покрито локально — записуємо без API-запиту.
                //     Розрізняємо два випадки для точного повідомлення:
                //       • суто технічний файл (0 перекладних рядків взагалі)
                //       • вже перекладено (рядки є, але покриті кешем/resume)
                // EN: Everything covered locally — write without any API call.
                //     Distinguish two cases for an accurate message:
                //       • purely technical file (0 translatable strings at all)
                //       • already translated (strings exist but covered by cache/resume)
                TsvIo.WriteFile(outPath, rows);
                bool pureTechnical = fileCacheHits == 0 && rowsNeedingTranslation == fileTechnical;
                // UA: Зелений — є реально перекладений контент (кеш або resume).
                //     DarkGray — суто технічний файл, нічого перекладати не було.
                // EN: Green — file has actually translated content (cache or resume).
                //     DarkGray — purely technical file, nothing to translate at all.
                Console.ForegroundColor = pureTechnical ? ConsoleColor.DarkGray : ConsoleColor.Green;
                Console.WriteLine(pureTechnical
                    ? Locale.S("file_all_technical")
                    : Locale.S("file_skip"));
                Console.ResetColor();
            }
            else
            {
                // UA: Є рядки для API — додаємо до черги.
                // EN: Has rows for the API — add to the queue.
                totalUniqueForApi += uniqueMap.Count;
                apiQueue.Add(new FileEntry
                {
                    File = fi,
                    OutputPath = outPath,
                    Rows = rows,
                    UniqueMap = uniqueMap
                });
            }
        }

        return new ScanResult
        {
            TotalFiles = total,
            CacheHits = totalCacheHits,
            Technical = totalTechnical,
            UniqueStringsForApi = totalUniqueForApi,
            Queue = apiQueue
        };
    }

    // ════════════════════════════════════════════════════════
    //  PHASE 2 — API TRANSLATION  (requires API key)
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Фаза 2 — надсилає підготовлену чергу до Gemini API.
    ///     Викликається лише якщо scan.NeedsApi == true.
    ///     Прогрес зберігається після КОЖНОГО пакету — захист від збоїв.
    ///     При RpdLimitReachedException зберігає частковий прогрес і зупиняється.
    /// EN: Phase 2 — sends the prepared queue to the Gemini API.
    ///     Called only if scan.NeedsApi == true.
    ///     Progress is saved after EVERY batch — crash protection.
    ///     On RpdLimitReachedException saves partial progress and stops.
    /// </summary>
    public async Task TranslateAsync(ScanResult scan)
    {
        int total = scan.Queue.Count;
        bool rpdDone = false;

        for (int qi = 0; qi < total && !rpdDone; qi++)
        {
            var entry = scan.Queue[qi];
            string fileName = entry.File.Name;

            // UA: Заголовок [X/Y] — стиль v01, зручно відстежувати прогрес.
            // EN: [X/Y] header — v01 style, convenient for progress tracking.
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(string.Format(Locale.S("file_header"), qi + 1, total, FormatFileLabel(entry.File)));
            Console.WriteLine($"  RPD: {RpdCounter.Total}/{Config.MaxRpd}" +
                              $"  |  RPM ≈ {RateLimiter.Instance.GetCurrentRPM():F1}");
            Console.ResetColor();

            // ── Retry guard ───────────────────────────────────────────────
            _retries.TryGetValue(fileName, out int tried);
            if (tried >= Config.MaxAutoRetries)
            {
                WriteReview(fileName, entry.OutputPath,
                    $"Exceeded MaxAutoRetries ({Config.MaxAutoRetries})");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(Locale.S("retry_exceeded"));
                Console.ResetColor();
                continue;
            }

            try
            {
                bool ok = await TranslateFileAsync(entry, fileName);
                if (!ok) _retries[fileName] = tried + 1;
                else _retries.Remove(fileName);
            }
            catch (RpdLimitReachedException ex)
            {
                rpdDone = true;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
                Console.WriteLine($"  ║  {string.Format(Locale.S("rpd_limit"), ex.Used, ex.Max).PadRight(52)}║");
                Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
                Console.ResetColor();
                File.AppendAllText(Config.LogPath, $"[{DateTime.UtcNow:HH:mm:ss}] {ex.Message}\n");
            }

            if (!rpdDone)
                await Task.Delay(Random.Shared.Next(300, 800));
        }

        // ── Session summary ───────────────────────────────────────────────
        double mins = _sw.Elapsed.TotalMinutes;
        double avgRpm = mins > 0 ? RpdCounter.Session / mins : 0;

        File.AppendAllText(Config.RpmLogPath,
            $"[{DateTime.UtcNow:HH:mm:ss}] SESSION: " +
            $"requests={RpdCounter.Session}, elapsed={_sw.Elapsed:hh\\:mm\\:ss}, " +
            $"avgRPM={avgRpm:F2}, totalRpd={RpdCounter.Total}/{Config.MaxRpd}\n");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(string.Format(Locale.S("session_summary"),
            RpdCounter.Session, avgRpm, RpdCounter.Total, Config.MaxRpd));
        Console.ResetColor();
    }

    // ── TRANSLATE ONE FILE ENTRY ──────────────────────────────────────────

    /// <summary>
    /// UA: Перекладає один FileEntry. UniqueMap вже побудовано у ScanAsync.
    ///     Тут тільки API-запити, dedup та запис прогресу після кожного пакету.
    /// EN: Translates one FileEntry. UniqueMap was built in ScanAsync.
    ///     Only API calls, dedup application, and per-batch progress saving here.
    /// </summary>
    async Task<bool> TranslateFileAsync(FileEntry entry, string fileName)
    {
        var uniqueKeys = entry.UniqueMap.Keys.ToList();
        int totalBatches = (int)Math.Ceiling((double)uniqueKeys.Count / Config.BatchSize);
        bool anyFailed = false;

        for (int b = 0; b < uniqueKeys.Count; b += Config.BatchSize)
        {
            int batchNum = b / Config.BatchSize + 1;
            var subKeys = uniqueKeys.Skip(b).Take(Config.BatchSize).ToList();
            var apiItems = subKeys.Select((k, i) => (id: i, text: k)).ToList();

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(string.Format(Locale.S("batch_status"),
                batchNum, totalBatches,
                RateLimiter.Instance.GetCurrentRPM(),
                RpdCounter.Total, Config.MaxRpd,
                $"{apiItems.Count} strings…"));
            Console.ResetColor();

            // UA: При галюцинації повторюємо пакет по ОДНОМУ рядку.
            //     Це "хірургічний" підхід: ізолюємо проблемний рядок
            //     замість того щоб відкидати весь пакет.
            //     Один рядок дає моделі максимально чіткий контекст.
            // EN: On hallucination, retry the batch ONE string at a time.
            //     This is a "surgical" approach: isolate the problematic string
            //     instead of discarding the whole batch.
            //     One string gives the model the clearest possible context.
            Dictionary<int, string> translated;
            try
            {
                translated = await _api.TranslateAsync(apiItems, fileName);
            }
            catch (RpdLimitReachedException)
            {
                TsvIo.WriteFile(entry.OutputPath, entry.Rows);
                throw;
            }

            // UA: Перевіряємо чи не є ВЕСЬ пакет галюцинацією.
            //     Якщо так — повторюємо кожен рядок окремо (по 1).
            // EN: Check whether the ENTIRE batch is hallucinated.
            //     If so — retry each string individually (1 at a time).
            bool batchAllHallucinated = translated.Count > 0 && translated.Values
                .All(t => Validator.ValidateTranslation("x", t) is (false, _));
            if (!batchAllHallucinated && translated.Count > 0)
            {
                // UA: Часткова галюцинація — перевіряємо рядок за рядком нижче.
                // EN: Partial hallucination — checked string by string below.
            }
            else if (batchAllHallucinated && apiItems.Count > 1)
            {
                File.AppendAllText(Config.LogPath,
                    $"[{DateTime.UtcNow:HH:mm:ss}] [{fileName}] " +
                    $"Batch {batchNum} fully hallucinated — retrying {apiItems.Count} strings one by one\n");

                var singleResults = new Dictionary<int, string>();
                foreach (var singleItem in apiItems)
                {
                    try
                    {
                        var singleResult = await _api.TranslateAsync(
                            new[] { singleItem }, fileName);
                        if (singleResult.TryGetValue(0, out var st))
                            singleResults[singleItem.id] = st;
                    }
                    catch (RpdLimitReachedException)
                    {
                        TsvIo.WriteFile(entry.OutputPath, entry.Rows);
                        throw;
                    }
                }
                translated = singleResults;
            }

            for (int i = 0; i < subKeys.Count; i++)
            {
                string src = subKeys[i];

                if (!translated.TryGetValue(i, out var tr) || string.IsNullOrWhiteSpace(tr))
                {
                    File.AppendAllText(Config.LogPath,
                        $"[{DateTime.UtcNow:HH:mm:ss}] [{fileName}] No result for: " +
                        $"{(src.Length > 60 ? src[..60] + "…" : src)}\n");
                    anyFailed = true;
                    continue;
                }

                var (ok, reason) = Validator.ValidateTranslation(src, tr);
                if (!ok)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(string.Format(Locale.S("val_failed"), reason));
                    Console.ResetColor();
                    File.AppendAllText(Config.LogPath,
                        $"[{DateTime.UtcNow:HH:mm:ss}] [{fileName}] " +
                        $"Validation: {reason} | " +
                        $"src={src[..Math.Min(40, src.Length)]} | " +
                        $"tr={tr[..Math.Min(40, tr.Length)]}\n");
                    // UA: Галюцинований результат НЕ зберігаємо і НЕ кешуємо.
                    //     Порожній TranslatedText змусить наступну сесію повторити спробу.
                    //     Зберігати поганий текст гірше ніж не мати перекладу взагалі.
                    // EN: Do NOT save or cache a hallucinated result.
                    //     Empty TranslatedText causes the next session to retry this string.
                    //     Saving bad text is worse than having no translation at all.
                    anyFailed = true;
                    continue;
                }

                // UA: Результат пройшов валідацію — кешуємо і записуємо у всі рядки.
                // EN: Result passed validation — cache and write to all rows.
                _cache.Add(src, tr);
                foreach (var row in entry.UniqueMap[src])
                    row.TranslatedText = tr;
            }

            // UA: Зберігаємо після КОЖНОГО пакету — захист від збоїв.
            // EN: Save after EVERY batch — crash protection.
            TsvIo.WriteFile(entry.OutputPath, entry.Rows);
            RpdCounter.Save();
            _cache.Save();
            Console.Write("  ✓");
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(string.Format(Locale.S("file_ok"), Path.GetFileName(entry.OutputPath)));
        Console.ResetColor();

        return !anyFailed;
    }

    // ── REVIEW HELPER ─────────────────────────────────────────────────────

    void WriteReview(string fileName, string outPath, string reason)
    {
        string notePath = Path.Combine(Config.ReviewDir,
            $"{Path.GetFileNameWithoutExtension(fileName)}" +
            $".{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");

        File.WriteAllText(notePath,
            $"File:   {fileName}\nOutput: {outPath}\n" +
            $"Reason: {reason}\nTime:   {DateTime.UtcNow:O}\n",
            Encoding.UTF8);

        File.AppendAllText(Config.LogPath,
            $"[{DateTime.UtcNow:HH:mm:ss}] REVIEW: {fileName} — {reason}\n");
    }
}