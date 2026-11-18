using System.Collections;
using System.Reflection;
using System.Text;

namespace FileTypeAnalyzer.Core;

/// <summary>
/// TrID 定义对象的二进制缓存。
///
/// 为什么需要：官方 XML 库有 2.2 万个定义文件，用 PatternEngine.LoadDefinitionByXml
/// 逐条解析需要 3.4 秒左右（XML 解析占大头）。把解析结果（FileDefPat / SomePattern）
/// 直接序列化成二进制后，下次启动读缓存只需几百毫秒。
///
/// 结构说明（来自 TrID.dll 的公开/半公开嵌套类型）：
///   FileDefPat { FileType, FileExt, FrontBlockSize, GlobalStrings(ArrayList), Patterns(ArrayList), ExtraInfo }
///   SomePattern { Pattern(byte[]), Pos, Len, Points, XM }   ← 类型非 public，需反射创建
///   ExtraInfo 是 struct，字段为字符串/整数
/// 版本号写进文件头，格式变化时旧缓存自动失效。
/// </summary>
internal static class TridDefsCache
{
    private const string Magic = "TRIDDEFS-CACHE-3";

    private static readonly Type EngineType = typeof(TrID.TrIDEngine);
    private static readonly Type DefType = EngineType.GetNestedType("FileDefPat")!;
    private static readonly Type PatternType =
        EngineType.GetNestedType("SomePattern", BindingFlags.NonPublic | BindingFlags.Public)!;

    private static readonly FieldInfo FBytes = PatternType.GetField("Pattern")!;
    private static readonly FieldInfo FPos = PatternType.GetField("Pos")!;
    private static readonly FieldInfo FLen = PatternType.GetField("Len")!;
    private static readonly FieldInfo FPoints = PatternType.GetField("Points")!;
    private static readonly FieldInfo FXm = PatternType.GetField("XM")!;

    // GlobalStrings / Patterns 在 FileDefPat 里是 readonly 字段，无法直接赋值，走反射
    private static readonly FieldInfo FGlobals = DefType.GetField("GlobalStrings")!;
    private static readonly FieldInfo FPatterns = DefType.GetField("Patterns")!;

    // GlobalStrings 装的不是 string，而是 TrID 内部的 ByteString（只有一个 byte[] data 字段）
    private static readonly Type ByteStringType =
        EngineType.GetNestedType("ByteString", BindingFlags.NonPublic | BindingFlags.Public)!;
    private static readonly FieldInfo FByteStringData = ByteStringType.GetField("data")!;

    /// <summary>读缓存；sourceCount 是定义文件数，与缓存头不一致（定义库已更新）时返回 null。</summary>
    public static ArrayList? TryLoad(string cachePath, int sourceCount)
    {
        if (!File.Exists(cachePath)) return null;
        try
        {
            using var fs = File.OpenRead(cachePath);
            using var br = new BinaryReader(fs, Encoding.UTF8);

            if (br.ReadString() != Magic) return null;
            var cachedSource = br.ReadInt32();
            if (cachedSource != sourceCount) return null;

            var defCount = br.ReadInt32();
            br.ReadInt64();   // 写出时间，仅作参考

            var list = new ArrayList(defCount);
            for (var i = 0; i < defCount; i++)
            {
                var def = (TrID.TrIDEngine.FileDefPat)Activator.CreateInstance(DefType)!;
                def.FileType = br.ReadString();
                def.FileExt = br.ReadString();
                def.FrontBlockSize = br.ReadInt32();

                var gCount = br.ReadInt32();
                var globals = new ArrayList(gCount);
                for (var g = 0; g < gCount; g++)
                {
                    var len = br.ReadInt32();
                    var raw = br.ReadBytes(len);
                    if (raw.Length != len) throw new EndOfStreamException();
                    var bs = Activator.CreateInstance(ByteStringType)!;
                    FByteStringData.SetValue(bs, raw);
                    globals.Add(bs);
                }
                FGlobals.SetValue(def, globals);

                var pCount = br.ReadInt32();
                var patterns = new ArrayList(pCount);
                for (var p = 0; p < pCount; p++)
                {
                    var pat = Activator.CreateInstance(PatternType, true)!;
                    var len = br.ReadInt32();
                    FBytes.SetValue(pat, br.ReadBytes(len));
                    FPos.SetValue(pat, br.ReadInt32());
                    FLen.SetValue(pat, br.ReadInt32());
                    FPoints.SetValue(pat, br.ReadInt32());
                    FXm.SetValue(pat, br.ReadBoolean());
                    patterns.Add(pat);
                }
                FPatterns.SetValue(def, patterns);

                var ei = new TrID.TrIDEngine.ExtraInfo
                {
                    FileType = br.ReadString(),
                    FileExt = br.ReadString(),
                    FilePts = br.ReadString(),
                    FilesScanned = br.ReadInt32(),
                    AuthorName = br.ReadString(),
                    AuthorEMail = br.ReadString(),
                    AuthorHome = br.ReadString(),
                    DefFile = br.ReadString(),
                    Remark = br.ReadString(),
                    RelURL = br.ReadString()
                };
                def.ExtraInfo = ei;

                list.Add(def);
            }

            return list.Count > 0 ? list : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string cachePath, ArrayList defs, int sourceCount)
    {
        var tmp = cachePath + ".tmp";
        try
        {
            using (var fs = File.Create(tmp))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8))
            {
                bw.Write(Magic);
                bw.Write(sourceCount);
                bw.Write(defs.Count);
                bw.Write(DateTime.UtcNow.Ticks);

                foreach (TrID.TrIDEngine.FileDefPat def in defs)
                {
                    bw.Write(def.FileType ?? "");
                    bw.Write(def.FileExt ?? "");
                    bw.Write(def.FrontBlockSize);

                    var globals = def.GlobalStrings ?? new ArrayList();
                    bw.Write(globals.Count);
                    foreach (var s in globals)
                    {
                        var data = s is null ? Array.Empty<byte>() : (byte[])FByteStringData.GetValue(s)!;
                        bw.Write(data.Length);
                        bw.Write(data);
                    }

                    var patterns = def.Patterns ?? new ArrayList();
                    bw.Write(patterns.Count);
                    foreach (var pat in patterns)
                    {
                        var bytes = (byte[])FBytes.GetValue(pat)!;
                        bw.Write(bytes.Length);
                        bw.Write(bytes);
                        bw.Write((int)FPos.GetValue(pat)!);
                        bw.Write((int)FLen.GetValue(pat)!);
                        bw.Write((int)FPoints.GetValue(pat)!);
                        bw.Write((bool)FXm.GetValue(pat)!);
                    }

                    var ei = def.ExtraInfo;
                    bw.Write(ei.FileType ?? "");
                    bw.Write(ei.FileExt ?? "");
                    bw.Write(ei.FilePts ?? "");
                    bw.Write(ei.FilesScanned);
                    bw.Write(ei.AuthorName ?? "");
                    bw.Write(ei.AuthorEMail ?? "");
                    bw.Write(ei.AuthorHome ?? "");
                    bw.Write(ei.DefFile ?? "");
                    bw.Write(ei.Remark ?? "");
                    bw.Write(ei.RelURL ?? "");
                }
            }
            File.Move(tmp, cachePath, true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}
