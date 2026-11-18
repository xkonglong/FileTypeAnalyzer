using System.Runtime.InteropServices;

namespace FileTypeAnalyzer.Core;

public sealed class TridEngine : ITridEngine
{
    private const string Dll = "TrIDLib.dll";

    private const int TRID_GET_RES_NUM = 1;
    private const int TRID_GET_RES_FILETYPE = 2;
    private const int TRID_GET_RES_FILEEXT = 3;
    private const int TRID_GET_RES_POINTS = 4;

    [DllImport(Dll, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall,
        EntryPoint = "TrID_LoadDefsPack")]
    private static extern int TridLoadDefsPack(string sPath);

    [DllImport(Dll, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall,
        EntryPoint = "TrID_SubmitFileA")]
    private static extern int TridSubmitFileA(string sFilename);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall,
        EntryPoint = "TrID_Analyze")]
    private static extern int TridAnalyze();

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall,
        EntryPoint = "TrID_GetInfo")]
    private static extern int TridGetInfo(int infoType, int infoIdx, IntPtr buffer);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall,
        EntryPoint = "TrID_Free")]
    private static extern void TridFree();

    private readonly object _lock = new();
    private bool _loaded;
    private string _loadError = "";

    public TridEngine(string defsPath)
    {
        try
        {
            // 第 2 步：从传入路径中取出目录
            var defsDir = Path.GetDirectoryName(defsPath);
            if (string.IsNullOrEmpty(defsDir) || !Directory.Exists(defsDir))
            {
                _loadError = $"找不到定义库目录: {defsDir}";
                return;
            }

            // 兼容文件名大小写：TrID_LoadDefsPack 固定查找 TrIDDefs.TRD
            var target = Path.Combine(defsDir, "TrIDDefs.TRD");
            if (!File.Exists(target))
            {
                var fallback = Path.Combine(defsDir, "triddefs.trd");
                if (File.Exists(fallback))
                {
                    try
                    {
                        File.Copy(fallback, target, overwrite: false);
                    }
                    catch
                    {
                        _loadError = "找到 triddefs.trd，但无法创建 TrIDDefs.TRD（目录无写权限），请手动重命名";
                        return;
                    }
                }
                else
                {
                    _loadError = $"目录中找不到 TrIDDefs.TRD: {defsDir}";
                    return;
                }
            }

            // 0 字节文件通常是之前加载失败留下的坏库
            var info = new FileInfo(target);
            if (!info.Exists || info.Length == 0)
            {
                _loadError = "TrIDDefs.TRD 为 0 字节或不可读，请重新下载定义库";
                return;
            }

            // 第 2 步核心：传目录，不传文件路径
            var loadResult = TridLoadDefsPack(defsDir);
            if (loadResult == 0)
            {
                _loadError = $"TrID_LoadDefsPack 返回 0（库大小 {info.Length / 1024} KB），请确认定义库有效";
                return;
            }

            _loaded = true;
        }
        catch (DllNotFoundException)
        {
            _loadError = "未找到 TrIDLib.dll（请确认是 32 位 DLL 且位于程序目录）";
        }
        catch (BadImageFormatException)
        {
            _loadError = "TrIDLib.dll 位数与进程不匹配（请将项目设为 x86）";
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
    }

    public bool IsReady => _loaded;
    public string LibraryInfo => _loaded ? "TrID 定义库已加载" : $"未就绪: {_loadError}";

    public TridResult Analyze(string filePath)
    {
        if (!_loaded) return new TridResult { Error = _loadError };

        lock (_lock)
        {
            try
            {
                if (TridSubmitFileA(filePath) == 0)
                    return new TridResult { Error = "TridSubmitFileA 失败（文件不可读或路径无效）" };

                var count = TridAnalyze();
                if (count <= 0) return new TridResult();

                var buffer = Marshal.AllocHGlobal(1024);
                try
                {
                    var raw = new List<(string type, string ext, int points)>(count);
                    long total = 0;

                    for (var i = 1; i <= count; i++)
                    {
                        var type = ReadString(TRID_GET_RES_FILETYPE, i, buffer);
                        var ext = ReadString(TRID_GET_RES_FILEEXT, i, buffer);
                        var points = TridGetInfo(TRID_GET_RES_POINTS, i, buffer);
                        total += points;
                        raw.Add((type, ext, points));
                    }

                    var matches = new List<TridMatch>(raw.Count);
                    for (var i = 0; i < raw.Count; i++)
                    {
                        var r = raw[i];
                        matches.Add(new TridMatch
                        {
                            Rank = i + 1,
                            TypeDescription = r.type,
                            Extension = r.ext,
                            Points = r.points,
                            Confidence = total > 0 ? (double)r.points / total : 0
                        });
                    }

                    return new TridResult { Matches = matches };
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                return new TridResult { Error = ex.Message };
            }
        }
    }

    private static string ReadString(int infoType, int index, IntPtr buffer)
    {
        TridGetInfo(infoType, index, buffer);
        return Marshal.PtrToStringAnsi(buffer) ?? "";
    }

    public void Dispose()
    {
        if (!_loaded) return;
        try { TridFree(); } catch { }
        _loaded = false;
    }
}