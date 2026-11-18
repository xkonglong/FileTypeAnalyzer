using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FileTypeAnalyzer.UI;

/// <summary>
/// 纯图标按钮。为什么不用普通 Button：
/// 1) Button 默认用 GDI 绘制文字，PrivateFontCollection 里的图标字体在 GDI 下取不到字形（会显示空白）；
/// 2) 打开 GDI+ 兼容渲染后又会走 ClearType 亚像素，深色背景上白色图标会出现彩色毛边。
/// 这里自绘字形并用灰度抗锯齿，图标在明暗两种主题下都是干净的白色/深色描边。
/// </summary>
internal sealed class IconButton : Button
{
    private readonly Color _normalBack;
    private readonly Color _hoverBack;

    public IconButton(string glyph, Font font, Color foreColor, Color backColor, Color hoverColor)
    {
        Glyph = glyph;
        Font = font;
        ForeColor = foreColor;
        _normalBack = backColor;
        _hoverBack = hoverColor;
        BackColor = backColor;

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(36, 32);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        UseMnemonic = false;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateStyles();
    }

    public string Glyph { get; }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        BackColor = _hoverBack;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        BackColor = _normalBack;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        BackColor = _hoverBack;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        BackColor = _hoverBack;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        using (var bg = new SolidBrush(BackColor))
            g.FillRectangle(bg, ClientRectangle);

        // 灰度抗锯齿：不启用 ClearType，避免暗色底上的彩色边缘
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoClip
        };
        using var brush = new SolidBrush(ForeColor);
        g.DrawString(Glyph, Font, brush, new RectangleF(0, 0, Width, Height), format);
    }
}
