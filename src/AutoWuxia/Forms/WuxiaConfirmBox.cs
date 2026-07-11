namespace AutoWuxia.Forms;

/// <summary>
/// 武侠风确认框样式(影响确认按钮配色)
/// </summary>
public enum WuxiaConfirmStyle
{
    Neutral,
    Danger,
    Success
}

/// <summary>
/// 深色武侠风模态确认/提示组件,替代对话上下文里的系统 MessageBox。
/// 与 DialogueForm/CombatForm 的深色基调一致:深底 + 金色边框/标题。
/// </summary>
public static class WuxiaConfirmBox
{
    // 色板
    private static readonly Color BackColor = Color.FromArgb(25, 25, 35);
    private static readonly Color TitleBarColor = Color.FromArgb(40, 40, 55);
    private static readonly Color BorderColor = Color.FromArgb(224, 139, 43);
    private static readonly Color TitleColor = Color.FromArgb(255, 220, 150);
    private static readonly Color MessageColor = Color.FromArgb(220, 220, 200);
    private static readonly Color CancelColor = Color.FromArgb(60, 60, 70);

    private static Color ConfirmColor(WuxiaConfirmStyle style) => style switch
    {
        WuxiaConfirmStyle.Danger => Color.FromArgb(120, 50, 50),
        WuxiaConfirmStyle.Success => Color.FromArgb(50, 90, 60),
        _ => Color.FromArgb(90, 70, 40)
    };

    /// <summary>
    /// 双按钮确认框。返回 true=用户点了确认按钮。
    /// </summary>
    public static bool Show(IWin32Window? owner, string title, string message,
        string confirmText = "确定", string cancelText = "取消",
        WuxiaConfirmStyle style = WuxiaConfirmStyle.Neutral)
    {
        using var form = BuildForm(title, message, confirmText, cancelText, style, isAlert: false, out var confirmBtn);
        // 手动设置居中位置
        if (owner != null && owner is Control control)
        {
            form.StartPosition = FormStartPosition.Manual;
            // 如果父窗口还未显示(Location为0,0)，使用屏幕中心；否则相对于父窗口居中
            if (control.Location.X == 0 && control.Location.Y == 0)
            {
                // 父窗口未显示，使用屏幕中心
                var screen = System.Windows.Forms.Screen.FromControl(control);
                int x = screen.WorkingArea.Left + (screen.WorkingArea.Width - form.Width) / 2;
                int y = screen.WorkingArea.Top + (screen.WorkingArea.Height - form.Height) / 2;
                form.Location = new System.Drawing.Point(x, y);
            }
            else
            {
                // 父窗口已显示，相对于父窗口居中
                int x = control.Location.X + (control.Width - form.Width) / 2;
                int y = control.Location.Y + (control.Height - form.Height) / 2;
                form.Location = new System.Drawing.Point(x, y);
            }
        }
        form.ShowDialog(owner);
        return form.DialogResult == DialogResult.OK;
    }

    /// <summary>
    /// 单按钮提示框(替代 OK 型 MessageBox)。点击或关闭即返回。
    /// </summary>
    public static void Alert(IWin32Window? owner, string title, string message,
        WuxiaConfirmStyle style = WuxiaConfirmStyle.Neutral)
    {
        using var form = BuildForm(title, message, "知道了", null!, style, isAlert: true, out _);
        // 手动设置居中位置
        if (owner != null && owner is Control control)
        {
            form.StartPosition = FormStartPosition.Manual;
            // 如果父窗口还未显示(Location为0,0)，使用屏幕中心；否则相对于父窗口居中
            if (control.Location.X == 0 && control.Location.Y == 0)
            {
                // 父窗口未显示，使用屏幕中心
                var screen = System.Windows.Forms.Screen.FromControl(control);
                int x = screen.WorkingArea.Left + (screen.WorkingArea.Width - form.Width) / 2;
                int y = screen.WorkingArea.Top + (screen.WorkingArea.Height - form.Height) / 2;
                form.Location = new System.Drawing.Point(x, y);
            }
            else
            {
                // 父窗口已显示，相对于父窗口居中
                int x = control.Location.X + (control.Width - form.Width) / 2;
                int y = control.Location.Y + (control.Height - form.Height) / 2;
                form.Location = new System.Drawing.Point(x, y);
            }
        }
        form.ShowDialog(owner);
    }

    private static Form BuildForm(string title, string message,
        string confirmText, string cancelText, WuxiaConfirmStyle style,
        bool isAlert, out Button confirmBtn)
    {
        var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = BackColor,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Width = 420
        };

        // 标题栏(自绘高条 + 金色标题)
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = TitleBarColor
        };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = TitleColor,
            BackColor = TitleBarColor,
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        titleBar.Controls.Add(titleLabel);
        FormDragHelper.EnableDrag(titleBar);  // 标题栏可拖动整窗

        // 消息正文(按内容估算高度)
        var msgLabel = new Label
        {
            Text = message,
            ForeColor = MessageColor,
            BackColor = BackColor,
            Font = WuxiaTheme.UiFont(10f),
            AutoSize = false,
            Location = new Point(24, 50),
            Size = new Size(372, 0)
        };
        // 估算消息高度
        using (var g = form.CreateGraphics())
        {
            var size = g.MeasureString(message, msgLabel.Font, 372);
            int lines = Math.Max(1, (int)Math.Ceiling(size.Height / msgLabel.Font.GetHeight(g)));
            msgLabel.Height = Math.Max(40, lines * (int)(msgLabel.Font.GetHeight(g) + 4) + 8);
        }

        // 按钮区
        var btnHeight = 34;
        var btnWidth = 96;
        var btnY = msgLabel.Bottom + 18;

        confirmBtn = new Button
        {
            Text = confirmText,
            Size = new Size(btnWidth, btnHeight),
            Font = WuxiaTheme.UiFont(9.5f),
            FlatStyle = FlatStyle.Flat,
            BackColor = ConfirmColor(style),
            ForeColor = Color.FromArgb(240, 235, 220),
            Cursor = Cursors.Hand
        };
        confirmBtn.FlatAppearance.BorderColor = BorderColor;
        confirmBtn.FlatAppearance.BorderSize = 1;
        confirmBtn.Click += (_, _) => { form.DialogResult = DialogResult.OK; form.Close(); };

        if (isAlert)
        {
            // 单按钮居中
            confirmBtn.Location = new Point((form.Width - btnWidth) / 2, btnY);
            form.Controls.Add(titleBar);
            form.Controls.Add(msgLabel);
            form.Controls.Add(confirmBtn);
            form.Height = btnY + btnHeight + 24;
        }
        else
        {
            var cancelBtn = new Button
            {
                Text = cancelText,
                Size = new Size(btnWidth, btnHeight),
                Font = WuxiaTheme.UiFont(9.5f),
                FlatStyle = FlatStyle.Flat,
                BackColor = CancelColor,
                ForeColor = Color.FromArgb(220, 220, 200),
                Cursor = Cursors.Hand,
                Location = new Point(form.Width - btnWidth - 24, btnY)
            };
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 90);
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.Click += (_, _) => { form.DialogResult = DialogResult.Cancel; form.Close(); };

            // 确认按钮在取消按钮左侧
            confirmBtn.Location = new Point(cancelBtn.Left - btnWidth - 12, btnY);

            form.Controls.Add(titleBar);
            form.Controls.Add(msgLabel);
            form.Controls.Add(confirmBtn);
            form.Controls.Add(cancelBtn);
            form.Height = btnY + btnHeight + 24;
        }

        // 自绘金色边框 + 标题栏底部分割线
        form.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 2);
            var r = new Rectangle(0, 0, form.Width - 1, form.Height - 1);
            e.Graphics.DrawRectangle(pen, r);
            using var linePen = new Pen(Color.FromArgb(70, 60, 40), 1);
            e.Graphics.DrawLine(linePen, 0, 38, form.Width, 38);
        };

        // 接受 Esc=取消、Enter=确认(符合常规对话框交互)
        form.KeyPreview = true;
        form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                form.DialogResult = DialogResult.Cancel;
                form.Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        };

        WuxiaTheme.ApplyScaling(form);  // 应用界面缩放
        return form;
    }
}
