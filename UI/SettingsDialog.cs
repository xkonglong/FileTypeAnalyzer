using FileTypeAnalyzer.Core;

namespace FileTypeAnalyzer.UI;

public sealed class SettingsDialog : Form
{
    private readonly EngineUpdater _updater;
    private readonly string _magikaExePath;
    private readonly string _langDir;

    private readonly Label _languageLabel = new();
    private readonly ComboBox _languageCombo = new();

    private readonly Label _tridLabel = new();
    private readonly Label _tridStatus = new();

    private readonly Label _magikaLabel = new();
    private readonly Label _magikaStatus = new();
    private readonly Button _magikaUpdateBtn = new();

    private readonly TextBox _logBox = new();
    private readonly Label _copyrightLabel = new();
    private readonly LinkLabel _websiteLink = new();
    private bool _suppressLangChange;

    // Magika 版本状态：供语言切换后重建按钮/状态文案使用（null 表示尚未检测）
    private bool? _magikaHasUpdate;
    private string _magikaLatestTag = "";
    private string _magikaCurrentVersion = "";
    private string? _magikaCheckError;

    public bool MagikaUpdated { get; private set; }
    public bool LanguageChanged { get; private set; }

    public SettingsDialog(EngineUpdater updater, string magikaExePath, string langDir)
    {
        _updater = updater;
        _magikaExePath = magikaExePath;
        _langDir = langDir;

        InitializeDialog();

        _suppressLangChange = true;
        _languageCombo.SelectedIndex = Localization.CurrentLanguage == "en-US" ? 1 : 0;
        _suppressLangChange = false;

        ApplyLocalization();
        _ = RefreshMagikaVersionAsync();
    }

    private void InitializeDialog()
    {
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppInfo.LoadIcon() ?? Icon;
        ClientSize = new Size(640, 500);
        BackColor = Theme.Panel;
        ForeColor = Theme.TextMain;
        Font = Theme.Sans;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
            BackColor = Theme.Panel
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildLanguageRow(), 0, 0);
        root.Controls.Add(BuildEngineRow(_tridLabel, _tridStatus, null, null), 0, 1);
        root.Controls.Add(BuildEngineRow(_magikaLabel, _magikaStatus, _magikaUpdateBtn, UpdateMagikaAsync), 0, 2);
        root.Controls.Add(BuildLogBox(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);

        Controls.Add(root);
    }

    private Control BuildLanguageRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _languageLabel.Font = Theme.SansBold;
        _languageLabel.ForeColor = Theme.TextMain;
        _languageLabel.AutoSize = true;
        _languageLabel.Margin = new Padding(0, 8, 0, 0);

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.FlatStyle = FlatStyle.Flat;
        _languageCombo.BackColor = Theme.Panel;
        _languageCombo.ForeColor = Theme.TextMain;
        _languageCombo.Font = Theme.Sans;
        _languageCombo.Width = 160;
        _languageCombo.Margin = new Padding(8, 4, 0, 0);
        _languageCombo.Items.Add("中文");
        _languageCombo.Items.Add("English");
        _languageCombo.SelectedIndexChanged += LanguageCombo_SelectedIndexChanged;

        row.Controls.Add(_languageLabel, 0, 0);
        row.Controls.Add(_languageCombo, 1, 0);
        return row;
    }

    /// <summary>引擎信息行。button 为 null 表示该引擎不需要更新（例如内置定义的 TrID）。</summary>
    private Control BuildEngineRow(Label titleLabel, Label status, Button? button, Func<Task>? onClick)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        titleLabel.Font = Theme.SansBold;
        titleLabel.ForeColor = Theme.TextMain;
        titleLabel.AutoSize = true;
        titleLabel.Margin = new Padding(0, 0, 0, 4);

        status.Font = Theme.SansSmall;
        status.ForeColor = Theme.TextMuted;
        status.AutoSize = true;

        row.Controls.Add(titleLabel, 0, 0);
        row.Controls.Add(status, 0, 1);

        if (button is not null && onClick is not null)
        {
            StyleUpdateButton(button);
            button.Click += async (_, _) => await onClick();
            row.Controls.Add(button, 1, 0);
            row.SetRowSpan(button, 2);
        }
        else
        {
            // 无按钮时让状态文字跨两行，避免布局塌陷
            row.SetRowSpan(status, 2);
        }

        return row;
    }

    private Control BuildFooter()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Panel,
            Margin = new Padding(0, 12, 0, 0)
        };

        _copyrightLabel.AutoSize = true;
        _copyrightLabel.Font = Theme.SansSmall;
        _copyrightLabel.ForeColor = Theme.TextMuted;
        _copyrightLabel.Margin = new Padding(0, 6, 0, 0);

        _websiteLink.AutoSize = true;
        _websiteLink.Font = Theme.SansSmall;
        _websiteLink.ForeColor = Theme.TextMuted;
        _websiteLink.LinkColor = Theme.Primary;
        _websiteLink.ActiveLinkColor = Theme.PrimaryHover;
        _websiteLink.VisitedLinkColor = Theme.Primary;
        _websiteLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _websiteLink.Margin = new Padding(0, 6, 0, 0);
        _websiteLink.LinkClicked += WebsiteLink_LinkClicked;

        row.Controls.Add(_copyrightLabel);
        row.Controls.Add(_websiteLink);
        return row;
    }

    private static void WebsiteLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppInfo.Website,
                UseShellExecute = true
            });
        }
        catch { /* 打不开浏览器时静默忽略 */ }
    }

    private Control BuildLogBox()
    {
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = Theme.DarkBg;
        _logBox.ForeColor = Theme.TextMuted;
        _logBox.Font = Theme.Mono;
        _logBox.BorderStyle = BorderStyle.None;
        _logBox.Margin = new Padding(0, 8, 0, 0);
        return _logBox;
    }

    private static void StyleUpdateButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Theme.Primary;
        b.ForeColor = Color.White;
        b.Font = Theme.Sans;
        b.AutoSize = true;
        b.UseMnemonic = false;   // 文案里含 "&"，避免被当作加速键
        b.Padding = new Padding(12, 4, 12, 4);
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.PrimaryHover;
        b.Cursor = Cursors.Hand;
        b.Margin = new Padding(8, 0, 0, 0);
    }

    private void ApplyLocalization()
    {
        Text = Localization.T("settings.title");
        _languageLabel.Text = Localization.T("settings.language.label");
        _tridLabel.Text = Localization.T("update.trid.label");
        _magikaLabel.Text = Localization.T("update.magika.label");

        _copyrightLabel.Text = Localization.T("settings.copyright");
        _websiteLink.Text = AppInfo.Website;

        _tridStatus.Text = Localization.T("update.trid.builtin");
        _tridStatus.ForeColor = Theme.TextMuted;

        ApplyMagikaState();
    }

    /// <summary>根据当前 Magika 状态（未安装 / 检测中 / 有更新 / 已最新 / 检测失败）刷新文案。</summary>
    private void ApplyMagikaState()
    {
        if (!File.Exists(_magikaExePath))
        {
            _magikaStatus.Text = Localization.T("update.magika.notInstalledHint");
            _magikaStatus.ForeColor = Theme.Warn;
            _magikaUpdateBtn.Text = Localization.T("update.magika.checkButton");
            return;
        }

        if (_magikaCheckError is not null)
        {
            _magikaStatus.Text = Localization.T("update.magika.checkFailed", _magikaCheckError);
            _magikaStatus.ForeColor = Theme.TextMuted;
            _magikaUpdateBtn.Text = Localization.T("update.magika.recheckButton");
            return;
        }

        switch (_magikaHasUpdate)
        {
            case null:
                _magikaStatus.Text = Localization.T("update.magika.detecting");
                _magikaStatus.ForeColor = Theme.TextMuted;
                _magikaUpdateBtn.Text = Localization.T("update.magika.checkButton");
                break;
            case true:
                _magikaStatus.Text = string.IsNullOrEmpty(_magikaCurrentVersion)
                    ? Localization.T("update.magika.hasUpdateNoCur", _magikaLatestTag)
                    : Localization.T("update.magika.hasUpdate", _magikaLatestTag, _magikaCurrentVersion);
                _magikaStatus.ForeColor = Theme.Warn;
                _magikaUpdateBtn.Text = Localization.T("update.trid.button");
                break;
            default:
                _magikaStatus.Text = string.IsNullOrEmpty(_magikaCurrentVersion)
                    ? Localization.T("update.magika.latestNoCur")
                    : Localization.T("update.magika.latest", _magikaCurrentVersion);
                _magikaStatus.ForeColor = Theme.Good;
                _magikaUpdateBtn.Text = Localization.T("update.magika.recheckButton");
                break;
        }
    }

    private void LanguageCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressLangChange) return;
        var code = _languageCombo.SelectedIndex == 1 ? "en-US" : "zh-CN";
        if (code == Localization.CurrentLanguage) return;

        Localization.Load(_langDir, code);
        var settings = AppSettings.Load();
        settings.Language = code;
        settings.Save();

        LanguageChanged = true;
        ApplyLocalization();
    }

    private void AppendLog(string msg) =>
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");

    private async Task RefreshMagikaVersionAsync()
    {
        if (!File.Exists(_magikaExePath)) return;

        try
        {
            var (hasUpdate, latestTag, _, currentVersion) =
                await _updater.CheckMagikaUpdateAsync(_magikaExePath);
            _magikaHasUpdate = hasUpdate;
            _magikaLatestTag = latestTag;
            _magikaCurrentVersion = currentVersion;
            _magikaCheckError = null;
            ApplyMagikaState();
        }
        catch (Exception ex)
        {
            _magikaCheckError = ex.Message;
            ApplyMagikaState();
        }
    }

    private async Task UpdateMagikaAsync()
    {
        _magikaUpdateBtn.Enabled = false;
        try
        {
            var (hasUpdate, latestTag, downloadUrl, currentVersion) =
                await _updater.CheckMagikaUpdateAsync(_magikaExePath);
            _magikaCurrentVersion = currentVersion;

            if (!hasUpdate)
            {
                AppendLog(Localization.T("update.magika.alreadyLatest"));
                await RefreshMagikaVersionAsync();
                return;
            }
            if (string.IsNullOrEmpty(downloadUrl))
                throw new InvalidOperationException(Localization.T("update.magika.noDownload"));

            AppendLog(Localization.T("update.magika.downloading", latestTag));
            var (msg, installedVersion) = await _updater.UpdateMagikaCliAsync(
                _magikaExePath, downloadUrl, latestTag, new Progress<string>(AppendLog));

            MagikaUpdated = true;
            AppendLog(Localization.T("update.log.ok", msg));
            _magikaHasUpdate = false;
            _magikaLatestTag = "";
            _magikaCurrentVersion = installedVersion;
            _magikaCheckError = null;
            ApplyMagikaState();
            _magikaStatus.Text = string.IsNullOrEmpty(installedVersion)
                ? Localization.T("update.magika.updatedNoVer")
                : Localization.T("update.magika.updated", installedVersion);
            _magikaStatus.ForeColor = Theme.Good;
        }
        catch (Exception ex)
        {
            _magikaStatus.Text = Localization.T("update.magika.failed");
            _magikaStatus.ForeColor = Theme.Bad;
            AppendLog(Localization.T("update.log.fail", "Magika", ex.Message));
        }
        finally { _magikaUpdateBtn.Enabled = true; }
    }
}
