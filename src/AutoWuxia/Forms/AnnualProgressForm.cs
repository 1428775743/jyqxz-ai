namespace AutoWuxia.Forms;

/// <summary>
/// 季度剧情进度弹窗 - 显示季度 Agent 生成剧情任务的过程(玩家视角)
/// </summary>
public class AnnualProgressForm : Form
{
    private readonly Label _statusLabel;
    private readonly RichTextBox _logBox;
    private readonly Button _confirmButton;
    private string _summary = "";

    public bool IsComplete { get; private set; }

    public AnnualProgressForm()
    {
        Text = "江湖风云 - 季度剧情";
        Size = new Size(680, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = WuxiaTheme.UiFont(9f);

        BackColor = Color.FromArgb(30, 30, 45);
        ForeColor = Color.FromArgb(220, 210, 190);

        _statusLabel = new Label
        {
            Text = "季末风云变幻，似有新的故事将起...",
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 215, 120),
            BackColor = Color.Transparent,
            Location = new Point(20, 15),
            AutoSize = true
        };
        Controls.Add(_statusLabel);

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

    public void AppendLog(string message, Color? color = null)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message, color));
            return;
        }

        var c = color ?? Color.FromArgb(200, 190, 170);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = c;
        _logBox.AppendText($"{message}\n");
        _logBox.ScrollToCaret();
    }

    public void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(status));
            return;
        }
        _statusLabel.Text = status;
    }

    public void AgentFinished(string summary)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AgentFinished(summary));
            return;
        }

        if (IsComplete) return;
        IsComplete = true;
        _summary = summary;
        _statusLabel.Text = "季度剧情已定稿";
        _statusLabel.ForeColor = Color.FromArgb(120, 220, 120);

        _logBox.AppendText("\n");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.FromArgb(255, 215, 120);
        _logBox.SelectionFont = new Font(WuxiaTheme.UiFont(10f), FontStyle.Bold);
        _logBox.AppendText("═══ 季度江湖纪事 ═══\n");

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.FromArgb(240, 230, 200);
        _logBox.SelectionFont = WuxiaTheme.UiFont(9.5f);
        _logBox.AppendText(summary + "\n");
        _logBox.ScrollToCaret();

        _confirmButton.Enabled = true;
    }

    public void AgentError(string error)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AgentError(error));
            return;
        }

        AppendLog($"异常: {error}", Color.FromArgb(255, 120, 120));
        IsComplete = true;
        _statusLabel.Text = "季度演化出错";
        _statusLabel.ForeColor = Color.FromArgb(255, 120, 120);
        _confirmButton.Enabled = true;
    }
}
