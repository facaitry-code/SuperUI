using System.Drawing;
using System.Drawing.Drawing2D;

namespace SuperUI.Rendering;

/// <summary>旋转指示器（spinner）绘制：底环 + 带尾迹的圆弧。</summary>
internal static class SuSpinner
{
    /// <summary>在指定区域内绘制一个旋转弧（<paramref name="angle"/> 为起始角度，顺时针增长）。</summary>
    public static void Draw(
        Graphics graphics,
        RectangleF bounds,
        float angle,
        Color color,
        float thickness,
        Color? trackColor = null)
    {
        if (bounds.Width <= 2f || bounds.Height <= 2f)
        {
            return;
        }

        SuDraw.SetupQuality(graphics);

        var half = thickness / 2f + 0.5f;
        var rect = RectangleF.Inflate(bounds, -half, -half);
        if (rect.Width <= 1f || rect.Height <= 1f)
        {
            return;
        }

        if (trackColor.HasValue && trackColor.Value.A > 0)
        {
            using var trackPen = new Pen(trackColor.Value, thickness)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            graphics.DrawArc(trackPen, rect, 0f, 360f);
        }

        // 主弧 + 两段递减透明度尾迹，形成“彗星”效果
        DrawArc(graphics, rect, angle, 220f, color, thickness, 255);
        DrawArc(graphics, rect, angle + 220f, 40f, color, thickness, 120);
        DrawArc(graphics, rect, angle + 260f, 26f, color, thickness, 55);
    }

    private static void DrawArc(
        Graphics graphics,
        RectangleF rect,
        float start,
        float sweep,
        Color color,
        float thickness,
        int alpha)
    {
        using var pen = new Pen(SuDraw.Alpha(color, alpha), thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        graphics.DrawArc(pen, rect, start, sweep);
    }
}
