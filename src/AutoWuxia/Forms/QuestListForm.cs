using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Forms.Controls;
using AutoWuxia.Items;
using AutoWuxia.Quests;

namespace AutoWuxia.Forms;

/// <summary>
/// 任务列表 Modal 窗口 - 三页签 (进行中 / 已完成 / 已结束)
/// 详情区显示任务描述/步骤/奖励, 底部根据状态展示对应动作按钮.
/// </summary>
public class QuestListForm : Form
{
    private readonly GameEngine _engine;

    private TabControl _tab = null!;
    private ListBox _listInProgress = null!;
    private ListBox _listCompleted = null!;
    private ListBox _listRewarded = null!;

    private TextLog _detailBox = null!;
    private FlowLayoutPanel _actionPanel = null!;
    private Label _emptyHint = null!;

    private QuestBase? _selected;

    /// <summary>关闭后是否返回首页(华山论剑通关后由结束画面透传)。</summary>
    public bool ReturnToStart { get; private set; }

    public QuestListForm(GameEngine engine)
    {
        _engine = engine;
        InitializeComponent();
        RefreshAll();
    }

    private void InitializeComponent()
    {
        Text = "任务列表";
        Size = new Size(720, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = WuxiaTheme.PanelBack;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = WuxiaTheme.UiFont(9.5f);

        _tab = new TabControl
        {
            Location = new Point(10, 10),
            Size = new Size(280, 480),
            Font = WuxiaTheme.UiFont(10f)
        };
        var pageA = new TabPage("进行中") { BackColor = WuxiaTheme.Surface };
        var pageB = new TabPage("已完成") { BackColor = WuxiaTheme.Surface };
        var pageC = new TabPage("已结束") { BackColor = WuxiaTheme.Surface };

        _listInProgress = MakeList();
        _listCompleted = MakeList();
        _listRewarded = MakeList();
        pageA.Controls.Add(_listInProgress);
        pageB.Controls.Add(_listCompleted);
        pageC.Controls.Add(_listRewarded);
        _tab.TabPages.Add(pageA);
        _tab.TabPages.Add(pageB);
        _tab.TabPages.Add(pageC);
        _tab.SelectedIndexChanged += (_, _) => OnSelectionChanged();

        // 右侧详情
        var detailLabel = new Label
        {
            Text = "任务详情",
            Location = new Point(300, 12),
            AutoSize = true,
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(11f, FontStyle.Bold)
        };
        _detailBox = new TextLog
        {
            Location = new Point(300, 38),
            Size = new Size(395, 380),
            ReadOnly = true
        };
        _emptyHint = new Label
        {
            Text = "(请从左侧选择一个任务)",
            Location = new Point(300, 200),
            Size = new Size(395, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = WuxiaTheme.TextDim,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Italic),
            Visible = true
        };

        _actionPanel = new FlowLayoutPanel
        {
            Location = new Point(300, 425),
            Size = new Size(395, 75),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = WuxiaTheme.SurfaceWarm,
            Padding = new Padding(4),
            AutoScroll = true
        };

        var closeBtn = new Button
        {
            Text = "关闭",
            Location = new Point(595, 510),
            Size = new Size(100, 32)
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { _tab, detailLabel, _detailBox, _emptyHint, _actionPanel, closeBtn });
        WuxiaTheme.ApplyScaling(this);
    }

    private ListBox MakeList()
    {
        var lb = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = WuxiaTheme.UiFont(9.5f)
        };
        WuxiaTheme.StyleListBox(lb);
        lb.SelectedIndexChanged += (_, _) => OnSelectionChanged();
        return lb;
    }

    private void RefreshAll()
    {
        var quests = _engine.State.Player.QuestLog;
        FillList(_listInProgress, quests.Where(q => q.Status == QuestStatus.InProgress).ToList());
        FillList(_listCompleted, quests.Where(q => q.Status == QuestStatus.Completed).ToList());
        FillList(_listRewarded, quests.Where(q => q.Status == QuestStatus.Rewarded || q.Status == QuestStatus.Failed).ToList());
        OnSelectionChanged();
    }

    private void FillList(ListBox lb, List<QuestBase> list)
    {
        lb.Items.Clear();
        if (list.Count == 0)
        {
            lb.Items.Add("(暂无任务)");
            lb.Enabled = false;
        }
        else
        {
            lb.Enabled = true;
            foreach (var q in list)
                lb.Items.Add(new QuestItem(q));
        }
    }

    private QuestBase? GetCurrentSelectedQuest()
    {
        var lb = _tab.SelectedIndex switch
        {
            0 => _listInProgress,
            1 => _listCompleted,
            2 => _listRewarded,
            _ => null
        };
        if (lb == null || lb.SelectedItem is not QuestItem item) return null;
        return item.Quest;
    }

    private void OnSelectionChanged()
    {
        _selected = GetCurrentSelectedQuest();
        _detailBox.Clear();
        _actionPanel.Controls.Clear();

        if (_selected == null)
        {
            _emptyHint.Visible = true;
            return;
        }
        _emptyHint.Visible = false;

        RenderDetail(_selected);
        RenderActions(_selected);
    }

    private void RenderDetail(QuestBase q)
    {
        _detailBox.AppendSystem($"═══ {q.Name} ═══");
        _detailBox.AppendText($"状态: {StatusText(q.Status)}");
        if (!string.IsNullOrEmpty(q.FactionId)) _detailBox.AppendText($"门派: {_engine.State.GetFactionName(q.FactionId, q.FactionId)}");
        if (!string.IsNullOrEmpty(q.IssuerNpcId))
        {
            var npcName = _engine.State.AllNPCs.TryGetValue(q.IssuerNpcId, out var npc) ? npc.Name : q.IssuerNpcId;
            _detailBox.AppendText($"委托人: {npcName}");
        }
        if (!string.IsNullOrEmpty(q.DungeonId)) _detailBox.AppendText($"关联副本: {q.DungeonId}");
        if (!string.IsNullOrEmpty(q.Difficulty) && q.Difficulty != "normal")
            _detailBox.AppendText($"难度: {DifficultyText(q.Difficulty)}");
        _detailBox.AppendText("");
        _detailBox.AppendText(q.Description);

        if (q.Steps.Count > 0)
        {
            _detailBox.AppendText("");
            _detailBox.AppendSystem("─── 任务步骤 ───");
            for (int i = 0; i < q.Steps.Count; i++)
            {
                var s = q.Steps[i];
                string mark = i < q.CurrentStepIndex ? "✓" : (i == q.CurrentStepIndex ? "▶" : "·");
                _detailBox.AppendText($"  {mark} {s.Description}");

                // 当前步骤显示推荐条件提示(防止玩家瞎猜去哪/找谁)
                if (i == q.CurrentStepIndex)
                {
                    string hint = BuildStepHint(s);
                    if (!string.IsNullOrEmpty(hint))
                        _detailBox.AppendText($"      ┗ 提示: {hint}");
                }

                // 显示节点奖励
                if (s.Reward != null)
                {
                    string rewardText = s.Reward.GetSummary(_engine.Config);
                    if (rewardText != "无奖励")
                        _detailBox.AppendText($"      ┗ 奖励: {rewardText}");
                }

                // 显示步骤级所需物品
                if (s.RequiredItems.Count > 0 && i == q.CurrentStepIndex)
                {
                    foreach (var r in s.RequiredItems)
                    {
                        string itemName = _engine.Config.Items.TryGetValue(r.ItemId, out var ic) ? ic.Name : r.ItemId;
                        _detailBox.AppendText($"      ┗ 需提交: {itemName} {r.Submitted}/{r.Quantity}");
                    }
                }
            }
        }

        if (q.RequiredItems.Count > 0)
        {
            _detailBox.AppendText("");
            _detailBox.AppendSystem("─── 所需物品 ───");
            foreach (var r in q.RequiredItems)
            {
                string itemName = _engine.Config.Items.TryGetValue(r.ItemId, out var ic) ? ic.Name : r.ItemId;
                _detailBox.AppendText($"  {itemName}: {r.Submitted}/{r.Quantity}");
            }
        }

        if (q.Reward != null)
        {
            _detailBox.AppendText("");
            _detailBox.AppendSystem("─── 任务奖励 ───");
            _detailBox.AppendSuccess(q.Reward.GetSummary(_engine.Config));
        }
    }

    private void RenderActions(QuestBase q)
    {
        if (q.Status == QuestStatus.InProgress)
        {
            // 链式任务步骤级物品提交
            if (q is ChainQuest cq && cq.CurrentStep?.RequiredItems.Count > 0)
            {
                var btn = MakeActionBtn("提交步骤物品", WuxiaTheme.Accent);
                btn.Click += (_, _) => OnSubmitStepItems(cq);
                _actionPanel.Controls.Add(btn);
            }
            // 提交物品 (有所需物品)
            else if (q.RequiredItems.Count > 0)
            {
                var btn = MakeActionBtn("提交物品", WuxiaTheme.Accent);
                btn.Click += (_, _) => OnSubmitItems(q);
                _actionPanel.Controls.Add(btn);
            }
            // 进入副本 / 处理(自动)
            if (!string.IsNullOrEmpty(q.DungeonId))
            {
                bool isBandit = q is FactionQuest fq && fq.SubType == "bandit";
                var btn = MakeActionBtn(isBandit ? "处理(自动)" : "进入副本", WuxiaTheme.Danger);
                btn.Click += (_, _) => OnEnterDungeon(q);
                _actionPanel.Controls.Add(btn);
            }
        }
        else if (q.Status == QuestStatus.Completed)
        {
            var btn = MakeActionBtn("领取奖励", WuxiaTheme.Success);
            btn.Click += (_, _) => OnClaimReward(q);
            _actionPanel.Controls.Add(btn);
        }
    }

    private Button MakeActionBtn(string text, Color color)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(120, 36),
            Margin = new Padding(3)
        };
        WuxiaTheme.StyleButton(btn, color);
        return btn;
    }

    // ── 动作 ──

    private void OnSubmitItems(QuestBase q)
    {
        var player = _engine.State.Player;
        // 校验持有
        var missing = new List<string>();
        foreach (var r in q.RequiredItems)
        {
            int need = r.Quantity - r.Submitted;
            if (need <= 0) continue;
            if (!player.Inventory.HasItem(r.ItemId, need))
            {
                string itemName = _engine.Config.Items.TryGetValue(r.ItemId, out var ic) ? ic.Name : r.ItemId;
                int has = player.Inventory.Items.Where(i => i.Id == r.ItemId).Sum(i => i.Quantity);
                missing.Add($"{itemName} 需 {need}, 现有 {has}");
            }
        }
        if (missing.Count > 0)
        {
            MessageBox.Show(this, "物品不足:\n" + string.Join("\n", missing), "提交失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var confirm = MessageBox.Show(this, "确认提交所需物品?", "提交物品", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (confirm != DialogResult.OK) return;

        NPC? issuer = null;
        if (!string.IsNullOrEmpty(q.IssuerNpcId))
            _engine.State.AllNPCs.TryGetValue(q.IssuerNpcId, out issuer);
        if (q.TrySubmitItems(player, issuer, out string msg))
        {
            EventSystem.Instance.Publish(GameEvents.QuestSubmitted,
                new Dictionary<string, object?> { { "questId", q.Id } });
            MessageBox.Show(this, msg + (q.Status == QuestStatus.Completed ? "\n任务已完成,可领取奖励!" : ""),
                "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshAll();
        }
        else
        {
            MessageBox.Show(this, msg, "提交失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSubmitStepItems(ChainQuest cq)
    {
        var player = _engine.State.Player;
        var step = cq.CurrentStep;
        if (step == null || step.RequiredItems.Count == 0) return;

        // 校验持有
        var missing = new List<string>();
        foreach (var r in step.RequiredItems)
        {
            int need = r.Quantity - r.Submitted;
            if (need <= 0) continue;
            if (!player.Inventory.HasItem(r.ItemId, need))
            {
                string itemName = _engine.Config.Items.TryGetValue(r.ItemId, out var ic) ? ic.Name : r.ItemId;
                int has = player.Inventory.Items.Where(i => i.Id == r.ItemId).Sum(i => i.Quantity);
                missing.Add($"{itemName} 需 {need}, 现有 {has}");
            }
        }
        if (missing.Count > 0)
        {
            MessageBox.Show(this, "物品不足:\n" + string.Join("\n", missing), "提交失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var confirm = MessageBox.Show(this, "确认提交当前步骤所需物品?", "提交步骤物品", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (confirm != DialogResult.OK) return;

        if (cq.TrySubmitStepItems(player, _engine.Config, out string msg))
        {
            string extra = cq.Status == QuestStatus.Completed ? "\n任务已完成,可领取奖励!" : "";
            MessageBox.Show(this, msg + extra, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshAll();
        }
        else
        {
            MessageBox.Show(this, msg, "提交失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnEnterDungeon(QuestBase q)
    {
        if (string.IsNullOrEmpty(q.DungeonId)) return;
        var player = _engine.State.Player;
        var isHuashan = q.DungeonId == "huashan_lunjian";

        // 体力检查
        if (_engine.Config.Dungeons.TryGetValue(q.DungeonId, out var dCfg))
        {
            if (player.Stamina < dCfg.StaminaCost)
            {
                MessageBox.Show(this, $"体力不足 (需要 {dCfg.StaminaCost}, 现有 {player.Stamina})",
                    "无法进入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        // 华山论剑为终章之战:败北即 Game Over,进入前确认(须在 CreateHuashanRunner 满血之前)
        if (isHuashan)
        {
            var confirm = MessageBox.Show(this,
                "华山论剑乃江湖终章之战。\n十位绝顶高手车轮鏖战,中途不回血,败北即游戏结束。\n\n是否登顶华山?",
                "华山论剑", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
        }

        var runner = isHuashan ? _engine.CreateHuashanRunner() : _engine.CreateDungeonRunner(q.DungeonId);
        if (runner == null)
        {
            MessageBox.Show(this, "副本配置未找到: " + q.DungeonId, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var form = new DungeonForm(runner, _engine);
        WuxiaTheme.ApplyScaling(form);
        var result = form.ShowDialog(this);

        if (form.FinalOutcome == DungeonOutcome.Victory)
        {
            // 任务推进到 Completed
            if (q.Status == QuestStatus.InProgress)
            {
                q.CurrentStepIndex = q.Steps.Count;
                q.Status = QuestStatus.Completed;
            }

            // 华山论剑通关 → 结束画面作传,可回首页
            if (isHuashan)
            {
                _engine.State.HuashanCompleted = true;
                using var ending = new EndingForm(_engine, runner.DefeatedOpponents);
                WuxiaTheme.ApplyScaling(ending);
                ending.ShowDialog(this);
                if (ending.ReturnToStart)
                {
                    ReturnToStart = true;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }
        }
        else if (form.FinalOutcome == DungeonOutcome.Defeat)
        {
            // 副本失败不直接 Failed 任务,玩家可重试
        }

        if (result == DialogResult.Abort)
        {
            // GameOver: 关闭整个窗口让 MainForm 处理
            DialogResult = DialogResult.Abort;
            Close();
            return;
        }
        RefreshAll();
    }

    private void OnClaimReward(QuestBase q)
    {
        if (q.ClaimReward(_engine.State.Player, _engine.Config, out string msg))
        {
            EventSystem.Instance.Publish(GameEvents.QuestRewarded,
                new Dictionary<string, object?> { { "questId", q.Id } });
            string summary = q.Reward?.GetSummary(_engine.Config) ?? "无";
            // 领奖时播放完成剧情对话(若有),播完再显示奖励
            StoryDialogueForm.Show(this, q.CompleteDialogue, _engine.State, () =>
            {
                MessageBox.Show(this, $"{msg}\n\n获得: {summary}", "领取成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            RefreshAll();
        }
        else
        {
            MessageBox.Show(this, msg, "领取失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string StatusText(QuestStatus s) => s switch
    {
        QuestStatus.InProgress => "进行中",
        QuestStatus.Completed => "已完成 (待领奖)",
        QuestStatus.Rewarded => "已结束",
        QuestStatus.Failed => "失败",
        _ => s.ToString()
    };

    private static string DifficultyText(string d) => d switch
    {
        "easy" => "简单",
        "medium" => "中等",
        "hard" => "高难",
        _ => d
    };

    /// <summary>构建当前步骤的推荐条件提示(目标地点/NPC + 武功/门派/善恶条件)。</summary>
    private string BuildStepHint(QuestStep step)
    {
        var parts = new List<string>();
        string? sceneName = null, npcName = null;
        if (!string.IsNullOrEmpty(step.TargetScene))
            sceneName = _engine.State.AllScenes.TryGetValue(step.TargetScene, out var sc) ? sc.Name : step.TargetScene;
        if (!string.IsNullOrEmpty(step.TargetNPC))
            npcName = _engine.State.AllNPCs.TryGetValue(step.TargetNPC, out var npc) ? npc.Name : step.TargetNPC;

        string target = step.ActionType switch
        {
            "go" => sceneName != null ? $"前往「{sceneName}」" : "",
            "talk" => npcName != null ? $"与「{npcName}」对话" : "",
            "fight" => npcName != null ? $"与「{npcName}」交手" : "",
            "spar" => npcName != null ? $"与「{npcName}」切磋" : "",
            "kill" => npcName != null ? $"击败「{npcName}」" : "",
            "mine" => sceneName != null ? $"在「{sceneName}」挖矿" : "挖矿",
            "meditate" => "面壁修炼",
            "dungeon" => "进入副本",
            _ => ""
        };
        if (!string.IsNullOrEmpty(target)) parts.Add(target);

        foreach (var (key, value) in step.Conditions)
        {
            string c = key switch
            {
                "minLevel" => $"需已学{value}门武功",
                "faction" => $"需加入{_engine.State.GetFactionName(value, value)}",
                "minKarma" => $"善恶≥{value}",
                "maxKarma" => $"善恶≤{value}",
                _ => ""
            };
            if (!string.IsNullOrEmpty(c)) parts.Add(c);
        }
        return string.Join("；", parts);
    }

    private class QuestItem
    {
        public QuestBase Quest { get; }
        public QuestItem(QuestBase q) { Quest = q; }
        public override string ToString()
        {
            string mark = Quest.Status switch
            {
                QuestStatus.Completed => "★",
                QuestStatus.Rewarded => "✓",
                QuestStatus.Failed => "✗",
                _ => "·"
            };
            return $"{mark} {Quest.Name}";
        }
    }
}
