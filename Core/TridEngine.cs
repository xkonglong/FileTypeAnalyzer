// 托管引擎的完整类型是 TrID.TrIDEngine（namespace TrID + class TrIDEngine）。
// 不能直接写 using TrID = TrID.TrIDEngine：别名 TrID 会和程序集里的命名空间 TrID 重名（CS0576），
// 所以这里用 ManagedTrID 作为别名，指代同一个类型。
using System.Runtime.CompilerServices;
using ManagedTrID = TrID.TrIDEngine;

namespace FileTypeAnalyzer.Core;

/// <summary>
/// TrID 签名引擎（托管实现）。
///
/// 为什么换掉原来的 TrIDLib.dll：那是 Marco Pontello 的 32 位原生 DLL，只能靠 P/Invoke 调用，
/// 进程必须固定 x86，在 64 位系统上兼容性反而更差，还得把 DLL 手工塞到程序目录。
/// 这里改用 NuGet 包 TriDEngine 1.2.4 里的托管 TrID.dll：
/// 纯 .NET 实现，约 6300 条签名定义在类型加载时一次装入，没有原生依赖、没有位数限制，
/// 因此项目保持 AnyCPU，也不需要 triddefs.trd 或额外的 DLL 文件。
///
/// 注意：TrIDEngine 的三个公开方法（GetExtensions / GetBestExtension / GetExtensionByFileContent）
/// 都是静态方法，内部状态可变，所以调用要串行。
/// </summary>
public sealed class TridEngine : ITridEngine
{
    /// <summary>与界面一致：只展示得分最高的 5 项。</summary>
    private const int MaxMatches = 5;

    private static readonly object CallLock = new();
    private readonly string _loadError = "";

    public TridEngine()
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

    public bool IsReady => _loadError.Length == 0;

    public string LibraryInfo => IsReady
        ? "TrID 签名库已就绪（托管引擎，定义内置）"
        : $"未就绪: {_loadError}";

    public TridResult Analyze(string filePath)
    {
        if (!IsReady) return new TridResult { Error = _loadError };
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new TridResult { Error = "文件不存在或不可读" };

        lock (CallLock)
        {
            try
            {
                var results = ManagedTrID.GetExtensions(filePath);
                if (results is null || results.Length == 0)
                    return new TridResult();

                var matches = new List<TridMatch>(MaxMatches);
                foreach (var r in results)
                {
                    if (matches.Count >= MaxMatches) break;
                    // 无匹配时引擎会返回一条空结果，跳过
                    if (r.Points <= 0 && string.IsNullOrEmpty(r.FileType)) continue;

                    matches.Add(new TridMatch
                    {
                        Rank = matches.Count + 1,
                        TypeDescription = r.FileType ?? "",
                        Extension = (r.FileExt ?? "").TrimStart('.'),
                        Points = r.Points,
                        // TrID 给的 Perc 已经是 0~100 的置信度，换成 0~1
                        Confidence = Math.Clamp(r.Perc / 100.0, 0, 1)
                    });
                }

                return new TridResult { Matches = matches };
            }
            catch (Exception ex)
            {
                return new TridResult { Error = ex.Message };
            }
        }
    }

    public void Dispose()
    {
        // 托管引擎没有非托管资源需要释放
    }
}
