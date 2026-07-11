namespace AutoWuxia.Forms;

internal static class WuxiaTheme
{
    public static readonly Color AppBack = Color.FromArgb(255, 244, 224);
    public static readonly Color PanelBack = Color.FromArgb(255, 232, 191);
    public static readonly Color PanelBackAlt = Color.FromArgb(255, 238, 209);
    public static readonly Color Surface = Color.FromArgb(255, 250, 239);
    public static readonly Color SurfaceWarm = Color.FromArgb(255, 218, 158);
    public static readonly Color Border = Color.FromArgb(224, 139, 43);
    public static readonly Color Accent = Color.FromArgb(219, 103, 21);
    public static readonly Color AccentSoft = Color.FromArgb(181, 80, 13);
    public static readonly Color Text = Color.FromArgb(65, 43, 26);
    public static readonly Color TextMuted = Color.FromArgb(126, 83, 43);
    public static readonly Color TextDim = Color.FromArgb(159, 119, 76);
    public static readonly Color Danger = Color.FromArgb(235, 156, 132);
    public static readonly Color Success = Color.FromArgb(206, 221, 151);

    /// <summary>界面缩放倍率(1.0=100%),由 DisplayConfig 启动时加载。</summary>
    public static double Scale { get; set; } = 1.0;

    public static Font UiFont(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font("Microsoft YaHei", (float)(size * Scale), style);
    }

    /// <summary>缩放后的尺寸</summary>
    public static Size S(int w, int h) => new((int)Math.Round(w * Scale), (int)Math.Round(h * Scale));
    /// <summary>缩放后的坐标</summary>
    public static Point P(int x, int y) => new((int)Math.Round(x * Scale), (int)Math.Round(y * Scale));
    /// <summary>缩放后的标量</summary>
    public static int V(int v) => (int)Math.Round(v * Scale);
    /// <summary>
    /// 对窗体应用缩放。字体在 <see cref="UiFont"/> 中已经按倍率创建，因此这里只处理
    /// 控件的几何尺寸。
    ///
    /// 不使用 Form.Scale：在高 DPI 显示器上 WinForms 会把它和系统 DPI 自动缩放混用，
    /// 造成窗体按设置倍率变大、子控件却只按系统 DPI 变大的情况，进而留下大片空白。
    /// </summary>
    public static void ApplyScaling(Form form)
    {
        if (Scale == 1.0) return;
        if (form.Tag is string tag && tag == "scaled") return;  // 防二次缩放

        // 所有窗体都由本主题统一缩放，避免 WinForms 在首次显示时再额外应用一次 DPI 缩放。
        form.AutoScaleMode = AutoScaleMode.None;
        form.SuspendLayout();
        form.Size = new Size((int)Math.Round(form.Size.Width * Scale), (int)Math.Round(form.Size.Height * Scale));
        if (form.MinimumSize != Size.Empty)
            form.MinimumSize = new Size((int)Math.Round(form.MinimumSize.Width * Scale), (int)Math.Round(form.MinimumSize.Height * Scale));
        if (form.MaximumSize != Size.Empty)
            form.MaximumSize = new Size((int)Math.Round(form.MaximumSize.Width * Scale), (int)Math.Round(form.MaximumSize.Height * Scale));

        ScaleControlTree(form, Scale);
        form.Tag = "scaled";
        form.ResumeLayout(true);
    }

    private static void ScaleControlTree(Control parent, double scale)
    {
        foreach (Control control in parent.Controls)
        {
            control.SuspendLayout();
            control.Location = P(control.Left, control.Top);
            control.Size = S(control.Width, control.Height);
            control.Margin = ScalePadding(control.Margin, scale);
            control.Padding = ScalePadding(control.Padding, scale);

            if (control.MinimumSize != Size.Empty)
                control.MinimumSize = S(control.MinimumSize.Width, control.MinimumSize.Height);
            if (control.MaximumSize != Size.Empty)
                control.MaximumSize = S(control.MaximumSize.Width, control.MaximumSize.Height);

            if (control is TableLayoutPanel table)
            {
                foreach (ColumnStyle style in table.ColumnStyles)
                    if (style.SizeType == SizeType.Absolute)
                        style.Width = (float)(style.Width * scale);
                foreach (RowStyle style in table.RowStyles)
                    if (style.SizeType == SizeType.Absolute)
                        style.Height = (float)(style.Height * scale);
            }

            ScaleControlTree(control, scale);
            control.ResumeLayout(false);
        }
    }

    private static Padding ScalePadding(Padding padding, double scale) => new(
        (int)Math.Round(padding.Left * scale),
        (int)Math.Round(padding.Top * scale),
        (int)Math.Round(padding.Right * scale),
        (int)Math.Round(padding.Bottom * scale));

    public static void StyleButton(Button button, Color? backColor = null)
    {
        var baseColor = backColor ?? SurfaceWarm;
        button.BackColor = baseColor;
        button.ForeColor = Text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Blend(baseColor, Accent, 0.35f);
        button.FlatAppearance.MouseDownBackColor = Blend(baseColor, Accent, 0.55f);
        button.Font = UiFont(9f);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    public static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = Surface;
        listBox.ForeColor = Text;
        listBox.Font = UiFont(9f);
        listBox.BorderStyle = BorderStyle.FixedSingle;
        listBox.IntegralHeight = false;
    }

    private static Color Blend(Color a, Color b, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * amount),
            (int)(a.G + (b.G - a.G) * amount),
            (int)(a.B + (b.B - a.B) * amount));
    }
}
