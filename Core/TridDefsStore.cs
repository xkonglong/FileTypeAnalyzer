using System.Net.Http;
using System.Text.Json;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace FileTypeAnalyzer.Core;

/// <summary>
/// TrID 官方 XML 定义库（签名库）的本地存储与更新。
///
/// 背景：托管引擎 TrID.dll 里内置的是 2016-01-28 的定义快照（约 6300 条），官方现在
/// 每几个月更新一次，最新包里已有 2 万多条。官方提供的定义包是
/// https://mark0.net/download/triddefs_xml.7z（每个定义一个 *.trid.xml），
/// 引擎公开的 PatternEngine.LoadDefinitionByXml 可以直接吃这些 XML，
/// 所以不需要退回 32 位原生 TrIDLib 也能用上最新定义。
///
/// 目录结构（默认放在 %LOCALAPPDATA%\FileTypeAnalyzer）：
///   defs\           解压后的 *.trid.xml（平铺存放）
///   triddefs.cache  解析后的定义二进制缓存（避免每次启动重新解析 2 万个 XML）
///   triddefs.json   清单：条数、包日期、写入时间
/// </summary>
public sealed class TridDefsStore
{
    public const string SourceUrl = "https://mark0.net/download/triddefs_xml.7z";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public string RootDir { get; }
    public string DefsDir { get; }
    public string CacheFile { get; }
    public string ManifestFile { get; }

    /// <summary>已安装定义条数；0 表示没有外部定义库（回退到程序集内置快照）。</summary>
    public int Count { get; private set; }

    /// <summary>定义包日期（取下载时的 Last-Modified，取不到就用安装当天）。</summary>
    public string PackageDate { get; private set; } = "";

    public TridDefsStore(string rootDir)
    {
        RootDir = rootDir;
        DefsDir = Path.Combine(rootDir, "defs");
        CacheFile = Path.Combine(rootDir, "triddefs.cache");
        ManifestFile = Path.Combine(rootDir, "triddefs.json");
        LoadManifest();
    }

    public bool HasDefinitions => Count > 0 && Directory.Exists(DefsDir);

    public string[] GetXmlFiles() =>
        Directory.Exists(DefsDir)
            ? Directory.GetFiles(DefsDir, "*.trid.xml", SearchOption.AllDirectories)
            : Array.Empty<string>();

    /// <summary>
    /// 下载官方 XML 包 → 解压 → 替换本地定义目录。返回安装后的定义条数。
    /// </summary>
    public async Task<int> UpdateAsync(IProgress<string>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(RootDir);
        var work = Path.Combine(RootDir, "update-tmp");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);

        try
        {
            progress?.Report(Localization.T("update.trid.downloading"));
            var archivePath = Path.Combine(work, "triddefs_xml.7z");
            var remoteDate = "";
            using (var resp = await Http.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.LastModified is { } lm)
                    remoteDate = lm.LocalDateTime.ToString("yyyy-MM-dd");
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(archivePath);
                await src.CopyToAsync(dst, ct);
            }

            progress?.Report(Localization.T("update.trid.extracting"));
            var xmlDir = Path.Combine(work, "xml");
            Directory.CreateDirectory(xmlDir);
            // 7z 是固实压缩，必须顺序解压；ArchiveFactory.WriteToDirectory 会一次性顺序展开全部条目
            // （逐个 entry.OpenEntryStream 在固实包上会反复从头解码，2 万个定义能跑十几分钟）。
            ArchiveFactory.WriteToDirectory(archivePath, xmlDir, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });

            var extracted = Directory.GetFiles(xmlDir, "*.trid.xml", SearchOption.AllDirectories).Length;
            if (extracted < 1000)
                throw new InvalidOperationException($"定义包内容异常：只解出 {extracted} 个定义文件。");

            progress?.Report(Localization.T("update.trid.installing", extracted));
            ReplaceDefsDir(xmlDir);

            if (File.Exists(CacheFile)) File.Delete(CacheFile);
            Count = extracted;
            PackageDate = string.IsNullOrEmpty(remoteDate)
                ? DateTime.Now.ToString("yyyy-MM-dd")
                : remoteDate;
            WriteManifest();
            return Count;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch { }
        }
    }

    /// <summary>替换定义目录：先把旧目录改名备份，失败时还原。</summary>
    private void ReplaceDefsDir(string newDir)
    {
        var backup = DefsDir + ".old";
        if (Directory.Exists(backup)) Directory.Delete(backup, true);

        var hadOld = Directory.Exists(DefsDir);
        if (hadOld) Directory.Move(DefsDir, backup);
        try
        {
            Directory.Move(newDir, DefsDir);
        }
        catch
        {
            if (hadOld && !Directory.Exists(DefsDir)) Directory.Move(backup, DefsDir);
            throw;
        }
        finally
        {
            try { if (Directory.Exists(backup)) Directory.Delete(backup, true); } catch { }
        }
    }

    private void LoadManifest()
    {
        try
        {
            if (!File.Exists(ManifestFile)) return;
            var m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestFile));
            if (m is null) return;
            Count = m.Count;
            PackageDate = m.Date ?? "";
        }
        catch
        {
            Count = 0;
            PackageDate = "";
        }
    }

    private void WriteManifest()
    {
        var m = new Manifest
        {
            Count = Count,
            Date = PackageDate,
            Source = SourceUrl,
            BuiltAt = DateTime.Now
        };
        File.WriteAllText(ManifestFile, JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class Manifest
    {
        public int Count { get; set; }
        public string? Date { get; set; }
        public string? Source { get; set; }
        public DateTime BuiltAt { get; set; }
    }
}
