using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Forms.Controls;
using AutoWuxia.Quests;

namespace AutoWuxia.Forms;

/// <summary>
/// 门派任务榜：玩家在执事NPC处可看到该门派可领取的任务，并选择接取。
/// 与 QuestListForm 不同：QuestListForm 显示玩家已接受的任务日志；
/// 本窗体显示 FactionQuestManager 中尚未被任何玩家接取的池子。
/// </summary>
public class FactionQuestBoardForm : Form
{
    private readonly GameEngine _engine;
    private readonly List<FactionQuest> _quests;
    private ListBox _listBox = null!;
    private TextLog _detailBox = null!;
    private Button _acceptBtn = null!;

    public FactionQuestBoardForm(GameEngine engine, string title, List<FactionQuest> quests)
    {
        _engine = engine;
        _quests = quests;
        Text = title;
        InitializeComponent();
        RefreshList();
    }

    private void InitializeComponent()
    {
        Size = new Size(720, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = WuxiaTheme.PanelBack;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = WuxiaTheme.UiFont(9.5f);

        var titleLabel = new Label
        {
            Text = Text,
            Font = WuxiaTheme.UiFont(13f, FontStyle.Bold),
            ForeColor = WuxiaTheme.AccentSoft,
            Location = new Point(15, 10),
            Size = new Size(680, 28)
        };

        _listBox = new ListBox
        {
            Location = new Point(10, 45),
            Size = new Size(280, 380),
            Font = WuxiaTheme.UiFont(10f)
        };
        WuxiaTheme.StyleListBox(_listBox);
        _listBox.SelectedIndexChanged += (_, _) => RenderDetail();

        var detailLabel = new Label
        {
            Text = "任务详情",
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold),
            ForeColor = WuxiaTheme.AccentSoft,
            Location = new Point(300, 45),
            Size = new Size(395, 22)
        };

        _detailBox = new TextLog
        {
            Location = new Point(300, 70),
            Size = new Size(395, 355),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = WuxiaTheme.SurfaceWarm,
            Font = WuxiaTheme.UiFont(9.5f)
        };

        _acceptBtn = new Button
        {
            Text = "接取任务",
            Location = new Point(300, 432),
            Size = new Size(140, 32),
            Enabled = false
        };
        WuxiaTheme.StyleButton(_acceptBtn, WuxiaTheme.Success);
        _acceptBtn.Click += (_, _) => OnAccept();

        var closeBtn = new Button
        {
            Text = "关闭",
            Location = new Point(595, 432),
            Size = new Size(100, 32)
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { titleLabel, _listBox, detailLabel, _detailBox, _acceptBtn, closeBtn });
        WuxiaTheme.ApplyScaling(this);
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        if (_quests.Count == 0)
        {
            _listBox.Items.Add("(暂无可领取任务)");
            _listBox.Enabled = false;
            _acceptBtn.Enabled = false;
            _detailBox.Clear();
            _detailBox.AppendText("近来江湖太平，暂无任务可接。");
            return;
        }
        _listBox.Enabled = true;
        foreach (var q in _quests)
        {
            string tag = q.SubType switch
            {
                "bandit" => "[剿匪]",
                "collect" => "[采办]",
                _ => "[门派]"
            };
            _listBox.Items.Add($"{tag} {q.Name}");
        }
        _listBox.SelectedIndex = 0;
    }

    private FactionQuest? GetSelected()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= _quests.Count) return null;
        return _quests[idx];
    }

    private void RenderDetail()
    {
        _detailBox.Clear();
        var q = GetSelected();
        if (q == null) { _acceptBtn.Enabled = false; return; }

        _detailBox.AppendSystem($"═══ {q.Name} ═══");
        if (!string.IsNullOrEmpty(q.FactionId))
            _detailBox.AppendText($"门派: {_engine.State.GetFactionName(q.FactionId, q.FactionId)}");
        if (!string.IsNullOrEmpty(q.IssuerNpcId))
        {
            var npcName = _engine.State.AllNPCs.TryGetValue(q.IssuerNpcId, out var n) ? n.Name : q.IssuerNpcId;
            _detailBox.AppendText($"委托人: {npcName}");
        }
        if (!string.IsNullOrEmpty(q.Difficulty) && q.Difficulty != "normal")
            _detailBox.AppendText($"难度: {DifficultyText(q.Difficulty)}");
        _detailBox.AppendText("");
        _detailBox.AppendText(q.Description);

        if (q.Steps.Count > 0)
        {
            _detailBox.AppendText("");
            _detailBox.AppendSystem("─── 任务步骤 ───");
            foreach (var s in q.Steps)
                _detailBox.AppendText($"  · {s.Description}");
        }

        if (q.RequiredItems.Count > 0)
        {
            _detailBox.AppendText("");
            _detailBox.AppendSystem("─── 所需物品 ───");
            foreach (var r in q.RequiredItems)
            {
                string itemName = _engine.Config.Items.TryGetValue(r.ItemId, out var ic) ? ic.Name : r.ItemId;
                _detailBox.AppendText($"  {itemName} x{r.Quantity}");
            }
        }

        if (q.Reward != null)
        {
            _detailBox.AppendText("");
            _detailBox.AppendSystem("─── 任务奖励 ───");
            _detailBox.AppendSuccess(q.Reward.GetSummary(_engine.Config));
        }

        // 已接取的不能再接
        bool already = _engine.State.Player.QuestLog.Any(x => x.Id == q.Id);
        if (already)
        {
            _detailBox.AppendText("");
            _detailBox.AppendWarning("（你已接取此任务）");
            _acceptBtn.Enabled = false;
        }
        else
        {
            _acceptBtn.Enabled = true;
        }
    }

    private void OnAccept()
    {
        var q = GetSelected();
        if (q == null) return;

        var player = _engine.State.Player;
        bool ok = _engine.FactionQuests.AcceptQuest(player, q.Id);
        if (!ok)
        {
            MessageBox.Show(this, "接取失败，可能已被其他人接走。", "接取任务",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _quests.Remove(q);
        MessageBox.Show(this, $"已接取任务【{q.Name}】，可在「任务列表」中查看进度。",
            "接取成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshList();
        if (_listBox.Items.Count > 0 && _listBox.Enabled) RenderDetail();
        else _detailBox.Clear();
    }

    private static string DifficultyText(string d) => d switch
    {
        "easy" => "简单",
        "medium" => "中等",
        "hard" => "高难",
        _ => d
    };
}
