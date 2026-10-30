using SuperUI.Forms;
using SuperUI.Theming;

namespace SuperUIDemo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // 高 DPI：.NET 8 用 API 设置；.NET Framework 4.8 由 App.config 的 DpiAwareness 设置。
#if !NETFRAMEWORK
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 演示程序：把未处理异常写到 exe 目录下，方便定位自绘控件的问题。
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);

        var themeName = "Dark";
        var page = "overview";
        var showcase = false;
        var openDialog = false;
        var openMessageBox = false;
        var selfTest = false;
        var titleBarHeight = 42;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase))
            {
                themeName = arg.Substring("--theme=".Length);
            }
            else if (arg.StartsWith("--titlebar=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(arg.Substring("--titlebar=".Length), out var parsedHeight))
            {
                titleBarHeight = parsedHeight;
            }
            else if (arg.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
            {
                page = arg.Substring("--page=".Length);
            }
            else if (arg.Equals("--dialog", StringComparison.OrdinalIgnoreCase))
            {
                openDialog = true;
            }
            else if (arg.Equals("--msgbox", StringComparison.OrdinalIgnoreCase))
            {
                openMessageBox = true;
            }
            else if (arg.Equals("--menu", StringComparison.OrdinalIgnoreCase))
            {
                page = "menu";
            }
            else if (arg.Equals("--gallery", StringComparison.OrdinalIgnoreCase))
            {
                page = "gallery";
            }
            else if (arg.Equals("--table", StringComparison.OrdinalIgnoreCase))
            {
                page = "table";
            }
            else if (arg.Equals("--pagination", StringComparison.OrdinalIgnoreCase))
            {
                page = "pagination";
            }
            else if (arg.Equals("--messagebox", StringComparison.OrdinalIgnoreCase))
            {
                page = "messagebox";
            }
            else if (arg.Equals("--showcase", StringComparison.OrdinalIgnoreCase))
            {
                showcase = true;
                page = "table";
            }
            else if (arg.Equals("--selftest", StringComparison.OrdinalIgnoreCase))
            {
                selfTest = true;
            }
        }

        SuThemeManager.Apply(SuThemePresets.Find(themeName) ?? SuThemePresets.Dark);

        // 所有演示现在都在同一个窗口里，靠左侧菜单（或 --page=）切页。
        var shell = new DemoShellForm(showcase, titleBarHeight);
        shell.NavigateTo(page);

        if (openDialog)
        {
            shell.Shown += (_, __) => shell.ShowDemoDialog();
        }

        if (openMessageBox)
        {
            // 启动即弹一个模态消息框：验证居中于父窗口 + 模态阻塞（关掉它才会回到演示台）。
            shell.Shown += (_, __) => SuMessageBox.Info(
                shell,
                "这是 SuMessageBox.Info 打开的信息弹窗：没有标题栏，圆角卡片 + 整窗可拖动，居中于父窗口。",
                "消息弹窗");
        }

        if (selfTest)
        {
            SelfTest.Run(shell);
        }

        Application.Run(shell);
    }

    private static void LogCrash(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "superui-crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // 记录失败时忽略。
        }
    }
}
