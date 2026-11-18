using System.Text.Json;
using FileTypeAnalyzer.Core;

namespace FileTypeAnalyzer.UI;

public partial class MainForm : Form
{
    private FileAnalyzer _analyzer;
    private readonly TridDefsStore _tridStore;
    private readonly string _magikaExePath;
    private readonly string _langDir;
    private readonly string[] _initialFiles;
    private readonly bool _isFromSendTo;
    private FileAnalysisResult? _lastResult;
    private string? _currentFile;

    private readonly ToolTip _toolTip = new()
    {
        InitialDelay = 300,
        ReshowDelay = 100,
        AutoPopDelay = 5000
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>程序版本，取自程序集版本（可在 csproj 的 Version 属性中维护）。</summary>
    private static readonly string AppVersion = AppInfo.Version;

    public MainForm(FileAnalyzer analyzer, TridDefsStore tridStore, string magikaExePath,
        string langDir, string[] initialFiles)
    {
        _analyzer = analyzer;
        _tridStore = tridStore;
        _magikaExePath = magikaExePath;
        _langDir = langDir;
        _initialFiles = initialFiles;
        _isFromSendTo = initialFiles.Length > 0 && File.Exists(initialFiles[0]);

        InitializeComponent();
        Icon = AppInfo.LoadIcon() ?? Icon;
        BuildUi();
        BindEvents();
        EnableDragDrop(this);

        ApplyLocalization();
        ApplyEngineStatus();
        Shown += MainForm_Shown;
    }

    private void BindEvents()
    {
        _openButton.Click += OpenButton_Click;
        _analyzeButton.Click += AnalyzeButton_Click;
        _settingsButton.Click += SettingsButton_Click;
        _themeButton.Click += ThemeButton_Click;
        _tridRawButton.Click += TridRawButton_Click;
        _magikaRawButton.Click += MagikaRawButton_Click;
        _fileNameLabel.DoubleClick += FileNameLabel_DoubleClick;
    }

    // ===== 界面装配 =====

    /// <summary>
    /// 按当前主题装配窗体内容：工具栏 / 状态行 / 结果面板 / 底部摘要。
    /// 特意写在 MainForm.cs 而不是 Designer.cs —— 用 VS 设计器保存窗体时只会重写
    /// InitializeComponent，不会动这里，避免再次出现"控件全为 null"的启动闪退。
    /// </summary>
    private void BuildUi()
    {
        BackColor = Theme.DarkBg;
        ForeColor = Theme.TextMain;
        Font = Theme.Sans;

        root.SuspendLayout();
        root.Controls.Clear();
        root.ColumnCount = 1;
        root.RowCount = 4;
        root.Dock = DockStyle.Fill;

        root.ColumnStyles.Clear();
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        root.RowStyles.Clear();
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildStatusRow(), 0, 1);
        root.Controls.Add(BuildResultPanels(), 0, 2);
        root.Controls.Add(BuildSummary(), 0, 3);

        root.ResumeLayout(true);
    }

    // ===== 拖放 =====

    private void EnableDragDrop(Control parent)
    {
        parent.AllowDrop = true;
        parent.DragEnter += MainForm_DragEnter;
        parent.DragDrop += MainForm_DragDrop;
        foreach (Control child in parent.Controls)
            EnableDragDrop(child);
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (!_analyzeButton.Enabled)
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        var file = files.FirstOrDefault(File.Exists);
        if (file is null) return;
        _ = RunAnalysisAsync(file);
    }

    // ===== 本地化 =====

    private void ApplyLocalization()
    {
        var titleKey = _isFromSendTo ? "app.title.sendTo" : "app.title";
        Text = $"{Localization.T(titleKey)} {Localization.T("app.version", AppVersion)}";

        _openButton.AccessibleName = Localization.T("tooltip.openFile");
        _settingsButton.AccessibleName = Localization.T("tooltip.settings");
        _themeButton.AccessibleName = Localization.T("tooltip.theme");
        _analyzeButton.Text = Localization.T("btn.analyze");

        _toolTip.SetToolTip(_openButton, Localization.T("tooltip.openFile"));
        _toolTip.SetToolTip(_settingsButton, Localization.T("tooltip.settings"));
        _toolTip.SetToolTip(_themeButton, Localization.T("tooltip.theme"));
        _toolTip.SetToolTip(_fileNameLabel, Localization.T("tooltip.copyPath"));

        if (string.IsNullOrEmpty(_currentFile))
            _fileNameLabel.Text = Localization.T("label.noFileSelected");

        _tridSubLabel.Text = Localization.T("trid.subtitle");
        _tridRawButton.Text = Localization.T("trid.rawButton");
        _magikaRawButton.Text = Localization.T("magika.rawButton");

        _magikaKeyLabels[0].Text = Localization.T("magika.field.label");
        _magikaKeyLabels[1].Text = Localization.T("magika.field.mime");
        _magikaKeyLabels[2].Text = Localization.T("magika.field.description");
        _magikaKeyLabels[3].Text = Localization.T("magika.field.score");
        _magikaKeyLabels[4].Text = Localization.T("magika.field.isText");

        if (_lastResult is null)
            _summaryLabel.Text = Localization.T("summary.placeholder");
    }

    private void ApplyEngineStatus()
    {
        var tridOk = _analyzer.TridReady;
        var magikaOk = _analyzer.MagikaReady;

        _tridTitleLabel.Text = tridOk
            ? Localization.T("trid.title")
            : Localization.T("trid.titleNotReady");
        _magikaTitleLabel.Text = magikaOk
            ? Localization.T("magika.title")
            : Localization.T("magika.titleNotReady");

        var tridState = tridOk ? Localization.T("status.ok") : Localization.T("status.notReady");
        var magikaState = magikaOk ? Localization.T("status.ok") : Localization.T("status.notReady");
        _statusLabel.Text = Localization.T("status.idle.engines", tridState, magikaState);
        _statusLabel.ForeColor = Theme.TextMuted;
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        if (_isFromSendTo)
            _ = RunAnalysisAsync(_initialFiles[0]);
        else
            ResetResultPanels();
    }

    // ===== 工具栏事件 =====

    private void OpenButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = Localization.T("dialog.selectFile.title"),
            Filter = Localization.T("dialog.selectFile.filter"),
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _ = RunAnalysisAsync(dlg.FileName);
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(new EngineUpdater(), _tridStore, _magikaExePath, _langDir);
        dlg.ShowDialog(this);

        if (dlg.LanguageChanged)
        {
            ApplyLocalization();
            ApplyEngineStatus();
            if (_lastResult is not null) RenderResult(_lastResult);
        }

        // Magika 换过 exe、或者 TrID 定义库更新过，都需要重建引擎
        if (dlg.MagikaUpdated || dlg.TridUpdated)
        {
            try { _analyzer.Dispose(); } catch { }
            _analyzer = new FileAnalyzer(
                new TridEngine(_tridStore),
                new MagikaCliEngine(_magikaExePath));
            ApplyEngineStatus();
            _statusLabel.Text = Localization.T("status.engineReloaded");
            _statusLabel.ForeColor = Theme.Good;
        }
    }

    private void ThemeButton_Click(object? sender, EventArgs e)
    {
        if (!_analyzeButton.Enabled) return;   // 分析进行中不做重建

        var settings = AppSettings.Load();
        settings.DarkTheme = !Theme.IsDark;
        settings.Save();
        Theme.Apply(settings.DarkTheme);

        RebuildUiForTheme();
    }

    /// <summary>
    /// Theme 的颜色是构建时读取的，运行期改值不会自动重绘。
    /// 这里重建整个界面（控件全部按新配色重新创建），再恢复当前文件与结果。
    /// </summary>
    private void RebuildUiForTheme()
    {
        SuspendLayout();
        _toolTip.RemoveAll();

        while (Controls.Count > 0)
            Controls[0].Dispose();

        InitializeComponent();
        BuildUi();
        BindEvents();
        // 窗体自身的拖放事件只在构造函数里挂一次，这里只给新建的子控件挂，避免重复触发
        foreach (Control child in Controls)
            EnableDragDrop(child);

        BackColor = Theme.DarkBg;
        ForeColor = Theme.TextMain;

        ApplyLocalization();

        if (!string.IsNullOrEmpty(_currentFile))
        {
            _fileNameLabel.Text = Path.GetFileName(_currentFile);
            _fileNameLabel.ForeColor = Theme.TextMain;
        }

        ApplyEngineStatus();
        if (_lastResult is not null) RenderResult(_lastResult);

        ResumeLayout(true);
        Invalidate(true);
        Update();
    }

    private void FileNameLabel_DoubleClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFile)) return;
        try
        {
            Clipboard.SetText(_currentFile);
            _statusLabel.Text = Localization.T("status.pathCopied");
            _statusLabel.ForeColor = Theme.Good;
        }
        catch { }
    }

    private void AnalyzeButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFile)) { OpenButton_Click(sender, e); return; }
        _ = RunAnalysisAsync(_currentFile);
    }

    // ===== Raw 输出 =====

    private void TridRawButton_Click(object? sender, EventArgs e)
    {
        var text = _lastResult is null
            ? Localization.T("dialog.raw.empty")
            : JsonSerializer.Serialize(_lastResult.Trid, JsonOpts);
        ShowRawDialog(Localization.T("dialog.raw.title.trid"), text);
    }

    private void MagikaRawButton_Click(object? sender, EventArgs e)
    {
        var text = _lastResult is null
            ? Localization.T("dialog.raw.empty")
            : JsonSerializer.Serialize(_lastResult.Magika, JsonOpts);
        ShowRawDialog(Localization.T("dialog.raw.title.magika"), text);
    }

    private void ShowRawDialog(string title, string content)
    {
        using var f = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(720, 480),
            BackColor = Theme.Panel,
            ForeColor = Theme.TextMain,
            Font = Theme.Sans,
            MinimizeBox = false,
            ShowInTaskbar = false
        };
        f.Controls.Add(new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            BackColor = Theme.DarkBg,
            ForeColor = Theme.TextMain,
            Font = Theme.Mono,
            BorderStyle = BorderStyle.None,
            Text = content
        });
        f.ShowDialog(this);
    }

    // ===== 分析流程 =====

    private async Task RunAnalysisAsync(string filePath)
    {
        _currentFile = filePath;
        _fileNameLabel.Text = Path.GetFileName(filePath);
        _fileNameLabel.ForeColor = Theme.TextMain;
        _statusLabel.Text = Localization.T("status.analyzing");
        _statusLabel.ForeColor = Theme.Warn;
        SetBusy(true);
        try
        {
            var result = await _analyzer.AnalyzeAsync(filePath);
            _lastResult = result;
            RenderResult(result);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = Localization.T("status.error", ex.Message);
            _statusLabel.ForeColor = Theme.Bad;
            ResetResultPanels();
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        _analyzeButton.Enabled = !busy;
    }

    private void RenderResult(FileAnalysisResult result)
    {
        if (result.Trid.Succeeded && result.Magika.Succeeded)
        {
            _statusLabel.Text = Localization.T("status.done",
                FormatSize(result.FileSize), result.ModifiedAt.ToString("yyyy-MM-dd HH:mm"));
            _statusLabel.ForeColor = Theme.Good;
        }
        else if (!result.Trid.Succeeded && !result.Magika.Succeeded)
        {
            _statusLabel.Text = Localization.T("status.failedBoth");
            _statusLabel.ForeColor = Theme.Bad;
        }
        else
        {
            var failed = !result.Trid.Succeeded ? "TrID" : "Magika";
            _statusLabel.Text = Localization.T("status.partial", failed);
            _statusLabel.ForeColor = Theme.Warn;
        }

        for (var i = 0; i < 5; i++)
        {
            if (result.Trid.Succeeded && i < result.Trid.Matches.Count)
            {
                var m = result.Trid.Matches[i];
                _tridTypeLabels[i].Text = m.TypeDescription;
                _tridConfLabels[i].Text = $"{m.Confidence * 100:0.0}%";
                _tridConfLabels[i].ForeColor = ConfidenceColor(m.Confidence);
            }
            else
            {
                _tridTypeLabels[i].Text = i == 0 && !result.Trid.Succeeded
                    ? Localization.T("trid.error", result.Trid.Error ?? "") : "";
                _tridConfLabels[i].Text = "";
                _tridConfLabels[i].ForeColor = Theme.TextMuted;
            }
        }

        if (result.Magika.Succeeded)
        {
            _magikaLabelValue.Text = result.Magika.Label;
            _magikaMimeValue.Text = result.Magika.MimeType;
            _magikaDescValue.Text = result.Magika.Description;
            _magikaScoreValue.Text = $"{result.Magika.Score * 100:0.0}%";
            _magikaScoreValue.ForeColor = ConfidenceColor(result.Magika.Score);
            _magikaIsTextValue.Text = result.Magika.IsText
                ? Localization.T("magika.value.true")
                : Localization.T("magika.value.false");
        }
        else
        {
            _magikaLabelValue.Text = Localization.T("magika.empty");
            _magikaMimeValue.Text = Localization.T("magika.empty");
            _magikaDescValue.Text = Localization.T("magika.error", result.Magika.Error ?? "");
            _magikaScoreValue.Text = Localization.T("magika.empty");
            _magikaScoreValue.ForeColor = Theme.TextMuted;
            _magikaIsTextValue.Text = Localization.T("magika.empty");
        }

        _summaryLabel.Text = result.ConsistencyText;
        _summaryLabel.ForeColor = result.Consistency switch
        {
            ConsistencyStatus.Match => Theme.Good,
            ConsistencyStatus.Mismatch => Theme.Warn,
            ConsistencyStatus.PartialFailure => Theme.Warn,
            _ => Theme.TextMuted
        };
    }

    private void ResetResultPanels()
    {
        foreach (var l in _tridTypeLabels) l.Text = "";
        foreach (var l in _tridConfLabels) { l.Text = ""; l.ForeColor = Theme.TextMuted; }
        _magikaLabelValue.Text = Localization.T("magika.empty");
        _magikaMimeValue.Text = Localization.T("magika.empty");
        _magikaDescValue.Text = Localization.T("magika.empty");
        _magikaScoreValue.Text = Localization.T("magika.empty");
        _magikaScoreValue.ForeColor = Theme.TextMuted;
        _magikaIsTextValue.Text = Localization.T("magika.empty");
        _summaryLabel.Text = Localization.T("summary.placeholder");
        _summaryLabel.ForeColor = Theme.TextMuted;
    }

    private static Color ConfidenceColor(double confidence)
    {
        if (confidence >= 0.60) return Theme.Good;
        if (confidence >= 0.10) return Theme.Warn;
        return Theme.Bad;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _analyzer.Dispose();
    }
}
