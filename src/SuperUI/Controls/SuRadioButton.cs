using System.ComponentModel;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 单选按钮：圆形指示器 + 内点，同容器内互斥（勾选一个会自动取消同级其它单选按钮）。
/// </summary>
[Description("SuperUI 单选按钮：圆形指示器 + 内点，同容器内互斥，颜色跟随全局主题。")]
[DefaultEvent(nameof(CheckedChanged))]
[DefaultProperty(nameof(Checked))]
public class SuRadioButton : SuOptionBase
{
    /// <summary>勾选时取消同级（同一个父容器）的其它单选按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool Exclusive { get; set; } = true;

    /// <inheritdoc />
    protected override void OnCheckedOn()
    {
        if (!Exclusive || Parent is null)
        {
            return;
        }

        foreach (Control sibling in Parent.Controls)
        {
            if (ReferenceEquals(sibling, this))
            {
                continue;
            }

            if (sibling is SuRadioButton other && other.Checked && other.Exclusive)
            {
                other.Checked = false;
            }
        }
    }

    /// <inheritdoc />
    protected override void DrawGlyph(
        Graphics graphics,
        SuTheme theme,
        RectangleF glyph,
        bool isChecked,
        bool enabled,
        bool hovered)
    {
        var ring = theme.Border;
        var fill = theme.Surface;
        var dot = theme.Primary;

        if (!enabled)
        {
            ring = SuDraw.Blend(theme.Border, theme.Background, 0.45f);
            fill = SuDraw.Blend(theme.Surface, theme.Background, 0.35f);
            dot = SuDraw.Blend(theme.Primary, theme.Background, 0.55f);
        }
        else if (isChecked)
        {
            ring = theme.Primary;
        }
        else if (hovered)
        {
            ring = SuDraw.Blend(theme.Border, theme.Primary, 0.55f);
        }

        var inner = RectangleF.Inflate(glyph, -0.8f, -0.8f);

        using (var brush = new SolidBrush(fill))
        {
            graphics.FillEllipse(brush, inner);
        }

        using (var pen = new Pen(ring, isChecked ? 1.6f : 1.2f))
        {
            graphics.DrawEllipse(pen, inner);
        }

        if (!isChecked)
        {
            return;
        }

        var dotSize = inner.Width * 0.46f;
        var dotRect = new RectangleF(
            inner.Left + (inner.Width - dotSize) / 2f,
            inner.Top + (inner.Height - dotSize) / 2f,
            dotSize,
            dotSize);

        using var dotBrush = new SolidBrush(dot);
        graphics.FillEllipse(dotBrush, dotRect);
    }
}
