using FileTypeAnalyzer.Core;
using FileTypeAnalyzer.UI;

namespace FileTypeAnalyzer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var baseDir = AppContext.BaseDirectory;
        var langDir = Path.Combine(baseDir, "lang");
        var magikaExe = Environment.GetEnvironmentVariable("MAGIKA_PATH")
                        ?? Path.Combine(baseDir, "magika.exe");

        var settings = AppSettings.Load();
        if (!Localization.Load(langDir, settings.Language))
            Localization.Load(langDir, "zh-CN");

        Theme.Apply(settings.DarkTheme);

        // TrID 定义库放在程序目录（便携、跟着 exe 走）；目录不可写（如装在 Program Files）时才退回用户目录
        var tridRoot = IsWritable(baseDir)
            ? baseDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileTypeAnalyzer");

        var tridStore = new TridDefsStore(tridRoot);

        var analyzer = new FileAnalyzer(new TridEngine(tridStore), new MagikaCliEngine(magikaExe));
        Application.Run(new MainForm(analyzer, tridStore, magikaExe, langDir, args));
    }

    /// <summary>探测目录是否可写（用临时文件试，失败即视为只读）。</summary>
    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".write-probe");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
