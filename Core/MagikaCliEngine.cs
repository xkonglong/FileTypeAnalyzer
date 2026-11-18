using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FileTypeAnalyzer.Core;

public sealed class MagikaCliEngine : IMagikaEngine
{
    private readonly string _exePath;
    public MagikaCliEngine(string exePath) => _exePath = exePath;

    public bool IsReady => File.Exists(_exePath);
    public string ModelInfo => IsReady ? "Magika CLI" : "未找到 magika.exe";

    public async Task<MagikaResult> AnalyzeAsync(string filePath, CancellationToken ct = default)
    {
        if (!IsReady)
            return new MagikaResult { Error = $"找不到 magika.exe: {_exePath}" };

        try
        {
            var psi = new ProcessStartInfo(_exePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("--json");
            psi.ArgumentList.Add(filePath);

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
                return new MagikaResult { Error = $"magika 退出码 {process.ExitCode}: {stderr.Trim()}" };
            if (string.IsNullOrWhiteSpace(stdout))
                return new MagikaResult { Error = "magika 输出为空" };

            return ParseJson(stdout);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new MagikaResult { Error = ex.Message }; }
    }

    private static MagikaResult ParseJson(string raw)
    {
        // 1) 先尝试整串作为一个 JSON 文档解析
        if (!TryParseDocument(raw, out var root))
        {
            // 2) 整串解析失败时，按行尝试 NDJSON（每行一个 JSON）
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] != '{') continue;
                if (TryParseDocument(trimmed, out root)) break;
            }
        }

        if (root.ValueKind == JsonValueKind.Undefined)
            return new MagikaResult
            {
                Error = "无法解析 Magika JSON。原始输出：" + Truncate(raw, 600)
            };

        // 3) 递归定位含 label / ct_label 的结果对象
        var resultObj = FindResultObject(root);
        if (resultObj.ValueKind != JsonValueKind.Object)
            return new MagikaResult
            {
                Error = "无法从 Magika 输出中定位 label 字段。原始输出：" + Truncate(raw, 600)
            };

        // 4) score 可能不在 resultObj 内部，从根文档按已知路径回退查找
        var score = TryGetScore(root);
        if (score is null)
            score = GetDouble(resultObj, "score");

        var label = GetString(resultObj, "label");
        if (string.IsNullOrEmpty(label))
            label = GetString(resultObj, "ct_label");

        return new MagikaResult
        {
            Label = label,
            MimeType = GetString(resultObj, "mime_type"),
            Description = GetString(resultObj, "description"),
            Score = score ?? 0,
            IsText = GetBool(resultObj, "is_text")
        };
    }

    private static bool TryParseDocument(string text, out JsonElement root)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            // 克隆，避免 doc 释放后 element 失效
            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            root = default;
            return false;
        }
    }

    /// <summary>
    /// 递归查找第一个包含 label 或 ct_label 的对象。
    /// 兼容 {output:{...}}、{result:{output:{...}}}、{result:{value:{output:{...}}}} 等任意嵌套。
    /// </summary>
    private static JsonElement FindResultObject(JsonElement el, int depth = 0)
    {
        if (depth > 8) return default;

        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("label", out _) || el.TryGetProperty("ct_label", out _))
                return el;

            foreach (var prop in el.EnumerateObject())
            {
                var found = FindResultObject(prop.Value, depth + 1);
                if (found.ValueKind == JsonValueKind.Object) return found;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var found = FindResultObject(item, depth + 1);
                if (found.ValueKind == JsonValueKind.Object) return found;
            }
        }

        return default;
    }

    /// <summary>
    /// 按优先级查找 score：
    ///   1. 新格式 result.value.score
    ///   2. 旧格式 result.value.output.score
    ///   3. 整个文档中第一个数值型 score
    /// </summary>
    private static double? TryGetScore(JsonElement root)
    {
        var value = Navigate(root, "result", "value");
        if (value is { ValueKind: JsonValueKind.Object } obj)
        {
            if (obj.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number)
                return s.GetDouble();

            if (obj.TryGetProperty("output", out var outEl) &&
                outEl.ValueKind == JsonValueKind.Object &&
                outEl.TryGetProperty("score", out var os) &&
                os.ValueKind == JsonValueKind.Number)
                return os.GetDouble();
        }

        return FindFirstScore(root);
    }

    private static JsonElement? Navigate(JsonElement root, params string[] path)
    {
        var cur = root;
        if (cur.ValueKind == JsonValueKind.Array)
        {
            if (cur.GetArrayLength() == 0) return null;
            cur = cur[0];
        }
        foreach (var key in path)
        {
            if (cur.ValueKind != JsonValueKind.Object ||
                !cur.TryGetProperty(key, out var next))
                return null;
            cur = next;
        }
        return cur;
    }

    private static double? FindFirstScore(JsonElement el, int depth = 0)
    {
        if (depth > 10) return null;
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number)
                return s.GetDouble();
            foreach (var prop in el.EnumerateObject())
            {
                var found = FindFirstScore(prop.Value, depth + 1);
                if (found is not null) return found;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var found = FindFirstScore(item, depth + 1);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static string GetString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double GetDouble(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static bool GetBool(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.True;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    public void Dispose() { }
}