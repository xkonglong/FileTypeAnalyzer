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
        var tridDefs = Path.Combine(baseDir, "triddefs.trd");
        var magikaExe = Environment.GetEnvironmentVariable("MAGIKA_PATH")
                        ?? Path.Combine(baseDir, "magika.exe");

        var settings = AppSettings.Load();
        if (!Localization.Load(langDir, settings.Language))
            Localization.Load(langDir, "zh-CN");

        Theme.Apply(settings.DarkTheme);

        var analyzer = new FileAnalyzer(new TridEngine(tridDefs), new MagikaCliEngine(magikaExe));
        Application.Run(new MainForm(analyzer, tridDefs, magikaExe, langDir, args));
    }
}