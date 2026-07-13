namespace AutoWuxia.Forms;

/// <summary>
/// 月度演化进度弹窗 - 显示 Agent 工具调用过程
/// </summary>
public class MonthlyProgressForm : Form
{
    private readonly Label _statusLabel;
    private readonly RichTextBox _logBox;
    private readonly Button _confirmButton;
    private string _summary = "";

    /// <summary>
    /// Agent 是否已完成处理
    /// </summary>
    public bool IsComplete { get; private set; }

    public MonthlyProgressForm()
    {
        // 窗体基本属性
        Text = "岁月流转 - 月度演化";
        Size = new Size(680, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = WuxiaTheme.UiFont(9f);

        // 暗色主题
        BackColor = Color.FromArgb(30, 30, 45);
        ForeColor = Color.FromArgb(220, 210, 190);

        // 状态标签
        _statusLabel = new Label
        {
            Text = "岁月流转，江湖风云变幻...",
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 215, 120),
            BackColor = Color.Transparent,
            Location = new Point(20, 15),
            AutoSize = true
        };
        Controls.Add(_statusLabel);

        // 日志文本框
        _logBox = new RichTextBox
        {
            ReadOnly = true,
            BackColor = Color.FromArgb(20, 20, 35),
            ForeColor = Color.FromArgb(200, 190, 170),
            Font = WuxiaTheme.UiFont(9f),
            BorderStyle = BorderStyle.None,
            Location = new Point(15, 50),
            Size = new Size(635, 410),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        Controls.Add(_logBox);

        // 确认按钮
        _confirmButton = new Button
        {
            Text = "确认",
            Size = new Size(120, 36),
            Location = new Point(275, 475),
            Enabled = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(80, 60, 30),
            ForeColor = Color.FromArgb(255, 215, 120),
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _confirmButton.FlatAppearance.BorderColor = Color.FromArgb(180, 140, 60);
        _confirmButton.FlatAppearance.BorderSize = 1;
        _confirmButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 80, 40);
        _confirmButton.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(_confirmButton);
        WuxiaTheme.ApplyScaling(this);
    }

    /// <summary>
    /// 追加工具调用日志
    /// </summary>
    public void AppendLog(string message, Color? color = null)
    {
        if (InvokeRequired)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() => AppendLog(message, color));
            return;
        }

        var c = color ?? Color.FromArgb(200, 190, 170);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = c;
        _logBox.AppendText($"{message}\n");
        _logBox.ScrollToCaret();
    }

    /// <summary>
    /// 设置状态标签
    /// </summary>
    public void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() => SetStatus(status));
            return;
        }
        _statusLabel.Text = status;
    }

    /// <summary>
    /// Agent 完成，显示总结并启用确认按钮
    /// </summary>
    public void AgentFinished(string summary)
    {
        if (InvokeRequired)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() => AgentFinished(summary));
            return;
        }

        if (IsComplete) return; // 避免重复调用
        IsComplete = true;
        _summary = summary;
        _statusLabel.Text = "月度演化完成";
        _statusLabel.ForeColor = Color.FromArgb(120, 220, 120);

        _logBox.AppendText("\n");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.FromArgb(255, 215, 120);
        _logBox.SelectionFont = new Font(WuxiaTheme.UiFont(10f), FontStyle.Bold);
        _logBox.AppendText("═══ 月度总结 ═══\n");

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.FromArgb(240, 230, 200);
        _logBox.SelectionFont = WuxiaTheme.UiFont(9.5f);
        _logBox.AppendText(summary + "\n");
        _logBox.ScrollToCaret();

        _confirmButton.Enabled = true;
    }

    /// <summary>
    /// Agent 出错
    /// </summary>
    public void AgentError(string error)
    {
        if (InvokeRequired)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() => AgentError(error));
            return;
        }

        AppendLog($"错误: {error}", Color.FromArgb(255, 120, 120));
        IsComplete = true;
        _statusLabel.Text = "月度演化出错";
        _statusLabel.ForeColor = Color.FromArgb(255, 120, 120);
        _confirmButton.Enabled = true;
    }
}
