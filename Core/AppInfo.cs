namespace FileTypeAnalyzer;

/// <summary>应用级常量：版本号、官网地址、程序图标。</summary>
public static class AppInfo
{
    /// <summary>版权信息中的官网地址，设置窗口中可点击打开。</summary>
    public const string Website = "https://xkonglong.com";

    /// <summary>版本号，取自程序集版本（可通过 csproj 的 Version 属性维护）。</summary>
    public static readonly string Version = FormatVersion(typeof(AppInfo).Assembly.GetName().Version);

    /// <summary>
    /// 窗体图标：读取嵌入的 FileTypeAnalyzer.ico（与 exe 图标同一个文件），
    /// 这样替换 .ico 后重新编译就能同时更新标题栏、任务栏和 exe 图标。
    /// 嵌入资源缺失时退回 exe 关联图标。
    /// </summary>
    public static Icon? LoadIcon()
    {
        try
        {
            using var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("app.ico");
            if (stream is not null)
                return new Icon(stream);
        }
        catch
        {
            // 忽略，走下面的回退
        }

        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                return Icon.ExtractAssociatedIcon(exePath);
        }
        catch
        {
            // 图标不是关键功能，失败时保持窗体默认图标
        }

        return null;
    }

    private static string FormatVersion(Version? version)
    {
        if (version is null) return "0.0.0";
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
