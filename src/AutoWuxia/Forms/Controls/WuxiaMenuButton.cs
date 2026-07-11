using System.Drawing.Drawing2D;

namespace AutoWuxia.Forms.Controls;

/// <summary>
/// 首页专用的半透明水墨按钮：圆角、柔影，并带轻微悬浮动画。
/// </summary>
internal sealed class WuxiaMenuButton : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer;
    private bool _hovered;
    private bool _pressed;
    private float _hoverProgress;

    public WuxiaMenuButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable,
            true);

        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(244, 204, 126);
        Cursor = Cursors.Hand;
        TabStop = true;

        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) =>
        {
            var target = _hovered ? 1f : 0f;
            _hoverProgress += (target - _hoverProgress) * 0.22f;
            if (Math.Abs(target - _hoverProgress) < 0.01f)
            {
                _hoverProgress = target;
                _animationTimer.Stop();
            }
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        _animationTimer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        _animationTimer.Start();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = true;
        Focus();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            PerformClick();
        }
    }

    private void PerformClick() => OnClick(EventArgs.Empty);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float lift = -3.5f * _hoverProgress + (_pressed ? 2f : 0f);
        var body = new RectangleF(7, 8 + lift, Width - 14, Height - 20);
        float radius = Math.Min(24f, body.Width * 0.34f);

        using var shadowPath = CreateRoundedPath(
            new RectangleF(body.X + 4, body.Y + 7 + 2 * _hoverProgress, body.Width, body.Height),
            radius);
        using var shadowBrush = new SolidBrush(Color.FromArgb(
            (int)(72 + 38 * _hoverProgress), 12, 8, 5));
        g.FillPath(shadowBrush, shadowPath);

        using var bodyPath = CreateRoundedPath(body, radius);
        var top = _pressed
            ? Color.FromArgb(182, 75, 46, 27)
            : Color.FromArgb((int)(138 + 25 * _hoverProgress), 76, 54, 36);
        var bottom = _pressed
            ? Color.FromArgb(205, 42, 28, 19)
            : Color.FromArgb((int)(168 + 22 * _hoverProgress), 35, 27, 20);
        using (var fill = new LinearGradientBrush(body, top, bottom, LinearGradientMode.Horizontal))
            g.FillPath(fill, bodyPath);

        // 内侧柔光让半透明墨色有漆器般的厚度。
        var highlightRect = RectangleF.Inflate(body, -4, -4);
        using var highlightPath = CreateRoundedPath(highlightRect, Math.Max(8, radius - 4));
        using var highlightPen = new Pen(Color.FromArgb(
            (int)(42 + 52 * _hoverProgress), 255, 226, 164), 1f);
        g.DrawPath(highlightPen, highlightPath);

        using var borderPen = new Pen(Color.FromArgb(
            (int)(185 + 55 * _hoverProgress), 218, 164, 77), 1.8f);
        g.DrawPath(borderPen, bodyPath);

        // 上下两枚小装饰，强化竖向飘带感。
        float cx = body.Left + body.Width / 2f;
        using var ornamentPen = new Pen(Color.FromArgb(175, 229, 180, 92), 1.2f);
        g.DrawLine(ornamentPen, cx - 11, body.Top + 18, cx + 11, body.Top + 18);
        g.DrawLine(ornamentPen, cx - 8, body.Bottom - 18, cx + 8, body.Bottom - 18);

        var textRect = RectangleF.Inflate(body, -7, -27);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None
        };
        using var textShadow = new SolidBrush(Color.FromArgb(180, 22, 12, 7));
        using var textBrush = new SolidBrush(_pressed
            ? Color.FromArgb(255, 224, 160)
            : Color.FromArgb(246, 205, 126));
        g.DrawString(Text, Font, textShadow,
            new RectangleF(textRect.X + 1.5f, textRect.Y + 2, textRect.Width, textRect.Height), format);
        g.DrawString(Text, Font, textBrush, textRect, format);

        if (Focused && ShowFocusCues)
        {
            var focusRect = Rectangle.Round(RectangleF.Inflate(body, -5, -5));
            ControlPaint.DrawFocusRectangle(g, focusRect, ForeColor, Color.Transparent);
        }
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animationTimer.Dispose();
        base.Dispose(disposing);
    }
}
