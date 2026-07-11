using AutoWuxia.Characters;
using AutoWuxia.Combat;
using AutoWuxia.Core;
using AutoWuxia.Forms.Controls;
using AutoWuxia.Quests;
using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

/// <summary>
/// 副本战斗窗口 - 多轮战斗,胜利触发 AI 战后对话.
/// 不通过 GameEngine.StartCombat,直接驱动 DungeonRunner 内部的 CombatEngine.
/// 界面按切磋(CombatForm)深色风格重做:头像/读条/攻防速面板/自适应技能框.
/// </summary>
public class DungeonForm : Form
{
    private readonly DungeonRunner _runner;
    private readonly GameEngine _engine;

    private TextLog _logBox = null!;
    private FlowLayoutPanel _skillPanel = null!;
    private Label _progressLabel = null!;
    private Label _playerHpLabel = null!;
    private Label _opponentHpLabel = null!;
    private ProgressBar _playerHpBar = null!;
    private ProgressBar _opponentHpBar = null!;
    private ProgressBar _playerChargeBar = null!;
    private ProgressBar _opponentChargeBar = null!;
    private Label _playerChargeLabel = null!;
    private Label _opponentChargeLabel = null!;
    private PictureBox _playerPortrait = null!;
    private PictureBox _opponentPortrait = null!;
    private Panel _playerPanel = null!;
    private Panel _opponentPanel = null!;
    private Button _continueBtn = null!;
    private Button _surrenderBtn = null!;

    /// <summary>副本结束后的最终结果(对外暴露给调用方)</summary>
    public DungeonOutcome FinalOutcome { get; private set; } = DungeonOutcome.Running;
    public string FinalRewardSummary { get; private set; } = "";
    public string FinalDefeatMessage { get; private set; } = "";

    public DungeonForm(DungeonRunner runner, GameEngine engine)
    {
        _runner = runner;
        _engine = engine;
        InitializeComponent();
        _runner.Start();
        // 应用进入副本的体力/时间消耗
        _engine.State.Player.ConsumeStamina(_runner.Dungeon.StaminaCost);
        _engine.State.GameTime.Advance(_runner.Dungeon.TimeCostHours / 2.0);

        AppendLog($"═══ 进入副本【{_runner.Dungeon.Name}】 ═══");
        AppendLog(_runner.Dungeon.Description);
        AppendLog($"消耗体力 {_runner.Dungeon.StaminaCost},耗时 {_runner.Dungeon.TimeCostHours} 时辰。");
        StartNextBattle();

        // 华山论剑副本播放"铸剑山庄"BGM(循环),关闭时停止(场景无默认BGM,恢复静默)
        if (_runner.Dungeon.Id == "huashan_lunjian")
        {
            var musicDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music");
            var lunjianBgm = Path.Combine(musicDir, "铸剑山庄 - 蔡志展.mp3");
            Shown += (_, _) => AudioManager.Instance.PlayMusic(lunjianBgm, true);
            FormClosed += (_, _) => AudioManager.Instance.StopMusic();
        }
    }

    private void InitializeComponent()
    {
        Text = $"副本：{_runner.Dungeon.Name}";
        Size = new Size(780, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(20, 20, 30);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // 顶部信息区(深色)
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            BackColor = Color.FromArgb(30, 30, 45),
            Padding = new Padding(10)
        };

        _progressLabel = new Label
        {
            Text = "进度:",
            Location = new Point(330, 5),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(11f, FontStyle.Bold)
        };

        // 双方头像
        _playerPortrait = new PictureBox
        {
            Location = new Point(10, 25),
            Size = new Size(80, 80),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.FromArgb(40, 40, 60)
        };
        _opponentPortrait = new PictureBox
        {
            Location = new Point(650, 25),
            Size = new Size(80, 80),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.FromArgb(40, 40, 60)
        };

        // 攻防速面板
        _playerPanel = CreateCharacterPanel(_runner.Player.Name, true);
        _playerPanel.Location = new Point(100, 25);
        _opponentPanel = CreateCharacterPanel("对手", false);
        _opponentPanel.Location = new Point(400, 25);

        // HP 血条 + 文字
        _playerHpBar = CreateHpBar(new Point(100, 50), Color.FromArgb(80, 200, 80));
        _playerHpLabel = CreateHpLabel(new Point(100, 55));
        _opponentHpBar = CreateHpBar(new Point(400, 50), Color.FromArgb(200, 80, 80));
        _opponentHpLabel = CreateHpLabel(new Point(400, 55));

        // 读条(速度条)
        _playerChargeBar = CreateChargeBar(new Point(100, 73));
        _playerChargeLabel = CreateChargeLabel(new Point(100, 73));
        _opponentChargeBar = CreateChargeBar(new Point(400, 73));
        _opponentChargeLabel = CreateChargeLabel(new Point(400, 73));

        topPanel.Controls.AddRange(new Control[]
        {
            _progressLabel,
            _playerPortrait, _opponentPortrait,
            _playerPanel, _opponentPanel,
            _playerHpBar, _playerHpLabel, _playerChargeBar, _playerChargeLabel,
            _opponentHpBar, _opponentHpLabel, _opponentChargeBar, _opponentChargeLabel
        });

        // 战斗日志
        _logBox = new TextLog
        {
            Location = new Point(10, 140),
            Size = new Size(745, 360),  // 高度由 LayoutSkillArea 调整
            ReadOnly = true
        };
        _logBox.BackColor = Color.FromArgb(15, 15, 25);
        _logBox.ForeColor = Color.FromArgb(220, 220, 200);

        // 技能面板(高度由 LayoutSkillArea 调整)
        _skillPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 488),
            Size = new Size(745, 50),
            BackColor = Color.FromArgb(25, 25, 40),
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(5),
            AutoScroll = true
        };

        // 底部按钮
        _continueBtn = new Button
        {
            Text = "继续下一场",
            Location = new Point(10, 551),
            Size = new Size(120, 32),
            Visible = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 55, 75),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(9f),
            Cursor = Cursors.Hand
        };
        _continueBtn.Click += (_, _) => StartNextBattle();

        _surrenderBtn = new Button
        {
            Text = "认输撤退",
            Location = new Point(140, 551),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(100, 60, 60),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(9f),
            Cursor = Cursors.Hand
        };
        _surrenderBtn.Click += (_, _) =>
        {
            // 当前战斗未结束: 在 CombatEngine 中执行 flee
            if (_runner.CurrentEngine != null && !_runner.CurrentEngine.IsCombatOver)
            {
                var skills = _runner.CurrentEngine.GetPlayerSkills();
                int fleeIdx = skills.FindIndex(s => s.IsFlee);
                if (fleeIdx >= 0) _ = ExecuteRound(fleeIdx);
                return;
            }
            // 已结束: 直接当作败退
            FinishDefeat();
        };

        Controls.AddRange(new Control[] { topPanel, _logBox, _skillPanel, _continueBtn, _surrenderBtn });

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
    }

    private Panel CreateCharacterPanel(string displayName, bool isPlayer)
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
            Text = "",
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
        int panelBottom = _surrenderBtn.Top - (int)Math.Round(8 * s);
        _skillPanel.Height = panelH;
        _skillPanel.Top = panelBottom - panelH;
        _logBox.Height = _skillPanel.Top - _logBox.Top - (int)Math.Round(8 * s);
    }

    private void StartNextBattle()
    {
        _continueBtn.Visible = false;
        _skillPanel.Visible = true;
        _skillPanel.Enabled = true;

        var engine = _runner.StartNextBattle();
        if (engine == null)
        {
            // 全胜
            FinishVictory();
            return;
        }
        var snap = _runner.GetSnapshot();
        if (_runner.IsHuashanLunjian)
            AppendLog($"═══ 华山论剑 · 第 {snap.OpponentIndex + 1} / {snap.OpponentTotalInRound} 位绝顶高手 ═══");
        else
            AppendLog($"═══ 第 {snap.RoundIndex + 1} 轮 / 共 {snap.RoundCount} 轮 - 第 {snap.OpponentIndex + 1} / {snap.OpponentTotalInRound} 名对手 ═══");
        AppendLog($"对手登场:{snap.Opponent?.Name}");
        RefreshStatus();
        RefreshSkills();
    }

    private void RefreshStatus()
    {
        var snap = _runner.GetSnapshot();
        _progressLabel.Text = snap.RoundIndex < 0
            ? "副本进度:准备中"
            : _runner.IsHuashanLunjian
                ? $"华山论剑 · 第 {snap.OpponentIndex + 1}/{snap.OpponentTotalInRound} 位绝顶高手"
                : $"副本进度:第 {snap.RoundIndex + 1}/{snap.RoundCount} 轮 · 对手 {snap.OpponentIndex + 1}/{snap.OpponentTotalInRound}";

        var p = _runner.Player;
        _playerPortrait.Image = PortraitHelper.GetPortraitOrDefault(p.PortraitPath, p.Name, 80);
        _playerPanel.Controls[0].Text = p.Name;
        _playerPanel.Controls[1].Text = $"攻:{p.GetTotalAttack()} 防:{p.GetTotalDefense()} 速:{p.GetTotalSpeed()}";
        _playerHpBar.Maximum = Math.Max(1, p.GetTotalMaxHP());
        _playerHpBar.Value = Math.Clamp(p.CurrentHP, 0, _playerHpBar.Maximum);
        _playerHpLabel.Text = $"HP:{p.CurrentHP}/{p.GetTotalMaxHP()}  MP:{p.CurrentMP}/{p.GetTotalMaxMP()}";
        _playerHpLabel.BringToFront();

        if (_runner.CurrentOpponent != null)
        {
            var o = _runner.CurrentOpponent;
            _opponentPortrait.Image = PortraitHelper.GetPortraitOrDefault(o.PortraitPath, o.Name, 80);
            _opponentPanel.Controls[0].Text = o.Name;
            _opponentPanel.Controls[1].Text = $"攻:{o.GetTotalAttack()} 防:{o.GetTotalDefense()} 速:{o.GetTotalSpeed()}";
            _opponentHpBar.Maximum = Math.Max(1, o.GetTotalMaxHP());
            _opponentHpBar.Value = Math.Clamp(o.CurrentHP, 0, _opponentHpBar.Maximum);
            _opponentHpLabel.Text = $"HP:{o.CurrentHP}/{o.GetTotalMaxHP()}  MP:{o.CurrentMP}/{o.GetTotalMaxMP()}";
            _opponentHpLabel.BringToFront();
        }

        // 读条进度同步
        var engine = _runner.CurrentEngine;
        if (engine != null)
        {
            int pCharge = (int)Math.Clamp(engine.PlayerCharge, 0, 1000);
            int oCharge = (int)Math.Clamp(engine.OpponentCharge, 0, 1000);
            _playerChargeBar.Value = pCharge;
            _opponentChargeBar.Value = oCharge;
            _playerChargeLabel.Text = $"读条:{pCharge}/1000";
            _opponentChargeLabel.Text = $"读条:{oCharge}/1000";
            _playerChargeLabel.BringToFront();
            _opponentChargeLabel.BringToFront();
        }
    }

    private void RefreshSkills()
    {
        _skillPanel.Controls.Clear();
        var engine = _runner.CurrentEngine;
        if (engine == null) { LayoutSkillArea(0); return; }
        var skills = engine.GetPlayerSkills();
        for (int i = 0; i < skills.Count; i++)
        {
            int idx = i;
            var s = skills[i];
            var btn = new Button
            {
                Text = s.DisplayText,
                Size = WuxiaTheme.S(155, 42),
                Margin = new Padding(WuxiaTheme.V(3)),
                Enabled = s.IsAvailable,
                BackColor = s.IsAvailable ? Color.FromArgb(55, 55, 75) : Color.FromArgb(35, 35, 50),
                ForeColor = s.IsAvailable ? Color.FromArgb(220, 220, 200) : Color.FromArgb(100, 100, 120),
                FlatStyle = FlatStyle.Flat,
                Font = WuxiaTheme.UiFont(9f),
                Cursor = s.IsAvailable ? Cursors.Hand : Cursors.Default
            };
            if (s.IsFlee)
                btn.BackColor = Color.FromArgb(100, 60, 60);
            else if (s.IsInternal)
                btn.BackColor = s.IsAvailable ? Color.FromArgb(60, 80, 60) : Color.FromArgb(35, 45, 35);

            btn.Click += async (_, _) => await ExecuteRound(idx);
            _skillPanel.Controls.Add(btn);
        }
        LayoutSkillArea(skills.Count);
    }

    private async Task ExecuteRound(int skillIndex)
    {
        var engine = _runner.CurrentEngine;
        if (engine == null) return;
        _skillPanel.Enabled = false;

        var logs = engine.ExecuteRound(skillIndex);
        foreach (var l in logs) AppendLog(l);
        RefreshStatus();

        if (engine.IsCombatOver)
        {
            _runner.OnCurrentBattleEnded();
            if (engine.Result.Outcome == CombatOutcome.PlayerWin)
            {
                AppendLog($"※ 击败 {_runner.CurrentOpponent?.Name}!");
                var snap = _runner.GetSnapshot();
                if (snap.TriggerDialogue)
                {
                    AppendLog("(对手喘息着,似有话要说...)");
                    var dialogue = await _runner.GetPostBattleDialogueAsync();
                    if (!string.IsNullOrWhiteSpace(dialogue))
                        AppendLog($"【{_runner.CurrentOpponent?.Name}】" + dialogue);
                }
                _skillPanel.Visible = false;
                _continueBtn.Visible = true;
            }
            else
            {
                FinishDefeat();
            }
        }
        else
        {
            RefreshSkills();
            _skillPanel.Enabled = true;
        }
    }

    private void FinishVictory()
    {
        FinalOutcome = DungeonOutcome.Victory;

        // 华山论剑:不弹通关框、不发奖励,由 EndingForm 收尾作传
        if (_runner.IsHuashanLunjian)
        {
            AppendLog("═══ 华山论剑 · 天下第一! ═══");
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        FinalRewardSummary = _runner.ApplyVictoryReward();
        AppendLog("═══ 副本通关! ═══");
        if (!string.IsNullOrEmpty(FinalRewardSummary))
            AppendLog($"获得奖励:{FinalRewardSummary}");
        MessageBox.Show(this, $"副本【{_runner.Dungeon.Name}】通关!\n\n奖励:{(string.IsNullOrEmpty(FinalRewardSummary) ? "无" : FinalRewardSummary)}",
            "副本完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void FinishDefeat()
    {
        FinalOutcome = DungeonOutcome.Defeat;
        var (gameOver, msg) = _runner.ApplyDefeatPenalty();
        FinalDefeatMessage = msg;
        AppendLog("═══ 副本失败 ═══");
        AppendLog(msg);
        MessageBox.Show(this, msg, "副本失败", MessageBoxButtons.OK,
            gameOver ? MessageBoxIcon.Error : MessageBoxIcon.Warning);
        DialogResult = gameOver ? DialogResult.Abort : DialogResult.Cancel;
        Close();
    }

    private void AppendLog(string text)
    {
        if (text.StartsWith("═")) _logBox.AppendSystem(text);
        else if (text.StartsWith("【")) _logBox.AppendDialogue(text);
        else if (text.StartsWith("※") || text.Contains("通关")) _logBox.AppendSuccess(text);
        else if (text.Contains("失败") || text.Contains("倒下")) _logBox.AppendWarning(text);
        else _logBox.AppendCombat(text);
    }
}
