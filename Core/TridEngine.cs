// 托管引擎的完整类型是 TrID.TrIDEngine（namespace TrID + class TrIDEngine）。
// 不能直接写 using TrID = TrID.TrIDEngine：别名 TrID 会和程序集里的命名空间 TrID 重名（CS0576），
// 所以这里用 ManagedTrID 作为别名，指代同一个类型。
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ManagedTrID = TrID.TrIDEngine;

namespace FileTypeAnalyzer.Core;

/// <summary>
/// TrID 签名引擎（托管实现）。
///
/// 两条数据通路：
/// 1. 有外部定义库（<see cref="TridDefsStore"/> 里装过官方 XML 包）时，用实例化的
///    TrID.TrIDEngine.PatternEngine：LoadDefinitionByXml 逐条装入官方最新定义，
///    再 SubmitFile / Analyze / GetResultsData(5) 取结果；
/// 2. 没有外部库时，回退到静态 API（GetExtensions），吃程序集里 2016 年的内置快照。
///
/// 为什么不用原生 TrIDLib.dll：那是 32 位 DLL，必须 P/Invoke 且进程锁 x86，兼容性差。
/// 引擎对外只要求 AnyCPU，纯 .NET，没有原生依赖。
/// </summary>
public sealed class TridEngine : ITridEngine
{
    /// <summary>与界面一致：只展示得分最高的 5 项。</summary>
    private const int MaxMatches = 5;

    /// <summary>PatternEngine 内部状态不是线程安全的，所有调用串行。</summary>
    private static readonly object CallLock = new();

    private static readonly Regex PointsSuffix = new(@"\s*\(\d+/\d+\)\s*$", RegexOptions.Compiled);

    private readonly TridDefsStore? _store;
    private readonly object _loadLock = new();
    private Task? _loader;
    private ManagedTrID.PatternEngine? _external;
    private string _loadError = "";
    private int _defsCount;

    /// <param name="store">外部定义库；传 null 或库里没有定义时退回程序集内置定义。</param>
    public TridEngine(TridDefsStore? store = null)
    {
        _store = store;
        if (_store is { HasDefinitions: true })
        {
            // 后台预加载：缓存命中时很快，首次解析 2 万条 XML 较慢但不挡住窗体显示
            _loader = Task.Run(EnsureExternalLoaded);
        }
        else
        {
            InitBuiltin();
        }
    }

    public bool IsReady => _loadError.Length == 0;

    public string LibraryInfo
    {
        get
        {
            if (!IsReady) return $"未就绪: {_loadError}";
            if (_external is null && _loader is { IsCompleted: false })
                return "TrID 定义库加载中…";
            if (_defsCount > 0)
                return $"TrID 官方定义库（{_defsCount} 条 / {_store?.PackageDate}）";
            return "TrID 内置定义（2016 快照）";
        }
    }

    public TridResult Analyze(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new TridResult { Error = "文件不存在或不可读" };

        lock (CallLock)
        {
            try
            {
                // 外部定义库：等后台加载完成（首次可能需要几秒）
                if (_store is { HasDefinitions: true })
                {
                    if (!EnsureExternalLoaded()) return new TridResult { Error = _loadError };

                    _external!.SubmitFile(filePath);
                    _external.Analyze();
                    var data = _external.GetResultsData(MaxMatches);
                    var matches = new List<TridMatch>(MaxMatches);
                    foreach (var r in data)
                    {
                        if (matches.Count >= MaxMatches) break;
                        if (r.Points <= 0 && string.IsNullOrEmpty(r.FileType)) continue;
                        matches.Add(ToMatch(matches.Count + 1, r.FileType, r.FileExt, r.Points, r.Perc));
                    }
                    return new TridResult { Matches = matches };
                }

                // 内置快照：静态 API
                if (!IsReady) return new TridResult { Error = _loadError };
                var results = ManagedTrID.GetExtensions(filePath);
                if (results is null || results.Length == 0)
                    return new TridResult();

                var builtinMatches = new List<TridMatch>(MaxMatches);
                foreach (var r in results)
                {
                    if (builtinMatches.Count >= MaxMatches) break;
                    // 无匹配时引擎会返回一条空结果，跳过
                    if (r.Points <= 0 && string.IsNullOrEmpty(r.FileType)) continue;
                    builtinMatches.Add(ToMatch(builtinMatches.Count + 1, r.FileType, r.FileExt, r.Points, r.Perc));
                }
                return new TridResult { Matches = builtinMatches };
            }
            catch (Exception ex)
            {
                return new TridResult { Error = ex.Message };
            }
        }
    }

    private static TridMatch ToMatch(int rank, string? fileType, string? fileExt, int points, float perc) => new()
    {
        Rank = rank,
        // 引擎返回的 FileType 末尾带 " (分数/序号)"，如 "Win64 Executable (generic) (6522/11)"，展示时去掉
        TypeDescription = PointsSuffix.Replace(fileType ?? "", "").Trim(),
        Extension = (fileExt ?? "").TrimStart('.'),
        Points = points,
        // Perc 已经是 0~100，换成 0~1
        Confidence = Math.Clamp(perc / 100.0, 0, 1)
    };

    private void InitBuiltin()
    {
        try
        {
            // 主动触发静态构造函数：定义装载失败能当场暴露，而不是拖到第一次分析
            RuntimeHelpers.RunClassConstructor(typeof(ManagedTrID).TypeHandle);
        }
        catch (Exception ex)
        {
            _loadError = $"TrID 引擎初始化失败: {ex.Message}";
        }
    }

    /// <summary>装入官方定义库（优先读缓存）。返回是否可用。</summary>
    private bool EnsureExternalLoaded()
    {
        if (_external is not null) return true;

        lock (_loadLock)
        {
            if (_external is not null) return true;
            if (!IsReady) return false;

            try
            {
                var files = _store!.GetXmlFiles();
                if (files.Length == 0)
                {
                    _loadError = "TrID 定义库为空，请重新下载。";
                    return false;
                }

                var cached = TridDefsCache.TryLoad(_store.CacheFile, files.Length);
                if (cached is not null)
                {
                    _external = new ManagedTrID.PatternEngine(cached, 2048);
                    _defsCount = cached.Count;
                    return true;
                }

                var engine = new ManagedTrID.PatternEngine();
                foreach (var f in files)
                    engine.LoadDefinitionByXml(File.ReadAllText(f));

                var defs = engine.GetDefinitions() as ArrayList;
                _external = engine;
                _defsCount = defs?.Count ?? engine.DefInMemory;

                // 落盘缓存，下次启动秒开（失败不影响功能）
                if (defs is not null) TridDefsCache.Save(_store.CacheFile, defs, files.Length);
                return true;
            }
            catch (Exception ex)
            {
                _loadError = $"TrID 定义库加载失败: {ex.Message}";
                return false;
            }
        }
    }

    public void Dispose()
    {
        // 托管引擎没有非托管资源需要释放
    }
}
