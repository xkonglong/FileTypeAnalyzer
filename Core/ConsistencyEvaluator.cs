namespace FileTypeAnalyzer.Core;

public static class ConsistencyEvaluator
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["doc"] = "word", ["docx"] = "word", ["rtf"] = "word",
        ["xls"] = "excel", ["xlsx"] = "excel",
        ["ppt"] = "powerpoint", ["pptx"] = "powerpoint",
        ["jpg"] = "jpeg", ["jpe"] = "jpeg",
        ["htm"] = "html", ["xhtml"] = "html",
        ["gz"] = "gzip", ["tgz"] = "gzip",
        ["7z"] = "7zip", ["7-zip"] = "7zip",
        ["cs"] = "csharp", ["c#"] = "csharp",
        ["js"] = "javascript", ["mjs"] = "javascript",
        ["py"] = "python", ["rb"] = "ruby",
        ["sh"] = "shell", ["bash"] = "shell"
    };

    private static readonly HashSet<string> GenericLabels = new(StringComparer.OrdinalIgnoreCase)
        { "generic", "unknown", "data", "empty", "text", "txt" };

    public static ConsistencyStatus Evaluate(TridResult trid, MagikaResult magika, out string text)
    {
        if (!trid.Succeeded && !magika.Succeeded)
        {
            text = Localization.T("consistency.bothFailed");
            return ConsistencyStatus.Unknown;
        }

        if (!trid.Succeeded || !magika.Succeeded)
        {
            var failed = !trid.Succeeded ? "TrID" : "Magika";
            text = Localization.T("consistency.partial", failed);
            return ConsistencyStatus.PartialFailure;
        }

        var tridKey = Normalize(trid.Top?.Extension);
        var magikaKey = Normalize(magika.Label);

        if (string.IsNullOrEmpty(tridKey) || string.IsNullOrEmpty(magikaKey) ||
            GenericLabels.Contains(magikaKey))
        {
            text = Localization.T("consistency.unknown");
            return ConsistencyStatus.Unknown;
        }

        if (tridKey == magikaKey)
        {
            text = Localization.T("consistency.match", trid.Top!.TypeDescription, magika.Label);
            return ConsistencyStatus.Match;
        }

        text = Localization.T("consistency.mismatch", trid.Top!.TypeDescription, magika.Label);
        return ConsistencyStatus.Mismatch;
    }

    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim().TrimStart('.').ToLowerInvariant();
        return Aliases.TryGetValue(s, out var canonical) ? canonical : s;
    }
}