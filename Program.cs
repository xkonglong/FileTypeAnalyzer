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

        // TrID 定义库放在用户目录（安装目录可能没有写权限）；没装外部库时引擎自动用内置快照
        var tridRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileTypeAnalyzer");
        var tridStore = new TridDefsStore(tridRoot);

        var analyzer = new FileAnalyzer(new TridEngine(tridStore), new MagikaCliEngine(magikaExe));
        Application.Run(new MainForm(analyzer, tridStore, magikaExe, langDir, args));
    }
}
