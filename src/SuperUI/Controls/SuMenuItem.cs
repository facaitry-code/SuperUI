using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SuperUI.Controls;

/// <summary>菜单项类型。</summary>
public enum SuMenuItemKind
{
    /// <summary>可点击的菜单项。</summary>
    Item,

    /// <summary>分组标题（不可点击，字号更小、颜色更淡）。</summary>
    Group,

    /// <summary>分隔线。</summary>
    Separator,
}

/// <summary>
/// 侧边菜单的数据项：文本、图标、徽标、子项与展开状态。
/// 任何属性变化都会冒泡触发 <see cref="Changed"/>，菜单据此重绘。
/// </summary>
[Description("SuperUI 侧边菜单项：文本 + 可选图标/徽标/子项。")]
[DefaultProperty(nameof(Text))]
public class SuMenuItem
{
    private string _text = string.Empty;
    private string? _badge;
    private string? _glyph;
    private Image? _icon;
    private bool _enabled = true;
    private bool _isExpanded;
    private SuMenuItemKind _kind = SuMenuItemKind.Item;

    public SuMenuItem()
    {
        Children.CollectionChanged += ChildrenOnCollectionChanged;
    }

    public SuMenuItem(string text)
        : this()
    {
        _text = text ?? string.Empty;
    }

    /// <summary>分组标题。</summary>
    public static SuMenuItem Group(string text) => new(text) { Kind = SuMenuItemKind.Group };

    /// <summary>分隔线。</summary>
    public static SuMenuItem Separator() => new(string.Empty) { Kind = SuMenuItemKind.Separator };

    /// <summary>属性变化通知（含子项冒泡）。</summary>
    public event EventHandler? Changed;

    /// <summary>显示文本。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value)
            {
                return;
            }

            _text = value;
            OnChanged();
        }
    }

    /// <summary>业务标识（导航时通常用它而不是显示文本）。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    public string Key { get; set; } = string.Empty;

    /// <summary>左侧图标；为空时可用 <see cref="Glyph"/>。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public Image? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnChanged();
        }
    }

    /// <summary>图标字体里的一段字形（例如 Segoe MDL2 Assets 的 "\uE80F"）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public string? Glyph
    {
        get => _glyph;
        set
        {
            if (_glyph == value)
            {
                return;
            }

            _glyph = value;
            OnChanged();
        }
    }

    /// <summary>右侧徽标文本（如数量、NEW）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public string? Badge
    {
        get => _badge;
        set
        {
            if (_badge == value)
            {
                return;
            }

            _badge = value;
            OnChanged();
        }
    }

    /// <summary>是否可用。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            OnChanged();
        }
    }

    /// <summary>子项是否展开（仅对含子项的项有效）。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnChanged();
        }
    }

    /// <summary>项类型。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuMenuItemKind.Item)]
    public SuMenuItemKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            OnChanged();
        }
    }

    /// <summary>业务数据。</summary>
    [Category("SuperUI")]
    [Browsable(false)]
    public object? Tag { get; set; }

    /// <summary>父项（由集合自动维护）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuMenuItem? Parent { get; internal set; }

    /// <summary>子项集合。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObservableCollection<SuMenuItem> Children { get; } = new();

    /// <summary>是否存在子项。</summary>
    [Browsable(false)]
    public bool HasChildren => Children.Count > 0;

    /// <summary>追加子项。</summary>
    public SuMenuItem Add(SuMenuItem child)
    {
        Children.Add(child);
        return this;
    }

    /// <summary>追加一个子项（按文本）。</summary>
    public SuMenuItem Add(string text) => Add(new SuMenuItem(text));

    /// <summary>按文本查找后代项（含自身）。</summary>
    public SuMenuItem? Find(string text)
    {
        if (string.Equals(_text, text, StringComparison.Ordinal))
        {
            return this;
        }

        foreach (var child in Children)
        {
            var found = child.Find(text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>展开所有祖先，使本项可见。</summary>
    public void ExpandAncestors()
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
        {
            parent.IsExpanded = true;
        }
    }

    /// <inheritdoc />
    public override string ToString()
        => Kind switch
        {
            SuMenuItemKind.Group => $"[分组] {Text}",
            SuMenuItemKind.Separator => "[分隔线]",
            _ => Text,
        };

    private void ChildrenOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SuMenuItem child in e.OldItems)
            {
                child.Parent = null;
                child.Changed -= ChildOnChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (SuMenuItem child in e.NewItems)
            {
                child.Parent = this;
                child.Changed += ChildOnChanged;
            }
        }

        OnChanged();
    }

    private void ChildOnChanged(object? sender, EventArgs e) => OnChanged();

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
