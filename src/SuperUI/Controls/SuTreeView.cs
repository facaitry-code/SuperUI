using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 树形控件：外层圆角边框 + 内部全自绘节点（折叠箭头、选中高亮、悬停底色）。
/// </summary>
[Description("SuperUI 树形控件：圆角外框 + 自绘节点（折叠箭头/选中/悬停），颜色跟随全局主题。")]
[DefaultProperty(nameof(Nodes))]
public class SuTreeView : SuControlBase
{
    private readonly TreeView _inner;
    private int _radius = -1;
    private bool _innerHovered;
    private TreeNode? _hoverNode;

    public SuTreeView()
    {
        Padding = new Padding(6, 6, 6, 6);

        _inner = new TreeView
        {
            BorderStyle = BorderStyle.None,
            DrawMode = TreeViewDrawMode.OwnerDrawAll,
            ShowLines = false,
            ShowRootLines = false,
            ShowPlusMinus = false,
            FullRowSelect = true,
            HideSelection = false,
            ItemHeight = 26,
            Indent = 18,
            TabIndex = 0,
        };

        Controls.Add(_inner);

        _inner.DrawNode += InnerOnDrawNode;
        _inner.AfterSelect += (_, e) =>
        {
            AfterSelect?.Invoke(this, e);
            _inner.Invalidate();
        };
        _inner.AfterExpand += (_, e) => AfterExpand?.Invoke(this, e);
        _inner.AfterCollapse += (_, e) => AfterCollapse?.Invoke(this, e);
        _inner.NodeMouseClick += (_, e) => NodeMouseClick?.Invoke(this, e);
        _inner.MouseMove += InnerOnMouseMove;
        _inner.MouseLeave += (_, __) =>
        {
            _innerHovered = false;
            _hoverNode = null;
            _inner.Invalidate();
            Invalidate();
        };
        _inner.Enter += (_, __) => Invalidate();
        _inner.Leave += (_, __) => Invalidate();

        ApplyTheme();
    }

    /// <summary>选中节点变化。</summary>
    public event TreeViewEventHandler? AfterSelect;

    /// <summary>节点展开。</summary>
    public event TreeViewEventHandler? AfterExpand;

    /// <summary>节点收起。</summary>
    public event TreeViewEventHandler? AfterCollapse;

    /// <summary>节点点击。</summary>
    public event TreeNodeMouseClickEventHandler? NodeMouseClick;

    /// <summary>根节点集合。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public TreeNodeCollection Nodes => _inner.Nodes;

    /// <summary>当前选中节点。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeNode? SelectedNode
    {
        get => _inner.SelectedNode;
        set => _inner.SelectedNode = value;
    }

    /// <summary>内部原生树控件（高级用法）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeView InnerTree => _inner;

    /// <summary>圆角半径，-1 表示跟随主题。</summary>
    [Category("SuperUI")]
    [DefaultValue(-1)]
    public int Radius
    {
        get => _radius;
        set
        {
            if (_radius == value)
            {
                return;
            }

            _radius = value;
            Invalidate();
        }
    }

    /// <summary>节点行高。</summary>
    [Category("SuperUI")]
    [DefaultValue(26)]
    public int ItemHeight
    {
        get => _inner.ItemHeight;
        set
        {
            _inner.ItemHeight = Math.Max(20, value);
            _inner.Invalidate();
        }
    }

    /// <summary>追加一个节点（parent 为 null 时加在根上）。</summary>
    public TreeNode AddNode(string text, TreeNode? parent = null, bool expanded = false)
    {
        var node = parent is null ? _inner.Nodes.Add(text) : parent.Nodes.Add(text);
        if (expanded)
        {
            node.Expand();
        }

        return node;
    }

    /// <summary>全部展开。</summary>
    public void ExpandAll() => _inner.ExpandAll();

    /// <summary>全部收起。</summary>
    public void CollapseAll() => _inner.CollapseAll();

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void ApplyTheme()
    {
        base.ApplyTheme();

        var theme = Theme;
        _inner.BackColor = theme.Surface;
        _inner.ForeColor = theme.TextPrimary;
        _inner.Invalidate();
        LayoutInner();
    }

    private void LayoutInner()
    {
        if (_inner is null)
        {
            return;
        }

        _inner.SetBounds(
            Padding.Left,
            Padding.Top,
            Math.Max(0, ClientSize.Width - Padding.Horizontal),
            Math.Max(0, ClientSize.Height - Padding.Vertical));
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        var radius = _radius >= 0 ? _radius : theme.Radius;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var surface = Enabled ? theme.Surface : theme.SurfaceAlt;
        var border = _inner.Focused
            ? theme.Primary
            : _innerHovered
                ? theme.BorderStrong
                : theme.Border;

        using var path = SuDraw.RoundedRect(rect, radius);
        using (var brush = new SolidBrush(surface))
        {
            graphics.FillPath(brush, path);
        }

        using (var pen = new Pen(border, _inner.Focused ? 1.4f : 1f))
        {
            graphics.DrawPath(pen, path);
        }
    }

    private void InnerOnMouseMove(object? sender, MouseEventArgs e)
    {
        _innerHovered = true;

        var node = _inner.GetNodeAt(e.Location);
        if (!ReferenceEquals(node, _hoverNode))
        {
            _hoverNode = node;
            _inner.Invalidate();
        }
    }

    private void InnerOnDrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        var theme = Theme;
        var graphics = e.Graphics;
        var node = e.Node;

        if (node is null)
        {
            return;
        }

        var rowBounds = new Rectangle(0, e.Bounds.Top, _inner.Width, Math.Max(e.Bounds.Height, _inner.ItemHeight));
        var selected = ReferenceEquals(_inner.SelectedNode, node);
        var hovered = ReferenceEquals(_hoverNode, node);
        var background = selected
            ? theme.PrimarySoft
            : hovered
                ? theme.SurfaceAlt
                : theme.Surface;

        using (var brush = new SolidBrush(background))
        {
            graphics.FillRectangle(brush, rowBounds);
        }

        var hasChildren = node.Nodes.Count > 0;
        var centerY = e.Bounds.Top + e.Bounds.Height / 2f;
        const int chevronGutter = 20;

        if (hasChildren)
        {
            var chevronCenter = new PointF(e.Bounds.Left + 9f, centerY);
            using var chevronPen = new Pen(selected ? theme.Primary : theme.TextSecondary, 1.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            if (node.IsExpanded)
            {
                graphics.DrawLines(
                    chevronPen,
                    new[]
                    {
                        new PointF(chevronCenter.X - 4f, chevronCenter.Y - 2f),
                        new PointF(chevronCenter.X, chevronCenter.Y + 2f),
                        new PointF(chevronCenter.X + 4f, chevronCenter.Y - 2f),
                    });
            }
            else
            {
                graphics.DrawLines(
                    chevronPen,
                    new[]
                    {
                        new PointF(chevronCenter.X - 2f, chevronCenter.Y - 4f),
                        new PointF(chevronCenter.X + 2f, chevronCenter.Y),
                        new PointF(chevronCenter.X - 2f, chevronCenter.Y + 4f),
                    });
            }
        }

        var textLeft = e.Bounds.Left + chevronGutter;
        var textRect = new Rectangle(
            textLeft,
            e.Bounds.Top,
            Math.Max(0, _inner.Width - textLeft - 8),
            e.Bounds.Height);

        TextRenderer.DrawText(
            graphics,
            node.Text,
            Font,
            textRect,
            selected ? theme.Primary : (Enabled ? theme.TextPrimary : theme.TextDisabled),
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }
}
