using System.Drawing;
using SuperUI.Controls;
using SuperUI.Forms;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 冒烟自检：主题、布局、标题栏、最大化边界、侧边菜单、开关、数值、选项、加载态、
/// 页面切换与每个演示页能否正常创建，结果写到 exe 目录的 superui-selftest.log。
/// 不依赖鼠标操作，net48 / net8.0 两个目标都能跑。
/// </summary>
internal static class SelfTest
{
    private static DemoShellForm _shell = null!;

    public static void Run(DemoShellForm shell)
    {
        _shell = shell;

        var steps = new List<(string Name, Action Action)>
        {
            ("Light", () => SuThemeManager.Apply("Light")),
            ("Dark", () => SuThemeManager.Apply("Dark")),
            ("Ocean", () => SuThemeManager.Apply("Ocean")),
            ("Grape", () => SuThemeManager.Apply("Grape")),
            ("Forest", () => SuThemeManager.Apply("Forest")),
            ("Toggle", SuThemeManager.ToggleLightDark),
            ("Layout", CheckContentLayout),
            ("TitleBarHeight", CheckTitleBarHeight),
            ("Maximize", CheckMaximize),
            ("Navigate", CheckNavigation),
            ("SideMenu", CheckSideMenu),
            ("Switch", CheckSwitch),
            ("Number", CheckNumber),
            ("Options", CheckOptions),
            ("Loading", CheckLoading),
            ("OverviewPage", () => ProbePage(() => new OverviewPage(_ => { }))),
            ("GalleryPage", () => ProbePage(() => new GalleryPage())),
            ("TablePage", () => ProbePage(() => new TablePage(true))),
            ("PaginationPage", () => ProbePage(() => new PaginationPage())),
            ("MessageBoxPage", () => ProbePage(() => new MessageBoxPage())),
            ("MenuPage", () => ProbePage(() => new MenuPage())),
            ("TableApi", CheckTable),
            ("PaginationApi", CheckPagination),
            ("MessageBoxApi", CheckMessageBox),
            ("Dialog", () =>
            {
                using var dialog = new DemoDialog();
                dialog.Show(_shell);
                Application.DoEvents();
                dialog.Close();
            }),
        };

        var log = new List<string>();
        var index = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 450 };

        timer.Tick += (_, _) =>
        {
            try
            {
                var (name, action) = steps[index];
                action();
                log.Add($"{name}: OK ({SuThemeManager.Current.Name})");
                index++;

                if (index < steps.Count)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                log.Add($"{steps[index].Name}: FAIL {exception}");
            }

            timer.Stop();
            timer.Dispose();

            var passed = log.TrueForAll(line => line.Contains("OK"));
            log.Add(passed ? "PASS" : "FAIL");
            WriteSelfTestLog(log);
            _shell.Close();
        };

        timer.Start();
    }

    private static void WriteSelfTestLog(List<string> lines)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "superui-selftest.log"),
                string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }
        catch (IOException)
        {
            // 写日志失败时忽略。
        }
    }

    /// <summary>校验：内容区 Dock=Fill 正好铺满内容容器，且侧栏 / 内容区都落在 DisplayRectangle 内。</summary>
    private static void CheckContentLayout()
    {
        var probe = new Panel { Dock = DockStyle.Fill };
        _shell.ContentHost.Controls.Add(probe);
        _shell.PerformLayout();
        Application.DoEvents();

        var expected = _shell.ContentHost.DisplayRectangle;
        var actual = probe.Bounds;
        _shell.ContentHost.Controls.Remove(probe);
        probe.Dispose();

        if (actual != expected)
        {
            throw new InvalidOperationException($"Dock=Fill 边界 {actual} != 内容容器 DisplayRectangle {expected}");
        }

        var display = _shell.DisplayRectangle;
        if (_shell.Sidebar.Left != display.Left || _shell.Sidebar.Height != display.Height)
        {
            throw new InvalidOperationException($"侧栏 {_shell.Sidebar.Bounds} 未贴住内容区左边 {display}");
        }

        if (_shell.ContentHost.Left != _shell.Sidebar.Right)
        {
            throw new InvalidOperationException($"内容区起点 {_shell.ContentHost.Left} != 侧栏右边 {_shell.Sidebar.Right}");
        }

        if (_shell.ContentHost.Top < _shell.TitleBar.Height)
        {
            throw new InvalidOperationException($"内容区 {_shell.ContentHost.Bounds} 压到了标题栏（高 {_shell.TitleBar.Height}）");
        }
    }

    /// <summary>校验：TitleBarHeight 手动设值后，标题栏高度与内容区起点同步变化。</summary>
    private static void CheckTitleBarHeight()
    {
        var original = _shell.TitleBarHeight;
        _shell.TitleBarHeight = 64;
        _shell.PerformLayout();
        Application.DoEvents();

        var barHeight = _shell.TitleBar.Height;
        var contentTop = _shell.DisplayRectangle.Y;
        _shell.TitleBarHeight = original;
        _shell.PerformLayout();
        Application.DoEvents();

        if (barHeight != 64)
        {
            throw new InvalidOperationException($"TitleBarHeight=64 未生效，实际 {barHeight}");
        }

        if (contentTop < 64)
        {
            throw new InvalidOperationException($"标题栏变高后内容区仍在 {contentTop}");
        }
    }

    /// <summary>
    /// 校验：最大化区域正好贴住工作区（不盖任务栏），还原后尺寸回到原样。
    /// </summary>
    private static void CheckMaximize()
    {
        var originalSize = _shell.Size;
        var originalState = _shell.WindowState;

        _shell.WindowState = FormWindowState.Maximized;
        Application.DoEvents();
        var maximized = _shell.Bounds;
        var workArea = Screen.FromControl(_shell).WorkingArea;

        _shell.WindowState = FormWindowState.Normal;
        Application.DoEvents();
        var restored = _shell.Size;

        _shell.WindowState = originalState;
        Application.DoEvents();

        if (maximized != workArea)
        {
            throw new InvalidOperationException($"最大化区域 {maximized} 不等于工作区 {workArea}");
        }

        if (restored != originalSize)
        {
            throw new InvalidOperationException($"还原尺寸 {restored} 不等于原尺寸 {originalSize}");
        }
    }

    /// <summary>校验：菜单选中 → 页面切换 → 标题栏文字三处同步，且内容区同一时刻只显示一页。</summary>
    private static void CheckNavigation()
    {
        var keys = new[] { "overview", "gallery", "table", "pagination", "menu" };

        foreach (var key in keys)
        {
            _shell.NavigateTo(key);
            Application.DoEvents();

            if (_shell.CurrentKey != key)
            {
                throw new InvalidOperationException($"NavigateTo(\"{key}\") 后 CurrentKey={_shell.CurrentKey}");
            }

            if (_shell.SideMenu.SelectedKey != key)
            {
                throw new InvalidOperationException($"代码切页后菜单选中项是 {_shell.SideMenu.SelectedKey}，期望 {key}");
            }

            var visible = 0;
            foreach (Control child in _shell.ContentHost.Controls)
            {
                if (child.Visible)
                {
                    visible++;
                }
            }

            if (visible != 1)
            {
                throw new InvalidOperationException($"内容区同时可见 {visible} 页，期望 1 页（当前 {key}）");
            }
        }

        // 用菜单自身选中（模拟点击）也要能切页。
        if (!_shell.SideMenu.SelectByKey("overview"))
        {
            throw new InvalidOperationException("SelectByKey(\"overview\") 失败");
        }

        Application.DoEvents();

        if (_shell.CurrentKey != "overview")
        {
            throw new InvalidOperationException($"菜单选中后 CurrentKey={_shell.CurrentKey}，期望 overview");
        }
    }

    /// <summary>校验：侧边菜单能选中子项、自动展开父项、折叠/展开全部。</summary>
    private static void CheckSideMenu()
    {
        using var host = new Form
        {
            ClientSize = new Size(320, 320),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, 0),
            ShowInTaskbar = false,
        };

        using var menu = new SuSideMenu { Dock = DockStyle.Left, Width = 220 };
        var parent = new SuMenuItem("输入框") { Key = "input" };
        parent.Add(new SuMenuItem("单行输入") { Key = "single" });
        parent.Add(new SuMenuItem("多行输入") { Key = "multi" });

        menu.AddGroup("基础");
        menu.Add(new SuMenuItem("控件总览") { Key = "overview" });
        menu.Add(parent);
        menu.AddSeparator();
        menu.Add(new SuMenuItem("主题") { Key = "theme", Badge = "5" });

        host.Controls.Add(menu);
        host.Show();
        Application.DoEvents();

        if (!menu.SelectByKey("single"))
        {
            throw new InvalidOperationException("SelectByKey 未能定位到子项");
        }

        if (!ReferenceEquals(menu.SelectedItem, parent.Children[0]) || menu.SelectedKey != "single")
        {
            throw new InvalidOperationException("选中项与 Key 不一致");
        }

        if (!parent.IsExpanded)
        {
            throw new InvalidOperationException("选中子项时应自动展开父项");
        }

        menu.CollapseAll();
        menu.ExpandAll();
        menu.Invalidate();
        Application.DoEvents();
        host.Close();
    }

    /// <summary>校验：开关能按文本自动定尺寸，切换会触发 CheckedChanged。</summary>
    private static void CheckSwitch()
    {
        var events = 0;
        using var toggle = new SuSwitch { Text = "启用通知" };
        toggle.CheckedChanged += (_, __) => events++;

        var preferred = toggle.PreferredSize;
        if (preferred.Width <= toggle.TrackWidth || preferred.Height < toggle.TrackHeight)
        {
            throw new InvalidOperationException($"开关自动尺寸异常：{preferred}（轨道 {toggle.TrackWidth}x{toggle.TrackHeight}）");
        }

        toggle.Checked = true;
        toggle.Checked = false;

        if (events != 2)
        {
            throw new InvalidOperationException($"CheckedChanged 触发 {events} 次，期望 2 次");
        }

        if (toggle.Checked)
        {
            throw new InvalidOperationException("开关状态未回到 false");
        }
    }

    /// <summary>校验：数值输入的夹取、步进与小数位。</summary>
    private static void CheckNumber()
    {
        var events = 0;
        using var number = new SuNumericUpDown
        {
            Minimum = 0m,
            Maximum = 10m,
            Increment = 2m,
            DecimalPlaces = 1,
        };
        number.ValueChanged += (_, __) => events++;

        number.Value = 100m;
        if (number.Value != 10m)
        {
            throw new InvalidOperationException($"上限夹取错误：{number.Value}");
        }

        number.Value = -5m;
        if (number.Value != 0m)
        {
            throw new InvalidOperationException($"下限夹取错误：{number.Value}");
        }

        number.Up();
        number.Up();
        if (number.Value != 4m)
        {
            throw new InvalidOperationException($"步进错误：{number.Value}（期望 4.0）");
        }

        number.Down();
        if (number.Value != 2m)
        {
            throw new InvalidOperationException($"反向步进错误：{number.Value}");
        }

        if (events != 5)
        {
            throw new InvalidOperationException($"ValueChanged 触发 {events} 次，期望 5 次（2 次夹取 + 3 次步进）");
        }
    }

    /// <summary>校验：复选框事件、单选互斥、进度条夹取。</summary>
    private static void CheckOptions()
    {
        using var host = new Panel { Size = new Size(320, 140) };

        var radioA = new SuRadioButton { Text = "A", Checked = true, Location = new Point(0, 0) };
        var radioB = new SuRadioButton { Text = "B", Location = new Point(0, 30) };
        host.Controls.Add(radioA);
        host.Controls.Add(radioB);

        radioB.Checked = true;
        if (radioA.Checked || !radioB.Checked)
        {
            throw new InvalidOperationException("单选按钮未互斥");
        }

        var checkEvents = 0;
        var checkBox = new SuCheckBox { Text = "启用", Location = new Point(0, 70) };
        checkBox.CheckedChanged += (_, __) => checkEvents++;
        host.Controls.Add(checkBox);

        checkBox.Checked = true;
        checkBox.Checked = false;
        if (checkEvents != 2 || checkBox.Checked)
        {
            throw new InvalidOperationException($"复选框状态/事件异常：events={checkEvents}, checked={checkBox.Checked}");
        }

        using var bar = new SuProgressBar { Minimum = 0, Maximum = 10, Value = 99 };
        if (bar.Value != 10)
        {
            throw new InvalidOperationException($"进度条上限夹取错误：{bar.Value}");
        }

        bar.Value = -5;
        if (bar.Value != 0)
        {
            throw new InvalidOperationException($"进度条下限夹取错误：{bar.Value}");
        }
    }

    /// <summary>校验：按钮加载态屏蔽点击、遮盖层显示/隐藏。</summary>
    private static void CheckLoading()
    {
        var clicks = 0;
        using var button = new SuButton { Text = "保存" };
        button.Click += (_, __) => clicks++;

        button.IsLoading = true;
        button.PerformClick();

        if (clicks != 0)
        {
            throw new InvalidOperationException("加载中的按钮仍触发了 Click");
        }

        button.IsLoading = false;
        button.PerformClick();

        if (clicks != 1)
        {
            throw new InvalidOperationException($"恢复后 Click 次数 {clicks}，期望 1");
        }

        using var host = new Form
        {
            ClientSize = new Size(320, 200),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, 0),
            ShowInTaskbar = false,
        };

        var panel = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(panel);
        host.Show();
        Application.DoEvents();

        var overlay = SuLoadingOverlay.For(panel);
        overlay.ShowLoading("测试加载");
        Application.DoEvents();

        if (!overlay.IsLoading || !overlay.Visible)
        {
            throw new InvalidOperationException("遮盖层未进入加载状态");
        }

        overlay.HideLoading();

        if (overlay.IsLoading || overlay.Visible)
        {
            throw new InvalidOperationException("遮盖层未退出加载状态");
        }

        host.Close();
    }

    /// <summary>把演示页放进屏幕外的窗口里跑一遍布局与绘制，确认能正常创建。</summary>
    private static void ProbePage(Func<DemoPage> create)
    {
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, 0),
            Size = new Size(1100, 760),
            ShowInTaskbar = false,
            Text = "page-probe",
        };

        var page = create();
        host.Controls.Add(page);
        host.Show();
        Application.DoEvents();

        if (page.Width <= 0 || page.Height <= 0)
        {
            throw new InvalidOperationException($"页面尺寸异常：{page.Size}（{page.PageTitle}）");
        }

        if (string.IsNullOrEmpty(page.PageTitle))
        {
            throw new InvalidOperationException("页面没有标题");
        }

        host.Close();
    }

    /// <summary>
    /// 校验 SuTable 的行为：加分行、排序（升/降/取消）、单选与全选、勾选、命中测试、滚动与自适应列宽。
    /// 表格放在屏幕外显示，避免自检时闪窗；布局在 OnResize 里完成，不依赖绘制。
    /// </summary>
    private static void CheckTable()
    {
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, 0),
            Size = new Size(760, 480),
            ShowInTaskbar = false,
            Text = "table-selftest",
        };

        var table = new SuTable
        {
            Dock = DockStyle.Fill,
            SelectionMode = SuTableSelectionMode.Multi,
            ShowCheckBoxes = true,
        };

        table.Columns.Add(new SuTableColumn("编号", 80) { Align = SuTableAlign.Right });
        table.Columns.Add(new SuTableColumn("名称", -1));
        table.Columns.Add(new SuTableColumn("金额", 110) { Align = SuTableAlign.Right, Format = "N2" });
        table.Columns.Add(new SuTableColumn("备注", 320));

        host.Controls.Add(table);
        host.Show();
        Application.DoEvents();

        for (var i = 1; i <= 40; i++)
        {
            table.AddRow(i, "行 " + i, i * 10.5m);
        }

        Application.DoEvents();

        if (table.RowCount != 40)
        {
            throw new InvalidOperationException($"SuTable 行数 {table.RowCount} != 40");
        }

        table.SortBy(0, false);
        if (Convert.ToInt32(table.Rows[0].Cells![0]) != 40 || table.SortColumnIndex != 0 || table.SortAscending)
        {
            throw new InvalidOperationException("SuTable 降序排序未生效");
        }

        table.ToggleSort(0);
        if (Convert.ToInt32(table.Rows[0].Cells![0]) != 1 || !table.SortAscending)
        {
            throw new InvalidOperationException("SuTable 再点表头未切换为升序");
        }

        table.ClearSort();
        if (table.SortColumnIndex != -1 || Convert.ToInt32(table.Rows[0].Cells![0]) != 1)
        {
            throw new InvalidOperationException("SuTable 取消排序未恢复添加顺序");
        }

        table.SelectIndex(3);
        if (table.SelectedIndex != 3 || table.SelectedRows.Count != 1)
        {
            throw new InvalidOperationException($"SuTable 单选失败：index={table.SelectedIndex} count={table.SelectedRows.Count}");
        }

        table.SelectAll();
        if (table.SelectedRows.Count != 40)
        {
            throw new InvalidOperationException($"SuTable 全选失败：{table.SelectedRows.Count}");
        }

        table.ClearSelection();
        if (table.SelectedRows.Count != 0)
        {
            throw new InvalidOperationException("SuTable 清空选择失败");
        }

        table.CheckAll();
        if (!table.Rows[39].Checked)
        {
            throw new InvalidOperationException("SuTable 全选勾选失败");
        }

        table.CheckAll(false);
        if (table.Rows[39].Checked)
        {
            throw new InvalidOperationException("SuTable 取消勾选失败");
        }

        var bodyCenter = new Point(table.Width / 2, table.Height - 60);
        if (table.RowIndexAt(bodyCenter) < 0)
        {
            throw new InvalidOperationException($"SuTable 行命中测试失败：{bodyCenter}");
        }

        var headerRight = new Point(table.Width - 60, 20);
        if (table.ColumnIndexAt(headerRight) < 0)
        {
            throw new InvalidOperationException($"SuTable 列命中测试失败：{headerRight}");
        }

        table.EnsureVisible(39);
        if (table.FirstVisibleRow <= 0)
        {
            throw new InvalidOperationException($"SuTable 滚动到末行失败：first={table.FirstVisibleRow}");
        }

        if (!table.HasVerticalScroll)
        {
            throw new InvalidOperationException("SuTable 行数超出高度时未出现纵向滚动条");
        }

        // 缩窄到列总宽以下：应出现横向滚动条，且滚动仍被正确夹取。
        host.Size = new Size(500, 320);
        Application.DoEvents();

        if (!table.HasHorizontalScroll)
        {
            throw new InvalidOperationException("SuTable 列总宽超出时未出现横向滚动条");
        }

        table.ScrollTo(99999, 99999);
        var bodyCenterAfterScroll = new Point(table.Width / 2, table.Height - 40);
        if (table.RowIndexAt(bodyCenterAfterScroll) < 0)
        {
            throw new InvalidOperationException("SuTable 横向滚动后行命中测试失败");
        }

        host.Size = new Size(760, 480);
        Application.DoEvents();

        if (table.HasHorizontalScroll)
        {
            throw new InvalidOperationException("SuTable 恢复宽度后横向滚动条未消失");
        }

        table.AutoSizeColumns();
        table.RowHeight = 30;
        table.ZebraStripes = false;
        table.ShowRowLines = false;
        table.SelectionMode = SuTableSelectionMode.None;
        table.ClearRows();

        if (table.RowCount != 0)
        {
            throw new InvalidOperationException("SuTable 清空行失败");
        }

        host.Close();
    }

    /// <summary>
    /// 校验 SuPagination：页数换算、页号夹取、事件与 ByUser 标记、页码省略序列、
    /// 自动紧凑模式、每页条数下拉联动，并强制走一遍绘制。
    /// </summary>
    private static void CheckPagination()
    {
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, 0),
            Size = new Size(760, 240),
            ShowInTaskbar = false,
            Text = "pagination-selftest",
        };

        using var pager = new SuPagination { Dock = DockStyle.Top, Height = 40 };
        host.Controls.Add(pager);
        host.Show();
        Application.DoEvents();

        pager.TotalCount = 95;
        pager.PageSize = 10;

        if (pager.PageCount != 10)
        {
            throw new InvalidOperationException($"SuPagination 页数 {pager.PageCount} != 10（95 条 / 每页 10 条）");
        }

        if (pager.Page != 1)
        {
            throw new InvalidOperationException($"SuPagination 初始页 {pager.Page} != 1");
        }

        pager.Page = 999;
        if (pager.Page != 10)
        {
            throw new InvalidOperationException($"SuPagination 上越界未夹取：{pager.Page}");
        }

        pager.Page = -3;
        if (pager.Page != 1)
        {
            throw new InvalidOperationException($"SuPagination 下越界未夹取：{pager.Page}");
        }

        var fired = 0;
        SuPageChangedEventArgs? last = null;
        pager.PageChanged += (_, e) =>
        {
            fired++;
            last = e;
        };

        pager.GoToPage(3);
        if (fired != 1 || last is null || last.NewPage != 3 || last.OldPage != 1 || !last.ByUser)
        {
            throw new InvalidOperationException($"SuPagination 翻页事件异常：fired={fired}, new={last?.NewPage}, byUser={last?.ByUser}");
        }

        pager.GoToPage(3);
        if (fired != 1)
        {
            throw new InvalidOperationException("SuPagination 跳到同一页仍触发了事件");
        }

        pager.NextPage();
        if (fired != 2 || pager.Page != 4)
        {
            throw new InvalidOperationException($"SuPagination 下一页失败：fired={fired}, page={pager.Page}");
        }

        pager.LastPage();
        if (fired != 3 || pager.Page != 10)
        {
            throw new InvalidOperationException($"SuPagination 末页失败：fired={fired}, page={pager.Page}");
        }

        pager.NextPage();
        if (pager.Page != 10 || fired != 3)
        {
            throw new InvalidOperationException("SuPagination 末页再下一页仍改变了页面");
        }

        pager.PageSize = 25;
        if (pager.PageCount != 4 || pager.Page != 4)
        {
            throw new InvalidOperationException($"SuPagination 改每页条数后未收页：pageCount={pager.PageCount}, page={pager.Page}");
        }

        if (last is null || last.ByUser)
        {
            throw new InvalidOperationException("SuPagination 数据变化导致的收页被标记成了用户操作");
        }

        pager.TotalCount = 0;
        if (pager.PageCount != 1 || pager.Page != 1)
        {
            throw new InvalidOperationException($"SuPagination 空数据未回到 1/1：{pager.Page}/{pager.PageCount}");
        }

        // 页码省略：始终保留首尾，当前页在窗口内
        pager.TotalCount = 1000;
        pager.PageSize = 10;
        pager.GoToPage(50);

        var sequence = pager.GetPageSequence();
        if (sequence[0] != 1 || sequence[sequence.Count - 1] != 100)
        {
            throw new InvalidOperationException($"SuPagination 页码序列未保留首尾：{string.Join(",", sequence)}");
        }

        if (!sequence.Contains(50))
        {
            throw new InvalidOperationException($"SuPagination 页码序列不含当前页：{string.Join(",", sequence)}");
        }

        if (sequence.Count > pager.MaxPageButtons + 2)
        {
            throw new InvalidOperationException($"SuPagination 页码过多：{sequence.Count} 个（上限 {pager.MaxPageButtons} + 2 个省略号）");
        }

        pager.FirstPage();
        sequence = pager.GetPageSequence();
        if (sequence[0] != 1 || sequence[1] != 2)
        {
            throw new InvalidOperationException($"SuPagination 首页时不该出现前导省略号：{string.Join(",", sequence)}");
        }

        // 自动紧凑：窄宽度切成「‹ N / M ›」
        using var narrow = new SuPagination { Width = 120, Height = 40, TotalCount = 1000, PageSize = 10 };
        if (!narrow.IsCompact)
        {
            throw new InvalidOperationException($"SuPagination 宽度 120 时未自动进入紧凑模式（slots 不足应切换）");
        }

        narrow.Width = 900;
        if (narrow.IsCompact)
        {
            throw new InvalidOperationException("SuPagination 宽度 900 时仍处于紧凑模式");
        }

        using (var bitmap = new Bitmap(Math.Max(1, pager.Width), Math.Max(1, pager.Height)))
        {
            pager.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        }

        // 每页条数下拉：开关后出现子控件，改下拉项能改 PageSize
        pager.ShowPageSizeSelector = true;
        Application.DoEvents();

        if (pager.Controls.Count != 1 || pager.Controls[0] is not SuComboBox combo)
        {
            throw new InvalidOperationException($"SuPagination 打开每页条数后子控件数量 {pager.Controls.Count} != 1");
        }

        if (combo.SelectedIndex < 0
            || combo.SelectedIndex >= pager.PageSizeOptions.Count
            || pager.PageSizeOptions[combo.SelectedIndex] != pager.PageSize)
        {
            throw new InvalidOperationException($"SuPagination 下拉选中项 {combo.SelectedIndex} 与每页条数 {pager.PageSize} 不同步");
        }

        combo.SelectedIndex = 2;
        if (pager.PageSize != pager.PageSizeOptions[2])
        {
            throw new InvalidOperationException($"SuPagination 下拉改每页条数未生效：{pager.PageSize}");
        }

        using (var bitmap = new Bitmap(Math.Max(1, pager.Width), Math.Max(1, pager.Height)))
        {
            pager.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        }

        pager.ShowPageSizeSelector = false;
        if (pager.Controls.Count != 0)
        {
            throw new InvalidOperationException("SuPagination 关掉每页条数后下拉未移除");
        }

        host.Close();
    }

    /// <summary>
    /// 校验 SuMessageBoxDialog：三种语义都能建出窗口、按钮组合与 DialogResult 正确、
    /// 正文长度真的影响窗口尺寸，并强制走一遍绘制（在屏幕外显示，避免自检时闪窗）。
    /// </summary>
    private static void CheckMessageBox()
    {
        foreach (var kind in new[] { SuMessageBoxKind.Info, SuMessageBoxKind.Warning, SuMessageBoxKind.Error })
        {
            using var box = new SuMessageBoxDialog
            {
                Kind = kind,
                Text = SuMessageBox.DefaultTitle(kind),
                Message = "连接数据库失败：超时时间已到（30000 ms）。请检查网络后重试。",
            };

            ShowOffscreen(box);

            if (box.ClientSize.Width < 240 || box.ClientSize.Height < 110)
            {
                throw new InvalidOperationException($"{kind} 弹窗尺寸异常：{box.ClientSize}");
            }

            if (box.Text != SuMessageBox.DefaultTitle(kind))
            {
                throw new InvalidOperationException($"{kind} 弹窗标题 {box.Text} 与默认标题不一致");
            }

            if (box.Controls.OfType<SuButton>().Count() != 1
                || box.Controls.OfType<SuButton>().FirstOrDefault() is not { } okButton)
            {
                throw new InvalidOperationException(
                    $"{kind} 弹窗「确定」按钮数量 {box.Controls.OfType<SuButton>().Count()} != 1");
            }

            if (okButton.DialogResult != DialogResult.OK
                || !ReferenceEquals(box.AcceptButton, okButton)
                || !ReferenceEquals(box.CancelButton, okButton))
            {
                throw new InvalidOperationException($"{kind} 弹窗的「确定」按钮未同时接住 Enter / Esc");
            }

            ProbeDraw(box);
            box.Close();
        }

        // 按钮组合
        using (var cancelBox = new SuMessageBoxDialog
               {
                   Message = "覆盖 3 个文件？",
                   ButtonSet = SuMessageBoxButtons.OkCancel,
               })
        {
            ShowOffscreen(cancelBox);

            if (cancelBox.Controls.OfType<SuButton>().Count() != 2
                || cancelBox.AcceptButton?.DialogResult != DialogResult.OK
                || cancelBox.CancelButton?.DialogResult != DialogResult.Cancel)
            {
                throw new InvalidOperationException(
                    $"OkCancel 按钮组合异常：count={cancelBox.Controls.OfType<SuButton>().Count()}, "
                    + $"accept={cancelBox.AcceptButton?.DialogResult}, cancel={cancelBox.CancelButton?.DialogResult}");
            }

            ProbeDraw(cancelBox);
            cancelBox.Close();
        }

        using (var confirmBox = new SuMessageBoxDialog
               {
                   Message = "确定要删除这 3 行吗？",
                   ButtonSet = SuMessageBoxButtons.YesNo,
               })
        {
            ShowOffscreen(confirmBox);

            if (confirmBox.AcceptButton?.DialogResult != DialogResult.Yes
                || confirmBox.CancelButton?.DialogResult != DialogResult.No)
            {
                throw new InvalidOperationException(
                    $"YesNo 按钮组合异常：accept={confirmBox.AcceptButton?.DialogResult}, "
                    + $"cancel={confirmBox.CancelButton?.DialogResult}");
            }

            ProbeDraw(confirmBox);
            confirmBox.Close();
        }

        // 正文越长窗口越大（宽度到上限后改为长高）
        var shortSize = MeasureMessageBox("已保存。");
        var longSize = MeasureMessageBox(
            "这段正文会在最大宽度处自动换行：弹窗按正文实际宽高量出窗口尺寸，"
            + "标题与正文共用一个文字列，按钮行贴右下角，文字更短时窗口就跟着变窄。");

        if (longSize.Width <= shortSize.Width && longSize.Height <= shortSize.Height)
        {
            throw new InvalidOperationException(
                $"弹窗没有按正文自适应：短 {shortSize}，长 {longSize}");
        }

        if (shortSize.Width < 240 || shortSize.Height < 110)
        {
            throw new InvalidOperationException($"短文本弹窗尺寸异常：{shortSize}");
        }

        // 遮罩：默认挂上去、关窗撤掉、ShowScrim = false 时不出现
        using var host = new Form
        {
            ClientSize = new Size(520, 320),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, 0),
            ShowInTaskbar = false,
            Text = "scrim-host",
        };

        host.Show();
        Application.DoEvents();
        var baseline = host.Controls.Count;

        using (var withScrim = new SuMessageBoxDialog
               {
                   Message = "遮罩测试：这一层要盖住宿主窗口。",
                   ScrimHost = host,
               })
        {
            ShowOffscreen(withScrim);
            Application.DoEvents();

            if (CountScrim(host) != 1)
            {
                throw new InvalidOperationException($"ShowScrim=true 时遮罩层数量 {CountScrim(host)} != 1");
            }

            withScrim.Close();
            Application.DoEvents();

            if (CountScrim(host) != 0)
            {
                throw new InvalidOperationException("弹窗关闭后遮罩层没有撤掉");
            }
        }

        using (var withoutScrim = new SuMessageBoxDialog
               {
                   Message = "ShowScrim = false。",
                   ScrimHost = host,
                   ShowScrim = false,
               })
        {
            ShowOffscreen(withoutScrim);
            Application.DoEvents();

            if (CountScrim(host) != 0)
            {
                throw new InvalidOperationException("ShowScrim=false 时仍然出现了遮罩层");
            }

            withoutScrim.Close();
            Application.DoEvents();
        }

        if (host.Controls.Count != baseline)
        {
            throw new InvalidOperationException(
                $"弹窗都关掉后宿主控件数 {host.Controls.Count} != 原始 {baseline}");
        }

        host.Close();
    }

    /// <summary>数一数宿主上挂了几层遮罩（遮罩层是内部类型，按类型名判断）。</summary>
    private static int CountScrim(Control host)
        => host.Controls.Cast<Control>().Count(c => c.GetType().Name == "SuScrimLayer");

    /// <summary>把弹窗挪到屏幕外显示，避免自检时弹到用户眼前。</summary>
    private static void ShowOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-6000, 0);
        form.Show();
        Application.DoEvents();
    }

    /// <summary>强制绘制一帧，兜住 OnPaint 里的异常。</summary>
    private static void ProbeDraw(Control control)
    {
        using var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
    }

    private static Size MeasureMessageBox(string message)
    {
        using var box = new SuMessageBoxDialog { Message = message };
        return box.ClientSize;
    }
}
