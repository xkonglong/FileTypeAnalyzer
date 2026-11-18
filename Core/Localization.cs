using System.Text.Json;

namespace FileTypeAnalyzer.Core;

public static class Localization
{
    private static readonly Dictionary<string, string> Strings = new(StringComparer.Ordinal);
    private static readonly object Lock = new();

    public static string CurrentLanguage { get; private set; } = "zh-CN";
    public static IReadOnlyList<string> AvailableLanguages { get; } = new[] { "zh-CN", "en-US" };

    public static bool Load(string langDir, string langCode)
    {
        try
        {
            var path = Path.Combine(langDir, $"{langCode}.json");
            if (!File.Exists(path)) return false;

            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is null) return false;

            lock (Lock)
            {
                Strings.Clear();
                foreach (var kv in dict)
                    Strings[kv.Key] = kv.Value;
            }

            CurrentLanguage = langCode;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string T(string key) =>
        Strings.TryGetValue(key, out var v) ? v : key;

    public static string T(string key, params object[] args)
    {
        var fmt = T(key);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }
}