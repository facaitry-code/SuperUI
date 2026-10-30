using System.Drawing;
using System.Runtime.InteropServices;

namespace SuperUI.Native;

/// <summary>无边框窗口所需的 Win32 / DWM 互操作声明。</summary>
internal static class SuNative
{
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_GETMINMAXINFO = 0x0024;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCLBUTTONDBLCLK = 0x00A3;

    public const int WS_THICKFRAME = 0x00040000;
    public const int WS_MAXIMIZEBOX = 0x00010000;

    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE</summary>
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    /// <summary>DWMWCP_ROUND</summary>
    public const int DWMWCP_ROUND = 2;

    /// <summary>DWMWCP_DONOTROUND</summary>
    public const int DWMWCP_DONOTROUND = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    /// <summary>GW_OWNER：取窗口的宿主（ShowDialog 会把父窗口登记成 Win32 owner）。</summary>
    public const int GW_OWNER = 4;

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    /// <summary>GWL_STYLE</summary>
    public const int GWL_STYLE = -16;

    /// <summary>读取窗口样式（按进程位数自动选择 SetWindowLongPtr / SetWindowLong）。</summary>
    public static int GetWindowStyle(IntPtr hWnd)
    {
        return IntPtr.Size == 8
            ? unchecked((int)GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64())
            : GetWindowLong32(hWnd, GWL_STYLE);
    }

    /// <summary>写入窗口样式。不触发 <c>SWP_FRAMECHANGED</c>，所以客户区大小不变、也不会有边框被画出来。</summary>
    public static void SetWindowStyle(IntPtr hWnd, int style)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GWL_STYLE, new IntPtr(style));
        }
        else
        {
            SetWindowLong32(hWnd, GWL_STYLE, style);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

    /// <summary>从 WM_NCHITTEST / 鼠标消息的 lParam 中解出屏幕坐标。</summary>
    public static Point PointFromLParam(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new Point(
            unchecked((short)(value & 0xFFFF)),
            unchecked((short)((value >> 16) & 0xFFFF)));
    }
}
