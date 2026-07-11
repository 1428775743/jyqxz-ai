using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Quests;

namespace AutoWuxia.Forms;

/// <summary>
/// RPG剧情对话窗:左侧大头像 + 底部对话框,点击/空格逐句推进。
/// 用于任务接取(intro)/步骤完成/领奖(complete)的预定义台词剧情。
/// </summary>
public class StoryDialogueForm : Form
{
    private readonly DialogueScript _script;
    private readonly GameState _state;
    private readonly Action? _onComplete;

    // 展平后的逐句序列:(说话人ID, 显示名, 台词)
    private readonly List<(string speaker, string name, string line)> _seq = new();
    private int _index = 0;

    private PictureBox _portraitBox = null!;
    private Label _nameLabel = null!;
    private Label _lineLabel = null!;
    private Label _hintLabel = null!;

    // 无边框窗体拖动支持(移动超阈值才拖,纯点击仍推进剧情)
    private Point _dragStart;
    private bool _dragMoved;

    private static readonly Color Bg = Color.FromArgb(25, 25, 38);
    private static readonly Color Border = Color.FromArgb(224, 139, 43);
    private static readonly Color Gold = Color.FromArgb(255, 220, 150);
    private static readonly Color TextColor = Color.FromArgb(220, 210, 190);
    private static readonly Color Narration = Color.FromArgb(170, 170, 180);

    private StoryDialogueForm(DialogueScript script, GameState state, Action? onComplete)
    {
        _script = script;
        _state = state;
        _onComplete = onComplete;
        Flatten();
        InitializeComponent();
    }

    private void Flatten()
    {
        foreach (var dl in _script.Lines)
        {
            var (name, _) = ResolveSpeaker(dl.Speaker);
            foreach (var line in dl.Lines)
                _seq.Add((dl.Speaker, name, line));
        }
    }

    /// <summary>解析说话人:返回(显示名, portraitPath)。旁白/玩家特殊处理。</summary>
    private (string name, string? portrait) ResolveSpeaker(string speaker)
    {
        if (speaker == "旁白" || string.IsNullOrEmpty(speaker))
            return ("", null);
        if (speaker == "玩家")
            return (_state.Player.Name, _state.Player.PortraitPath);
        if (_state.AllNPCs.TryGetValue(speaker, out var npc))
            return (npc.Name, npc.PortraitPath);
        return (speaker, null); // 未知ID,降级显示ID
    }

    private void InitializeComponent()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(720, 260);
        BackColor = Bg;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        // 左侧大头像
        _portraitBox = new PictureBox
        {
            Location = new Point(16, 16),
            Size = new Size(96, 96),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.FromArgb(40, 40, 55)
        };
        Controls.Add(_portraitBox);

        // 说话人名
        _nameLabel = new Label
        {
            Location = new Point(124, 16),
            Size = new Size(580, 28),
            Font = WuxiaTheme.UiFont(13f, FontStyle.Bold),
            ForeColor = Gold,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_nameLabel);

        // 对话框主体
        _lineLabel = new Label
        {
            Location = new Point(124, 50),
            Size = new Size(580, 170),
            Font = WuxiaTheme.UiFont(12f),
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.TopLeft
        };
        Controls.Add(_lineLabel);

        // 底部提示
        _hintLabel = new Label
        {
            Text = "▼ 点击继续 / 空格",
            Location = new Point(560, 226),
            Size = new Size(144, 22),
            Font = WuxiaTheme.UiFont(8.5f),
            ForeColor = Color.FromArgb(140, 130, 110),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        Controls.Add(_hintLabel);

        // 自绘金色边框
        Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 2);
            e.Graphics.DrawRectangle(pen, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        };

        // 无边框窗体可拖动:按下移动超阈值则拖动,纯点击仍推进剧情
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragStart = e.Location; _dragMoved = false; } };
        MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (!_dragMoved && (Math.Abs(e.X - _dragStart.X) > 4 || Math.Abs(e.Y - _dragStart.Y) > 4))
                _dragMoved = true;
            if (_dragMoved)
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
        };
        // 点击/空格推进(本次若发生拖动则不推进)
        Click += (_, _) => { if (!_dragMoved) Advance(); _dragMoved = false; };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) Advance(); };

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
        RenderCurrent();
    }

    private void RenderCurrent()
    {
        if (_index >= _seq.Count)
        {
            Finish();
            return;
        }
        var (speaker, name, line) = _seq[_index];
        bool isNarration = speaker == "旁白" || string.IsNullOrEmpty(speaker);

        _nameLabel.Text = isNarration ? "" : name;
        _nameLabel.ForeColor = isNarration ? Narration : Gold;

        _lineLabel.Text = line;
        _lineLabel.ForeColor = isNarration ? Narration : TextColor;
        _lineLabel.Font = WuxiaTheme.UiFont(isNarration ? 11f : 12f, isNarration ? FontStyle.Italic : FontStyle.Regular);

        // 头像
        if (isNarration)
        {
            _portraitBox.Image = null;
            _portraitBox.BackColor = Color.FromArgb(30, 30, 42);
        }
        else
        {
            var (_, portrait) = ResolveSpeaker(speaker);
            _portraitBox.Image = PortraitHelper.GetPortraitOrDefault(portrait, name, 96);
        }
    }

    private void Advance()
    {
        _index++;
        if (_index >= _seq.Count)
        {
            Finish();
            return;
        }
        RenderCurrent();
    }

    private void Finish()
    {
        _onComplete?.Invoke();
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// 显示剧情对话窗(模态)。播完触发 onComplete。script 无内容则直接回调不弹窗。
    /// </summary>
    public static void Show(IWin32Window? owner, DialogueScript? script, GameState state, Action? onComplete = null)
    {
        if (script == null || !script.HasContent)
        {
            onComplete?.Invoke();
            return;
        }
        using var form = new StoryDialogueForm(script, state, onComplete);
        form.ShowDialog(owner);
    }
}
