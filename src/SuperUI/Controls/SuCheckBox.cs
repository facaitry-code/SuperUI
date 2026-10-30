using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 复选框：圆角方框 + 对勾，悬停/聚焦有过渡，文字标签在右侧。
/// </summary>
[Description("SuperUI 复选框：圆角方框 + 对勾，文字在右侧，颜色跟随全局主题。")]
[DefaultEvent(nameof(CheckedChanged))]
[DefaultProperty(nameof(Checked))]
public class SuCheckBox : SuOptionBase
{
    private int _radius = 4;

    /// <summary>方框圆角半径。</summary>
    [Category("SuperUI")]
    [DefaultValue(4)]
    public int Radius
    {
        get => _radius;
        set
        {
            var radius = Math.Max(0, Math.Min(8, value));
            if (_radius == radius)
            {
                return;
            }

            _radius = radius;
            Invalidate();
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
        var border = SuDraw.Blend(theme.Border, theme.TextSecondary, 0.35f);
        var fill = theme.Surface;
        var mark = theme.TextOnAccent;

        if (!enabled)
        {
            border = SuDraw.Blend(theme.Border, theme.Background, 0.45f);
            fill = SuDraw.Blend(theme.Surface, theme.Background, 0.35f);
            mark = SuDraw.Blend(theme.TextOnAccent, theme.Background, 0.35f);
        }
        else if (isChecked)
        {
            border = theme.Primary;
            fill = theme.Primary;
        }
        else if (hovered)
        {
            border = SuDraw.Blend(theme.Border, theme.Primary, 0.55f);
        }

        var inner = RectangleF.Inflate(glyph, -0.6f, -0.6f);
        using var path = SuDraw.RoundedRect(inner, _radius);

        using (var brush = new SolidBrush(fill))
        {
            graphics.FillPath(brush, path);
        }

        using (var pen = new Pen(border, isChecked || hovered ? 1.4f : 1.2f))
        {
            graphics.DrawPath(pen, path);
        }

        if (!isChecked)
        {
            return;
        }

        var points = new[]
        {
            new PointF(inner.Left + inner.Width * 0.24f, inner.Top + inner.Height * 0.52f),
            new PointF(inner.Left + inner.Width * 0.43f, inner.Top + inner.Height * 0.71f),
            new PointF(inner.Left + inner.Width * 0.76f, inner.Top + inner.Height * 0.3f),
        };

        using var markPen = new Pen(mark, Math.Max(1.6f, glyph.Height * 0.12f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        graphics.DrawLines(markPen, points);
    }
}
