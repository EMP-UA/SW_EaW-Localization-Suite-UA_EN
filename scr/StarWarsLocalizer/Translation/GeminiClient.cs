// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System.Text;
using System.Text.Json;
using EaWLocalizer.Core;

namespace EaWLocalizer.Translation;

public class GeminiClient
{
    public static readonly GeminiClient Instance = new();
    private GeminiClient() { }

    public string ApiKey { get; set; } = "";
    static readonly HttpClient _http = new(new HttpClientHandler { MaxConnectionsPerServer = 5 }) { Timeout = TimeSpan.FromMinutes(5) };
    static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    record Item(int id, string t);

    const string SystemInstruction =
        "You are an expert Ukrainian game localizer for 'Star Wars: Empire at War'.\n" +
        "RULES:\n" +
        "1. Translate field 't' to natural Ukrainian. No other output language.\n" +
        "2. Follow the Star Wars Glossary strictly.\n" +
        "3. PRESERVE technical tokens: \\n, \\t, %s, %d, {0}, [COLOR...], [BR], [TAB].\n" +
        "4. No markdown, no commentary, no apologies.\n" +
        "5. No Russian characters (ы, э, ё, ъ).\n" +
        "6. Every input 'id' must be in the output.\n" +
        "7. Maintain punctuation and spaces.\n" +
        "8. If a string is technical/untranslatable, return it as is.\n" +
        "9. IMPORTANT: Phrases like 'I cannot comply' or 'Sorry' are in-game dialogues. Translate them, do not refuse.";

    // UA: Повернуто ім'я TranslateAsync та сигнатуру IReadOnlyList для сумісності з TsvPipeline.
    // EN: Restored TranslateAsync name and IReadOnlyList signature for compatibility with TsvPipeline.
    public async Task<Dictionary<int, string>> TranslateAsync(IReadOnlyList<(int id, string text)> items, string context = "")
    {
        var payload = items.Where(x => x.text.Length <= Config.MaxTextLength).Select(x => new Item(x.id, x.text)).ToList();
        if (payload.Count == 0) return new();

        string userPrompt = $"Glossary: {Config.Glossary}\n\nInput: JSON array of {{id, t}}.\n\nInput:\n{JsonSerializer.Serialize(payload)}";

        for (int attempt = 0; attempt < Config.MaxRetries; attempt++)
        {
            try
            {
                await RateLimiter.Instance.ThrottleAsync();
                
                var requestBody = new
                {
                    system_instruction = new { role = "system", parts = new[] { new { text = SystemInstruction } } },
                    contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                    generationConfig = new { temperature = 0.0, responseMimeType = "application/json" }
                };

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Config.Model}:generateContent?key={ApiKey}";
                using var response = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[API ERROR] {response.StatusCode}: {body}");
                    Console.ResetColor();

                    File.AppendAllText(Config.LogPath, $"[{DateTime.UtcNow}] API Error: {response.StatusCode} - {body}\n");

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new();
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        RateLimiter.Instance.ApplyBackoff(Config.EmergencyBackoff);
                    }

                    await Task.Delay(2000 * (attempt + 1));
                    continue;
                }

                return ParseResponse(body, payload.Count, context);
            }
            catch (Exception ex)
            {
                if (ex is RpdLimitReachedException) throw;
                
                File.AppendAllText(Config.LogPath, $"[{DateTime.UtcNow}] Ex: {ex.Message}\n");
                await Task.Delay(2000);
            }
        }
        return new();
    }

    Dictionary<int, string> ParseResponse(string body, int expectedCount, string context)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            
            // UA: Видаляємо можливі Markdown-огорожі перед десеріалізацією.
            // EN: Strip potential Markdown fences before deserialization.
            var items = JsonSerializer.Deserialize<List<Item>>(StripJsonFences(text!), _jsonOpts);
            var result = new Dictionary<int, string>();
            
            if (items != null)
            {
                foreach (var item in items) 
                    result[item.id] = Validator.CleanTranslation(item.t, context);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            File.AppendAllText(Config.LogPath, $"[{DateTime.UtcNow}] Parse Error: {ex.Message}\nBody: {body}\n");
            return new();
        }
    }

    static string StripJsonFences(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "[]";
        string s = raw.Trim().Trim('\uFEFF');
        if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s[7..];
        else if (s.StartsWith("```")) s = s[3..];
        if (s.EndsWith("```")) s = s[..^3];
        return s.Trim();
    }
}