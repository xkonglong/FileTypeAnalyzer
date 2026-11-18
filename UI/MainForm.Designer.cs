using System.Drawing;
using System.Windows.Forms;

namespace FileTypeAnalyzer.UI;

public partial class MainForm
{
    private Button _openButton = null!;
    private Button _analyzeButton = null!;
    private Button _settingsButton = null!;
    private Button _themeButton = null!;
    private Label _fileNameLabel = null!;
    private Label _statusLabel = null!;

    private Label[] _tridTypeLabels = null!;
    private Label[] _tridConfLabels = null!;
    private Label _tridTitleLabel = null!;
    private Label _tridSubLabel = null!;
    private Button _tridRawButton = null!;

    private Label _magikaTitleLabel = null!;
    private Label[] _magikaKeyLabels = null!;
    private Label _magikaLabelValue = null!;
    private Label _magikaMimeValue = null!;
    private Label _magikaDescValue = null!;
    private Label _magikaScoreValue = null!;
    private Label _magikaIsTextValue = null!;
    private Button _magikaRawButton = null!;

    private Label _summaryLabel = null!;

    private void InitializeComponent()
    {
        root = new TableLayoutPanel();
        SuspendLayout();
        // 
        // root
        // 
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.Location = new Point(0, 0);
        root.Name = "root";
        root.RowStyles.Add(new RowStyle());
        root.RowStyles.Add(new RowStyle());
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle());
        root.Size = new Size(200, 100);
        root.TabIndex = 0;
        // 
        // MainForm
        // 
        BackColor = Color.FromArgb(18, 20, 23);
        ClientSize = new Size(1100, 720);
        Controls.Add(root);
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.FromArgb(214, 217, 224);
        // 图标在 MainForm 构造函数里从文件（AppInfo.LoadIcon）加载，
        // 不再依赖 resx 里设计器留下的图标副本，替换 .ico 后重新编译即可生效。
        MinimumSize = new Size(900, 600);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        ResumeLayout(false);
    }

    private Control BuildToolbar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            // 顶部留出一点呼吸空间，避免图标贴住标题栏/窗体上沿
            Padding = new Padding(6, 8, 6, 0),
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Theme.DarkBg
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.DarkBg,
            Margin = new Padding(0)
        };

        _openButton = MakeIconButton(Glyphs.OpenFile);
        _fileNameLabel = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Theme.TextMuted,
            Font = Theme.Sans,
            Margin = new Padding(8, 8, 0, 0),
            Cursor = Cursors.Hand
        };
        left.Controls.Add(_openButton);
        left.Controls.Add(_fileNameLabel);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            BackColor = Theme.DarkBg,
            Margin = new Padding(0)
        };

        _themeButton = MakeIconButton(Glyphs.Theme);
        _settingsButton = MakeIconButton(Glyphs.Setting);
        _analyzeButton = MakePrimaryButton();
        right.Controls.Add(_themeButton);
        right.Controls.Add(_settingsButton);
        right.Controls.Add(_analyzeButton);

        bar.Controls.Add(left, 0, 0);
        bar.Controls.Add(right, 1, 0);
        return bar;
    }

    private Control BuildStatusRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            Padding = new Padding(16, 8, 16, 8),
            Margin = new Padding(0, 0, 0, 12),
            Height = 40
        };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "",
            ForeColor = Theme.TextMuted,
            Font = Theme.Sans,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private Control BuildResultPanels()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.DarkBg,
            Margin = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.Controls.Add(BuildTridPanel(), 0, 0);
        grid.Controls.Add(BuildMagikaPanel(), 1, 0);
        return grid;
    }

    private Control BuildTridPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 8, 0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Panel
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _tridTitleLabel = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Theme.TextMain,
            Font = Theme.SansBold,
            Margin = new Padding(0, 0, 0, 8)
        };
        _tridSubLabel = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Theme.TextMuted,
            Font = Theme.SansSmall,
            Margin = new Padding(0, 0, 0, 8)
        };

        var matchGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Theme.Panel
        };
        matchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        matchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _tridTypeLabels = new Label[5];
        _tridConfLabels = new Label[5];
        for (var i = 0; i < 5; i++)
        {
            matchGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            _tridTypeLabels[i] = MakeGridLabel(Theme.TextMain, ContentAlignment.MiddleLeft);
            _tridConfLabels[i] = MakeGridLabel(Theme.TextMuted, ContentAlignment.MiddleRight);
            matchGrid.Controls.Add(_tridTypeLabels[i], 0, i);
            matchGrid.Controls.Add(_tridConfLabels[i], 1, i);
        }

        _tridRawButton = MakeLinkButton();
        layout.Controls.Add(_tridTitleLabel, 0, 0);
        layout.Controls.Add(_tridSubLabel, 0, 1);
        layout.Controls.Add(matchGrid, 0, 2);
        layout.Controls.Add(_tridRawButton, 0, 3);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildMagikaPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            Padding = new Padding(16),
            Margin = new Padding(8, 0, 0, 0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Panel
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _magikaTitleLabel = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Theme.TextMain,
            Font = Theme.SansBold,
            Margin = new Padding(0, 0, 0, 12)
        };
        var kv = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Theme.Panel
        };
        kv.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        kv.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _magikaKeyLabels = new Label[5];
        _magikaLabelValue = MakeGridLabel(Theme.TextMain, ContentAlignment.MiddleLeft);
        _magikaMimeValue = MakeGridLabel(Theme.TextMain, ContentAlignment.MiddleLeft);
        _magikaDescValue = MakeGridLabel(Theme.TextMain, ContentAlignment.MiddleLeft);
        _magikaScoreValue = MakeGridLabel(Theme.Good, ContentAlignment.MiddleLeft);
        _magikaIsTextValue = MakeGridLabel(Theme.TextMain, ContentAlignment.MiddleLeft);

        AddKeyValueRow(kv, 0, _magikaLabelValue);
        AddKeyValueRow(kv, 1, _magikaMimeValue);
        AddKeyValueRow(kv, 2, _magikaDescValue);
        AddKeyValueRow(kv, 3, _magikaScoreValue);
        AddKeyValueRow(kv, 4, _magikaIsTextValue);

        _magikaRawButton = MakeLinkButton();
        layout.Controls.Add(_magikaTitleLabel, 0, 0);
        layout.Controls.Add(kv, 0, 1);
        layout.Controls.Add(_magikaRawButton, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildSummary()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 12, 0, 0)
        };
        _summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "",
            ForeColor = Theme.TextMuted,
            Font = Theme.Mono,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(_summaryLabel);
        return panel;
    }

    private void AddKeyValueRow(TableLayoutPanel kv, int row, Label value)
    {
        kv.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var keyLabel = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextMuted,
            Font = Theme.Mono,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        _magikaKeyLabels[row] = keyLabel;
        value.Dock = DockStyle.Fill;
        kv.Controls.Add(keyLabel, 0, row);
        kv.Controls.Add(value, 1, row);
    }

    private static Label MakeGridLabel(Color color, ContentAlignment align) => new()
    {
        Dock = DockStyle.Fill,
        ForeColor = color,
        Font = Theme.Mono,
        TextAlign = align,
        Margin = new Padding(0),
        AutoSize = false
    };

    private static Button MakeIconButton(string glyph)
    {
        var b = new IconButton(glyph, Theme.IconFont, Theme.TextMain, Theme.Panel, Theme.Border)
        {
            Margin = new Padding(0, 0, 6, 0)
        };
        return b;
    }

    private static Button MakePrimaryButton()
    {
        var b = new Button
        {
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Primary,
            ForeColor = Color.White,
            Font = Theme.Sans,
            UseMnemonic = false,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.PrimaryHover;
        b.FlatAppearance.MouseDownBackColor = Theme.PrimaryHover;
        return b;
    }

    private static Button MakeLinkButton()
    {
        var b = new Button
        {
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Panel,
            ForeColor = Theme.TextMuted,
            Font = Theme.SansSmall,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Panel;
        return b;
    }

    private TableLayoutPanel root;
}
