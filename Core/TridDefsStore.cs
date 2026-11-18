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
/// 目录结构（默认放在程序目录，即 exe 同级）：
///   defs\           解压后的 *.trid.xml（按官方 0…27 分目录存放）
///   triddefs.cache  解析后的定义二进制缓存（避免每次启动重新解析 2 万个 XML）
///   triddefs.json   清单：条数、包日期、包时间戳、包大小、写入时间
/// 程序目录不可写时才退回 %LOCALAPPDATA%\FileTypeAnalyzer。
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

    /// <summary>本地已装定义包的精确时间戳（Last-Modified）；旧清单可能只有日期没有它。</summary>
    public DateTime? PackageTimestamp { get; private set; }

    /// <summary>本地已装定义包的大小（字节）。</summary>
    public long PackageSize { get; private set; }

    public TridDefsStore(string rootDir)
    {
        RootDir = rootDir;
        DefsDir = Path.Combine(rootDir, "defs");
        CacheFile = Path.Combine(rootDir, "triddefs.cache");
        ManifestFile = Path.Combine(rootDir, "triddefs.json");
        LoadManifest();
    }

    public bool HasDefinitions => Count > 0 && (Directory.Exists(DefsDir) || File.Exists(CacheFile));

    /// <summary>
    /// 把旧位置（%LOCALAPPDATA%）的定义库搬到程序目录，只搬一次；目标已有目录时不动。
    /// 迁移失败不影响使用（下次点更新会重新下载）。
    /// </summary>
    public static void MigrateFrom(string legacyRoot, string targetRoot)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(legacyRoot), Path.GetFullPath(targetRoot),
                    StringComparison.OrdinalIgnoreCase))
                return;

            var targetDefs = Path.Combine(targetRoot, "defs");
            if (Directory.Exists(targetDefs)) return;

            // 清单和缓存是小文件，同步搬走：本次启动就能直接用最新定义，不必等 XML 拷完
            Directory.CreateDirectory(targetRoot);
            foreach (var name in new[] { "triddefs.cache", "triddefs.json" })
            {
                var src = Path.Combine(legacyRoot, name);
                if (File.Exists(src)) File.Move(src, Path.Combine(targetRoot, name), true);
            }

            var legacyDefs = Path.Combine(legacyRoot, "defs");
            if (!Directory.Exists(legacyDefs)) return;

            // XML 有 2 万多个小文件，跨卷时只能逐个拷；放到后台拷到 defs.partial 再改名，
            // 避免启动时在 UI 线程上卡半分钟，也避免留下拷了一半的 defs 目录
            Task.Run(() =>
            {
                var partial = targetDefs + ".partial";
                try
                {
                    if (Directory.Exists(partial)) Directory.Delete(partial, true);
                    CopyDirectory(legacyDefs, partial);
                    if (Directory.Exists(targetDefs)) Directory.Delete(targetDefs, true);
                    Directory.Move(partial, targetDefs);
                    Directory.Delete(legacyDefs, true);
                }
                catch
                {
                    // 拷失败就留着旧的不管，下次启动重试
                    try { if (Directory.Exists(partial)) Directory.Delete(partial, true); } catch { }
                }
            });
        }
        catch
        {
            // 迁移失败静默忽略，不影响启动
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var dest = Path.Combine(target, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }

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
            DateTime? remoteStamp = null;
            long remoteSize = 0;
            using (var resp = await Http.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.LastModified is { } lm)
                {
                    remoteStamp = lm.UtcDateTime;
                    remoteDate = lm.LocalDateTime.ToString("yyyy-MM-dd");
                }
                remoteSize = resp.Content.Headers.ContentLength ?? 0;
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

            // 官方包内自带一层 defs 根目录（defs/0/xxx.trid.xml），装到本地时把这层剥掉，
            // 免得出现 defs\defs\0 这种套娃路径
            var sourceDir = xmlDir;
            var inner = Path.Combine(xmlDir, "defs");
            if (Directory.Exists(inner) && Directory.GetFileSystemEntries(xmlDir).Length == 1)
                sourceDir = inner;

            var extracted = Directory.GetFiles(sourceDir, "*.trid.xml", SearchOption.AllDirectories).Length;
            if (extracted < 1000)
                throw new InvalidOperationException($"定义包内容异常：只解出 {extracted} 个定义文件。");

            progress?.Report(Localization.T("update.trid.installing", extracted));
            ReplaceDefsDir(sourceDir);

            if (File.Exists(CacheFile)) File.Delete(CacheFile);
            Count = extracted;
            PackageDate = string.IsNullOrEmpty(remoteDate)
                ? DateTime.Now.ToString("yyyy-MM-dd")
                : remoteDate;
            PackageTimestamp = remoteStamp ?? DateTime.Now.ToUniversalTime();
            PackageSize = remoteSize > 0 ? remoteSize : new FileInfo(archivePath).Length;
            WriteManifest();
            return Count;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch { }
        }
    }

    /// <summary>替换定义目录：先把旧目录改名备份，失败时还原。</summary>
    private void ReplaceDefsDir(string newDir)    {
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
            PackageSize = m.Size;
            if (!string.IsNullOrWhiteSpace(m.LastModified) &&
                DateTime.TryParse(m.LastModified, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                PackageTimestamp = ts.ToUniversalTime();
        }
        catch
        {
            Count = 0;
            PackageDate = "";
            PackageSize = 0;
            PackageTimestamp = null;
        }
    }

    private void WriteManifest()
    {
        var m = new Manifest
        {
            Count = Count,
            Date = PackageDate,
            LastModified = PackageTimestamp?.ToString("o"),
            Size = PackageSize,
            Source = SourceUrl,
            BuiltAt = DateTime.Now
        };
        File.WriteAllText(ManifestFile, JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// 比对本地定义包与官方包的日期：远端 Last-Modified 不比本地新（一致或更旧）就不需要更新。
    /// 只发 HEAD 请求，不下载正文。
    /// </summary>
    public async Task<TridDefsCheck> CheckUpdateAsync(CancellationToken ct = default)
    {
        var check = new TridDefsCheck
        {
            HasLocalDefs = HasDefinitions,
            LocalDate = PackageDate,
            LocalCount = Count
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, SourceUrl);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var remoteStamp = resp.Content.Headers.LastModified?.UtcDateTime;
            check.RemoteSize = resp.Content.Headers.ContentLength ?? 0;
            check.RemoteDate = remoteStamp?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";

            if (!HasDefinitions)
            {
                // 本地只有内置快照，官方包总是"更新"
                check.HasUpdate = true;
                return check;
            }

            if (remoteStamp is null)
            {
                // 服务器没给日期就无从比对，允许下载
                check.HasUpdate = true;
                return check;
            }

            if (PackageTimestamp is { } localStamp)
            {
                // 精确到秒：远端更新才算有新版；相等（或本地更晚）就不动
                check.HasUpdate = remoteStamp.Value > localStamp;
            }
            else
            {
                // 老清单只有日期（yyyy-MM-dd）：同一天视为一致，不更新
                check.HasUpdate = !string.Equals(check.LocalDate, check.RemoteDate, StringComparison.Ordinal);
            }
        }
        catch (Exception ex)
        {
            // 网络失败时不让用户卡住：允许下载，错误信息交给界面显示
            check.HasUpdate = true;
            check.Error = ex.Message;
        }

        return check;
    }

    private sealed class Manifest
    {
        public int Count { get; set; }
        public string? Date { get; set; }
        /// <summary>包时间戳（ISO 8601，UTC），用于和远端 Last-Modified 精确比对。</summary>
        public string? LastModified { get; set; }
        public long Size { get; set; }
        public string? Source { get; set; }
        public DateTime BuiltAt { get; set; }
    }
}

/// <summary>定义库更新检查结果。</summary>
public sealed class TridDefsCheck
{
    /// <summary>本地是否装了外部定义库。</summary>
    public bool HasLocalDefs { get; set; }
    /// <summary>是否需要下载更新（远端比本地新，或本地没有外部定义库）。</summary>
    public bool HasUpdate { get; set; }
    public string RemoteDate { get; set; } = "";
    public string LocalDate { get; set; } = "";
    public int LocalCount { get; set; }
    public long RemoteSize { get; set; }
    /// <summary>检查失败的原因（如断网）；此时 HasUpdate=true，仍允许用户手动下载。</summary>
    public string? Error { get; set; }
}
