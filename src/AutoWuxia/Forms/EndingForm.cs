using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Quests;
using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

/// <summary>
/// 华山论剑通关后的结束画面:玩家头像+属性摘要,AI 流式作传(打字机效果),
/// 收尾"感谢游玩",提供"回到首页"按钮。AI 未配置/失败时走降级模板文案。
/// </summary>
public class EndingForm : Form
{
    private readonly GameEngine _engine;
    private readonly IReadOnlyList<NPC> _defeated;

    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly PictureBox _portraitBox;
    private readonly Label _playerSummaryLabel;
    private readonly RichTextBox _storyBox;
    private readonly Label _statusLabel;
    private readonly Button _homeButton;

    private readonly CancellationTokenSource _cts = new();

    /// <summary>关闭后是否返回首页 StartForm(由调用方读取)。</summary>
    public bool ReturnToStart { get; private set; }

    public EndingForm(GameEngine engine, IReadOnlyList<NPC> defeatedOpponents)
    {
        _engine = engine;
        _defeated = defeatedOpponents;

        Text = "江湖终章 · 华山论剑";
        Size = new Size(780, 720);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = WuxiaTheme.UiFont(9.5f);
        BackColor = WuxiaTheme.AppBack;
        ForeColor = WuxiaTheme.Text;

        _titleLabel = new Label
        {
            Text = "江 湖 终 章",
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = WuxiaTheme.UiFont(20f, FontStyle.Bold),
            ForeColor = WuxiaTheme.AccentSoft
        };

        _subtitleLabel = new Label
        {
            Text = "华山之巅 · 力挫群雄 · 天下第一",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = WuxiaTheme.UiFont(11f),
            ForeColor = WuxiaTheme.TextMuted
        };

        var profilePanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = WuxiaTheme.PanelBackAlt,
            Padding = new Padding(12, 8, 12, 8)
        };

        _portraitBox = new PictureBox
        {
            Location = new Point(12, 10),
            Size = new Size(96, 96),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = WuxiaTheme.PanelBackAlt
        };

        _playerSummaryLabel = new Label
        {
            Location = new Point(120, 12),
            Size = new Size(630, 88),
            Font = WuxiaTheme.UiFont(10f),
            ForeColor = WuxiaTheme.Text,
            BackColor = WuxiaTheme.PanelBackAlt
        };
        profilePanel.Controls.AddRange(new Control[] { _portraitBox, _playerSummaryLabel });

        _statusLabel = new Label
        {
            Text = "说书人研墨提笔,为你作传……",
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = WuxiaTheme.UiFont(9.5f, FontStyle.Italic),
            ForeColor = WuxiaTheme.Accent
        };

        _storyBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(255, 250, 239),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(11f),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = WuxiaTheme.PanelBackAlt
        };
        _homeButton = new Button
        {
            Text = "回到首页",
            Size = new Size(160, 36),
            Enabled = false
        };
        WuxiaTheme.StyleButton(_homeButton, WuxiaTheme.Accent);
        _homeButton.Click += (_, _) =>
        {
            ReturnToStart = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        bottomPanel.Layout += (_, _) =>
            _homeButton.Location = new Point((bottomPanel.Width - _homeButton.Width) / 2,
                                             (bottomPanel.Height - _homeButton.Height) / 2);
        bottomPanel.Controls.Add(_homeButton);

        Controls.Add(_storyBox);
        Controls.Add(_statusLabel);
        Controls.Add(profilePanel);
        Controls.Add(_subtitleLabel);
        Controls.Add(_titleLabel);
        Controls.Add(bottomPanel);
        WuxiaTheme.ApplyScaling(this);

        LoadPlayerHeader();
        FormClosed += (_, _) => _cts.Cancel();

        // 结束曲(若文件存在)
        var endingBgm = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music", "沧海一声笑 (笛子) - 纯音乐.mp3");
        if (File.Exists(endingBgm))
        {
            Shown += (_, _) => AudioManager.Instance.PlayMusic(endingBgm, true);
            FormClosed += (_, _) => AudioManager.Instance.StopMusic();
        }
    }

    private void LoadPlayerHeader()
    {
        var p = _engine.State.Player;
        var img = PortraitHelper.GetPortraitOrDefault(p.PortraitPath, p.Name, 96);
        _portraitBox.Image = img;

        var faction = _engine.State.GetFactionName(p.FactionId);
        _playerSummaryLabel.Text =
            $"{p.Name}　|　阅历 Lv.{p.JianghuLevel}　|　{faction}\n" +
            $"善恶 {p.Karma}（{KarmaSystem.GetKarmaDescription(p.Karma)}）　|　声望 {p.Reputation}　|　金钱 {p.Gold} 两\n" +
            $"武学造诣:攻 {p.GetTotalAttack()} / 防 {p.GetTotalDefense()} / HP {p.GetTotalMaxHP()} / MP {p.GetTotalMaxMP()}\n" +
            $"江湖行走 {_engine.State.GameTime.YearDisplay} · 第 {_engine.State.GameTime.Day} 天";
    }

    /// <summary>窗体显示后启动流式总结。</summary>
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await StreamEpilogueAsync();
    }

    private async Task StreamEpilogueAsync()
    {
        var receivedAny = false;
        try
        {
            var (system, user) = BuildPrompts();
            await _engine.AI.ChatStreamAsync(system, user, chunk =>
            {
                receivedAny = true;
                AppendStory(chunk);
            }, _cts.Token);
        }
        catch (Exception ex)
        {
            GameLogger.Error("[结束画面] 流式总结异常", ex);
        }

        if (!receivedAny)
        {
            // AI 未配置 / 失败:走降级模板
            try { AppendStory(BuildFallbackSummary()); }
            catch (Exception ex) { GameLogger.Error("[结束画面] 降级文案异常", ex); }
        }

        // 收尾
        AppendStory("\n\n");
        AppendLineColor("═══ 感谢游玩 · 江湖路远,后会有期 ═══", WuxiaTheme.AccentSoft, bold: true);
        _statusLabel.Text = "传记已毕,江湖路远";
        _statusLabel.ForeColor = WuxiaTheme.Success;
        _homeButton.Enabled = true;
    }

    private (string system, string user) BuildPrompts()
    {
        var system =
            "你是江湖说书人,文笔古风典雅。玩家刚在华山之巅论剑,连胜十位绝顶高手,夺得天下第一。" +
            "请依据其生平经历与武学造诣,作一篇百字以上的江湖传记:评其为人、武学、恩怨情仇,收尾点出其江湖地位与传说。" +
            "直接输出传记正文,不要 JSON、不要标题前缀、不要分点列举,用流畅的古风散文。";

        var user = BuildPlayerDigest();
        return (system, user);
    }

    /// <summary>汇总玩家经历供 AI 作传(也用于降级文案的数据来源)。</summary>
    private string BuildPlayerDigest()
    {
        var p = _engine.State.Player;
        var sb = new StringBuilder();
        sb.AppendLine($"姓名:{p.Name}");
        if (!string.IsNullOrWhiteSpace(p.Description)) sb.AppendLine($"身世:{p.Description}");
        sb.AppendLine($"江湖阅历:第{p.JianghuLevel}级　声望:{p.Reputation}　善恶:{p.Karma}（{KarmaSystem.GetKarmaDescription(p.Karma)}）");
        sb.AppendLine($"门派:{_engine.State.GetFactionName(p.FactionId)}　天赋悟性:{p.Talent}　金钱:{p.Gold}两");
        sb.AppendLine($"武学:攻{p.GetTotalAttack()} 防{p.GetTotalDefense()} 气血{p.GetTotalMaxHP()} 内力{p.GetTotalMaxMP()}");

        // 已习武功
        var arts = p.LearnedArts.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (arts.Count > 0)
            sb.AppendLine("所学武学:" + string.Join("、", arts));

        // 长期标签(称号/重伤/阉人等)
        if (p.Tags.Count > 0)
            sb.AppendLine("印记:" + string.Join("、", p.Tags.Select(t => t.Name)));

        // 江湖时日
        sb.AppendLine($"闯荡江湖:{_engine.State.GameTime.YearDisplay} 第{_engine.State.GameTime.Day}天");

        // 重要关系
        var relGroups = p.Relations
            .Where(kv => kv.Value.Type != RelationType.Stranger)
            .GroupBy(kv => kv.Value.Type)
            .OrderByDescending(g => RelationWeight(g.Key));
        var relLines = new List<string>();
        foreach (var g in relGroups)
        {
            var names = g.Select(kv => ResolveName(kv.Key)).Where(n => n != null).ToList();
            if (names.Count > 0)
                relLines.Add($"{g.First().Value.GetRelationDescription()}{string.Join("、", names!)}");
        }
        if (relLines.Count > 0)
            sb.AppendLine("江湖羁绊:" + string.Join("; ", relLines));

        // 已完成任务数
        var doneCount = p.QuestLog.Count(q => q.Status == QuestStatus.Completed || q.Status == QuestStatus.Rewarded);
        sb.AppendLine($"已了结江湖事:{doneCount}件");

        // 华山连胜十人(末位为最终Boss)
        if (_defeated.Count > 0)
        {
            sb.AppendLine("华山论剑连胜高手(按出场顺序,末位为最终对手):");
            sb.AppendLine(string.Join(" → ", _defeated.Select(n => n.Name)));
        }

        return sb.ToString();
    }

    /// <summary>无 AI 时的固定文案(同样基于玩家数据)。</summary>
    private string BuildFallbackSummary()
    {
        var p = _engine.State.Player;
        var faction = _engine.State.GetFactionName(p.FactionId);
        var arts = p.LearnedArts.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Take(5).ToList();
        var artsText = arts.Count > 0 ? string.Join("、", arts) : "一身根基武学";
        var defeatedText = _defeated.Count > 0
            ? string.Join("、", _defeated.Select(n => n.Name))
            : "群雄";

        return
            $"江湖风雨数载,{p.Name}自{faction}而出,善恶{KarmaSystem.GetKarmaDescription(p.Karma)}," +
            $"声望赫赫,阅历第{p.JianghuLevel}级。所学{artsText},在江湖行走{_engine.State.GameTime.YearDisplay}" +
            $"第{_engine.State.GameTime.Day}天之际,登临华山之巅。\n\n" +
            $"论剑台上,十位绝顶高手次第而出,{p.Name}以一敌众,力挫{defeatedText},终成天下第一。\n\n" +
            $"自此,江湖再传其名,后世说书人提起,皆道一声:好一位{p.Name}!";
    }

    // ── UI 追加(线程安全) ──

    private void AppendStory(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (InvokeRequired) { BeginInvoke(() => AppendStory(text)); return; }
        _storyBox.SelectionStart = _storyBox.TextLength;
        _storyBox.SelectionLength = 0;
        _storyBox.SelectionColor = WuxiaTheme.Text;
        _storyBox.SelectionFont = WuxiaTheme.UiFont(11f);
        _storyBox.AppendText(text);
        _storyBox.ScrollToCaret();
    }

    private void AppendLineColor(string text, Color color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (InvokeRequired) { BeginInvoke(() => AppendLineColor(text, color, bold)); return; }
        _storyBox.SelectionStart = _storyBox.TextLength;
        _storyBox.SelectionLength = 0;
        _storyBox.SelectionColor = color;
        _storyBox.SelectionFont = WuxiaTheme.UiFont(11f, bold ? FontStyle.Bold : FontStyle.Regular);
        _storyBox.AppendText(text + "\n");
        _storyBox.ScrollToCaret();
    }

    // ── 辅助 ──

    private string? ResolveName(string characterId)
    {
        if (_engine.Config.Characters.TryGetValue(characterId, out var c)) return c.Name;
        if (_engine.State.AllNPCs.TryGetValue(characterId, out var n)) return n.Name;
        return null;
    }

    private static int RelationWeight(RelationType t) => t switch
    {
        RelationType.Spouse => 9,
        RelationType.Master => 8,
        RelationType.SwornBrother => 7,
        RelationType.Disciple => 6,
        RelationType.CloseFriend => 5,
        RelationType.Enemy => 4,
        RelationType.Rival => 3,
        RelationType.Friend => 2,
        _ => 1
    };
}
