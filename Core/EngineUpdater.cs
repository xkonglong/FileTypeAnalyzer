using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace FileTypeAnalyzer.Core;

public sealed class EngineUpdater
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    // ---------- TrID 定义库 ----------

    public async Task<string> UpdateTridDefsAsync(string targetDefsPath,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        const string url = "https://mark0.net/download/triddefs.zip";
        progress?.Report("正在下载 TrID 定义库...");

        var tempDir = Path.Combine(Path.GetTempPath(), "FTAtrid_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var zipPath = Path.Combine(tempDir, "triddefs.zip");
            await DownloadFileAsync(url, zipPath, ct);

            progress?.Report("正在解压...");
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

            var extracted = Directory.EnumerateFiles(tempDir, "triddefs.trd",
                SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException("下载包中未找到 triddefs.trd");

            if (File.Exists(targetDefsPath))
                File.Copy(targetDefsPath, targetDefsPath + ".bak", overwrite: true);

            File.Copy(extracted, targetDefsPath, overwrite: true);
            progress?.Report("TrID 定义库已更新。");
            return $"TrID 定义库已更新（{new FileInfo(targetDefsPath).Length / 1024} KB）";
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ---------- Magika CLI ----------

    public async Task<(bool HasUpdate, string LatestTag, string DownloadUrl, string CurrentVersion)>
        CheckMagikaUpdateAsync(string currentExePath, CancellationToken ct = default)
    {
        // 用 magika -V 读取本地版本，例如 “magika 1.1.0 standard_v3_3”
        var currentVersion = GetMagikaVersion(currentExePath);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/google/magika/releases");
        request.Headers.UserAgent.ParseAdd("FileTypeAnalyzer/1.0");

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            // 跳过草稿与预发布
            if (release.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) continue;
            if (release.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) continue;

            if (!release.TryGetProperty("tag_name", out var tagEl)) continue;
            var tag = tagEl.GetString() ?? "";
            if (!tag.StartsWith("cli/v", StringComparison.OrdinalIgnoreCase)) continue;

            if (!release.TryGetProperty("assets", out var assets)) continue;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                // Windows 资产通常是 magika-cli-x86_64-pc-windows-msvc.zip
                if (name.Contains("windows", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var url = asset.TryGetProperty("browser_download_url", out var u)
                        ? u.GetString() : null;
                    if (string.IsNullOrEmpty(url)) continue;

                    var latestVersion = ExtractVersion(tag);
                    // 取不到本地版本时按“可能有更新”处理，避免误报为已是最新
                    var hasUpdate = string.IsNullOrEmpty(currentVersion)
                        || CompareVersions(latestVersion, currentVersion) > 0;

                    return (hasUpdate, tag, url, currentVersion);
                }
            }
        }

        return (false, currentVersion, "", currentVersion);
    }

    /// <summary>
    /// 下载并安装 Magika CLI。
    /// 流程：下到临时目录 → 复制成同目录暂存文件 magika.new.exe → 实跑 -V 校验版本
    /// → 备份旧 exe → 原子替换 → 再跑一次 -V，失败则回滚 .bak。
    /// 返回安装后的版本号；任何一步失败都抛异常且不改动现有 exe。
    /// </summary>
    public async Task<(string Message, string Version)> UpdateMagikaCliAsync(
        string targetExePath, string downloadUrl, string latestTag,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report("正在下载 Magika CLI...");

        var tempDir = Path.Combine(Path.GetTempPath(), "FTAmagika_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var stagingPath = Path.Combine(
            Path.GetDirectoryName(targetExePath) ?? ".",
            Path.GetFileNameWithoutExtension(targetExePath) + ".new.exe");
        var backupPath = targetExePath + ".bak";

        try
        {
            var zipPath = Path.Combine(tempDir, "magika.zip");
            await DownloadFileAsync(downloadUrl, zipPath, ct);

            progress?.Report("正在解压...");
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

            var extractedExe = Directory.EnumerateFiles(tempDir, "magika.exe",
                SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException("压缩包中未找到 magika.exe");

            // 1) 先写到暂存文件，不碰正在使用的 magika.exe
            File.Copy(extractedExe, stagingPath, overwrite: true);

            // 2) 实际运行暂存版，确认可执行且版本不低于目标
            progress?.Report("正在校验新版本...");
            var stagingVersion = GetMagikaVersion(stagingPath);
            var expectedVersion = ExtractVersion(latestTag);

            if (string.IsNullOrEmpty(stagingVersion))
                throw new InvalidOperationException("新下载的 magika.exe 无法运行（-V 无输出）");

            if (!string.IsNullOrEmpty(expectedVersion) &&
                CompareVersions(stagingVersion, expectedVersion) < 0)
                throw new InvalidOperationException(
                    $"新版本校验失败：期望 {expectedVersion}，实际 {stagingVersion}");

            // 3) 备份旧版并原子替换（任一步被占用都给出明确提示，旧文件保持不变）
            var hadOldExe = File.Exists(targetExePath);
            try
            {
                if (hadOldExe)
                    File.Copy(targetExePath, backupPath, overwrite: true);

                File.Move(stagingPath, targetExePath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "替换 magika.exe 失败，文件可能正被使用（有分析正在进行？），请稍后重试。" + ex.Message, ex);
            }

            // 5) 安装后再校验一次，失败则回滚
            var installedVersion = GetMagikaVersion(targetExePath);
            if (string.IsNullOrEmpty(installedVersion))
            {
                if (hadOldExe && File.Exists(backupPath))
                {
                    try { File.Copy(backupPath, targetExePath, overwrite: true); }
                    catch { /* 回滚也失败时保留 .bak 供手工恢复 */ }
                    throw new InvalidOperationException("替换后 magika.exe 无法运行，已回滚到旧版本");
                }
                throw new InvalidOperationException("替换后 magika.exe 无法运行");
            }

            progress?.Report("Magika CLI 已更新。");
            return ($"Magika CLI 已更新（{installedVersion}）", installedVersion);
        }
        finally
        {
            try { if (File.Exists(stagingPath)) File.Delete(stagingPath); } catch { }
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task DownloadFileAsync(string url, string dest, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs, ct);
    }

    /// <summary>运行 magika -V（回退 --version）并解析出版本号，如 1.1.0。</summary>
    private static string GetMagikaVersion(string exePath)
    {
        if (!File.Exists(exePath)) return "";

        foreach (var arg in new[] { "-V", "--version" })
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exePath, arg)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = System.Diagnostics.Process.Start(psi);
                if (p is null) continue;

                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                var version = ExtractVersion(output);
                if (!string.IsNullOrEmpty(version)) return version;
            }
            catch { /* 换下一个参数继续尝试 */ }
        }

        return "";
    }

    /// <summary>从任意文本（如 “magika 1.1.0 standard_v3_3” 或 “cli/v1.1.0”）中取出版本号。</summary>
    private static string ExtractVersion(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"\d+\.\d+\.\d+");
        if (m.Success) return m.Value;
        m = System.Text.RegularExpressions.Regex.Match(text, @"\d+\.\d+");
        return m.Success ? m.Value + ".0" : "";
    }

    /// <summary>按点分段做数值比较，a &gt; b 返回正数。</summary>
    private static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        var len = Math.Max(pa.Length, pb.Length);
        for (var i = 0; i < len; i++)
        {
            var x = i < pa.Length ? int.TryParse(pa[i], out var xi) ? xi : 0 : 0;
            var y = i < pb.Length ? int.TryParse(pb[i], out var yi) ? yi : 0 : 0;
            if (x != y) return x > y ? 1 : -1;
        }
        return 0;
    }
}