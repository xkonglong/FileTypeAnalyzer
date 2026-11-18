using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace FileTypeAnalyzer.UI;

/// <summary>
/// 图标字形。字体随程序打包（assets/fa-solid-900.ttf，Font Awesome Free Solid，SIL OFL 1.1），
/// 不再依赖系统是否安装 Segoe Fluent Icons / Segoe MDL2 Assets，Win10 与 Win11 表现一致。
/// </summary>
public static class Glyphs
{
    public const string OpenFile = "\uF07C";   // folder-open
    public const string Setting = "\uF013";    // gear
    public const string Theme = "\uF042";      // circle-half-stroke（半明半暗）
}

/// <summary>
/// 主题颜色。颜色为可变属性，可在运行期通过 <see cref="Apply"/> 切换明暗；
/// 已经创建的控件不会自动重绘，需要在切换后重新设置其 BackColor / ForeColor。
/// </summary>
public static class Theme
{
    public static bool IsDark { get; private set; } = true;

    public static Color DarkBg { get; private set; } = ColorTranslator.FromHtml("#121417");
    public static Color Panel { get; private set; } = ColorTranslator.FromHtml("#1A1D22");
    public static Color Border { get; private set; } = ColorTranslator.FromHtml("#2C313A");
    public static Color TextMain { get; private set; } = ColorTranslator.FromHtml("#D6D9E0");
    public static Color TextMuted { get; private set; } = ColorTranslator.FromHtml("#8A929F");
    public static Color Good { get; private set; } = ColorTranslator.FromHtml("#63E2A8");
    public static Color Warn { get; private set; } = ColorTranslator.FromHtml("#FFC864");
    public static Color Bad { get; private set; } = ColorTranslator.FromHtml("#FF6B72");
    public static Color Primary { get; private set; } = ColorTranslator.FromHtml("#2563EB");
    public static Color PrimaryHover { get; private set; } = ColorTranslator.FromHtml("#1D4ED8");

    public static readonly Font Sans = new("Segoe UI", 9.5f);
    public static readonly Font SansBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font SansSmall = new("Segoe UI", 8.5f);
    public static readonly Font Mono = new("Consolas", 9.5f);

    /// <summary>
    /// 工具栏图标字体：从嵌入资源加载 Font Awesome，任何 Windows 版本都能显示。
    /// 资源缺失时退回系统字体，避免启动崩溃。
    /// 注意：IconFonts 必须声明在 IconFont 之前，静态字段按声明顺序初始化。
    /// </summary>
    private static readonly PrivateFontCollection IconFonts = new();
    private static byte[]? _iconFontBytes;

    public static readonly Font IconFont = CreateIconFont();

    private static Font CreateIconFont()
    {
        try
        {
            using var stream = typeof(Theme).Assembly.GetManifestResourceStream("icons.ttf");
            if (stream is not null)
            {
                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);

                // AddMemoryFont 要求内存在字体使用期间一直有效，因此保留这块非托管内存
                var ptr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                _iconFontBytes = bytes;
                IconFonts.AddMemoryFont(ptr, bytes.Length);

                if (IconFonts.Families.Length > 0)
                    return new Font(IconFonts.Families[0], 13f);
            }
        }
        catch
        {
            // 退回系统字体：即使图标显示为方框，也不影响其它功能
        }

        return new Font("Segoe UI", 13f);
    }

    public static void Apply(bool dark)
    {
        IsDark = dark;
        if (dark)
        {
            DarkBg = ColorTranslator.FromHtml("#121417");
            Panel = ColorTranslator.FromHtml("#1A1D22");
            Border = ColorTranslator.FromHtml("#2C313A");
            TextMain = ColorTranslator.FromHtml("#D6D9E0");
            TextMuted = ColorTranslator.FromHtml("#8A929F");
            Good = ColorTranslator.FromHtml("#63E2A8");
            Warn = ColorTranslator.FromHtml("#FFC864");
            Bad = ColorTranslator.FromHtml("#FF6B72");
            Primary = ColorTranslator.FromHtml("#2563EB");
            PrimaryHover = ColorTranslator.FromHtml("#1D4ED8");
        }
        else
        {
            DarkBg = ColorTranslator.FromHtml("#F4F5F7");
            Panel = ColorTranslator.FromHtml("#FFFFFF");
            Border = ColorTranslator.FromHtml("#D7DBE2");
            TextMain = ColorTranslator.FromHtml("#1F242C");
            TextMuted = ColorTranslator.FromHtml("#6B7280");
            Good = ColorTranslator.FromHtml("#12805C");
            Warn = ColorTranslator.FromHtml("#B26A00");
            Bad = ColorTranslator.FromHtml("#C0303A");
            Primary = ColorTranslator.FromHtml("#2563EB");
            PrimaryHover = ColorTranslator.FromHtml("#1D4ED8");
        }
    }
}
