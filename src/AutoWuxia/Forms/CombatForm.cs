using AutoWuxia.Characters;
using AutoWuxia.Combat;
using AutoWuxia.Core;

namespace AutoWuxia.Forms;

public class CombatForm : Form
{
    private readonly CombatEngine _combat;
    private readonly GameEngine _engine;

    private RichTextBox _logBox = null!;
    private FlowLayoutPanel _skillPanel = null!;
    private Label _skillLabel = null!;
    private Label _playerHpLabel = null!;
    private Label _opponentHpLabel = null!;
    private Label _roundLabel = null!;
    private ProgressBar _playerHpBar = null!;
    private ProgressBar _opponentHpBar = null!;
    private ProgressBar _playerChargeBar = null!;
    private ProgressBar _opponentChargeBar = null!;
    private Label _playerChargeLabel = null!;
    private Label _opponentChargeLabel = null!;

    public CombatForm(CombatEngine combat, GameEngine engine)
    {
        _combat = combat;
        _engine = engine;
        InitializeComponent();
        RefreshStatus();
        RefreshSkills();
        AppendLog($"═══ {_combat.GetActionOrderDisplay()} ═══");
    }

    private void InitializeComponent()
    {
        Text = $"{(_combat.IsSpar ? "切磋" : "战斗")}：{_combat.Player.Name} VS {_combat.Opponent.Name}";
        Size = new Size(750, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(20, 20, 30);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // 顶部信息区
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            BackColor = Color.FromArgb(30, 30, 45),
            Padding = new Padding(10)
        };

        _roundLabel = new Label
        {
            Text = "第 0 回合",
            Location = new Point(330, 5),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(11f, FontStyle.Bold)
        };

        // 玩家状态
        var playerPanel = CreateCharacterPanel(_combat.Player, "你", true);
        playerPanel.Location = new Point(100, 25);

        _playerHpBar = CreateHpBar(new Point(100, 50), Color.FromArgb(80, 200, 80));
        _playerHpLabel = CreateHpLabel(new Point(100, 55));

        // 玩家读条(速度条)
        _playerChargeBar = CreateChargeBar(new Point(100, 73));
        _playerChargeLabel = CreateChargeLabel(new Point(100, 73));

        // 对手状态
        var opponentPanel = CreateCharacterPanel(_combat.Opponent, _combat.Opponent.Name, false);
        opponentPanel.Location = new Point(400, 25);

        _opponentHpBar = CreateHpBar(new Point(400, 50), Color.FromArgb(200, 80, 80));
        _opponentHpLabel = CreateHpLabel(new Point(400, 55));

        // 对手读条
        _opponentChargeBar = CreateChargeBar(new Point(400, 73));
        _opponentChargeLabel = CreateChargeLabel(new Point(400, 73));

        // 双方头像(血条旁)
        var playerPortrait = new PictureBox
        {
            Location = new Point(10, 25),
            Size = new Size(80, 80),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.FromArgb(40, 40, 60)
        };
        playerPortrait.Image = PortraitHelper.GetPortraitOrDefault(
            (_combat.Player as Player)?.PortraitPath, _combat.Player.Name, 80);

        var opponentPortrait = new PictureBox
        {
            Location = new Point(650, 25),
            Size = new Size(80, 80),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.FromArgb(40, 40, 60)
        };
        opponentPortrait.Image = PortraitHelper.GetPortraitOrDefault(
            (_combat.Opponent as NPC)?.PortraitPath, _combat.Opponent.Name, 80);

        topPanel.Controls.AddRange(new Control[] {
            _roundLabel, playerPortrait, playerPanel, _playerHpBar, _playerHpLabel,
            _playerChargeBar, _playerChargeLabel,
            opponentPortrait, opponentPanel, _opponentHpBar, _opponentHpLabel,
            _opponentChargeBar, _opponentChargeLabel
        });

        // 战斗日志
        _logBox = new RichTextBox
        {
            Location = new Point(10, 140),
            Size = new Size(715, 310),
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 15, 25),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        // 技能面板
        _skillLabel = new Label
        {
            Text = "选择技能：",
            Location = new Point(10, 458),
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 180, 200),
            Font = WuxiaTheme.UiFont(9f, FontStyle.Bold)
        };

        _skillPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 480),
            Size = new Size(715, 120),  // 高度由 LayoutSkillArea 调整
            BackColor = Color.FromArgb(25, 25, 40),
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(5),
            AutoScroll = true
        };

        Controls.AddRange(new Control[] { topPanel, _logBox, _skillLabel, _skillPanel });

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
    }

    private Panel CreateCharacterPanel(CharacterBase character, string displayName, bool isPlayer)
    {
        var panel = new Panel { Size = new Size(240, 20) };
        var color = isPlayer ? Color.FromArgb(100, 255, 100) : Color.FromArgb(255, 100, 100);

        panel.Controls.Add(new Label
        {
            Text = displayName,
            Location = new Point(0, 0),
            AutoSize = true,
            ForeColor = color,
            Font = WuxiaTheme.UiFont(11f, FontStyle.Bold)
        });

        panel.Controls.Add(new Label
        {
            Text = $"攻:{character.GetTotalAttack()} 防:{character.GetTotalDefense()} 速:{character.GetTotalSpeed()}",
            Location = new Point(80, 2),
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 180, 200),
            Font = WuxiaTheme.UiFont(8.5f)
        });

        return panel;
    }

    private ProgressBar CreateHpBar(Point loc, Color color)
    {
        return new ProgressBar
        {
            Location = loc,
            Size = new Size(240, 16),
            Minimum = 0,
            Maximum = 100,
            Value = 100,
            Style = ProgressBarStyle.Continuous
        };
    }

    private Label CreateHpLabel(Point loc)
    {
        return new Label
        {
            Text = "",
            Location = loc,
            Size = new Size(240, 16),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font = WuxiaTheme.UiFont(8f),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private ProgressBar CreateChargeBar(Point loc)
    {
        return new ProgressBar
        {
            Location = loc,
            Size = new Size(240, 14),
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
            Style = ProgressBarStyle.Continuous,
            ForeColor = Color.FromArgb(100, 160, 255),
            BackColor = Color.FromArgb(20, 20, 35)
        };
    }

    private Label CreateChargeLabel(Point loc)
    {
        return new Label
        {
            Text = "读条:0/1000",
            Location = loc,
            Size = new Size(240, 14),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(180, 210, 255),
            Font = WuxiaTheme.UiFont(7.5f),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private void RefreshStatus()
    {
        var player = _combat.Player;
        var opponent = _combat.Opponent;

        int playerHpPct = (int)((double)player.CurrentHP / player.GetTotalMaxHP() * 100);
        int opponentHpPct = (int)((double)opponent.CurrentHP / opponent.GetTotalMaxHP() * 100);

        _playerHpBar.Value = Math.Clamp(playerHpPct, 0, 100);
        _playerHpLabel.Text = $"HP:{player.CurrentHP}/{player.GetTotalMaxHP()}  MP:{player.CurrentMP}/{player.GetTotalMaxMP()}";
        _playerHpLabel.BringToFront();

        _opponentHpBar.Value = Math.Clamp(opponentHpPct, 0, 100);
        _opponentHpLabel.Text = $"HP:{opponent.CurrentHP}/{opponent.GetTotalMaxHP()}  MP:{opponent.CurrentMP}/{opponent.GetTotalMaxMP()}";
        _opponentHpLabel.BringToFront();

        // 读条进度同步
        int pCharge = (int)Math.Clamp(_combat.PlayerCharge, 0, 1000);
        int oCharge = (int)Math.Clamp(_combat.OpponentCharge, 0, 1000);
        _playerChargeBar.Value = pCharge;
        _opponentChargeBar.Value = oCharge;
        _playerChargeLabel.Text = $"读条:{pCharge}/1000";
        _opponentChargeLabel.Text = $"读条:{oCharge}/1000";
        _playerChargeLabel.BringToFront();
        _opponentChargeLabel.BringToFront();

        _roundLabel.Text = $"第 {_combat.Round} 回合";
    }

    private void RefreshSkills()
    {
        _skillPanel.Controls.Clear();
        var skills = _combat.GetPlayerSkills();

        for (int i = 0; i < skills.Count; i++)
        {
            int idx = i;
            var skill = skills[i];

            var btn = new Button
            {
                Text = skill.DisplayText,
                Size = WuxiaTheme.S(155, 42),
                Enabled = skill.IsAvailable,
                BackColor = skill.IsAvailable ? Color.FromArgb(55, 55, 75) : Color.FromArgb(35, 35, 50),
                ForeColor = skill.IsAvailable ? Color.FromArgb(220, 220, 200) : Color.FromArgb(100, 100, 120),
                FlatStyle = FlatStyle.Flat,
                Font = WuxiaTheme.UiFont(9f),
                Margin = new Padding(WuxiaTheme.V(3)),
                Cursor = skill.IsAvailable ? Cursors.Hand : Cursors.Default
            };

            if (skill.IsFlee)
                btn.BackColor = Color.FromArgb(100, 60, 60);
            else if (skill.IsInternal)
                btn.BackColor = skill.IsAvailable ? Color.FromArgb(60, 80, 60) : Color.FromArgb(35, 45, 35);

            if (skill.IsAvailable)
            {
                btn.Click += async (s, e) => await ExecuteRound(idx);
            }

            _skillPanel.Controls.Add(btn);
        }
        LayoutSkillArea(skills.Count);
    }

    /// <summary>按技能数量自适应技能面板高度,并让日志区让出空间(缩放感知).</summary>
    private void LayoutSkillArea(int skillCount)
    {
        double s = WuxiaTheme.Scale;
        int btnW = (int)Math.Round(155 * s), btnH = (int)Math.Round(42 * s);
        int margin = (int)Math.Round(6 * s), pad = (int)Math.Round(5 * s);
        int innerW = _skillPanel.Width - 2 * pad;
        int perRow = Math.Max(1, innerW / (btnW + margin));
        int rows = Math.Max(1, (int)Math.Ceiling(skillCount / (double)perRow));
        int panelH = rows * (btnH + margin) + 2 * pad;
        int panelBottom = ClientSize.Height - (int)Math.Round(10 * s);
        _skillPanel.Height = panelH;
        _skillPanel.Top = panelBottom - panelH;
        _skillLabel.Top = _skillPanel.Top - (int)Math.Round(22 * s);
        _logBox.Height = _skillPanel.Top - _logBox.Top - (int)Math.Round(8 * s);
    }

    private async Task ExecuteRound(int skillIndex)
    {
        _skillPanel.Enabled = false;

        var logs = _engine.ExecuteCombatRound(skillIndex);
        if (logs != null)
        {
            foreach (var log in logs)
                AppendLog(log);
        }

        RefreshStatus();

        if (_combat.IsCombatOver)
        {
            AppendLog("═══ 战斗结束 ═══");
            _skillPanel.Enabled = false;

            await Task.Delay(1500);

            // NPC 胜利时由 AI 决策处置(杀/赎金/羞辱 + debuff),处置日志同步显示在战斗窗
            if (!string.IsNullOrEmpty(_engine.PendingNPCVictoryOpponentId))
            {
                AppendLog("……对方正在决定你的生死……");
                void OnTail(string msg)
                {
                    if (IsDisposed) return;
                    try
                    {
                        if (InvokeRequired) Invoke(() => AppendLog(msg));
                        else AppendLog(msg);
                    }
                    catch { }
                }
                _engine.OnLog += OnTail;
                try { await _engine.ProcessNPCVictoryAsync(); }
                finally { _engine.OnLog -= OnTail; }
                await Task.Delay(1200);
            }

            Close();
            return;
        }

        RefreshSkills();
        _skillPanel.Enabled = true;
    }

    private void AppendLog(string text)
    {
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;

        if (text.Contains("═══"))
            _logBox.SelectionColor = Color.FromArgb(255, 220, 150);
        else if (text.Contains("HP:") && text.Contains("攻击"))
            _logBox.SelectionColor = Color.FromArgb(255, 120, 120);
        else if (text.Contains("防御") || text.Contains("运起"))
            _logBox.SelectionColor = Color.FromArgb(120, 200, 120);
        else
            _logBox.SelectionColor = Color.FromArgb(220, 220, 200);

        _logBox.AppendText(text + Environment.NewLine);
        _logBox.ScrollToCaret();
    }
}
