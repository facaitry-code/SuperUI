namespace SuperUI.Rendering;

/// <summary>数值夹取工具（.NET Framework 4.8 没有 Math.Clamp，这里补齐同一份语义）。</summary>
internal static class SuMath
{
    public static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;
}
