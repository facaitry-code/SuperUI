using System.Drawing;
using SuperUI.Controls;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 更多控件页：Label / CheckBox / RadioButton / ProgressBar / ComboBox / DateTimePicker /
/// CheckedListBox / TreeView / 按钮状态 / 遮盖层。
/// </summary>
public sealed class GalleryPage : DemoPage
{
    private const int ColLeft = 28;
    private const int ColRight = 480;

    private readonly List<(Label Label, float Size, FontStyle Style, bool Secondary)> _styled = new();
    private readonly List<Label> _cardLabels = new();
    private readonly SuProgressBar _progress40 = new();
    private readonly SuProgressBar _progress85 = new();
    private Panel _card = null!;
    private SuLoadingOverlay _cardOverlay = null!;
    private System.Windows.Forms.Timer? _fullPageTimer;

    public GalleryPage()
    {
        BuildUi();
        OnPageThemeChanged();
    }

    public override string PageTitle => "更多控件";

    protected override void Dispose(bool disposing)
    {
        if (disposing && _fullPageTimer is not null)
        {
            _fullPageTimer.Stop();
            _fullPageTimer.Dispose();
            _fullPageTimer = null;
        }

        base.Dispose(disposing);
    }

    private void BuildUi()
    {
        var origin = Origin;
        var left = origin.X + ColLeft;
        var right = origin.X + ColRight;
        var top = origin.Y;

        // ---------------- 左列：标签 ----------------
        AddHeading("标签 SuLabel", left, top + 14);

        var tones = new[]
        {
            ("主文字", SuLabelTone.Primary),
            ("次要", SuLabelTone.Secondary),
            ("占位", SuLabelTone.Placeholder),
            ("强调", SuLabelTone.Accent),
            ("危险", SuLabelTone.Danger),
        };

        var toneX = left;
        foreach (var (text, tone) in tones)
        {
            var label = new SuLabel { Text = text, Tone = tone, Location = new Point(toneX, top + 44) };
            Controls.Add(label);
            toneX += 70;
        }

        // ---------------- 复选框 / 单选 ----------------
        AddHeading("复选框 / 单选按钮", left, top + 82);

        Controls.Add(new SuCheckBox
        {
            Text = "启用缓存",
            Checked = true,
            Location = new Point(left, top + 108),
        });

        Controls.Add(new SuCheckBox
        {
            Text = "只读模式",
            Location = new Point(left + 150, top + 108),
        });

        var disabledCheck = new SuCheckBox
        {
            Text = "禁用项",
            Checked = true,
            Enabled = false,
            Location = new Point(left + 270, top + 108),
        };
        Controls.Add(disabledCheck);

        var radioA = new SuRadioButton
        {
            Text = "选项 A",
            Checked = true,
            Location = new Point(left, top + 144),
        };
        var radioB = new SuRadioButton { Text = "选项 B", Location = new Point(left + 96, top + 144) };
        var radioC = new SuRadioButton { Text = "选项 C", Location = new Point(left + 192, top + 144) };

        Controls.Add(radioA);
        Controls.Add(radioB);
        Controls.Add(radioC);

        // ---------------- 进度条 ----------------
        AddHeading("进度条 SuProgressBar", left, top + 186);

        ConfigureBar(_progress40, left, top + 214, 420, 40, true);
        ConfigureBar(_progress85, left, top + 242, 420, 85, true);

        var marquee = new SuProgressBar
        {
            IsIndeterminate = true,
            Bounds = new Rectangle(left, top + 270, 420, 12),
        };
        Controls.Add(marquee);

        var bump = new SuButton
        {
            Text = "进度 +10",
            Variant = SuButtonVariant.Secondary,
            Bounds = new Rectangle(left, top + 296, 120, 34),
        };
        bump.Click += (_, __) =>
        {
            _progress40.Value = Math.Min(100, _progress40.Value + 10);
            _progress85.Value = Math.Min(100, _progress85.Value + 10);
        };
        Controls.Add(bump);

        // ---------------- 下拉框 ----------------
        AddHeading("下拉框 SuComboBox", left, top + 344);

        var placeholderCombo = new SuComboBox
        {
            PlaceholderText = "请选择……",
            Bounds = new Rectangle(left, top + 372, 200, 38),
        };
        Controls.Add(placeholderCombo);

        var combo = new SuComboBox
        {
            Bounds = new Rectangle(left + 216, top + 372, 204, 38),
        };
        combo.Items.AddRange(new object[] { "选项一", "选项二", "选项三" });
        combo.SelectedIndex = 1;
        Controls.Add(combo);

        // ---------------- 右列：日期 ----------------
        AddHeading("日期选择 SuDateTimePicker", right, top + 14);

        Controls.Add(new SuDateTimePicker
        {
            Value = DateTime.Today,
            Bounds = new Rectangle(right, top + 44, 200, 38),
        });

        var longFormat = new SuDateTimePicker
        {
            Value = DateTime.Today,
            DisplayFormat = "yyyy 年 MM 月 dd 日",
            Bounds = new Rectangle(right + 216, top + 44, 204, 38),
        };
        Controls.Add(longFormat);

        // ---------------- 带复选框的列表框 ----------------
        AddHeading("带复选框的列表", right, top + 92);

        var checkedList = new SuCheckedListBox
        {
            Bounds = new Rectangle(right, top + 120, 200, 200),
        };
        checkedList.SetItems("勾选项一", "勾选项二", "勾选项三", "勾选项四", "勾选项五");
        checkedList.SetItemChecked(0, true);
        checkedList.SetItemChecked(2, true);
        checkedList.SelectedIndex = 1;
        Controls.Add(checkedList);

        // ---------------- 树形控件 ----------------
        AddHeading("树形控件 SuTreeView", right + 216, top + 92);

        var tree = new SuTreeView { Bounds = new Rectangle(right + 216, top + 120, 212, 200) };
        var root = tree.AddNode("解决方案", null, true);
        var controls = tree.AddNode("控件", root, true);
        tree.AddNode("SuButton", controls);
        tree.AddNode("SuTextBox", controls);
        tree.AddNode("SuTreeView", controls);
        tree.AddNode("示例", root, true);
        tree.AddNode("演示程序", tree.AddNode("文档", root), false);
        tree.SelectedNode = tree.Nodes[0].Nodes[0];
        Controls.Add(tree);

        // ---------------- 按钮状态（图标 / 加载中） ----------------
        AddHeading("按钮状态 SuButton", left, top + 452);

        var iconButton = new SuButton
        {
            Text = "带图标",
            Variant = SuButtonVariant.Secondary,
            IconGlyph = "\uE70F",
            Bounds = new Rectangle(left, top + 480, 112, 36),
        };
        Controls.Add(iconButton);

        var refresh = new SuButton
        {
            Text = "刷新",
            Variant = SuButtonVariant.Primary,
            IconGlyph = "\uE72C",
            Bounds = new Rectangle(left + 124, top + 480, 100, 36),
        };
        refresh.Click += (_, __) => FlashLoading(refresh, 1200);
        Controls.Add(refresh);

        var alwaysLoading = new SuButton
        {
            Text = "保存",
            LoadingText = "保存中…",
            IsLoading = true,
            Bounds = new Rectangle(left + 236, top + 480, 128, 36),
        };
        Controls.Add(alwaysLoading);

        var clickLoading = new SuButton
        {
            Text = "点我加载 2 秒",
            Variant = SuButtonVariant.Secondary,
            Bounds = new Rectangle(left, top + 526, 150, 36),
        };
        clickLoading.Click += (_, __) => FlashLoading(clickLoading, 2000);
        Controls.Add(clickLoading);

        var fullPage = new SuButton
        {
            Text = "整页遮盖 2 秒",
            Variant = SuButtonVariant.Danger,
            Bounds = new Rectangle(left + 162, top + 526, 156, 36),
        };

        var noIcon = new SuButton
        {
            Text = "无图标",
            Variant = SuButtonVariant.Ghost,
            Bounds = new Rectangle(left + 330, top + 526, 100, 36),
        };
        Controls.Add(noIcon);
        fullPage.Click += (_, __) =>
        {
            // 遮盖层盖住的是当前页面（不是整个窗口），切换页面时自动跟着走。
            SuLoadingOverlay.Show(this, "正在处理中…");
            StartFullPageTimer();
        };
        Controls.Add(fullPage);

        // ---------------- 遮盖层 ----------------
        AddHeading("遮盖层 SuLoadingOverlay", right, top + 452);

        _card = new Panel
        {
            Bounds = new Rectangle(right, top + 480, 428, 128),
            BackColor = SuThemeManager.Current.Surface,
        };
        _card.Paint += (_, e) =>
        {
            using var pen = new Pen(SuThemeManager.Current.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, _card.Width - 1, _card.Height - 1);
        };
        Controls.Add(_card);

        var cardTitle = new Label
        {
            Text = "订单 #1024",
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(18, 16),
        };
        _cardLabels.Add(cardTitle);
        _card.Controls.Add(cardTitle);

        var cardCaption = new Label
        {
            Text = "共 3 件商品 · 待发货",
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(18, 44),
        };
        _cardLabels.Add(cardCaption);
        _card.Controls.Add(cardCaption);

        var cardBar = new SuProgressBar { Value = 60, ShowText = true, Bounds = new Rectangle(18, 78, 300, 14) };
        _card.Controls.Add(cardBar);

        _cardOverlay = SuLoadingOverlay.For(_card);

        var overlaySwitch = new SuSwitch
        {
            Text = "显示遮盖层",
            Checked = true,
            Location = new Point(right, top + 622),
        };
        overlaySwitch.CheckedChanged += (_, __) =>
        {
            if (overlaySwitch.Checked)
            {
                _cardOverlay.ShowLoading("正在加载订单…");
            }
            else
            {
                _cardOverlay.HideLoading();
            }
        };
        Controls.Add(overlaySwitch);

        _cardOverlay.ShowLoading("正在加载订单…");
    }

    private void FlashLoading(SuButton button, int milliseconds)
    {
        button.IsLoading = true;

        var timer = new System.Windows.Forms.Timer { Interval = milliseconds };
        timer.Tick += (_, __) =>
        {
            timer.Stop();
            timer.Dispose();
            button.IsLoading = false;
        };
        timer.Start();
    }

    private void StartFullPageTimer()
    {
        _fullPageTimer ??= new System.Windows.Forms.Timer { Interval = 2000 };
        _fullPageTimer.Stop();
        _fullPageTimer.Tick -= FullPageTimerOnTick;
        _fullPageTimer.Tick += FullPageTimerOnTick;
        _fullPageTimer.Start();
    }

    private void FullPageTimerOnTick(object? sender, EventArgs e)
    {
        _fullPageTimer?.Stop();
        SuLoadingOverlay.Hide(this);
    }

    private void ConfigureBar(SuProgressBar bar, int x, int y, int width, int value, bool showText)
    {
        bar.Value = value;
        bar.ShowText = showText;
        bar.Bounds = new Rectangle(x, y, width, 14);
        Controls.Add(bar);
    }

    private void AddHeading(string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(x, y),
        };

        Controls.Add(label);
        _styled.Add((label, 11f, FontStyle.Bold, false));
    }

    protected override void OnPageThemeChanged()
    {
        var theme = SuThemeManager.Current;

        foreach (var (label, size, style, secondary) in _styled)
        {
            label.Font = DemoFonts.Get(theme, size, style);
            label.ForeColor = secondary ? theme.TextSecondary : theme.TextPrimary;
        }

        if (_card is not null)
        {
            _card.BackColor = theme.Surface;
            _card.Invalidate();
        }

        for (var i = 0; i < _cardLabels.Count; i++)
        {
            var label = _cardLabels[i];
            label.Font = DemoFonts.Get(theme, i == 0 ? 11f : 9f, i == 0 ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = i == 0 ? theme.TextPrimary : theme.TextSecondary;
        }
    }
}
