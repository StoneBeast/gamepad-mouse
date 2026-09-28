using System.Drawing.Drawing2D;

namespace GamepadMouse.Ui;

/// <summary>圆角卡片容器：标题 + 内容区。</summary>
internal class Card : UiPanel
{
    public string Title { get; }
    public int ContentTop => 38;

    public Card(string title, Point location, Size size)
    {
        Title = title;
        Location = location;
        Size = size;
        BackColor = UiTheme.WindowBg;

    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = UiTheme.Round(r, 12))
        {
            using var bg = new SolidBrush(UiTheme.CardBg);
            g.FillPath(bg, p);
            using var border = new Pen(UiTheme.CardBorder);
            g.DrawPath(border, p);
        }
        // 标题左侧主题色小竖条
        using (var bar = new SolidBrush(UiTheme.Accent))
            g.FillRectangle(bar, 20, 15, 3, 13);
        TextRenderer.DrawText(g, Title, UiTheme.FontUiBold, new Point(30, 12), UiTheme.TextSecondary);
        base.OnPaint(e);
    }
}

/// <summary>现代化按钮：Primary（主题色填充）/ Ghost（描边）。</summary>
internal class ModernButton : UiControl
{
    public enum Style { Primary, Ghost }
    public Style ButtonStyle { get; set; } = Style.Ghost;

    private bool _hover, _down;

    public ModernButton(string text, Point location, Size size, Style style = Style.Ghost)
    {
        Text = text;
        Location = location;
        Size = size;
        ButtonStyle = style;

        Cursor = Cursors.Hand;
        Font = UiTheme.FontUiBold;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool primary = ButtonStyle == Style.Primary;
        bool enabled = Enabled;

        Color fill = primary
            ? !enabled ? UiTheme.TrackOff : _down ? UiTheme.AccentPressed : _hover ? UiTheme.AccentHover : UiTheme.Accent
            : _hover ? UiTheme.CardBgHover : UiTheme.CardBg;
        Color textColor = primary ? Color.White : enabled ? UiTheme.TextPrimary : UiTheme.TextSecondary;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = UiTheme.Round(r, 8))
        {
            using var bg = new SolidBrush(fill);
            g.FillPath(bg, p);
            using var border = new Pen(primary ? fill : _hover ? UiTheme.Accent : UiTheme.CardBorder);
            g.DrawPath(border, p);
        }
        TextRenderer.DrawText(g, Text, Font, r, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        base.OnPaint(e);
    }
}

/// <summary>拨动开关（带滑动动画）。</summary>
internal class ToggleSwitch : UiControl
{
    public bool Checked { get; private set; }
    public event EventHandler? CheckedChanged;

    /// <summary>程序化设置状态（不触发 CheckedChanged，直接就位不播动画）。</summary>
    public void SetChecked(bool value)
    {
        if (Checked == value) return;
        Checked = value;
        _anim = value ? 1f : 0f;
        _timer.Stop();
        Invalidate();
    }

    private float _anim;
    private readonly System.Windows.Forms.Timer _timer;

    public ToggleSwitch(bool isChecked, Point location)
    {
        Checked = isChecked;
        _anim = isChecked ? 1f : 0f;
        Location = location;
        Size = new Size(46, 24);

        Cursor = Cursors.Hand;

        _timer = new System.Windows.Forms.Timer { Interval = 12 };
        _timer.Tick += (_, _) =>
        {
            float target = Checked ? 1f : 0f;
            _anim += (target - _anim) * 0.38f;
            if (Math.Abs(target - _anim) < 0.02f) { _anim = target; _timer.Stop(); }
            Invalidate();
        };
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        _timer.Start();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float t = Checked ? _anim : 1f - _anim;
        var track = UiTheme.Lerp(UiTheme.TrackOff, UiTheme.Accent, t);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = UiTheme.Round(r, Height / 2))
        using (var b = new SolidBrush(track))
            g.FillPath(b, p);

        int thumbD = Height - 6;
        int x = 3 + (int)((Width - thumbD - 6) * (Checked ? _anim : 1f - _anim));
        using (var b = new SolidBrush(Color.White))
            g.FillEllipse(b, x, 3, thumbD, thumbD);
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>滑杆（轨道 + 圆形滑块 + 右侧数值文本）。</summary>
internal class ModernSlider : UiControl
{
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;
    public Func<double, string> Format { get; set; } = v => $"{v:0.##}";

    private double _value;
    private bool _drag;
    public event EventHandler? ValueChanged;

    private int TrackLeft => 10;
    private int TrackRight => Width - 66;
    private int TrackY => Height / 2;

    public double Value
    {
        get => _value;
        set
        {
            double snapped = Math.Round((Math.Clamp(value, Min, Max) - Min) / Step) * Step + Min;
            snapped = Math.Clamp(snapped, Min, Max);
            if (Math.Abs(snapped - _value) < 1e-9) return;
            _value = snapped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ModernSlider(double min, double max, double step, double value, Point location, Size size)
    {
        Min = min; Max = max; Step = step; _value = value;
        Location = location;
        Size = size;

        Cursor = Cursors.Hand;
        Font = UiTheme.FontValue;
    }

    private double PosToValue(int x)
    {
        double t = (x - TrackLeft) / (double)Math.Max(1, TrackRight - TrackLeft);
        return Min + Math.Clamp(t, 0, 1) * (Max - Min);
    }

    private int ValueToPos()
    {
        double t = (Max - Min) < 1e-9 ? 0 : (_value - Min) / (Max - Min);
        return (int)Math.Round(TrackLeft + t * (TrackRight - TrackLeft));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _drag = true; Value = PosToValue(e.X); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag) Value = PosToValue(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int pos = ValueToPos();

        // 轨道
        var trackRect = new Rectangle(TrackLeft, TrackY - 2, TrackRight - TrackLeft, 5);
        using (var p = UiTheme.Round(trackRect, 2))
        {
            using var bg = new SolidBrush(UiTheme.SliderTrack);
            g.FillPath(bg, p);
            var fillRect = new Rectangle(TrackLeft, TrackY - 2, Math.Max(4, pos - TrackLeft), 5);
            using var p2 = UiTheme.Round(fillRect, 2);
            using var fill = new SolidBrush(UiTheme.Accent);
            g.FillPath(fill, p2);
        }

        // 滑块
        int d = _drag ? 16 : 14;
        using (var b = new SolidBrush(Color.White))
            g.FillEllipse(b, pos - d / 2, TrackY - d / 2, d, d);
        using (var pen = new Pen(UiTheme.Accent, 2f))
            g.DrawEllipse(pen, pos - d / 2, TrackY - d / 2, d, d);

        // 数值
        var text = Format(_value);
        var rect = new Rectangle(TrackRight + 6, 0, 58, Height);
        TextRenderer.DrawText(g, text, Font, rect, UiTheme.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        base.OnPaint(e);
    }
}

/// <summary>分段选择器。</summary>
internal class Segmented : UiControl
{
    public string[] Items { get; }
    public int Selected { get; private set; }

    public void SetSelected(int idx)
    {
        if (idx == Selected || idx < 0 || idx >= Items.Length) return;
        Selected = idx;
        Invalidate();
    }
    public event EventHandler? SelectedChanged;

    public Segmented(string[] items, int selected, Point location, Size size)
    {
        Items = items;
        Selected = selected;
        Location = location;
        Size = size;

        Cursor = Cursors.Hand;
        Font = UiTheme.FontUi;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int w = Width / Items.Length;
        int idx = Math.Clamp(e.X / w, 0, Items.Length - 1);
        if (idx != Selected)
        {
            Selected = idx;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = UiTheme.Round(r, 8))
        {
            using var bg = new SolidBrush(UiTheme.WindowBg);
            g.FillPath(bg, p);
            using var border = new Pen(UiTheme.CardBorder);
            g.DrawPath(border, p);
        }

        int w = Width / Items.Length;
        for (int i = 0; i < Items.Length; i++)
        {
            var seg = new Rectangle(i * w, 2, w - (i == Items.Length - 1 ? 2 : 0), Height - 4);
            bool sel = i == Selected;
            if (sel)
            {
                using var p = UiTheme.Round(new Rectangle(seg.X + 2, seg.Y, seg.Width - 4, seg.Height), 6);
                using var b = new SolidBrush(UiTheme.Accent);
                g.FillPath(b, p);
            }
            TextRenderer.DrawText(g, Items[i], sel ? UiTheme.FontUiBold : Font, seg,
                sel ? Color.White : UiTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        base.OnPaint(e);
    }
}

/// <summary>状态胶囊（圆点 + 文本）。</summary>
internal class StatusPill : UiControl
{
    private bool _ok = true;
    private string _text = "";

    public StatusPill(Point location)
    {
        Location = location;
        Size = new Size(120, 26);

        Font = UiTheme.FontSmall;
    }

    public void Set(string text, bool ok)
    {
        _text = text;
        _ok = ok;
        Width = TextRenderer.MeasureText(_text, UiTheme.FontSmall).Width + 46;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var main = _ok ? UiTheme.Success : UiTheme.TextSecondary;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = UiTheme.Round(r, Height / 2))
        {
            using var bg = new SolidBrush(Color.FromArgb(30, main));
            g.FillPath(bg, p);
            using var border = new Pen(Color.FromArgb(70, main));
            g.DrawPath(border, p);
        }
        using (var b = new SolidBrush(main))
            g.FillEllipse(b, 12, Height / 2 - 4, 8, 8);
        TextRenderer.DrawText(g, _text, UiTheme.FontSmall,
            new Rectangle(26, 0, Width - 30, Height), main,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        base.OnPaint(e);
    }
}

/// <summary>小圆片标签（显示组合键）。</summary>
internal class Chip : UiControl
{
    public Chip(string text)
    {
        Text = text;

        Font = UiTheme.FontUiBold;
        Height = 24;
        UpdateWidth();
    }

    private void UpdateWidth()
    {
        Width = TextRenderer.MeasureText(Text, Font).Width + 22;
    }

    public void SetText(string text) { Text = text; UpdateWidth(); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = UiTheme.Round(r, Height / 2))
        {
            using var bg = new SolidBrush(UiTheme.AccentDim);
            g.FillPath(bg, p);
            using var border = new Pen(Color.FromArgb(120, UiTheme.Accent));
            g.DrawPath(border, p);
        }
        TextRenderer.DrawText(g, Text, Font, r, UiTheme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        base.OnPaint(e);
    }
}
