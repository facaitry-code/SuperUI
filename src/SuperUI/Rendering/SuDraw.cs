using System.Drawing;
using System.Drawing.Drawing2D;

namespace SuperUI.Rendering;

/// <summary>绘制辅助方法：圆角路径、颜色混合、画质设置。</summary>
internal static class SuDraw
{
    public static void SetupQuality(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }

    /// <summary>生成圆角矩形路径（半径自动收敛到短边一半）。</summary>
    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var max = Math.Min(rect.Width, rect.Height) / 2f;
        radius = SuMath.Clamp(radius, 0f, Math.Max(0f, max));

        if (radius <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        var diameter = radius * 2f;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    /// <summary>线性插值混合：t = 0 返回 from，t = 1 返回 to。</summary>
    public static Color Blend(Color from, Color to, float t)
    {
        t = SuMath.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(from.A + (to.A - from.A) * t),
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    /// <summary>替换透明度。</summary>
    public static Color Alpha(Color color, int alpha)
        => Color.FromArgb(SuMath.Clamp(alpha, 0, 255), color);

    /// <summary>按比例压暗。</summary>
    public static Color Darken(Color color, float amount) => Blend(color, Color.Black, amount);

    /// <summary>按比例提亮。</summary>
    public static Color Lighten(Color color, float amount) => Blend(color, Color.White, amount);
}
