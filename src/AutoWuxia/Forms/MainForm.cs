using AutoWuxia.Characters;
using AutoWuxia.Combat;
using AutoWuxia.Config;
using AutoWuxia.Core;
using AutoWuxia.Items;
using AutoWuxia.MartialArts;
using AutoWuxia.Quests;
using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

/// <summary>
/// 带稀有度颜色的列表项
/// </summary>
public class RarityListItem
{
    public string Text { get; set; } = "";
    public Color RarityColor { get; set; } = Color.Gray;
    public string RarityName { get; set; } = "普通";

    public RarityListItem(string text, Color rarityColor, string rarityName)
    {
        Text = text;
        RarityColor = rarityColor;
        RarityName = rarityName;
    }

    public override string ToString() => Text;

    /// <summary>
    /// 设置 ListBox 为 OwnerDraw 模式，自动绘制稀有度颜色边框
    /// </summary>
    public static void SetupOwnerDraw(ListBox listBox)
    {
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        listBox.ItemHeight = 24;
        listBox.DrawItem -= RarityDrawItem;
        listBox.DrawItem += RarityDrawItem;
    }

    private static void RarityDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox lb || e.Index < 0 || e.Index >= lb.Items.Count) return;

        e.DrawBackground();

        if (lb.Items[e.Index] is RarityListItem ri)
        {
            // 绘制左侧稀有度色条
            var colorBar = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 2, 4, e.Bounds.Height - 4);
            using var brush = new SolidBrush(ri.RarityColor);
            e.Graphics.FillRectangle(brush, colorBar);

            // 绘制文字（使用稀有度颜色）
            var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            var textColor = (e.State & DrawItemState.Selected) != 0 ? Color.White : ri.RarityColor;
            TextRenderer.DrawText(e.Graphics, ri.Text, lb.Font, textRect, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        else
        {
            // 普通项回退
            var text = lb.Items[e.Index]?.ToString() ?? "";
            var textColor = (e.State & DrawItemState.Selected) != 0 ? Color.White : lb.ForeColor;
            TextRenderer.DrawText(e.Graphics, text, lb.Font, e.Bounds, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        e.DrawFocusRectangle();
    }
}

public class MainForm : Form
{
    private GameEngine _engine = null!;
    private ConfigManager _config = null!;

    private Panel _topPanel = null!;
    private Panel _leftPanel = null!;
    private Panel _centerPanel = null!;
    private Panel _rightPanel = null!;
    private Controls.TextLog _logBox = null!;
    private Label _statusLabel = null!;
    private Label _timeLabel = null!;
    private FlowLayoutPanel _actionPanel = null!;
    private FlowLayoutPanel _npcActionPanel = null!;
    private FlowLayoutPanel _npcFlowPanel = null!;
    private string? _selectedNpcId;
    private ListBox _sceneListBox = null!;
    private Button _factionQuestBtn = null!;
    private TableLayoutPanel _rootLayout = null!;
    private TableLayoutPanel _contentLayout = null!;
    private FlowLayoutPanel _topButtonPanel = null!;
    private Image? _headerImage;
    private Label _sceneNameLabel = null!;
    private Label _sceneRegionLabel = null!;
    private Label _sceneDescriptionLabel = null!;
    private Image? _sceneBackgroundImage;
    private string? _displayedSceneImagePath;

    /// <summary>关闭后是否返回首页 StartForm(由 Program.Main 循环读取)。</summary>
    public bool ReturnToStart { get; private set; }

    public MainForm() : this(null, null) { }

    /// <summary>读档进入:loadSlot 不为 null 时启动后自动读档。</summary>
    public MainForm(int? loadSlot) : this(loadSlot, null) { }

    /// <summary>新游戏并使用角色创建结果:creation 不为 null 时应用到玩家。</summary>
    public MainForm(CharacterCreationData? creation) : this(null, creation) { }

    /// <summary>
    /// 统一构造:loadSlot 为读档槽位(与新游戏互斥),creation 为新游戏角色创建数据。
    /// </summary>
    public MainForm(int? loadSlot, CharacterCreationData? creation)
    {
        GameLogger.Info($"=== MainForm 启动 (loadSlot={loadSlot?.ToString() ?? "新游戏"}, creation={creation?.Talent.ToString() ?? "无"}) ===");

        // 进入游戏主界面:暂不为场景播放默认BGM(新手村等场景保持安静,后续按场景单独配乐)。
        // 停止标题/角色创建界面遗留的音乐,确保场景静默。
        Systems.AudioManager.Instance.StopMusic();

        InitializeGame();
        InitializeComponent();

        if (creation != null)
        {
            ApplyCreation(_engine.State.Player, creation);
        }

        if (loadSlot.HasValue)
        {
            try { _engine.LoadGame(loadSlot.Value); RefreshAll(); }
            catch (Exception ex) { GameLogger.Error("启动时读档失败", ex); }
        }
        else
        {
            RefreshAll();
        }
        GameLogger.Info("MainForm 初始化完成");
    }

    /// <summary>将角色创建结果(roll属性/技艺/天赋)应用到玩家,覆盖默认 player。</summary>
    private static void ApplyCreation(Characters.Player p, CharacterCreationData d)
    {
        var talent = d.Talent;
        p.Name = talent == TalentChoice.XiaoXiaMi ? "小虾米" : d.Name;
        p.PortraitPath = d.PortraitPath;
        p.MaxHP = d.MaxHP + (talent == TalentChoice.XiaoXiaMi ? 1000 : 0);
        p.CurrentHP = p.MaxHP;
        p.BaseAttack = d.BaseAttack + (talent == TalentChoice.XiaoXiaMi ? 20 : 0);
        p.BaseDefense = d.BaseDefense + (talent == TalentChoice.XiaoXiaMi ? 20 : 0);
        p.Speed = 50 + (talent == TalentChoice.QingGongDaShi ? 20 : 0);
        p.Gender = d.Gender;
        p.Talent = d.Wuxing;
        p.TrainingSpeedBonus = talent == TalentChoice.QinXueKuLian ? 0.5 : 0;

        p.CraftSkills.Clear();
        foreach (var (skillId, level) in d.CraftSkills)
            p.SetCraftSkill(skillId, level);

        GameLogger.Info($"角色创建应用: {p.Name} HP={p.MaxHP} ATK={p.BaseAttack} DEF={p.BaseDefense} SPD={p.Speed} 天赋={talent} 修炼加成={p.TrainingSpeedBonus}");
    }

    private void InitializeGame()
    {
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        GameLogger.Info($"数据目录: {dataPath}");
        GameLogger.Info($"数据目录存在: {Directory.Exists(dataPath)}");
        _config = new ConfigManager(dataPath);
        _engine = new GameEngine(_config);
        _engine.OnLog += msg =>
        {
            if (InvokeRequired) Invoke(() => _logBox?.AppendText(msg));
            else _logBox?.AppendText(msg);
        };
        GameLogger.Info("开始初始化游戏引擎...");
        _engine.Initialize();
        GameLogger.Info("游戏引擎初始化完成");

        // 订阅剧情事件:黑木崖之变
        Core.EventSystem.Instance.Subscribe("quest.riyue_choice", OnRiyueChoiceEvent);
    }

    private void OnRiyueChoiceEvent(object? sender, Core.GameEventArgs e)
    {
        if (InvokeRequired) Invoke(() => ShowRiyueChoiceDialog());
        else ShowRiyueChoiceDialog();
    }

    /// <summary>黑木崖之变:玩家两难抉择,帮东方不败或助任我行三人</summary>
    private void ShowRiyueChoiceDialog()
    {
        // 让任我行、向问天现身(令狐冲本来就在游戏内)
        RevealNpc("ren_woxing");
        RevealNpc("xiang_wentian");

        var result = WuxiaConfirmBox.Show(this,
            "黑木崖之变",
            "崖后狂风骤起!任我行携向问天、令狐冲三人破空而至,杀气腾腾。\n\n" +
            "东方教主厉声道:\"小子,你今日助我还是助他?!\"\n" +
            "任我行冷笑:\"东方不败篡我教主之位十二年!今日清算!\"\n\n" +
            "你将站在哪一边?",
            "助东方不败", "助任我行三人", WuxiaConfirmStyle.Danger);

        if (result)
            ExecuteRiyueChoiceDongfang();
        else
            ExecuteRiyueChoiceSanren();
    }

    /// <summary>分支1:帮东方不败,连战三人</summary>
    private void ExecuteRiyueChoiceDongfang()
    {
        _logBox.AppendSuccess("你毅然站到东方教主身旁!");
        var targets = new[] { "ren_woxing", "xiang_wentian", "linghu_chong" };
        foreach (var id in targets)
        {
            if (!_engine.State.AllNPCs.TryGetValue(id, out var foe) || !foe.IsAlive) continue;
            _logBox.AppendText($"———— 与 {foe.Name} 决战 ————");
            StartCombatWithNPC(foe, false);
            if (!foe.IsAlive) continue;       // 被杀,继续下一个
            // 玩家败/逃跑则中断
            _logBox.AppendWarning($"{foe.Name}未死,黑木崖之变中断。");
            return;
        }

        // 三人都被杀,推进任务并发奖励
        CompleteRiyueQuest();
        GrantRiyueRewardDongfang();
    }

    /// <summary>分支2:帮任我行三人,单战东方不败</summary>
    private void ExecuteRiyueChoiceSanren()
    {
        _logBox.AppendSuccess("你转身与任前辈并肩,共战东方教主!");
        if (!_engine.State.AllNPCs.TryGetValue("dongfang_bubai", out var df) || !df.IsAlive)
        {
            _logBox.AppendWarning("东方不败已不在,黑木崖之变中断。");
            return;
        }
        _logBox.AppendText("———— 与 东方不败 决战 ————");
        StartCombatWithNPC(df, false);
        if (df.IsAlive)
        {
            _logBox.AppendWarning("东方教主未死,黑木崖之变中断。");
            return;
        }

        // 东方不败被杀,推进任务并发奖励
        CompleteRiyueQuest();
        GrantRiyueRewardSanren();
    }

    /// <summary>强行推进黑木崖之变任务到完成态</summary>
    private void CompleteRiyueQuest()
    {
        var quest = _engine.State.Player.QuestLog.FirstOrDefault(q =>
            q.Id == "riyue_dongfang_test" && q.Status == Quests.QuestStatus.InProgress);
        if (quest == null) return;
        // 当前步骤就是 final_choice
        quest.CurrentStepIndex = quest.Steps.Count;
        quest.Status = Quests.QuestStatus.Completed;
        _logBox.AppendSuccess($"[{quest.Name}] 全部步骤完成,可领取奖励!");
    }

    /// <summary>帮东方不败奖励:吸星大法+1万金+东方不败好感40</summary>
    private void GrantRiyueRewardDongfang()
    {
        var player = _engine.State.Player;
        player.Gold += 10000;
        _logBox.AppendSuccess("东方教主朱唇轻启:\"难得你重情重义。\"");
        _logBox.AppendSuccess("获得银两 +10000");

        // 吸星大法秘籍
        var art = _config.CreateMartialArt("xixing_dafa");
        if (art != null && !player.LearnedArts.Any(a => a.Id == "xixing_dafa"))
        {
            player.LearnArt(art);
            _logBox.AppendSuccess($"东方教主传你【{art.Name}】!");
        }

        // 好感度+40
        if (_engine.State.AllNPCs.TryGetValue("dongfang_bubai", out var df))
        {
            df.GetRelation(player.Id).ChangeFavorability(40);
            _logBox.AppendSuccess($"{df.Name}对你好感+40");
        }
        RefreshAll();
    }

    /// <summary>帮任我行三人奖励:葵花宝典三本+三人好感各+40</summary>
    private void GrantRiyueRewardSanren()
    {
        var player = _engine.State.Player;
        _logBox.AppendSuccess("任我行抚掌大笑:\"少侠果然性情中人!\"");

        // 葵花宝典三本套
        foreach (var artId in new[] { "kuihua_baodian", "kuihua_xiuhuazhen", "kuihua_shenfa" })
        {
            if (player.LearnedArts.Any(a => a.Id == artId)) continue;
            var art = _config.CreateMartialArt(artId);
            if (art == null) continue;
            player.LearnArt(art);
            _logBox.AppendSuccess($"获得武功【{art.Name}】!");
        }

        // 三人好感+40
        foreach (var id in new[] { "ren_woxing", "xiang_wentian", "linghu_chong" })
        {
            if (_engine.State.AllNPCs.TryGetValue(id, out var npc) && npc.IsAlive)
            {
                npc.GetRelation(player.Id).ChangeFavorability(40);
                _logBox.AppendSuccess($"{npc.Name}对你好感+40");
            }
        }
        RefreshAll();
    }

    private void InitializeComponent()
    {
        Text = "金庸群侠传-AI";
        Size = new Size(1280, 850);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WuxiaTheme.AppBack;
        MinimumSize = new Size(1000, 700);
        DoubleBuffered = true;
        FormClosed += (_, _) =>
        {
            _contentLayout.BackgroundImage = null;
            _sceneBackgroundImage?.Dispose();
            _headerImage?.Dispose();
        };

        BuildTopPanel();
        BuildLeftPanel();
        BuildRightPanel();
        BuildCenterPanel();

        // 关键: 按Dock顺序添加 - Top/Left/Right先添加, Fill最后添加
        Controls.Add(_topPanel);
        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WuxiaTheme.AppBack,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245f));
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245f));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108f));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _rootLayout.Controls.Add(_topPanel, 0, 0);
        _rootLayout.SetColumnSpan(_topPanel, 3);

        _contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(48, 39, 29),
            BackgroundImageLayout = ImageLayout.Stretch,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245f));
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245f));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _contentLayout.Controls.Add(_leftPanel, 0, 0);
        _contentLayout.Controls.Add(_centerPanel, 1, 0);
        _contentLayout.Controls.Add(_rightPanel, 2, 0);

        _rootLayout.Controls.Add(_contentLayout, 0, 1);
        _rootLayout.SetColumnSpan(_contentLayout, 3);

        Controls.Add(_rootLayout);

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
        RefreshAll();
    }

    // ── UI构建 ──

    private void BuildTopPanel()
    {
        _topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = WuxiaTheme.PanelBack,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _headerImage = LoadHeaderImage();
        if (_headerImage != null)
        {
            var banner = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = _headerImage,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Enabled = false
            };
            _topPanel.Controls.Add(banner);
            banner.SendToBack();
        }

        var topRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.Transparent
        };

        _timeLabel = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(13f, FontStyle.Bold),
            Location = new Point(18, 16)
        };
        topRow.Controls.Add(_timeLabel);

        var statusRow = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = WuxiaTheme.PanelBackAlt
        };
        _statusLabel = new Label
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            Location = new Point(18, 8),
            Size = new Size(1220, 22),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
        };
        statusRow.Controls.Add(_statusLabel);

        _topButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 560,
            Height = 72,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 15, 8, 0)
        };
        topRow.Controls.Add(_topButtonPanel);

        AddTopButton("存档", (_, _) => ShowSaveDialog());
        AddTopButton("读档", (_, _) => ShowLoadDialog());
        AddTopButton("设置", (_, _) =>
        {
            using var sf = new SettingsForm(_engine.AI);
            sf.ShowDialog();
            if (sf.ScaleChanged)
                MessageBox.Show(this, "界面缩放已保存,重启游戏后主界面生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        AddTopButton("武学装备", (_, _) => OpenMartialEquipForm());
        AddTopButton("地图", (_, _) => ShowMapInfo());
        AddTopButton("返回首页", (_, _) => ReturnToHomePage());

        _topPanel.Controls.AddRange(new Control[] { topRow, statusRow });
        topRow.BringToFront();
        statusRow.BringToFront();
    }

    private void AddTopButton(string text, EventHandler click)
    {
        // 中文按钮长度自适应:超过2字加宽
        int width = text.Length >= 4 ? 92 : 72;
        var btn = MakeButton(text, Point.Empty, new Size(width, 34), WuxiaTheme.SurfaceWarm);
        btn.Margin = new Padding(3, 0, 3, 0);
        btn.Click += click;
        _topButtonPanel.Controls.Add(btn);
    }

    /// <summary>返回首页:询问是否存档,设置 ReturnToStart 后关闭窗体,由 Program.Main 循环重新显示 StartForm。</summary>
    private void ReturnToHomePage()
    {
        var r = MessageBox.Show(this,
            "返回首页前是否保存当前进度？\n\n" +
            "  是   = 先存档再返回首页\n" +
            "  否   = 直接返回（未保存进度将丢失）\n" +
            "  取消 = 留在游戏",
            "返回首页", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (r == DialogResult.Cancel) return;

        if (r == DialogResult.Yes)
        {
            // 存档对话框为模态,关闭后继续返回流程
            ShowSaveDialog();
        }

        ReturnToStart = true;
        Close();
    }

    /// <summary>打开武学装备窗口(外功多选/内功+轻功单选)</summary>
    private void OpenMartialEquipForm()
    {
        using var form = new MartialEquipForm(_engine.State.Player);
        WuxiaTheme.ApplyScaling(form);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _logBox.AppendSuccess("武学装备已更新。");
            RefreshAll();
        }
    }

    private void BuildLeftPanel()
    {
        _leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 8, 0)
        };

        AddSectionLabel(_leftPanel, "【可去的地方】", 5);

        _sceneListBox = MakeListBox(new Point(10, 36), new Size(225, 220));
        // 双击即前往
        _sceneListBox.DoubleClick += (_, _) => TravelToSelectedScene();
        _leftPanel.Controls.Add(_sceneListBox);

        var goBtn = MakeButton("前往", new Point(10, 270), new Size(225, 34));
        goBtn.Click += (_, _) => TravelToSelectedScene();
        _leftPanel.Controls.Add(goBtn);

        var restBtn = MakeButton("休息（过一天）", new Point(10, 312), new Size(225, 34));
        restBtn.Click += (_, _) => { _engine.Rest(); RefreshAll(); };
        _leftPanel.Controls.Add(restBtn);

        var rest6Btn = MakeButton("休息6个时辰", new Point(10, 348), new Size(225, 30));
        rest6Btn.Click += (_, _) => { _engine.Rest6ShiChen(); RefreshAll(); };
        _leftPanel.Controls.Add(rest6Btn);

        var rest3Btn = MakeButton("休息3天", new Point(10, 381), new Size(225, 30));
        rest3Btn.Click += (_, _) => { _engine.Rest3Days(); RefreshAll(); };
        _leftPanel.Controls.Add(rest3Btn);

        AddSectionLabel(_leftPanel, "【任务】", 415);
        var questHint = new Label
        {
            Text = "(右下「任务列表」可查看)",
            Location = new Point(10, 448),
            Size = new Size(225, 24),
            ForeColor = WuxiaTheme.TextDim,
            Font = WuxiaTheme.UiFont(9f, FontStyle.Italic)
        };
        _leftPanel.Controls.Add(questHint);
    }

    private void BuildRightPanel()
    {
        _rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(10),
            Margin = new Padding(8, 0, 0, 0)
        };

        AddSectionLabel(_rightPanel, "【当前场景人物】", 5);

        _npcFlowPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 36),
            Size = new Size(225, 200),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            AutoScroll = true,
            Padding = new Padding(2)
        };
        _rightPanel.Controls.Add(_npcFlowPanel);

        // NPC操作按钮面板（动态显示）
        _npcActionPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 246),
            Size = new Size(225, 205),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            AutoScroll = true
        };
        _rightPanel.Controls.Add(_npcActionPanel);

        var playerBtn = MakeButton("我的信息", new Point(10, 466), new Size(110, 34));
        playerBtn.Click += (_, _) => ShowPlayerInfo();
        _rightPanel.Controls.Add(playerBtn);

        var questBtn = MakeButton("任务列表", new Point(125, 466), new Size(110, 34));
        questBtn.Click += (_, _) => ShowQuestList();
        _rightPanel.Controls.Add(questBtn);

        var factionQuestBtn = MakeButton("门派任务榜", new Point(10, 505), new Size(225, 34), WuxiaTheme.Accent);
        factionQuestBtn.Click += (_, _) => ShowPlayerFactionQuestBoard();
        factionQuestBtn.Visible = false;
        _factionQuestBtn = factionQuestBtn;
        _rightPanel.Controls.Add(factionQuestBtn);
    }

    private void BuildCenterPanel()
    {
        _centerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 0, 10, 10),
            Margin = new Padding(0)
        };

        _logBox = new Controls.TextLog
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle
        };

        _actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 64,
            BackColor = WuxiaTheme.PanelBackAlt,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 10, 8, 8),
            AutoScroll = true
        };

        var sceneInfoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            BackColor = WuxiaTheme.PanelBack,
            Margin = new Padding(0, 0, 0, 8)
        };
        _sceneNameLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 8),
            BackColor = Color.Transparent,
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(14f, FontStyle.Bold)
        };
        _sceneRegionLabel = new Label
        {
            AutoSize = true,
            Location = new Point(150, 14),
            BackColor = Color.Transparent,
            ForeColor = WuxiaTheme.TextMuted,
            Font = WuxiaTheme.UiFont(9f)
        };
        _sceneDescriptionLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(18, 39),
            Size = new Size(530, 40),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };

        var viewButtonHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = 150,
            Padding = new Padding(9, 18, 9, 18),
            BackColor = Color.Transparent
        };
        var viewSceneImageBtn = MakeButton(
            "查看场景", Point.Empty, new Size(132, 52), WuxiaTheme.SurfaceWarm);
        viewSceneImageBtn.Dock = DockStyle.Fill;
        viewSceneImageBtn.Click += (_, _) => ShowCurrentSceneImage();
        viewButtonHost.Controls.Add(viewSceneImageBtn);

        sceneInfoPanel.Controls.AddRange(new Control[]
        {
            _sceneNameLabel, _sceneRegionLabel, _sceneDescriptionLabel, viewButtonHost
        });
        viewButtonHost.BringToFront();

        // WinForms Dock顺序: 先添加的控件先分配空间，Fill最后添加填充剩余
        _centerPanel.Controls.Add(_logBox);      // Fill先加，先占空间
        _centerPanel.Controls.Add(_actionPanel);  // Bottom后加，从底部切分64px
        _centerPanel.Controls.Add(sceneInfoPanel);
    }

    // ── NPC选择变化时更新按钮 ──

    private void OnNPCSelectionChanged()
    {
        _npcActionPanel.Controls.Clear();
        var npcId = GetSelectedNPCId();
        if (npcId == null) return;

        if (!_engine.State.AllNPCs.TryGetValue(npcId, out var npc)) return;
        if (!npc.IsAlive) return;

        // 对话按钮（始终显示）
        var talkBtn = MakeButton("对话", new Point(0, 0), WuxiaTheme.S(105, 35));
        talkBtn.Click += async (_, _) => await OpenDialogueForm(npc);
        _npcActionPanel.Controls.Add(talkBtn);

        // 门派执事：接取任务
        if (npc.NpcRole == "quest_giver" && !string.IsNullOrEmpty(npc.FactionId))
        {
            var questBtn = MakeButton("接取任务", new Point(0, 0), WuxiaTheme.S(105, 35), WuxiaTheme.Accent);
            questBtn.Click += (_, _) => ShowFactionQuestBoard(npc);
            _npcActionPanel.Controls.Add(questBtn);
        }

        // 收集任务委托人：查看委托（按是否真有委托决定）
        var npcCommissions = _engine.FactionQuests.GetIssuedByNpc(npc.Id);
        if (npcCommissions.Count > 0)
        {
            var commissionBtn = MakeButton("查看委托", new Point(0, 0), WuxiaTheme.S(105, 35), WuxiaTheme.Accent);
            commissionBtn.Click += (_, _) => ShowNpcCommissions(npc, npcCommissions);
            _npcActionPanel.Controls.Add(commissionBtn);
        }

        // 商贩：显示购买按钮替代切磋/挑战
        if (ShopSystem.IsMerchant(npc.NpcRole))
        {
            var shopBtn = MakeButton("购买", new Point(0, 0), WuxiaTheme.S(105, 35), Color.FromArgb(200, 160, 60));
            shopBtn.Click += (_, _) => ShowShopForm(npc);
            _npcActionPanel.Controls.Add(shopBtn);
        }
        else
        {
            // 切磋按钮
            var sparBtn = MakeButton("切磋", new Point(0, 0), WuxiaTheme.S(105, 35));
            sparBtn.Click += (_, _) => StartCombatWithNPC(npc, isSpar: true);
            _npcActionPanel.Controls.Add(sparBtn);

            // 挑战按钮
            var combatBtn = MakeButton("挑战", new Point(0, 0), WuxiaTheme.S(105, 35), WuxiaTheme.Danger);
            combatBtn.Click += (_, _) => StartCombatWithNPC(npc, isSpar: false, playerInitiated: true);
            _npcActionPanel.Controls.Add(combatBtn);
        }

        // 查看详情
        var infoBtn = MakeButton("查看详情", new Point(0, 0), WuxiaTheme.S(220, 35));
        infoBtn.Click += (_, _) => ShowNPCInfo(npc);
        _npcActionPanel.Controls.Add(infoBtn);

        // 赠送
        var giftBtn = MakeButton("赠送物品", new Point(0, 0), WuxiaTheme.S(105, 35), Color.FromArgb(180, 140, 80));
        giftBtn.Click += (_, _) => GiftToNPC(npc);
        _npcActionPanel.Controls.Add(giftBtn);

        // 拜入门派 / 请教武功
        // 掌门(SectLeader)负责接纳新弟子, 同门派的传武护法(IsTrainer)负责教武
        bool isFactionLeader = npc is SectLeader sl && sl.FactionId != null;
        bool isTrainer = npc.IsTrainer && !string.IsNullOrEmpty(npc.FactionId);
        if (isFactionLeader || isTrainer)
        {
            string factionId = npc.FactionId!;
            bool alreadyJoined = _engine.State.Player.FactionId == factionId;
            if (alreadyJoined)
            {
                var learnBtn = MakeButton("请教武功", new Point(0, 0), WuxiaTheme.S(220, 35), WuxiaTheme.Success);
                learnBtn.Click += (_, _) => LearnMartialArts(npc);
                _npcActionPanel.Controls.Add(learnBtn);
            }
            else if (_engine.State.Player.FactionId == null && isFactionLeader)
            {
                // 只有掌门可以接纳新弟子,护法不行
                var factionBtn = MakeButton("拜入门派", new Point(0, 0), WuxiaTheme.S(220, 35), WuxiaTheme.Success);
                factionBtn.Click += async (_, _) =>
                {
                    factionBtn.Enabled = false;
                    try { await TryJoinFaction(npc); }
                    finally { factionBtn.Enabled = true; }
                };
                _npcActionPanel.Controls.Add(factionBtn);
            }
        }
    }


    // ── 对话弹窗 ──

    private async Task OpenDialogueForm(NPC npc)
    {
        if (_engine.PostCombat != null) return;

        GameLogger.Info($"[操作] 对话NPC: {npc.Id}");

        // 显示等待弹窗
        var loadingForm = CreateLoadingForm($"{npc.Name}正在思考...", this);
        loadingForm.Show(this);
        this.Enabled = false;

        DialogueResponse response;
        try
        {
            response = await _engine.DialogueSystem.StartDialogue(npc, _engine.State.Player, _engine.State.GameTime);
        }
        finally
        {
            this.Enabled = true;
            loadingForm.Close();
            loadingForm.Dispose();
        }

        var history = _engine.DialogueSystem.GetHistory(npc.Id);
        var form = new DialogueForm(npc, _engine.State.Player, _engine.DialogueSystem, _engine.State.GameTime, history);

        // 注入任务委托查询/接取(在对话窗内聊天流中呈现,替代对话关闭后弹系统框)
        form.QueryOfferableQuests = () => GetOfferableQuests(npc);
        form.AcceptQuest = (qid) => AcceptChainQuest(qid, npc);

        // 订阅NPC行为回调(捕获 form 引用,作为确认框 owner)
        form.NPCActionTriggered += (_, e) =>
        {
            HandleNPCActionFromDialogue(form, npc, e.Action, e.ActionTarget, e.MusicFee, e.CraftFee);
        };

        form.ShowOpeningLine(response);
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);

        // 对话结束后尝试自动推进已接任务的步骤(无弹窗)
        TryAutoAdvanceQuests("talk", npc.Id);

        RefreshAll();
    }

    /// <summary>
    /// 创建等待提示弹窗
    /// </summary>
    private static Form CreateLoadingForm(string message, Form parentForm)
    {
        var form = new Form
        {
            Text = "",
            Size = new Size(280, 100),
            StartPosition = FormStartPosition.Manual,
            BackColor = Color.FromArgb(30, 30, 45),
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            TopMost = true,
            Opacity = 0.95
        };

        // 计算居中位置
        int x = parentForm.Location.X + (parentForm.Width - form.Width) / 2;
        int y = parentForm.Location.Y + (parentForm.Height - form.Height) / 2;
        form.Location = new Point(x, y);
        FormDragHelper.EnableDrag(form);  // 等待弹窗可拖动

        var label = new Label
        {
            Text = message,
            AutoSize = false,
            Size = new Size(260, 30),
            Location = new Point(10, 20),
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(11f),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Location = new Point(20, 58),
            Size = new Size(240, 8)
        };

        form.Controls.AddRange(new Control[] { label, progressBar });
        WuxiaTheme.ApplyScaling(form);
        return form;
    }

    /// <summary>
    /// 处理对话中NPC触发的行为。owner 为对话窗实例,用作武侠风确认框的父窗口。
    /// musicFee 仅 play_music 时由乐师NPC所定赏钱(玩家付给NPC)。
    /// </summary>
    private void HandleNPCActionFromDialogue(IWin32Window owner, NPC npc, string action, string? actionTarget, int musicFee = 0, int craftFee = 0)
    {
        switch (action)
        {
            case "spar":
                // 确认已在 DialogueForm 内完成(武侠风确认框),此处直接开战
                StartCombatWithNPC(npc, isSpar: true, owner);
                break;

            case "attack":
                // attack 无需玩家选择;提示已在 DialogueForm 内用 Alert 呈现,此处直接开战
                StartCombatWithNPC(npc, isSpar: false, owner);
                break;

            case "swear_brotherhood":
                RelationshipSystem.BecomeSwornBrothers(_engine.State.Player, npc);
                _engine.State.Player.AddHistory($"与{npc.Name}义结金兰");
                _logBox.AppendSuccess($"你与{npc.Name}义结金兰,结为异姓兄弟!");
                RefreshAll();
                break;

            case "marry":
                RelationshipSystem.BecomeSpouses(_engine.State.Player, npc);
                _engine.State.Player.AddHistory($"与{npc.Name}结为夫妻");
                _logBox.AppendSuccess($"你与{npc.Name}喜结连理!");
                RefreshAll();
                break;

            case "take_disciple":
                RelationshipSystem.BecomeMasterDisciple(npc, _engine.State.Player);
                _engine.State.Player.AddHistory($"拜{npc.Name}为师");
                _logBox.AppendSuccess($"你正式拜{npc.Name}为师!");
                RefreshAll();
                break;


            case "teach_art":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleTeachArt(npc, actionTarget, owner);
                break;

            case "ask_item":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleNPCAskItem(npc, actionTarget, owner);
                break;

            case "heal":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleHealAction(npc, actionTarget, owner);
                break;

            case "castrate":
                HandleCastrateAction(npc, owner);
                break;

            case "query_location":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleQueryLocation(npc, actionTarget, owner);
                break;

            case "play_music":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandlePlayMusic(npc, actionTarget, musicFee);
                break;

            case "craft_medicine":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleCraftMedicine(npc, actionTarget, craftFee);
                break;

            case "craft_food":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleCraftFood(npc, actionTarget, craftFee);
                break;

            case "craft_forge":
                if (!string.IsNullOrEmpty(actionTarget))
                    HandleCraftForge(npc, actionTarget, craftFee);
                break;
        }
    }

    /// <summary>
    /// 处理乐师演奏: 校验曲名合法性后单曲播放(不循环,演奏完即止)。
    /// fee 为NPC自定的赏钱(玩家付给NPC,DialogueSystem已clamp到玩家可承受范围),>0时扣款。
    /// </summary>
    private void HandlePlayMusic(NPC npc, string musicFileName, int fee = 0)
    {
        // 防AI幻觉: 二次校验文件名在 assets/music 下确实存在
        var validFiles = Systems.AudioManager.ListMusicFiles()
            .Select(p => Path.GetFileName(p))
            .ToHashSet();
        if (!validFiles.Contains(musicFileName))
        {
            GameLogger.Info($"[乐师] play_music 曲名无效,忽略: {musicFileName}");
            return;
        }

        var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music", musicFileName);
        // 去掉扩展名作为曲名展示
        var displayName = Path.GetFileNameWithoutExtension(musicFileName);
        _logBox.AppendSuccess($"{npc.Name}为你演奏了一曲《{displayName}》...");

        // 收取赏钱(NPC自定,已clamp到玩家可承受范围)
        var player = _engine.State.Player;
        if (fee > 0 && player.Gold >= fee)
        {
            player.Gold -= fee;
            npc.Gold += fee;
            _logBox.AppendText($"（你支付了{fee}两赏钱给{npc.Name}）");
            GameLogger.Info($"[乐师] {npc.Name} 收取赏钱 {fee} 两");
        }

        Systems.AudioManager.Instance.PlayMusic(fullPath, loop: false);
        GameLogger.Info($"[乐师] {npc.Name} 播放音乐: {musicFileName}, 赏钱: {fee}");
    }

    /// <summary>
    /// 处理药师炼药:校验配方ID、NPC医术、好感、玩家材料与银两,通过后扣料扣银、产出丹药入背包。
    /// recipeId 即产出丹药ID(配方ID=ResultItemId)。防AI幻觉:二次校验配方存在与材料。
    /// </summary>
    private void HandleCraftMedicine(NPC npc, string recipeId, int fee)
    {
        var player = _engine.State.Player;

        if (!_engine.Config.MedicineRecipes.TryGetValue(recipeId, out var recipe))
        {
            GameLogger.Info($"[药师] craft_medicine 配方无效,忽略: {recipeId}");
            _logBox.AppendWarning($"{npc.Name}似乎记错了方子,此药炼不得。");
            return;
        }

        // NPC 医术门槛
        int medSkill = npc.GetCraftSkill("medicine");
        if (recipe.RequiredMedicineSkill > medSkill)
        {
            _logBox.AppendWarning($"{npc.Name}医术({medSkill})不足以炼制【{recipe.Name}】(需{recipe.RequiredMedicineSkill})。");
            return;
        }

        // 材料校验
        var missing = new List<string>();
        foreach (var m in recipe.Materials)
        {
            var have = player.Inventory.GetItem(m.ItemId);
            int qty = have?.Quantity ?? 0;
            if (qty < m.Quantity)
            {
                string name = _engine.Config.Items.TryGetValue(m.ItemId, out var it) ? it.Name : m.ItemId;
                missing.Add($"{name}×{m.Quantity}(你有{qty})");
            }
        }
        if (missing.Count > 0)
        {
            _logBox.AppendWarning($"材料不足,无法炼制【{recipe.Name}】: {string.Join("、", missing)}。");
            return;
        }

        // 银两校验
        if (fee > 0 && player.Gold < fee)
        {
            _logBox.AppendWarning($"银两不足,无法支付{fee}两工费。");
            return;
        }

        // 扣材料
        foreach (var m in recipe.Materials)
            player.Inventory.RemoveItem(m.ItemId, m.Quantity);

        // 扣工费
        if (fee > 0)
        {
            player.Gold -= fee;
            npc.Gold += fee;
        }

        // 产出丹药
        if (_engine.Config.Items.TryGetValue(recipe.ResultItemId, out var resultCfg))
        {
            var item = ConfigManager.ItemFromConfig(resultCfg);
            item.Quantity = Math.Max(1, recipe.ResultQuantity);
            player.Inventory.AddItem(item);
            string feeMsg = fee > 0 ? $",支付工费{fee}两" : "";
            _logBox.AppendSuccess($"【炼药成功】{npc.Name}为你炼得【{recipe.Name}】×{item.Quantity}{feeMsg}!");
            GameLogger.Info($"[药师] {npc.Name} 炼制 {recipe.Name} x{item.Quantity}, 工费{fee}");
        }
        else
        {
            _logBox.AppendWarning($"配方产出物品配置缺失:{recipe.ResultItemId}");
            GameLogger.Info($"[药师] 产出物品缺失: {recipe.ResultItemId}");
        }
    }

    /// <summary>处理厨师做菜(craft_food):校验菜谱/厨艺/好感/食材/银两,扣料扣费产出菜肴。</summary>
    private void HandleCraftFood(NPC npc, string recipeId, int fee)
    {
        var player = _engine.State.Player;

        if (!_engine.Config.FoodRecipes.TryGetValue(recipeId, out var recipe))
        {
            GameLogger.Info($"[厨师] craft_food 配方无效,忽略: {recipeId}");
            _logBox.AppendWarning($"{npc.Name}似乎记错了菜谱,此菜做不得。");
            return;
        }

        // NPC 厨艺门槛
        int cookSkill = npc.GetCraftSkill("cooking");
        if (recipe.RequiredCookingSkill > cookSkill)
        {
            _logBox.AppendWarning($"{npc.Name}厨艺({cookSkill})不足以烹制【{recipe.Name}】(需{recipe.RequiredCookingSkill})。");
            return;
        }

        // 食材校验
        var missing = new List<string>();
        foreach (var m in recipe.Materials)
        {
            var have = player.Inventory.GetItem(m.ItemId);
            int qty = have?.Quantity ?? 0;
            if (qty < m.Quantity)
            {
                string name = _engine.Config.Items.TryGetValue(m.ItemId, out var it) ? it.Name : m.ItemId;
                missing.Add($"{name}×{m.Quantity}(你有{qty})");
            }
        }
        if (missing.Count > 0)
        {
            _logBox.AppendWarning($"食材不足,无法烹制【{recipe.Name}】: {string.Join("、", missing)}。");
            return;
        }

        // 银两校验
        if (fee > 0 && player.Gold < fee)
        {
            _logBox.AppendWarning($"银两不足,无法支付{fee}两工费。");
            return;
        }

        // 扣食材
        foreach (var m in recipe.Materials)
            player.Inventory.RemoveItem(m.ItemId, m.Quantity);

        // 扣工费
        if (fee > 0)
        {
            player.Gold -= fee;
            npc.Gold += fee;
        }

        // 产出菜肴
        if (_engine.Config.Items.TryGetValue(recipe.ResultItemId, out var resultCfg))
        {
            var item = ConfigManager.ItemFromConfig(resultCfg);
            item.Quantity = Math.Max(1, recipe.ResultQuantity);
            player.Inventory.AddItem(item);
            string feeMsg = fee > 0 ? $",支付工费{fee}两" : "";
            _logBox.AppendSuccess($"【烹饪成功】{npc.Name}为你烹得【{recipe.Name}】×{item.Quantity}{feeMsg}!");
            GameLogger.Info($"[厨师] {npc.Name} 烹制 {recipe.Name} x{item.Quantity}, 工费{fee}");
        }
        else
        {
            _logBox.AppendWarning($"菜谱产出物品配置缺失:{recipe.ResultItemId}");
            GameLogger.Info($"[厨师] 产出物品缺失: {recipe.ResultItemId}");
        }
    }

    /// <summary>处理铁匠打造(craft_forge):校验配方/锻造/好感/材料/银两,扣料扣费产出装备(仅T1)。</summary>
    private void HandleCraftForge(NPC npc, string recipeId, int fee)
    {
        var player = _engine.State.Player;

        if (!_engine.Config.ForgeRecipes.TryGetValue(recipeId, out var recipe))
        {
            GameLogger.Info($"[铁匠] craft_forge 配方无效,忽略: {recipeId}");
            _logBox.AppendWarning($"{npc.Name}似乎记错了图样,此物打不得。");
            return;
        }

        // NPC 锻造门槛
        int forgeSkill = npc.GetCraftSkill("forging");
        if (recipe.RequiredForgingSkill > forgeSkill)
        {
            _logBox.AppendWarning($"{npc.Name}锻造({forgeSkill})不足以打造【{recipe.Name}】(需{recipe.RequiredForgingSkill})。");
            return;
        }

        // 材料校验
        var missing = new List<string>();
        foreach (var m in recipe.Materials)
        {
            var have = player.Inventory.GetItem(m.ItemId);
            int qty = have?.Quantity ?? 0;
            if (qty < m.Quantity)
            {
                string name = _engine.Config.Items.TryGetValue(m.ItemId, out var it) ? it.Name : m.ItemId;
                missing.Add($"{name}×{m.Quantity}(你有{qty})");
            }
        }
        if (missing.Count > 0)
        {
            _logBox.AppendWarning($"材料不足,无法打造【{recipe.Name}】: {string.Join("、", missing)}。");
            return;
        }

        // 银两校验
        if (fee > 0 && player.Gold < fee)
        {
            _logBox.AppendWarning($"银两不足,无法支付{fee}两工费。");
            return;
        }

        // 扣材料
        foreach (var m in recipe.Materials)
            player.Inventory.RemoveItem(m.ItemId, m.Quantity);

        // 扣工费
        if (fee > 0)
        {
            player.Gold -= fee;
            npc.Gold += fee;
        }

        // 产出装备
        if (_engine.Config.Items.TryGetValue(recipe.ResultItemId, out var resultCfg))
        {
            var item = ConfigManager.ItemFromConfig(resultCfg);
            item.Quantity = Math.Max(1, recipe.ResultQuantity);
            player.Inventory.AddItem(item);
            string feeMsg = fee > 0 ? $",支付工费{fee}两" : "";
            _logBox.AppendSuccess($"【打造成功】{npc.Name}为你打成【{recipe.Name}】×{item.Quantity}{feeMsg}!");
            GameLogger.Info($"[铁匠] {npc.Name} 打造 {recipe.Name} x{item.Quantity}, 工费{fee}");
        }
        else
        {
            _logBox.AppendWarning($"配方产出物品配置缺失:{recipe.ResultItemId}");
            GameLogger.Info($"[铁匠] 产出物品缺失: {recipe.ResultItemId}");
        }
    }

    /// <summary>
    /// 处理NPC传授武功
    /// </summary>
    private void HandleTeachArt(NPC npc, string artId, IWin32Window? owner = null)
    {
        var player = _engine.State.Player;
        var gameTime = _engine.State.GameTime;

        // 检查传授冷却:师徒关系(玩家拜该NPC为师)CD 2天,普通 5天
        bool isMyMaster = player.GetRelation(npc.Id).Type == RelationType.Disciple;
        int teachCD = isMyMaster ? 2 : 5;
        int daysSinceLastTeach = gameTime.Day - npc.LastTeachArtDay;
        if (daysSinceLastTeach < teachCD)
        {
            int remaining = teachCD - daysSinceLastTeach;
            _logBox.AppendText($"{npc.Name}说道:近来精力有限,{remaining}天内无法再传授武功了。");
            return;
        }

        // 检查NPC是否会这门武功
        if (!npc.LearnedArts.Any(a => a.Id == artId))
        {
            _logBox.AppendText($"{npc.Name}并不会这门武功。");
            return;
        }

        // 检查玩家是否已会
        if (player.LearnedArts.Any(a => a.Id == artId))
        {
            _logBox.AppendText($"你已经会这门武功了。");
            return;
        }

        // 查找武功配置获取名称
        _engine.Config.MartialArts.TryGetValue(artId, out var artConfig);
        var artName = artConfig?.Name ?? artId;

        var accept = WuxiaConfirmBox.Show(owner ?? this, "传授武功",
            $"{npc.Name}想传授你【{artName}】，是否接受？",
            "接受", "婉拒", WuxiaConfirmStyle.Success);
        if (accept)
        {
            // 自宫前置条件检查
            if (!player.CanLearnArt(artId, out var blockReason))
            {
                _logBox.AppendWarning($"无法修炼【{artName}】：{blockReason}。");
                return;
            }
            var art = _engine.Config.CreateMartialArt(artId);
            if (art != null)
            {
                player.LearnArt(art);
                npc.LastTeachArtDay = gameTime.Day; // 记录传授时间
                _logBox.AppendSuccess($"学会了【{art.Name}】！");
                player.AddLifeEvent(gameTime.Day, LifeEventType.Training, $"习得武功【{art.Name}】");
                int expGain = 30;
                int lv = player.GainJianghuExp(expGain);
                _logBox.AppendSuccess($"获得江湖阅历 +{expGain}（阅历Lv.{player.JianghuLevel}）");
                if (lv > 0) _logBox.AppendSuccess($"阅历提升！等级达到 Lv.{player.JianghuLevel}！");
                npc.AddLifeEvent(gameTime.Day, LifeEventType.Social,
                    $"传授{player.Name}武功：{art.Name}。");
            }
            else
            {
                _logBox.AppendWarning($"未能学会该武功。");
            }
        }
    }

    /// <summary>
    /// 处理NPC索要物品
    /// </summary>
    private void HandleNPCAskItem(NPC npc, string itemId, IWin32Window? owner = null)
    {
        var player = _engine.State.Player;
        if (!player.Inventory.HasItem(itemId))
        {
            _logBox.AppendText($"你没有这个物品。");
            return;
        }

        var item = player.Inventory.GetItem(itemId);
        if (item == null) return;

        var give = WuxiaConfirmBox.Show(owner ?? this, "索要物品",
            $"{npc.Name}想要你的【{item.Name}】，是否给予？",
            "给予", "婉拒", WuxiaConfirmStyle.Neutral);
        if (give)
        {
            if (player.Inventory.TransferTo(npc.Inventory, itemId, 1))
            {
                _logBox.AppendSuccess($"你将【{item.Name}】给了{npc.Name}。");
                var rel = npc.GetRelation(player.Id);
                rel.ChangeFavorability(5);
                npc.AddLifeEvent(_engine.State.GameTime.Day, LifeEventType.Social,
                    $"从{player.Name}处得到了{item.Name}。");
            }
        }
    }

    /// <summary>
    /// 处理医者NPC的治疗行为
    /// </summary>
    private void HandleHealAction(NPC npc, string tagId, IWin32Window? owner = null)
    {
        var player = _engine.State.Player;
        var npcRole = npc.NpcRole ?? "";

        // 校验NPC能否治疗该标签
        bool canHeal = false;
        switch (npcRole)
        {
            case "medicine_merchant":
                canHeal = tagId == "poison";
                break;
            case "imperial_doctor":
                canHeal = tagId is "poison" or "severe_poison" or "heavy_injury";
                break;
            case "wandering_doctor":
                canHeal = tagId is "poison" or "severe_poison" or "heavy_injury" or "eunuch";
                break;
        }

        if (!canHeal)
        {
            _logBox.AppendText($"{npc.Name}摇了摇头：“这个病老夫无能为力。”");
            return;
        }

        if (!player.HasTag(tagId))
        {
            _logBox.AppendText($"{npc.Name}诊了诊脉：“你并无此病状。”");
            return;
        }

        // 确认治疗
        var tagName = tagId switch
        {
            "poison" => "普通中毒",
            "severe_poison" => "剧毒",
            "heavy_injury" => "重伤",
            "eunuch" => "阉人之躯",
            _ => tagId
        };

        var confirm = WuxiaConfirmBox.Show(owner ?? this, "治疗",
            $"{npc.Name}愿意为你治疗【{tagName}】，是否接受？",
            "接受", "婉拒", WuxiaConfirmStyle.Success);
        if (!confirm) return;

        // 执行治疗
        player.RemoveTag(tagId);
        int healthRestore = tagId switch
        {
            "poison" => 20,
            "severe_poison" => 40,
            "heavy_injury" => 30,
            "eunuch" => 10,
            _ => 10
        };
        player.ChangeHealth(healthRestore, $"{npc.Name}治疗{tagName}");
        _logBox.AppendSuccess($"{npc.Name}施针用药，为你治好了【{tagName}】，健康度+{healthRestore}。");
        Core.GameLogger.Info($"[治疗] {npc.Name}治疗了玩家的{tagName}，健康度+{healthRestore}");
    }

    /// <summary>
    /// 处理自宫手术行为
    /// </summary>
    private void HandleCastrateAction(NPC npc, IWin32Window? owner = null)
    {
        var player = _engine.State.Player;
        if (player.HasTag("eunuch"))
        {
            _logBox.AppendText($"{npc.Name}看了看你：“你已经是净身之人了，不必再来。”");
            return;
        }

        var confirm = WuxiaConfirmBox.Show(owner ?? this, "自宫手术",
            $"{npc.Name}说道：“此刀一落，此生便无回头路。你确定要自宫吗？”\n\n" +
            "自宫后可修炼辟邪剑法、葵花宝典等绝世武功，但无法恢复。",
            "下定决心", "三思而后行", WuxiaConfirmStyle.Danger);
        if (!confirm) return;

        var tag = PlayerTag.CreateEunuch(_engine.State.GameTime.Day);
        player.AddTag(tag);
        player.ChangeHealth(-20, "自宫手术");
        _logBox.AppendSuccess($"{npc.Name}手法利落，手术完成。你已获【阉人】标签。");
        Core.GameLogger.Info($"[自宫] 玩家在{npc.Name}处行自宫之术，获得阉人标签，健康度-20");
    }

    /// <summary>
    /// 处理百晓阁门人查询 NPC 位置行为(按目标江湖等级浮动收费)
    /// </summary>
    private void HandleQueryLocation(NPC npc, string targetNpcId, IWin32Window? owner = null)
    {
        var state = _engine.State;
        var player = state.Player;

        if (!state.AllNPCs.TryGetValue(targetNpcId, out var target))
        {
            _logBox.AppendText($"{npc.Name}翻了翻册子：“此人……恕我未有所闻。”");
            return;
        }

        if (!target.IsAlive)
        {
            _logBox.AppendText($"{npc.Name}叹道：“{target.Name}……已不在人世了。”");
            return;
        }

        // 浮动收费:按目标江湖等级
        int fee = target.JianghuLevel switch
        {
            <= 10 => 50,
            <= 25 => 100 + (target.JianghuLevel - 10) * 7,   // 11~25 → 107~203
            <= 50 => 300,
            _ => 500
        };

        if (player.Gold < fee)
        {
            _logBox.AppendText($"{npc.Name}摇头：“打听【{target.Name}】的行踪需银{fee}两，阁下银两不足，攒够再来吧。”");
            return;
        }

        var confirm = WuxiaConfirmBox.Show(owner ?? this, "百晓阁查询",
            $"{npc.Name}翻看百晓册：“打听【{target.Name}】的下落，需银{fee}两。是否付账？”",
            "付账", "作罢", WuxiaConfirmStyle.Neutral);
        if (!confirm) return;

        player.Gold -= fee;
        npc.Gold += fee;

        // 查询目标当前所在场景(按当前时辰从Schedule算实际位置,与NPCLocationManager一致;
        // 任务副作用如围攻光明顶改了成昆Schedule到光明顶,此处能正确反映)
        var sceneId = target.GetCurrentSceneByTime(state.GameTime.GetTimePeriod());
        string sceneName = "未知";
        if (!string.IsNullOrEmpty(sceneId) && state.AllScenes.TryGetValue(sceneId, out var scene))
            sceneName = scene.Name;
        else if (!string.IsNullOrEmpty(sceneId))
            sceneName = sceneId;

        _logBox.AppendSuccess($"{npc.Name}收下{fee}两银，查了查百晓册：“【{target.Name}】目下在【{sceneName}】一带。”");
        Core.GameLogger.Info($"[百晓阁] 玩家在{npc.Name}处付费{fee}两查询{target.Name}位置 → {sceneName}");
    }

    // ── 战斗 ──

    private void StartCombatWithNPC(NPC npc, bool isSpar, IWin32Window? owner = null, bool playerInitiated = false)
    {
        if (_engine.PostCombat != null) return;

        var npcId = npc.Id;
        string combatType = isSpar ? "切磋" : "挑战";

        // 仅玩家主动发起生死战时弹确认;NPC主动攻击或剧情强制触发的生死战不弹
        if (!isSpar && playerInitiated)
        {
            var result = WuxiaConfirmBox.Show(owner ?? this, "生死相搏",
                $"确定要发起生死{combatType}吗？\n战败可能会被杀死或羞辱！",
                "放手一搏", "再想想", WuxiaConfirmStyle.Danger);
            if (!result) return;
        }

        // NPC回应
        string npcResponse;
        var relation = npc.GetRelation(_engine.State.Player.Id);
        if (isSpar)
        {
            if (relation.Favorability > 20)
                npcResponse = $"{npc.Name}笑道：\"好，切磋切磋！\"";
            else
                npcResponse = $"{npc.Name}抱拳道：\"请赐教。\"";
        }
        else
        {
            if (relation.Type == RelationType.Enemy)
                npcResponse = $"{npc.Name}冷笑道：\"正合我意，今日了结恩怨！\"";
            else if (relation.Favorability > 30)
                npcResponse = $"{npc.Name}叹道：\"既然你执意如此，休怪我不客气了。\"";
            else
                npcResponse = $"{npc.Name}沉声道：\"来吧！\"";
        }
        _logBox.AppendText(npcResponse);

        GameLogger.Info($"[操作] {combatType}NPC: {npcId}");

        var combat = _engine.StartCombat(npcId, isSpar);
        if (combat == null) return;

        var combatForm = new CombatForm(combat, _engine);
        WuxiaTheme.ApplyScaling(combatForm);
        combatForm.ShowDialog(this);

        // 战斗胜利后尝试自动推进链式任务
        if (combat.Result.Outcome == CombatOutcome.PlayerWin)
            TryAutoAdvanceQuests("fight", npcId);

        RefreshAll();
    }

    // ── 辅助控件 ──

    private Button MakeButton(string text, Point loc, Size size, Color? bgColor = null)
    {
        var button = new Button
        {
            Text = text, Location = loc, Size = size,
            TextAlign = ContentAlignment.MiddleCenter
        };
        WuxiaTheme.StyleButton(button, bgColor);
        return button;
    }

    private ListBox MakeListBox(Point loc, Size size)
    {
        var listBox = new ListBox
        {
            Location = loc, Size = size,
        };
        WuxiaTheme.StyleListBox(listBox);
        return listBox;
    }

    private void AddSectionLabel(Panel parent, string text, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text, Location = new Point(10, y), AutoSize = true,
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(9.5f, FontStyle.Bold)
        });
    }

    // ── 刷新 ──

    private Image? LoadHeaderImage()
    {
        return ImageAssetLoader.Load("assets/ui/wuxia-header.png");
    }

    private void RefreshAll()
    {
        _timeLabel.Text = $"时间：{_engine.State.GameTime.YearDayShiChenDisplay}";
        _statusLabel.Text = _engine.State.Player.GetStatusSummary();
        RefreshSceneBackground();
        RefreshSceneList();
        RefreshNPCList();
        RefreshActions();
        OnNPCSelectionChanged(); // 刷新NPC操作按钮

        // 检查月度更新
        CheckMonthlyUpdate();

        // 检查年度大事件生成(月度之后)
        CheckAnnualUpdate();

        // 检查玩家死亡
        if (_engine.PlayerIsDead)
        {
            MessageBox.Show("油尽灯枯，驾鹤西去...\n\n你的江湖之旅到此结束。",
                "游戏结束", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            _engine.PlayerIsDead = false;
            // 返回主菜单或退出
            Application.Exit();
        }
    }

    private void RefreshSceneBackground()
    {
        var scene = _engine.GetCurrentScene();
        if (scene == null)
        {
            _sceneNameLabel.Text = "未知之地";
            _sceneRegionLabel.Text = "";
            _sceneDescriptionLabel.Text = "";
            ReplaceSceneBackground(null, null);
            return;
        }

        _sceneNameLabel.Text = scene.Name;
        _sceneRegionLabel.Text = string.IsNullOrWhiteSpace(scene.Region) ? "" : $"【{scene.Region}】";
        _sceneDescriptionLabel.Text = scene.Description;

        if (string.Equals(_displayedSceneImagePath, scene.BackgroundImagePath, StringComparison.OrdinalIgnoreCase))
            return;

        ReplaceSceneBackground(ImageAssetLoader.Load(scene.BackgroundImagePath), scene.BackgroundImagePath);
    }

    private void ReplaceSceneBackground(Image? image, string? path)
    {
        _contentLayout.BackgroundImage = null;
        _sceneBackgroundImage?.Dispose();
        _sceneBackgroundImage = image;
        _contentLayout.BackgroundImage = image;
        _displayedSceneImagePath = path;
    }

    private void ShowCurrentSceneImage()
    {
        var scene = _engine.GetCurrentScene();
        if (scene == null)
            return;

        using var image = ImageAssetLoader.Load(scene.BackgroundImagePath);
        if (image == null)
        {
            MessageBox.Show(this, "该场景尚未配置图片。", "场景图片",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new Form
        {
            Text = $"{scene.Name} · 场景图片",
            Size = new Size(1100, 720),
            MinimumSize = new Size(720, 480),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(35, 29, 22),
            FormBorderStyle = FormBorderStyle.Sizable,
            ShowIcon = false
        };

        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(35, 29, 22)
        };
        var closeBtn = MakeButton("关闭", Point.Empty, new Size(110, 38));
        closeBtn.Dock = DockStyle.Right;
        closeBtn.Click += (_, _) => form.Close();
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(8, 6, 10, 6),
            BackColor = WuxiaTheme.PanelBack
        };
        footer.Controls.Add(closeBtn);

        form.Controls.Add(picture);
        form.Controls.Add(footer);
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
        picture.Image = null;
    }

    private bool _monthlyUpdateInProgress;

    private async void CheckMonthlyUpdate()
    {
        GameLogger.Info($"CheckMonthlyUpdate: inProgress={_monthlyUpdateInProgress}, needsUpdate={_engine.NeedsMonthlyUpdate()}, day={_engine.State.GameTime.Day}, inCombat={_engine.IsInCombat}, postCombat={_engine.HasPostCombatChoices}");

        if (_monthlyUpdateInProgress) return;
        if (!_engine.NeedsMonthlyUpdate()) return;
        // 战斗中也允许过月（只是延后处理）
        if (_engine.IsInCombat || _engine.HasPostCombatChoices)
        {
            GameLogger.Info("月度更新被战斗状态延迟");
            return;
        }

        GameLogger.Info("月度更新触发！");
        _monthlyUpdateInProgress = true;

        // 创建进度弹窗
        var progressForm = new MonthlyProgressForm();

        // 订阅 Agent 事件
        void OnToolCall(string msg)
        {
            if (progressForm.IsDisposed) return;
            // 仅"轮次开始"的思考提示打印一行,工具调用前的状态更新不重复打印
            if (msg.Contains("思考中") || msg.Contains("沉思"))
                progressForm.AppendLog("月度演化中...", Color.FromArgb(150, 150, 170));
            progressForm.SetStatus(msg);
        }
        void OnToolResult(string msg)
        {
            if (progressForm.IsDisposed) return;
            progressForm.AppendLog(msg, Color.FromArgb(180, 210, 150));
        }
        void OnAgentFinish(string summary)
        {
            if (progressForm.IsDisposed) return;
            progressForm.AgentFinished(summary);
        }
        void OnAgentError(string error)
        {
            if (progressForm.IsDisposed) return;
            progressForm.AgentError(error);
        }

        _engine.MonthlyUpdateSystem.OnToolCall += OnToolCall;
        _engine.MonthlyUpdateSystem.OnToolResult += OnToolResult;
        _engine.MonthlyUpdateSystem.OnAgentFinish += OnAgentFinish;
        _engine.MonthlyUpdateSystem.OnAgentError += OnAgentError;

        try
        {
            _logBox.AppendText("═══════════════════════════════");
            _logBox.AppendText("  岁月流转，已过一月...");

            // 先显示弹窗，然后在弹窗内执行月度更新
            progressForm.Show(this);
            progressForm.SetStatus("正在启动月度演化...");

            // 让UI处理一下显示
            Application.DoEvents();

            // 执行月度更新
            string summary;
            try
            {
                summary = await _engine.TriggerMonthlyUpdate();
            }
            catch (Exception ex2)
            {
                GameLogger.Error("TriggerMonthlyUpdate异常", ex2);
                summary = "月度演化异常，江湖保持原样。";
            }

            // 通知弹窗完成（如果事件回调还没有处理的话）
            if (!progressForm.IsDisposed && !progressForm.IsComplete)
            {
                progressForm.AgentFinished(summary ?? "月度演化完成。");
            }

            // 等待用户确认关闭弹窗
            while (!progressForm.IsDisposed && progressForm.Visible)
            {
                await Task.Delay(100);
            }

            RefreshAll();
        }
        catch (Exception ex)
        {
            GameLogger.Error("月度更新异常", ex);
            _logBox.AppendText($"[月度更新出错: {ex.Message}]");
        }
        finally
        {
            _engine.MonthlyUpdateSystem.OnToolCall -= OnToolCall;
            _engine.MonthlyUpdateSystem.OnToolResult -= OnToolResult;
            _engine.MonthlyUpdateSystem.OnAgentFinish -= OnAgentFinish;
            _engine.MonthlyUpdateSystem.OnAgentError -= OnAgentError;
            if (!progressForm.IsDisposed) progressForm.Dispose();
            _monthlyUpdateInProgress = false;
        }
    }

    private bool _annualUpdateInProgress;

    private async void CheckAnnualUpdate()
    {
        if (_annualUpdateInProgress) return;
        if (!_engine.NeedsAnnualUpdate()) return;
        if (_engine.IsInCombat || _engine.HasPostCombatChoices || _monthlyUpdateInProgress)
        {
            GameLogger.Info("年度更新被战斗/月度状态延迟");
            return;
        }

        GameLogger.Info("年度大事件生成触发！");
        _annualUpdateInProgress = true;

        var progressForm = new AnnualProgressForm();

        void OnToolCall(string msg)
        {
            if (progressForm.IsDisposed) return;
            if (msg.Contains("沉思") || msg.Contains("思考中"))
                progressForm.AppendLog("年度演化中...", Color.FromArgb(150, 150, 170));
            progressForm.SetStatus(msg);
        }
        void OnToolResult(string msg)
        {
            if (progressForm.IsDisposed) return;
            if (!string.IsNullOrEmpty(msg))
                progressForm.AppendLog(msg, Color.FromArgb(180, 210, 150));
        }
        void OnAgentFinish(string summary)
        {
            if (progressForm.IsDisposed) return;
            progressForm.AgentFinished(summary);
        }
        void OnAgentError(string error)
        {
            if (progressForm.IsDisposed) return;
            progressForm.AgentError(error);
        }

        _engine.AnnualUpdateSystem.OnToolCall += OnToolCall;
        _engine.AnnualUpdateSystem.OnToolResult += OnToolResult;
        _engine.AnnualUpdateSystem.OnAgentFinish += OnAgentFinish;
        _engine.AnnualUpdateSystem.OnAgentError += OnAgentError;

        try
        {
            _logBox.AppendText("═══════════════════════════════");
            _logBox.AppendText("  岁末年初，江湖似有大事将起...");

            progressForm.Show(this);
            progressForm.SetStatus("正在酝酿江湖大事件...");
            Application.DoEvents();

            string summary;
            try
            {
                summary = await _engine.TriggerAnnualUpdate();
            }
            catch (Exception ex2)
            {
                GameLogger.Error("TriggerAnnualUpdate异常", ex2);
                summary = "这一年江湖风平浪静。";
            }

            if (!progressForm.IsDisposed && !progressForm.IsComplete)
                progressForm.AgentFinished(summary ?? "年度大事件已生。");

            while (!progressForm.IsDisposed && progressForm.Visible)
                await Task.Delay(100);

            RefreshAll();
        }
        catch (Exception ex)
        {
            GameLogger.Error("年度更新异常", ex);
            _logBox.AppendText($"[年度更新出错: {ex.Message}]");
        }
        finally
        {
            _engine.AnnualUpdateSystem.OnToolCall -= OnToolCall;
            _engine.AnnualUpdateSystem.OnToolResult -= OnToolResult;
            _engine.AnnualUpdateSystem.OnAgentFinish -= OnAgentFinish;
            _engine.AnnualUpdateSystem.OnAgentError -= OnAgentError;
            if (!progressForm.IsDisposed) progressForm.Dispose();
            _annualUpdateInProgress = false;
        }
    }

    private void RefreshSceneList()
    {
        _sceneListBox.Items.Clear();
        var scene = _engine.GetCurrentScene();
        if (scene == null) return;

        // 门派任务榜按钮：仅在玩家已加入门派且当前场景属于该门派时显示
        var playerFaction = _engine.State.Player.FactionId;
        bool inFactionScene = !string.IsNullOrEmpty(playerFaction)
            && !string.IsNullOrEmpty(scene.FactionId)
            && scene.FactionId == playerFaction;
        _factionQuestBtn.Visible = inFactionScene;

        foreach (var id in scene.ConnectedSceneIds)
        {
            if (_engine.State.AllScenes.TryGetValue(id, out var s))
            {
                _sceneListBox.Items.Add($"{s.Name}");
            }
        }
    }

    private void RefreshNPCList()
    {
        _npcFlowPanel.Controls.Clear();
        _selectedNpcId = null;
        var scene = _engine.GetCurrentScene();
        if (scene == null) return;

        foreach (var npc in scene.PresentNPCs)
        {
            if (!npc.IsAlive || npc.IsHidden) continue;
            _npcFlowPanel.Controls.Add(CreateNPCPortraitControl(npc));
        }
    }

    /// <summary>
    /// 创建单个NPC头像控件（64x64头像 + 名字，点击选中加描边）
    /// </summary>
    private Control CreateNPCPortraitControl(NPC npc)
    {
        const int imgSize = 64;
        const int totalWidth = 72;
        const int totalHeight = 108;

        var panel = new Panel
        {
            Size = new Size(totalWidth, totalHeight),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Tag = npc.Id,
            Margin = new Padding(2)
        };

        // 头像PictureBox
        var borderPanel = new Panel
        {
            Location = new Point(4, 0),
            Size = new Size(imgSize + 4, imgSize + 4),
            BackColor = Color.FromArgb(60, 60, 80),
            Tag = "border_" + npc.Id
        };

        var picBox = new PictureBox
        {
            Location = new Point(2, 2),
            Size = new Size(imgSize, imgSize),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Transparent
        };
        picBox.Image = PortraitHelper.GetPortraitOrDefault(npc.PortraitPath, npc.Name, imgSize);
        borderPanel.Controls.Add(picBox);
        panel.Controls.Add(borderPanel);

        // 名字标签：允许自动换行展示完整姓名，最多2行
        var nameLabel = new Label
        {
            Text = npc.Name,
            Location = new Point(0, imgSize + 5),
            Size = new Size(totalWidth, 36),
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(245, 235, 200),
            Font = WuxiaTheme.UiFont(8f),
            BackColor = Color.FromArgb(175, 22, 20, 30),
            AutoEllipsis = false
        };
        panel.Controls.Add(nameLabel);

        // 点击事件
        void OnClick(object? sender, EventArgs e) => SelectNPC(npc.Id);
        panel.Click += OnClick;
        picBox.Click += OnClick;
        borderPanel.Click += OnClick;
        nameLabel.Click += OnClick;

        return panel;
    }

    /// <summary>
    /// 选中NPC（高亮描边 + 更新操作按钮）
    /// </summary>
    private void SelectNPC(string npcId)
    {
        _selectedNpcId = npcId;

        // 更新描边
        foreach (Control ctrl in _npcFlowPanel.Controls)
        {
            if (ctrl is not Panel p) continue;
            foreach (Control child in p.Controls)
            {
                if (child is Panel border && border.Tag is string tag && tag.StartsWith("border_"))
                {
                    var id = tag[7..];
                    border.BackColor = id == npcId
                        ? Color.FromArgb(255, 220, 150)
                        : Color.FromArgb(60, 60, 80);
                }
            }
        }

        OnNPCSelectionChanged();
    }

    private void RefreshActions()
    {
        _actionPanel.Controls.Clear();
        var scene = _engine.GetCurrentScene();
        if (scene == null) return;

        // 战后选择
        if (_engine.PostCombat?.Phase == PostCombatPhase.PlayerWinChoice)
        {
            AddActionButton("杀死", () => { _engine.PostCombatKill(); RefreshAll(); },
                WuxiaTheme.Danger);
            AddActionButton("羞辱", () => { _engine.PostCombatHumiliate(); RefreshAll(); },
                Color.FromArgb(246, 195, 119));
            AddActionButton("放过", () => { _engine.PostCombatSpare(); RefreshAll(); },
                WuxiaTheme.Success);
            return;
        }

        // 场景信息
        AddActionButton($"当前：{scene.Name}", null, Color.FromArgb(255, 226, 174));
        AddActionButton("背包", () => ShowBackpack());

        // 场景服务
        if (scene.CraftLessons.Count > 0)
            AddActionButton("学习技艺", () => ShowCraftLessonForm(), Color.FromArgb(140, 200, 140));
        if (scene.MartialLessons.Count > 0)
            AddActionButton("武术馆", () => ShowMartialLessonForm(), Color.FromArgb(140, 160, 220));
        if (scene.Mine != null)
            AddActionButton("挖矿", () => ShowMiningForm(), Color.FromArgb(180, 140, 100));
        if (scene.Hunt != null)
            AddActionButton("打猎", () => ShowHuntingForm(), Color.FromArgb(140, 160, 100));
        if (scene.HerbGarden != null)
            AddActionButton("采药", () => ShowHerbGatheringForm(), Color.FromArgb(120, 180, 120));
    }

    private void AddActionButton(string text, Action? action, Color? bgColor = null)
    {
        var btn = new Button
        {
            Text = text, Size = WuxiaTheme.S(115, 38),
            Margin = new Padding(WuxiaTheme.V(3)),
            Cursor = action != null ? Cursors.Hand : Cursors.Default
        };
        WuxiaTheme.StyleButton(btn, bgColor);
        if (action != null)
            btn.Click += (_, _) =>
            {
                GameLogger.UI($"点击按钮: {text}");
                try { action(); }
                catch (Exception ex)
                {
                    GameLogger.Error($"按钮[{text}]异常", ex);
                    _logBox.AppendText($"[错误: {ex.Message}]");
                }
                RefreshAll();
            };
        _actionPanel.Controls.Add(btn);
    }

    // ── 操作 ──

    private void TravelToSelectedScene()
    {
        try
        {
            if (_engine.PostCombat != null) return;
            var scene = _engine.GetCurrentScene();
            if (scene == null || _sceneListBox.SelectedIndex < 0) return;
            if (_sceneListBox.SelectedIndex >= scene.ConnectedSceneIds.Count) return;
            var targetId = scene.ConnectedSceneIds[_sceneListBox.SelectedIndex];

            // 华山论剑触发: 声望 ≥ 10000 且未邀请过
            if (_engine.State.Player.Reputation >= 10000 && !_engine.State.HuashanInvited)
            {
                _engine.State.HuashanInvited = true;
                MessageBox.Show(this,
                    "飞鸽传书:\n\n江湖盛事,华山之巅,论剑英雄毕集。望阁下莫负盛名,如约而至。\n\n(华山论剑副本已开启,可在任务列表中查看)",
                    "飞鸽传书", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (_engine.Config.Dungeons.ContainsKey("huashan_lunjian"))
                {
                    var hQuest = new Quests.DungeonQuest
                    {
                        Id = "main_huashan_lunjian",
                        Name = "华山论剑",
                        Description = "声望已成,江湖召唤——华山之巅,与诸位英雄一较高下。",
                        DungeonId = "huashan_lunjian",
                        Difficulty = "hard",
                        Reward = new Quests.QuestReward { GoldBonus = 1000, ReputationBonus = 500 },
                        Steps = { new Quests.QuestStep { Id = "duel", Description = "登顶华山,败尽群雄", ActionType = "dungeon" } }
                    };
                    _engine.State.Player.AddQuest(hQuest);
                }
            }

            GameLogger.Info($"[操作] 前往场景: {targetId} (索引{_sceneListBox.SelectedIndex})");
            _engine.TravelToScene(targetId);
            GameLogger.Info($"[操作] 到达场景: {_engine.State.CurrentSceneId}");

            // 场景切换后:推进 go 步骤 + 接取场景触发任务
            OnSceneEntered(_engine.State.CurrentSceneId);
        }
        catch (Exception ex)
        {
            GameLogger.Error("[操作] 前往场景异常", ex);
            _logBox.AppendText($"[错误: {ex.Message}]");
        }
        RefreshAll();
    }

    private string? GetSelectedNPCId()
    {
        return _selectedNpcId;
    }

    private async Task TryJoinFaction(NPC npc)
    {
        if (npc.FactionId == null) { _logBox.AppendWarning("此人没有门派。"); return; }
        
        // 禁用按钮防止重复点击
        Enabled = false;
        _logBox.AppendSystem($"正在向掌门请示……");
        
        try
        {
            await _engine.TryJoinFactionAsync(npc.FactionId, npc);
        }
        finally
        {
            Enabled = true;
        }
        RefreshAll();
    }

    private void LearnMartialArts(NPC npc)
    {
        if (npc.FactionId == null) return;
        if (!_engine.State.AllFactions.TryGetValue(npc.FactionId, out var faction))
        {
            _logBox.AppendWarning("门派信息未找到。");
            return;
        }

        var player = _engine.State.Player;
        var learnable = faction.GetLearnableArts(player);
        var progress = FactionTrainingSystem.GetTrainingProgress(player, faction);

        // 构建弹窗
        var form = new Form
        {
            Text = $"向 {npc.Name} 请教武功",
            Size = new Size(550, 480),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(25, 25, 35),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var title = new Label
        {
            Text = $"【{faction.Name}】武功传承",
            Location = new Point(10, 8),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold)
        };

        var descLabel = new Label
        {
            Text = $"{npc.Name}说道：\"{GetMasterTeachLine(npc, player)}\"",
            Location = new Point(10, 38),
            Size = new Size(520, 40),
            ForeColor = Color.FromArgb(180, 180, 200),
            Font = WuxiaTheme.UiFont(9f)
        };

        // 武功列表
        var listBox = new ListBox
        {
            Location = new Point(10, 85),
            Size = new Size(520, 280),
            BackColor = Color.FromArgb(20, 20, 30),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(10f),
            BorderStyle = BorderStyle.FixedSingle
        };

        foreach (var tp in progress)
        {
            var artName = _config.MartialArts.TryGetValue(tp.ArtId, out var cfg) ? cfg.Name : tp.ArtId;
            var typeTag = _config.MartialArts.TryGetValue(tp.ArtId, out var c2) ? (c2.Type == "internal" ? "[内功]" : "[外功]") : "";
            string line;
            if (tp.IsLearned)
                line = $"  {tp.Order}. {artName} {typeTag} - 熟练度 {tp.ProficiencyLevel}/10层";
            else if (tp.IsUnlocked)
                line = $"> {tp.Order}. {artName} {typeTag} - 【可学习】";
            else
                line = $"  {tp.Order}. {artName} {typeTag} - {tp.GetStatusText()}";
            listBox.Items.Add(line);
        }

        // 学习按钮
        var learnBtn = new Button
        {
            Text = "请教学习",
            Location = new Point(10, 375),
            Size = new Size(250, 38),
            BackColor = Color.FromArgb(60, 100, 60),
            ForeColor = Color.FromArgb(220, 220, 200),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(10f),
            Cursor = Cursors.Hand
        };

        // 关闭按钮
        var closeBtn = new Button
        {
            Text = "告辞",
            Location = new Point(270, 375),
            Size = new Size(250, 38),
            BackColor = Color.FromArgb(100, 60, 60),
            ForeColor = Color.FromArgb(220, 220, 200),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(10f),
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => form.Close();

        // 状态提示
        var statusLabel = new Label
        {
            Text = "",
            Location = new Point(10, 420),
            Size = new Size(520, 25),
            ForeColor = Color.FromArgb(255, 200, 100),
            Font = WuxiaTheme.UiFont(9f)
        };

        learnBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= progress.Count)
            {
                statusLabel.Text = "请先选择要学习的武功。";
                return;
            }

            var selected = progress[listBox.SelectedIndex];
            var artId = selected.ArtId;

            if (selected.IsLearned)
            {
                statusLabel.Text = "你已经学会了这门武功，通过战斗提升熟练度吧！";
                return;
            }

            if (!learnable.Contains(artId))
            {
                statusLabel.Text = "还不能学习这门武功，需要先提升前置武功的熟练度。";
                return;
            }

            var art = _config.CreateMartialArt(artId);
            if (art == null)
            {
                statusLabel.Text = "武功配置加载失败。";
                return;
            }

            player.LearnArt(art);
            player.AddHistory($"向{npc.Name}请教学习了【{art.Name}】");
            int learnExp = 30;
            int learnLv = player.GainJianghuExp(learnExp);
            _logBox.AppendSuccess($"恭喜！你向{npc.Name}请教，学会了【{art.Name}】！");
            _logBox.AppendSuccess($"获得江湖阅历 +{learnExp}（阅历Lv.{player.JianghuLevel}）");
            if (learnLv > 0) _logBox.AppendSuccess($"阅历提升！等级达到 Lv.{player.JianghuLevel}！");
            statusLabel.ForeColor = Color.FromArgb(100, 255, 100);
            statusLabel.Text = $"学会了【{art.Name}】！通过战斗来提升熟练度吧。";

            // 刷新列表
            listBox.Items.Clear();
            var newProgress = FactionTrainingSystem.GetTrainingProgress(player, faction);
            foreach (var tp in newProgress)
            {
                var artName = _config.MartialArts.TryGetValue(tp.ArtId, out var cfg2) ? cfg2.Name : tp.ArtId;
                var typeTag2 = _config.MartialArts.TryGetValue(tp.ArtId, out var c3) ? (c3.Type == "internal" ? "[内功]" : "[外功]") : "";
                string line2;
                if (tp.IsLearned)
                    line2 = $"  {tp.Order}. {artName} {typeTag2} - 熟练度 {tp.ProficiencyLevel}/10层";
                else if (tp.IsUnlocked)
                    line2 = $"> {tp.Order}. {artName} {typeTag2} - 【可学习】";
                else
                    line2 = $"  {tp.Order}. {artName} {typeTag2} - {tp.GetStatusText()}";
                listBox.Items.Add(line2);
            }
        };

        form.Controls.AddRange(new Control[] { title, descLabel, listBox, learnBtn, closeBtn, statusLabel });
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);

        RefreshAll();
    }

    private string GetMasterTeachLine(NPC npc, Player player)
    {
        var learned = player.LearnedArts.Count;
        if (learned == 0)
            return "既入我门，便传你本门武功，好好修炼。";
        if (learned <= 2)
            return "你的武功还需磨练，不过可以学习新的招式了。";
        return "你的进步不错，本门还有更高深的武学可以传授于你。";
    }

    private void ShowNPCInfo(NPC npc)
    {
        var rel = npc.GetRelation(_engine.State.Player.Id);

        var form = new Form
        {
            Text = $"{npc.Name} - 详细信息",
            Size = new Size(560, 520),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        // 左侧Tab按钮面板
        var tabPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(100, 475),
            BackColor = WuxiaTheme.PanelBack
        };
        form.Controls.Add(tabPanel);

        // 右侧内容面板
        var contentPanel = new Panel
        {
            Location = new Point(105, 5),
            Size = new Size(430, 465),
            BackColor = WuxiaTheme.Surface
        };
        form.Controls.Add(contentPanel);

        // 创建三个内容面板
        var basicPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent };
        var artsPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent, Visible = false };
        var bagPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent, Visible = false };
        var expPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent, Visible = false };
        contentPanel.Controls.AddRange(new Control[] { basicPanel, artsPanel, bagPanel, expPanel });

        // ── Tab 1: 基本信息 ──
        // 头像(点击放大查看)
        var npcPortrait = new PictureBox
        {
            Location = new Point(5, 5),
            Size = new Size(80, 80),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Cursor = Cursors.Hand
        };
        npcPortrait.Image = PortraitHelper.GetPortraitOrDefault(npc.PortraitPath, npc.Name, 80);
        npcPortrait.Click += (_, _) => ShowPortraitPreview(npc.PortraitPath, npc.Name);
        basicPanel.Controls.Add(npcPortrait);

        var infoBox = new RichTextBox
        {
            Location = new Point(90, 5),
            Size = new Size(335, 455),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        infoBox.AppendText($"【{npc.Name}】{npc.Description}\n\n");
        infoBox.AppendText($"江湖阅历：Lv.{npc.JianghuLevel}\n");
        infoBox.AppendText($"门派：{_engine.State.GetFactionName(npc.FactionId)}\n");
        infoBox.AppendText($"善恶：{npc.Karma} ({KarmaSystem.GetKarmaDescription(npc.Karma)})\n");
        infoBox.AppendText($"关系：{rel.GetRelationDescription()} (好感度{rel.Favorability})\n\n");
        var nAtk = npc.EquippedWeapon?.AttackBonus ?? 0;
        var nDef = npc.EquippedArmor?.DefenseBonus ?? 0;
        var nAtkStr = nAtk > 0 ? $"{npc.GetTotalAttack() - nAtk}(+{nAtk})" : $"{npc.GetTotalAttack()}";
        var nDefStr = nDef > 0 ? $"{npc.GetTotalDefense() - nDef}(+{nDef})" : $"{npc.GetTotalDefense()}";
        infoBox.AppendText($"攻击：{nAtkStr}  防御：{nDefStr}  速度：{npc.GetTotalSpeed()}\n");
        infoBox.AppendText($"HP：{npc.CurrentHP}/{npc.GetTotalMaxHP()}  MP：{npc.CurrentMP}/{npc.GetTotalMaxMP()}\n\n");
        infoBox.AppendText($"性格：{npc.Personality}\n\n");
        infoBox.AppendText($"当前内功：{npc.ActiveInternalArt?.Name ?? "无"}\n");
        infoBox.AppendText($"当前外功：{(npc.ActiveExternalArts.Count == 0 ? "无" : string.Join("、", npc.ActiveExternalArts.Select(a => a.Name)))}\n");
        infoBox.AppendText($"当前身法：{npc.ActiveLightArt?.Name ?? "无"}\n");
        infoBox.AppendText($"装备武器：{npc.EquippedWeapon?.Name ?? "无"}  防具：{npc.EquippedArmor?.Name ?? "无"}\n\n");
        infoBox.AppendText($"── 技艺 ──\n  {npc.GetCraftSkillsSummary()}\n");
        if (npc.BuddhistValue > 0)
            infoBox.AppendText($"\n佛法值：{npc.BuddhistValue}\n");
        if (ShopSystem.IsMerchant(npc.NpcRole))
            infoBox.AppendText($"\n身份：{ShopSystem.GetRoleDisplayName(npc.NpcRole)}\n");
        basicPanel.Controls.Add(infoBox);

        // ── Tab 2: 武功列表 ──
        var artsListBox = new ListBox
        {
            Location = new Point(5, 5),
            Size = new Size(420, 200),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            IntegralHeight = false,
            BorderStyle = BorderStyle.None
        };
        artsPanel.Controls.Add(artsListBox);

        var artsDetailBox = new RichTextBox
        {
            Location = new Point(5, 210),
            Size = new Size(420, 250),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        artsPanel.Controls.Add(artsDetailBox);

        // 填充武功列表
        RarityListItem.SetupOwnerDraw(artsListBox);
        foreach (var art in npc.LearnedArts)
        {
            string typeTag = art is InternalArt ? "[内]" : "[外]";
            artsListBox.Items.Add(new RarityListItem(
                $"{typeTag} {art.Name}  Lv.{art.Level}  熟练度{art.Proficiency}",
                art.RarityColor, art.RarityName));
        }
        if (artsListBox.Items.Count == 0)
            artsListBox.Items.Add("（未学习任何武功）");
        else
            artsListBox.SelectedIndex = 0;

        artsListBox.SelectedIndexChanged += (_, _) =>
        {
            artsDetailBox.Clear();
            if (artsListBox.SelectedIndex < 0 || artsListBox.SelectedIndex >= npc.LearnedArts.Count) return;
            var art = npc.LearnedArts[artsListBox.SelectedIndex];

            artsDetailBox.AppendText($"【{art.Name}】 Lv.{art.Level}/{art.MaxLevel}\n");
            artsDetailBox.AppendText($"品质：{art.RarityName}\n");
            artsDetailBox.AppendText($"{art.Description}\n\n");
            artsDetailBox.AppendText($"熟练度：{art.GetProficiencyDescription()}\n\n");

            if (art is ExternalArt ext)
            {
                artsDetailBox.AppendText($"类型：外功\n");
                artsDetailBox.AppendText($"伤害系数：{ext.DamageCoefficient:F1}\n");
                artsDetailBox.AppendText($"暴击率：{ext.CritChance:P0}\n");
                artsDetailBox.AppendText($"冷却：{ext.Cooldown}回合\n");
                artsDetailBox.AppendText($"内力消耗：{ext.MPCost}\n");
            }
            else if (art is InternalArt intArt)
            {
                artsDetailBox.AppendText($"类型：内功\n");
                artsDetailBox.AppendText($"HP加成：+{intArt.GetHPBonus()}\n");
                artsDetailBox.AppendText($"MP加成：+{intArt.GetMPBonus()}\n");
                artsDetailBox.AppendText($"攻击加成：+{intArt.GetAttackBonus()}\n");
                artsDetailBox.AppendText($"防御加成：+{intArt.GetDefenseBonus()}\n");
                artsDetailBox.AppendText($"冷却：{intArt.Cooldown}回合  内力消耗：{intArt.MPCost}\n");
            }

            if (art.Effects.Count > 0)
            {
                artsDetailBox.AppendText($"\n── 特效 ──\n");
                foreach (var eff in art.Effects)
                    artsDetailBox.AppendText($"  {eff.Description} (触发率{eff.Chance:P0})\n");
            }

            bool isActive = (art == npc.ActiveExternalArt || art == npc.ActiveInternalArt);
            artsDetailBox.AppendText($"\n状态：{(isActive ? "✓ 当前装备中" : "未装备")}");
        };

        // ── Tab 3: 背包 ──
        var bagListBox = new ListBox
        {
            Location = new Point(5, 5),
            Size = new Size(420, 220),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            IntegralHeight = false,
            BorderStyle = BorderStyle.None
        };
        RarityListItem.SetupOwnerDraw(bagListBox);
        bagPanel.Controls.Add(bagListBox);

        var bagDetailBox = new RichTextBox
        {
            Location = new Point(5, 230),
            Size = new Size(420, 230),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        bagPanel.Controls.Add(bagDetailBox);

        void RefreshBagList()
        {
            bagListBox.Items.Clear();
            if (npc.Inventory.IsEmpty)
            {
                bagListBox.Items.Add("（背包空空如也）");
            }
            else
            {
                bagListBox.Items.Add(new RarityListItem($"银两：{npc.Gold}", Color.Gray, "普通"));
                foreach (var item in npc.Inventory.Items)
                    bagListBox.Items.Add(new RarityListItem(
                        $"  {item.GetDisplayText()}", item.RarityColor, item.RarityName));
            }
        }
        RefreshBagList();

        bagListBox.SelectedIndexChanged += (_, _) =>
        {
            bagDetailBox.Clear();
            int idx = bagListBox.SelectedIndex;
            if (idx < 0) return;
            if (idx == 0 && npc.Inventory.IsEmpty) return;
            if (idx == 0) { bagDetailBox.AppendText($"银两：{npc.Gold}"); return; }
            int itemIdx = idx - 1; // 第一行是银两
            if (itemIdx < 0 || itemIdx >= npc.Inventory.Items.Count) return;
            var item = npc.Inventory.Items[itemIdx];
            bagDetailBox.AppendText($"【{item.Name}】\n{item.Description}\n\n");
            bagDetailBox.AppendText($"类型：{item.GetDisplayText()}\n");
            bagDetailBox.AppendText($"价值：{item.Value}银  数量：{item.Quantity}\n");
            if (item.HPRecovery > 0) bagDetailBox.AppendText($"恢复HP：{item.HPRecovery}\n");
            if (item.MPRecovery > 0) bagDetailBox.AppendText($"恢复MP：{item.MPRecovery}\n");
        };

        // ── Tab 4: 经历 ──
        var expBox = new RichTextBox
        {
            Location = new Point(5, 5),
            Size = new Size(420, 445),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        if (npc.LifeEvents.Count == 0)
            expBox.AppendText("（尚无江湖经历）");
        else
            foreach (var ev in npc.LifeEvents.TakeLast(15))
                expBox.AppendText($"  {ev.Display}\n");
        expPanel.Controls.Add(expBox);

        // ── Tab按钮 ──
        int btnY = 10;

        Button MakeTabButton(string text, Panel panel)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(5, btnY),
                Size = new Size(90, 45),
                TextAlign = ContentAlignment.MiddleCenter
            };
            WuxiaTheme.StyleButton(btn);
            btnY += 50;

            btn.Click += (_, _) =>
            {
                basicPanel.Visible = false;
                artsPanel.Visible = false;
                bagPanel.Visible = false;
                expPanel.Visible = false;
                panel.Visible = true;

                // 高亮当前选中的tab
                foreach (Control c in tabPanel.Controls)
                {
                    if (c is Button b)
                        WuxiaTheme.StyleButton(b, b == btn ? WuxiaTheme.Accent : null);
                }
            };
            tabPanel.Controls.Add(btn);
            return btn;
        }

        var btn1 = MakeTabButton("基本信息", basicPanel);
        var btn2 = MakeTabButton("武功", artsPanel);
        var btn3 = MakeTabButton("背包", bagPanel);
        var btn4 = MakeTabButton("经历", expPanel);

        // 默认显示基本信息并高亮
        WuxiaTheme.StyleButton(btn1, WuxiaTheme.Accent);
        btn1.ForeColor = Color.White;

        // 关闭按钮
        var closeBtn = new Button
        {
            Text = "关闭",
            Location = new Point(5, 425),
            Size = new Size(90, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        tabPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    private void ShowFactionQuestBoard(NPC giverNpc)
    {
        if (string.IsNullOrEmpty(giverNpc.FactionId)) return;
        // 仅本门弟子可查看/接取该门派任务
        var player = _engine.State.Player;
        if (player.FactionId != giverNpc.FactionId)
        {
            MessageBox.Show(this, $"你非{_engine.State.GetFactionName(giverNpc.FactionId)}弟子，不可接取本门任务。",
                "门派任务", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var quests = _engine.FactionQuests.GetAvailableQuests(giverNpc.FactionId);
        using var form = new FactionQuestBoardForm(
            _engine, $"{giverNpc.Name} · 任务榜", quests);
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    /// <summary>
    /// 显示玩家门派的任务榜（右侧面板按钮）
    /// </summary>
    private void ShowPlayerFactionQuestBoard()
    {
        var factionId = _engine.State.Player.FactionId;
        if (string.IsNullOrEmpty(factionId))
        {
            MessageBox.Show(this, "你尚未加入任何门派，无法查看门派任务。\n请先找到掌门拜入门派！",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var faction = _engine.FactionSystem.GetFaction(factionId);
        var factionName = faction?.Name ?? factionId;
        var quests = _engine.FactionQuests.GetAvailableQuests(factionId);

        using var form = new FactionQuestBoardForm(_engine, $"{factionName} · 任务榜", quests);
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    private void ShowNpcCommissions(NPC npc, List<AutoWuxia.Quests.FactionQuest> quests)
    {
        using var form = new FactionQuestBoardForm(
            _engine, $"{npc.Name} 的委托", quests);
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    private void ShowQuestList()
    {
        using var form = new QuestListForm(_engine);
        WuxiaTheme.ApplyScaling(form);
        var r = form.ShowDialog(this);

        // 华山论剑通关后,结束画面请求回首页 → 直达首页(旅程已圆满,不弹存档询问)
        if (form.ReturnToStart)
        {
            ReturnToStart = true;
            Close();
            return;
        }

        if (r == DialogResult.Abort)
        {
            // 副本 GameOver 透传
            MessageBox.Show(this, "胜败乃兵家常事,英雄请重新来过", "游戏结束",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Exit();
            return;
        }
        RefreshAll();
    }

    /// <summary>
    /// 尝试自动推进进行中的任务步骤，并将结果写入日志。
    /// </summary>
    private void TryAutoAdvanceQuests(string actionType, string? targetId)
    {
        var (logs, dialogues) = QuestAutoAdvance.TryAdvanceAll(_engine.State.Player, actionType, targetId, _engine.Config);
        foreach (var log in logs)
            _logBox.AppendSuccess(log);
        // 步骤完成时播放剧情对话(逐段播放)
        foreach (var script in dialogues)
            StoryDialogueForm.Show(this, script, _engine.State, null);
    }

    /// <summary>
    /// 进入场景后的统一处理:推进 go 步骤 + 自动接取场景触发任务。
    /// 本地步行与跨城镇旅行都走这里。
    /// </summary>
    private void OnSceneEntered(string? sceneId)
    {
        if (string.IsNullOrEmpty(sceneId)) return;
        TryAutoAdvanceQuests("go", sceneId);
        OfferSceneTriggeredQuests(sceneId);
    }

    /// <summary>进入场景时自动接取 triggerSceneId==sceneId 的链式任务(弹委托框)。</summary>
    private void OfferSceneTriggeredQuests(string sceneId)
    {
        var player = _engine.State.Player;
        bool Match(Config.Models.QuestConfig cfg)
        {
            if (!string.Equals(cfg.Type, "chain", StringComparison.OrdinalIgnoreCase)) return false;
            if (cfg.AutoAcceptOnly) return false;
            if (string.IsNullOrEmpty(cfg.TriggerSceneId) || cfg.TriggerSceneId != sceneId) return false;
            if (player.QuestLog.Any(q => q.Id == cfg.Id)) return false;
            if (cfg.PrerequisiteQuestIds.Count > 0)
            {
                foreach (var preId in cfg.PrerequisiteQuestIds)
                {
                    var pre = player.QuestLog.FirstOrDefault(q => q.Id == preId);
                    if (pre == null || pre.Status == QuestStatus.InProgress || pre.Status == QuestStatus.Failed)
                        return false;
                }
            }
            if (cfg.ExclusiveWithQuestIds.Count > 0)
            {
                foreach (var exId in cfg.ExclusiveWithQuestIds)
                    if (player.QuestLog.Any(q => q.Id == exId)) return false;
            }
            return true;
        }

        Config.Models.QuestConfig? quest = null;
        foreach (var (id, cfg) in _engine.Config.Quests)
            if (Match(cfg)) { quest = cfg; break; }
        if (quest == null)
            foreach (var cfg in _engine.State.RuntimeQuests)
                if (Match(cfg)) { quest = cfg; break; }
        if (quest == null) return;

        if (WuxiaConfirmBox.Show(this, "任务委托",
            $"【{quest.Name}】\n\n{quest.Description}\n\n是否接受该任务?",
            "接受", "婉拒", WuxiaConfirmStyle.Neutral))
        {
            AcceptChainQuest(quest.Id, null!);
        }
    }

    /// <summary>
    /// 查询该 NPC 当前可委派给玩家的链式任务(玩家未接取的)。
    /// 供 DialogueForm 在聊天流中呈现。
    /// </summary>
    private List<ChainQuestOffer> GetOfferableQuests(NPC npc)
    {
        var offers = new List<ChainQuestOffer>();
        var player = _engine.State.Player;

        // 处理单个任务配置的本地函数(配置文件任务 + 运行时任务池共用)
        bool TryOffer(Config.Models.QuestConfig cfg)
        {
            if (!string.Equals(cfg.Type, "chain", StringComparison.OrdinalIgnoreCase)) return false;
            if (cfg.TriggerNpcId != npc.Id) return false;
            // 仅供自动接取的任务(如失败后的惩罚任务)不出现在可委托列表
            if (cfg.AutoAcceptOnly) return false;
            if (player.QuestLog.Any(q => q.Id == cfg.Id)) return false;
            // 前置任务：须全部完成(已完成/已领奖)方可接取(用于"多线汇聚"剧情,如少室山大战)
            if (cfg.PrerequisiteQuestIds.Count > 0)
            {
                foreach (var preId in cfg.PrerequisiteQuestIds)
                {
                    var pre = player.QuestLog.FirstOrDefault(q => q.Id == preId);
                    if (pre == null
                        || pre.Status == QuestStatus.InProgress
                        || pre.Status == QuestStatus.Failed)
                        return false;
                }
            }
            // 互斥任务:玩家任务日志中已有任一互斥任务(任何状态)则不可接取(正/恶线互斥)
            if (cfg.ExclusiveWithQuestIds.Count > 0)
            {
                foreach (var exId in cfg.ExclusiveWithQuestIds)
                {
                    if (player.QuestLog.Any(q => q.Id == exId))
                        return false;
                }
            }
            // 门派归属:RequireSameFaction 时玩家必须与发布者同门派
            if (cfg.RequireSameFaction)
            {
                if (string.IsNullOrEmpty(npc.FactionId)) return false;
                if (player.FactionId != npc.FactionId) return false;
            }
            // 好感度门槛：未达到则不弹委托（剧情上"还不熟"，由玩家继续对话拉好感度）
            if (cfg.MinFavorabilityToOffer > 0)
            {
                var relation = npc.GetRelation(player.Id);
                if (relation.Favorability < cfg.MinFavorabilityToOffer) return false;
            }
            // 武功等级门槛：玩家任一武功 Level 须≥要求(用于"武功学到中段才能触发"剧情)
            if (cfg.MinAnyArtLevel > 0)
            {
                if (!player.LearnedArts.Any(a => a.Level >= cfg.MinAnyArtLevel)) return false;
            }
            offers.Add(new ChainQuestOffer(cfg.Id, cfg.Name, cfg.Description));
            return true;
        }

        // 1. 配置文件任务
        foreach (var (id, cfg) in _engine.Config.Quests)
            TryOffer(cfg);

        // 2. 运行时任务池(年度AI生成的大事件)
        foreach (var cfg in _engine.State.RuntimeQuests)
            TryOffer(cfg);

        return offers;
    }

    /// <summary>
    /// 接取指定链式任务(DialogueForm 确认后回调)。
    /// </summary>
    private void AcceptChainQuest(string questId, NPC npc)
    {
        // 先查配置文件任务,再查运行时任务池(年度AI生成的大事件)
        Config.Models.QuestConfig? cfg = null;
        if (!_engine.Config.Quests.TryGetValue(questId, out cfg))
            cfg = _engine.State.RuntimeQuests.FirstOrDefault(q => q.Id == questId);

        if (cfg != null)
        {
            var chain = ChainQuest.FromConfig(cfg);
            _engine.State.Player.AddQuest(chain);

            // 接任务时播放剧情对话(若有),播完再提示
            StoryDialogueForm.Show(this, chain.IntroDialogue, _engine.State, () =>
            {
                _logBox.AppendSuccess($"接受任务：{cfg.Name}");
                if (chain.CurrentStep != null)
                    _logBox.AppendSuccess($"当前步骤：{chain.CurrentStep.Description}");
            });

            // 任务剧情副作用:让相关 NPC 现身
            HandleQuestAcceptSideEffects(questId);
        }
    }

    /// <summary>
    /// 处理任务接取后的副作用(如让某些隐藏 NPC 现身)
    /// </summary>
    private void HandleQuestAcceptSideEffects(string questId)
    {
        switch (questId)
        {
            case "riyue_dongfang_test":
                // 东方不败在黑木崖后山现身
                RevealNpc("dongfang_bubai");
                _logBox.AppendSuccess("（杨代教主低声道:东方教主就在崖后,前去拜见吧。）");
                break;
            case "tianlong_qiaofeng":
                // 乔峰线:段延庆与萧远山现身少室山(供身世线交手)
                RevealNpc("duan_yanqing");
                RevealNpc("xiao_yuanshan");
                _logBox.AppendSuccess("（少室山后山风云变色,两道身影若隐若现——段延庆与萧远山已在此候你。）");
                break;
            case "tianlong_xuzhu":
                // 虚竹线:无崖子现身灵鹫宫(传北冥神功)
                RevealNpc("wu_yazi");
                _logBox.AppendSuccess("（灵鹫宫深处,一位残年老人正等你前来,似有厚赠。）");
                break;
            case "tianlong_shaoshi":
                // 少室山大战:群雄齐聚少室山
                foreach (var id in new[] { "murong_fu", "ding_chunqiu", "jiumozhi", "you_tanzhi", "murong_bo" })
                    RevealNpc(id);
                _logBox.AppendSuccess("（少室山前群雄齐聚——慕容复、丁春秋、鸠摩智、游坦之各据一方,萧远山、慕容博亦在,大战一触即发!）");
                break;
            case "yitian_main":
                // 围攻光明顶:成昆潜伏光明顶,将其从少林移至光明顶供交手
                if (_engine.State.AllNPCs.TryGetValue("cheng_kun", out var chengKun))
                {
                    chengKun.IsHidden = false;
                    chengKun.DefaultSceneId = "guangming_scene";
                    chengKun.Schedule["清晨"] = "guangming_scene";
                    chengKun.Schedule["白天"] = "guangming_scene";
                    chengKun.Schedule["黄昏"] = "guangming_scene";
                    chengKun.Schedule["夜晚"] = "guangming_scene";
                    _engine.UpdateSceneNPCsExternal();
                    _logBox.AppendSuccess("（据张无忌所言,成昆那老贼就潜伏在光明顶上,此番围攻定要将其揪出!）");
                }
                break;
        }
    }

    /// <summary>取消 NPC 的 IsHidden 标记并刷新场景列表</summary>
    private void RevealNpc(string npcId)
    {
        if (_engine.State.AllNPCs.TryGetValue(npcId, out var npc) && npc.IsHidden)
        {
            npc.IsHidden = false;
            _engine.UpdateSceneNPCsExternal();
        }
    }

    /// <summary>
    /// 刷新武功列表的详情显示及"装备此武功"按钮可用状态
    /// </summary>
    private static void artsListBox_RefreshDetail(ListBox artsListBox, RichTextBox artsDetailBox, Button equipBtn, Characters.Player p)
    {
        artsDetailBox.Clear();
        equipBtn.Enabled = false;
        if (artsListBox.SelectedIndex < 0 || artsListBox.SelectedIndex >= p.LearnedArts.Count) return;
        var art = p.LearnedArts[artsListBox.SelectedIndex];

        artsDetailBox.AppendText($"【{art.Name}】 Lv.{art.Level}/{art.MaxLevel}\n");
        artsDetailBox.AppendText($"品质：{art.RarityName}\n");
        artsDetailBox.AppendText($"{art.Description}\n\n");
        artsDetailBox.AppendText($"熟练度：{art.GetProficiencyDescription()}\n\n");

        if (art is ExternalArt ext)
        {
            artsDetailBox.AppendText($"类型：外功\n");
            artsDetailBox.AppendText($"伤害系数：{ext.DamageCoefficient:F1}\n");
            artsDetailBox.AppendText($"暴击率：{ext.CritChance:P0}\n");
            artsDetailBox.AppendText($"冷却：{ext.Cooldown}回合  内力消耗：{ext.MPCost}\n");
        }
        else if (art is InternalArt intArt)
        {
            artsDetailBox.AppendText($"类型：内功\n");
            artsDetailBox.AppendText($"HP加成：+{intArt.GetHPBonus()}\n");
            artsDetailBox.AppendText($"MP加成：+{intArt.GetMPBonus()}\n");
            artsDetailBox.AppendText($"攻击加成：+{intArt.GetAttackBonus()}\n");
            artsDetailBox.AppendText($"防御加成：+{intArt.GetDefenseBonus()}\n");
            artsDetailBox.AppendText($"冷却：{intArt.Cooldown}回合  内力消耗：{intArt.MPCost}\n");
        }
        else if (art is LightArt la)
        {
            artsDetailBox.AppendText($"类型：身法(轻功)\n");
            artsDetailBox.AppendText($"速度加成：+{la.GetSpeedBonus()}\n");
            if (la.GetAttackBonus() > 0)
                artsDetailBox.AppendText($"攻击加成：+{la.GetAttackBonus()}\n");
            if (la.GetDefenseBonus() > 0)
                artsDetailBox.AppendText($"防御加成：+{la.GetDefenseBonus()}\n");
            artsDetailBox.AppendText($"(身法只能装备一种)\n");
        }

        if (art.Effects.Count > 0)
        {
            artsDetailBox.AppendText($"\n── 特效 ──\n");
            foreach (var eff in art.Effects)
                artsDetailBox.AppendText($"  {eff.Description} (触发率{eff.Chance:P0})\n");
        }

        bool isActive = (art is ExternalArt eaArt
            ? p.ActiveExternalArts.Any(a => a.Id == eaArt.Id)
            : art == p.ActiveInternalArt || art == p.ActiveLightArt);
        artsDetailBox.AppendText($"\n状态：{(isActive ? "✓ 当前装备中" : "未装备")}");
        equipBtn.Enabled = true;
        if (art is ExternalArt)
            equipBtn.Text = isActive ? "卸下此外功" : "装备此外功";
        else
        {
            equipBtn.Enabled = !isActive;
            equipBtn.Text = isActive ? "已装备" : "装备此武功";
        }
    }

    /// <summary>
    /// 放大查看人物头像:模态窗口居中显示原图(无原图时显示默认头像放大版)。
    /// 点击图片或窗口任意处、按 Esc 关闭。
    /// </summary>
    private void ShowPortraitPreview(string? portraitPath, string name)
    {
        var img = PortraitHelper.LoadPortrait(portraitPath);
        if (img == null)
            img = PortraitHelper.GenerateDefaultPortrait(name, 256);

        // 按原图比例放大,上限 480x480
        int maxSize = 480;
        int w = img.Width, h = img.Height;
        if (w > maxSize || h > maxSize)
        {
            double scale = (double)maxSize / Math.Max(w, h);
            w = (int)(w * scale);
            h = (int)(h * scale);
        }
        var shown = PortraitHelper.ResizeImage(img, w, h);

        using var form = new Form
        {
            Text = $"{name} - 头像",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(28, 22, 16),
            ClientSize = new Size(w + 24, h + 24),
            KeyPreview = true
        };

        var pic = new PictureBox
        {
            Location = new Point(12, 12),
            Size = new Size(w, h),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand,
            Image = shown
        };
        // 默认头像(非文件)ResizeImage 已是独立副本,可直接用;原图来自缓存则 PictureBox 引用即可
        form.Controls.Add(pic);

        void CloseForm(object? s, EventArgs e) => form.Close();
        pic.Click += CloseForm;
        form.Click += CloseForm;
        form.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) form.Close(); };

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    private void ShowPlayerInfo()
    {
        var p = _engine.State.Player;

        var form = new Form
        {
            Text = $"个人信息 - {p.Name}",
            Size = new Size(560, 520),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        // 左侧Tab按钮面板
        var tabPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(100, 475),
            BackColor = WuxiaTheme.PanelBack
        };
        form.Controls.Add(tabPanel);

        // 右侧内容面板
        var contentPanel = new Panel
        {
            Location = new Point(105, 5),
            Size = new Size(430, 465),
            BackColor = WuxiaTheme.Surface
        };
        form.Controls.Add(contentPanel);

        var basicPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent };
        var artsPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent, Visible = false };
        var bagPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent, Visible = false };
        var expPanel = new Panel { Location = Point.Empty, Size = contentPanel.Size, BackColor = Color.Transparent, Visible = false };
        contentPanel.Controls.AddRange(new Control[] { basicPanel, artsPanel, bagPanel, expPanel });

        // ── Tab 1: 基本信息 ──
        // 玩家创建时选择的头像
        var playerPortrait = new PictureBox
        {
            Location = new Point(5, 5),
            Size = new Size(80, 80),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Cursor = Cursors.Hand
        };
        playerPortrait.Image = PortraitHelper.GetPortraitOrDefault(p.PortraitPath, p.Name, 80);
        playerPortrait.Click += (_, _) => ShowPortraitPreview(p.PortraitPath, p.Name);
        basicPanel.Controls.Add(playerPortrait);

        var infoBox = new RichTextBox
        {
            Location = new Point(90, 5),
            Size = new Size(335, 455),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        infoBox.AppendText($"【{p.Name}】{p.Description}\n\n");
        infoBox.AppendText($"江湖阅历：Lv.{p.JianghuLevel}  经验：{p.JianghuExp}/{p.GetExpToNextLevel()}\n");
        infoBox.AppendText($"门派：{_engine.State.GetFactionName(p.FactionId)}\n");
        infoBox.AppendText($"善恶：{p.Karma} ({KarmaSystem.GetKarmaDescription(p.Karma)})\n");
        infoBox.AppendText($"心情：{p.Mood} ({MoodSystem.GetMoodDescription(p.Mood)})\n");
        infoBox.AppendText($"银两：{p.Gold}  佛法值：{p.BuddhistValue}\n");
        infoBox.AppendText($"声望：{p.Reputation}\n");
        if (p.FactionContributions.Count > 0)
        {
            var contribStr = string.Join("  ", p.FactionContributions.Select(kv => $"{kv.Key}:{kv.Value}"));
            infoBox.AppendText($"门派贡献：{contribStr}\n");
        }
        infoBox.AppendText("\n");
        var pAtk = p.EquippedWeapon?.AttackBonus ?? 0;
        var pDef = p.EquippedArmor?.DefenseBonus ?? 0;
        var pAtkStr = pAtk > 0 ? $"{p.GetTotalAttack() - pAtk}(+{pAtk})" : $"{p.GetTotalAttack()}";
        var pDefStr = pDef > 0 ? $"{p.GetTotalDefense() - pDef}(+{pDef})" : $"{p.GetTotalDefense()}";
        infoBox.AppendText($"攻击：{pAtkStr}  防御：{pDefStr}  速度：{p.GetTotalSpeed()}\n");
        infoBox.AppendText($"HP：{p.CurrentHP}/{p.GetTotalMaxHP()}  MP：{p.CurrentMP}/{p.GetTotalMaxMP()}\n");
        infoBox.AppendText($"悟性：{p.Talent}/10 (熟练度倍率 ×{p.EffectiveTrainingMultiplier:F1}" +
            (p.TrainingSpeedBonus > 0 ? $", 含修炼加成 +{p.TrainingSpeedBonus*100:0}%)\n" : ")\n"));
        // 健康度
        var healthColor = p.Health > 60 ? "健康" : (p.Health > 30 ? "虚弱" : "危重");
        infoBox.AppendText($"健康度：{p.Health}/{p.MaxHealth} ({healthColor})\n\n");
        // 标签
        if (p.Tags.Count > 0)
        {
            infoBox.AppendText($"── 标签 ──\n");
            foreach (var tag in p.Tags)
            {
                infoBox.AppendText($"  {tag.GetSummary()}  {tag.Description}\n");
            }
            infoBox.AppendText("\n");
        }
        else
        {
            infoBox.AppendText("\n");
        }
        infoBox.AppendText($"当前内功：{p.ActiveInternalArt?.Name ?? "无"}\n");
        infoBox.AppendText($"当前外功：{(p.ActiveExternalArts.Count == 0 ? "无" : string.Join("、", p.ActiveExternalArts.Select(a => a.Name)))}\n");
        infoBox.AppendText($"当前身法：{p.ActiveLightArt?.Name ?? "无"}\n");
        infoBox.AppendText($"装备武器：{p.EquippedWeapon?.Name ?? "无"}  防具：{p.EquippedArmor?.Name ?? "无"}\n\n");
        infoBox.AppendText($"── 技艺 ──\n  {p.GetCraftSkillsSummary()}\n");
        basicPanel.Controls.Add(infoBox);

        // ── Tab 2: 武功 ──
        var artsListBox = new ListBox
        {
            Location = new Point(5, 5),
            Size = new Size(420, 200),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            IntegralHeight = false,
            BorderStyle = BorderStyle.None
        };
        artsPanel.Controls.Add(artsListBox);

        // 装备/切换按钮(轻功多本时可在这里切换)
        var equipBtn = new Button
        {
            Text = "装备此武功",
            Location = new Point(305, 430),
            Size = new Size(120, 28),
            BackColor = WuxiaTheme.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(9f),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        artsPanel.Controls.Add(equipBtn);

        var artsDetailBox = new RichTextBox
        {
            Location = new Point(5, 210),
            Size = new Size(420, 215),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        artsPanel.Controls.Add(artsDetailBox);

        RarityListItem.SetupOwnerDraw(artsListBox);
        foreach (var art in p.LearnedArts)
        {
            string typeTag = art is InternalArt ? "[内]" : art is LightArt ? "[轻]" : "[外]";
            artsListBox.Items.Add(new RarityListItem(
                $"{typeTag} {art.Name}  Lv.{art.Level}  熟练度{art.Proficiency}",
                art.RarityColor, art.RarityName));
        }
        if (artsListBox.Items.Count == 0)
            artsListBox.Items.Add("（未学习任何武功）");
        else
            artsListBox.SelectedIndex = 0;

        // 切换激活武功
        equipBtn.Click += (_, _) =>
        {
            if (artsListBox.SelectedIndex < 0 || artsListBox.SelectedIndex >= p.LearnedArts.Count) return;
            var art = p.LearnedArts[artsListBox.SelectedIndex];
            if (art is InternalArt ia) { p.ActiveInternalArt = ia; _logBox.AppendSuccess($"已切换内功:{ia.Name}"); }
            else if (art is LightArt la) { p.ActiveLightArt = la; _logBox.AppendSuccess($"已切换身法:{la.Name} (速度+{la.GetSpeedBonus()})"); }
            else if (art is ExternalArt ea)
            {
                // 外功:已装备就卸下,未装备就加入(满则替换最后一本)
                int existing = p.ActiveExternalArts.FindIndex(a => a.Id == ea.Id);
                if (existing >= 0)
                {
                    p.ActiveExternalArts.RemoveAt(existing);
                    _logBox.AppendSuccess($"已卸下外功:{ea.Name}");
                }
                else if (p.ActiveExternalArts.Count < CharacterBase.MaxActiveExternalArts)
                {
                    p.ActiveExternalArts.Add(ea);
                    _logBox.AppendSuccess($"已装备外功:{ea.Name}");
                }
                else
                {
                    var replaced = p.ActiveExternalArts[^1];
                    p.ActiveExternalArts[^1] = ea;
                    _logBox.AppendSuccess($"已装备外功:{ea.Name} (替换 {replaced.Name})");
                }
            }
            // 触发刷新详情
            artsListBox.SelectedIndex = artsListBox.SelectedIndex;
            artsListBox_RefreshDetail(artsListBox, artsDetailBox, equipBtn, p);
            RefreshAll();
        };

        artsListBox.SelectedIndexChanged += (_, _) =>
        {
            artsListBox_RefreshDetail(artsListBox, artsDetailBox, equipBtn, p);
        };
        // 初始刷新
        if (artsListBox.Items.Count > 0 && p.LearnedArts.Count > 0)
            artsListBox_RefreshDetail(artsListBox, artsDetailBox, equipBtn, p);

        // 门派武功进度（追加在artsDetailBox下方不可见时不影响）

        // ── Tab 3: 背包 ──
        var bagListBox = new ListBox
        {
            Location = new Point(5, 5),
            Size = new Size(420, 220),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            IntegralHeight = false,
            BorderStyle = BorderStyle.None
        };
        RarityListItem.SetupOwnerDraw(bagListBox);
        bagPanel.Controls.Add(bagListBox);

        var bagDetailBox = new RichTextBox
        {
            Location = new Point(5, 230),
            Size = new Size(420, 175),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        bagPanel.Controls.Add(bagDetailBox);

        var openFullBagBtn = new Button
        {
            Text = "打开完整背包（可使用/修炼）",
            Location = new Point(5, 415),
            Size = new Size(420, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(openFullBagBtn, Color.FromArgb(180, 140, 80));
        openFullBagBtn.Click += (_, _) => { form.Close(); ShowBackpack(); };
        bagPanel.Controls.Add(openFullBagBtn);

        void RefreshPlayerBag()
        {
            bagListBox.Items.Clear();
            bagListBox.Items.Add($"银两：{p.Gold}");
            if (p.Inventory.IsEmpty)
            {
                bagListBox.Items.Add("（背包空空如也）");
            }
            else
            {
                foreach (var item in p.Inventory.Items)
                    bagListBox.Items.Add(new RarityListItem(
                        $"  {item.GetDisplayText()}", item.RarityColor, item.RarityName));
            }
        }
        RefreshPlayerBag();

        bagListBox.SelectedIndexChanged += (_, _) =>
        {
            bagDetailBox.Clear();
            int idx = bagListBox.SelectedIndex;
            if (idx <= 0) { bagDetailBox.AppendText($"银两：{p.Gold}"); return; }
            int itemIdx = idx - 1;
            if (itemIdx < 0 || itemIdx >= p.Inventory.Items.Count) return;
            var item = p.Inventory.Items[itemIdx];
            bagDetailBox.AppendText($"【{item.Name}】\n{item.Description}\n\n");
            bagDetailBox.AppendText($"类型：{item.GetDisplayText()}\n");
            bagDetailBox.AppendText($"价值：{item.Value}银  数量：{item.Quantity}\n");
            if (item.HPRecovery > 0) bagDetailBox.AppendText($"恢复HP：{item.HPRecovery}\n");
            if (item.MPRecovery > 0) bagDetailBox.AppendText($"恢复MP：{item.MPRecovery}\n");
            if (item.IsManual) bagDetailBox.AppendText($"\n★ 武功秘籍 - 可在背包中修炼");
        };

        // ── Tab 4: 经历 ──
        var expBox = new RichTextBox
        {
            Location = new Point(5, 5),
            Size = new Size(420, 445),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        if (p.LifeEvents.Count == 0)
            expBox.AppendText("（尚无江湖经历）");
        else
            foreach (var ev in p.LifeEvents.TakeLast(15))
                expBox.AppendText($"  {ev.Display}\n");
        expPanel.Controls.Add(expBox);

        // ── Tab按钮 ──
        int btnY = 10;

        Button MakeTabButton(string text, Panel panel)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(5, btnY),
                Size = new Size(90, 45),
                TextAlign = ContentAlignment.MiddleCenter
            };
            WuxiaTheme.StyleButton(btn);
            btnY += 50;

            btn.Click += (_, _) =>
            {
                basicPanel.Visible = false;
                artsPanel.Visible = false;
                bagPanel.Visible = false;
                expPanel.Visible = false;
                panel.Visible = true;

                foreach (Control c in tabPanel.Controls)
                {
                    if (c is Button b)
                        WuxiaTheme.StyleButton(b, b == btn ? WuxiaTheme.Accent : null);
                }
            };
            tabPanel.Controls.Add(btn);
            return btn;
        }

        var btn1 = MakeTabButton("基本信息", basicPanel);
        var btn2 = MakeTabButton("武功", artsPanel);
        var btn3 = MakeTabButton("背包", bagPanel);
        var btn4 = MakeTabButton("经历", expPanel);

        // 默认显示基本信息并高亮
        WuxiaTheme.StyleButton(btn1, WuxiaTheme.Accent);
        btn1.ForeColor = Color.White;

        // 关闭按钮
        var closeBtn = new Button
        {
            Text = "关闭",
            Location = new Point(5, 425),
            Size = new Size(90, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        tabPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    private void ShowMapInfo()
    {
        if (_engine.WorldMap == null)
        {
            _logBox.AppendWarning("地图未加载。");
            return;
        }

        var currentLoc = _engine.GetCurrentLocation();
        var player = _engine.State.Player;

        var form = new Form
        {
            Text = "天下地图",
            Size = new Size(840, 760),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        // 当前城镇标签
        var currentLabel = new Label
        {
            Text = $"当前：{currentLoc?.Name ?? "未知"}  银两：{player.Gold}",
            Location = new Point(10, 5),
            Size = new Size(400, 22),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(currentLabel);

        // 地图画布（可滚动+拖动）
        var mapPanel = new Panel
        {
            Location = new Point(10, 30),
            Size = new Size(800, 550),
            BackColor = Color.FromArgb(245, 235, 210),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };
        form.Controls.Add(mapPanel);

        // 内部内容面板
        var contentPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(1050, 800),
            BackColor = Color.FromArgb(245, 235, 210)
        };
        mapPanel.Controls.Add(contentPanel);

        // ── 鼠标拖动逻辑 ──
        bool isDragging = false;
        Point dragStart = Point.Empty;
        Point scrollStart = Point.Empty;

        void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStart = e.Location;
                scrollStart = mapPanel.AutoScrollPosition;
                // 将sender的坐标转换到mapPanel坐标系
                if (sender is Control ctrl && ctrl != mapPanel)
                {
                    dragStart = mapPanel.PointToClient(ctrl.PointToScreen(e.Location));
                }
            }
        }

        void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var currentPos = e.Location;
                if (sender is Control ctrl && ctrl != mapPanel)
                {
                    currentPos = mapPanel.PointToClient(ctrl.PointToScreen(e.Location));
                }
                int dx = currentPos.X - dragStart.X;
                int dy = currentPos.Y - dragStart.Y;
                mapPanel.AutoScrollPosition = new Point(
                    Math.Max(0, -scrollStart.X - dx),
                    Math.Max(0, -scrollStart.Y - dy));
            }
        }

        void OnMouseUp(object? sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        // 给mapPanel和contentPanel都绑定拖动事件
        mapPanel.MouseDown += OnMouseDown;
        mapPanel.MouseMove += OnMouseMove;
        mapPanel.MouseUp += OnMouseUp;
        contentPanel.MouseDown += OnMouseDown;
        contentPanel.MouseMove += OnMouseMove;
        contentPanel.MouseUp += OnMouseUp;

        // 绘制路线线
        void DrawRoutes(Graphics g)
        {
            // 城镇标签由统一缩放器调整了坐标，路线也必须使用同一坐标系。
            using var pen = new Pen(Color.FromArgb(80, 180, 140, 80), Math.Max(1, WuxiaTheme.V(2)));
            foreach (var route in _engine.WorldMap.Routes)
            {
                if (_engine.WorldMap.Locations.TryGetValue(route.From, out var fromLoc) &&
                    _engine.WorldMap.Locations.TryGetValue(route.To, out var toLoc))
                {
                    g.DrawLine(pen,
                        WuxiaTheme.V(fromLoc.MapX), WuxiaTheme.V(fromLoc.MapY),
                        WuxiaTheme.V(toLoc.MapX), WuxiaTheme.V(toLoc.MapY));
                }
            }
        }

        contentPanel.Paint += (_, e) => DrawRoutes(e.Graphics);

        // 底部信息栏
        var infoPanel = new Panel
        {
            Location = new Point(10, 585),
            Size = new Size(800, 80),
            BackColor = WuxiaTheme.PanelBackAlt
        };
        form.Controls.Add(infoPanel);

        var infoLabel = new Label
        {
            Text = "点击城镇查看详情，鼠标拖动可平移地图",
            Location = new Point(10, 5),
            Size = new Size(520, 20),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f)
        };
        infoPanel.Controls.Add(infoLabel);

        var detailLabel = new Label
        {
            Text = "",
            Location = new Point(10, 26),
            Size = new Size(520, 20),
            ForeColor = WuxiaTheme.TextMuted,
            Font = WuxiaTheme.UiFont(9f)
        };
        infoPanel.Controls.Add(detailLabel);

        var pathLabel = new Label
        {
            Text = "",
            Location = new Point(10, 48),
            Size = new Size(520, 20),
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(8.5f)
        };
        infoPanel.Controls.Add(pathLabel);

        var travelBtn = new Button
        {
            Text = "前往",
            Location = new Point(560, 12),
            Size = new Size(100, 50),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(travelBtn, WuxiaTheme.Success);
        infoPanel.Controls.Add(travelBtn);

        var closeBtn = new Button
        {
            Text = "关闭",
            Location = new Point(680, 12),
            Size = new Size(100, 50),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        infoPanel.Controls.Add(closeBtn);

        string? selectedLocationId = null;

        // 为每个城镇创建点击区域
        foreach (var (locId, loc) in _engine.WorldMap.Locations)
        {
            bool isCurrent = currentLoc?.Id == locId;
            int labelWidth = Math.Max(loc.Name.Length * 16 + 16, 70);
            int labelHeight = 32;

            var locLabel = new Label
            {
                Text = loc.Name,
                Location = new Point(loc.MapX - labelWidth / 2, loc.MapY - labelHeight / 2),
                Size = new Size(labelWidth, labelHeight),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Font = WuxiaTheme.UiFont(isCurrent ? 10f : 9f, isCurrent ? FontStyle.Bold : FontStyle.Regular)
            };

            locLabel.BackColor = isCurrent ? WuxiaTheme.Accent : Color.FromArgb(255, 240, 200);
            locLabel.ForeColor = isCurrent ? Color.White : WuxiaTheme.Text;
            locLabel.BorderStyle = BorderStyle.FixedSingle;
            locLabel.Padding = new Padding(4, 2, 4, 2);

            if (isCurrent)
                locLabel.Text = $"{loc.Name} ←";

            // 悬停提示
            var scenes = string.Join("、", loc.SceneIds.Select(sid =>
                _engine.State.AllScenes.TryGetValue(sid, out var sc) ? sc.Name : sid));
            var tooltip = new ToolTip();
            tooltip.SetToolTip(locLabel, $"{loc.Name} [{loc.Region}]\n包含：{scenes}");

            // 给标签也绑定拖动事件（这样在标签上拖拽也能平移地图）
            locLabel.MouseDown += OnMouseDown;
            locLabel.MouseMove += OnMouseMove;
            locLabel.MouseUp += OnMouseUp;

            locLabel.Click += (_, _) =>
            {
                // 如果是拖动操作则不触发点击
                selectedLocationId = locId;

                if (isCurrent)
                {
                    infoLabel.Text = $"{loc.Name} — 你在这里";
                    detailLabel.Text = $"包含场景：{scenes}";
                    pathLabel.Text = "";
                    travelBtn.Enabled = false;
                }
                else
                {
                    // 使用Dijkstra最短路径
                    var pathResult = _engine.WorldMap!.FindShortestPath(currentLoc?.Id ?? "", locId);
                    if (pathResult != null)
                    {
                        var (totalDist, totalTime, path) = pathResult.Value;
                        int cost = (int)(totalDist * 0.5);
                        infoLabel.Text = $"前往 {loc.Name}  距离：{totalDist:F0}  费用：{cost}银  耗时：{totalTime:F1}时辰";
                        detailLabel.Text = $"包含场景：{scenes}";

                        // 显示路线
                        if (path.Count > 2)
                        {
                            var pathNames = path.Select(id =>
                                _engine.WorldMap!.Locations.TryGetValue(id, out var l) ? l.Name : id);
                            pathLabel.Text = $"路线：{string.Join(" → ", pathNames)}";
                        }
                        else
                        {
                            pathLabel.Text = "直达路线";
                        }

                        travelBtn.Enabled = true;
                        WuxiaTheme.StyleButton(travelBtn, player.Gold >= cost ? WuxiaTheme.Success : Color.FromArgb(220, 180, 80));
                    }
                    else
                    {
                        infoLabel.Text = $"{loc.Name} — 无法到达";
                        detailLabel.Text = $"包含场景：{scenes}";
                        pathLabel.Text = "";
                        travelBtn.Enabled = false;
                    }
                }

                // 高亮选中的城镇
                foreach (Control c in contentPanel.Controls)
                {
                    if (c is Label lbl)
                    {
                        bool isSel = lbl == locLabel;
                        bool isCur = currentLoc?.Id == locId;
                        lbl.BackColor = isSel ? WuxiaTheme.Accent
                                    : isCur ? WuxiaTheme.AccentSoft
                                    : Color.FromArgb(255, 240, 200);
                        lbl.ForeColor = (isSel || isCur) ? Color.White : WuxiaTheme.Text;
                    }
                }
            };

            contentPanel.Controls.Add(locLabel);
        }

        travelBtn.Click += (_, _) =>
        {
            if (selectedLocationId == null) return;
            bool success = _engine.TravelToLocation(selectedLocationId);
            if (success)
            {
                form.Close();
                OnSceneEntered(_engine.State.CurrentSceneId);
                RefreshAll();
            }
            else
            {
                MessageBox.Show("旅行失败，请检查体力。", "提示");
            }
        };

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 背包弹窗 ──

    private void ShowBackpack()
    {
        var p = _engine.State.Player;

        var form = new Form
        {
            Text = $"背包 - 银两:{p.Gold}",
            Size = new Size(500, 520),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        var goldLabel = new Label
        {
            Text = $"银两：{p.Gold}",
            Location = new Point(10, 10),
            Size = new Size(200, 25),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(goldLabel);

        // 物品列表
        var listBox = new ListBox
        {
            Location = new Point(10, 40),
            Size = new Size(460, 280),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            IntegralHeight = false
        };
        RarityListItem.SetupOwnerDraw(listBox);
        form.Controls.Add(listBox);

        // 详情区
        var detailBox = new RichTextBox
        {
            Location = new Point(10, 325),
            Size = new Size(460, 80),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(detailBox);

        // 操作按钮区
        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 415),
            Size = new Size(460, 55),
            FlowDirection = FlowDirection.LeftToRight
        };
        form.Controls.Add(btnPanel);

        void RefreshList()
        {
            listBox.Items.Clear();
            foreach (var item in p.Inventory.Items)
                listBox.Items.Add(new RarityListItem(
                    $"{item.GetDisplayText()}  (价值:{item.Value}银)",
                    item.RarityColor, item.RarityName));
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
        }
        RefreshList();

        // 装备按钮(武器/防具)
        var equipBtn = new Button
        {
            Text = "装备",
            Size = new Size(100, 40),
            Margin = new Padding(3),
            Cursor = Cursors.Hand,
            Visible = false
        };
        WuxiaTheme.StyleButton(equipBtn, WuxiaTheme.Accent);
        equipBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= p.Inventory.Items.Count) return;
            var item = p.Inventory.Items[listBox.SelectedIndex];
            if (!item.IsEquipment)
            {
                MessageBox.Show("这不是可装备的物品。", "提示");
                return;
            }
            var err = p.EquipItem(item);
            if (err != null) { MessageBox.Show(err, "装备失败"); return; }
            string slotName = item.Slot == EquipSlot.Weapon ? "武器" : "防具";
            _logBox.AppendSuccess($"装备{slotName}【{item.Name}】(攻+{item.AttackBonus} 防+{item.DefenseBonus})");
            RefreshList();
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
        };

        listBox.SelectedIndexChanged += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= p.Inventory.Items.Count) return;
            var item = p.Inventory.Items[listBox.SelectedIndex];
            detailBox.Clear();
            detailBox.AppendText($"【{item.Name}】{item.GetDisplayText()}\n");
            detailBox.AppendText($"{item.Description}\n");
            detailBox.AppendText($"价值：{item.Value}银  数量：{item.Quantity}\n");

            // 装备物品显示加成及当前同槽装备
            if (item.IsEquipment)
            {
                var equipped = item.Slot == EquipSlot.Weapon ? p.EquippedWeapon : p.EquippedArmor;
                detailBox.AppendText(equipped == null ? "当前未装备同类物品\n" : $"当前已装备：【{equipped.Name}】(装备后将替换)\n");
            }

            if (item.IsManual && item.Prerequisite != null)
            {
                var check = item.CanLearn(p);
                if (check.Passed)
                    detailBox.AppendText("✓ 满足修炼条件，可学习！");
                else
                    detailBox.AppendText($"✗ 不满足条件：{check.FailureSummary}");
            }
            equipBtn.Visible = item.IsEquipment;
        };

        // 使用按钮（消耗品）
        var useBtn = new Button
        {
            Text = "使用",
            Size = new Size(100, 40),
            Margin = new Padding(3),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(useBtn, WuxiaTheme.Success);
        useBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= p.Inventory.Items.Count) return;
            var item = p.Inventory.Items[listBox.SelectedIndex];
            if (item.Type != ItemType.Consumable)
            {
                MessageBox.Show("这个物品不能使用。", "提示");
                return;
            }
            int oldHP = p.CurrentHP, oldMP = p.CurrentMP;
            bool used = item.Use(p);
            if (used)
            {
                string msg = $"使用了{item.Name}！";
                if (p.CurrentHP > oldHP) msg += $" HP+{p.CurrentHP - oldHP}";
                if (p.CurrentMP > oldMP) msg += $" MP+{p.CurrentMP - oldMP}";
                if (p is Player pl && item.BuddhistChange > 0) msg += $" 佛法值+{item.BuddhistChange}";
                // 使用后数量为0则从背包移除
                if (item.Quantity <= 0)
                    p.Inventory.RemoveItem(item.Id, 1);
                _logBox.AppendSuccess(msg);
                RefreshList();
                goldLabel.Text = $"银两：{p.Gold}";
            }
            else
            {
                MessageBox.Show("使用失败。", "提示");
            }
        };
        btnPanel.Controls.Add(useBtn);

        // 修炼按钮（秘籍）
        var learnBtn = new Button
        {
            Text = "修炼秘籍",
            Size = new Size(120, 40),
            Margin = new Padding(3),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(learnBtn, Color.FromArgb(180, 140, 80));
        learnBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= p.Inventory.Items.Count) return;
            var item = p.Inventory.Items[listBox.SelectedIndex];
            if (!item.IsManual)
            {
                MessageBox.Show("这不是武功秘籍。", "提示");
                return;
            }
            var check = item.CanLearn(p);
            if (!check.Passed)
            {
                MessageBox.Show($"不满足修炼条件：\n{check.FailureSummary}", "修炼失败");
                return;
            }
            if (item.ContainedArtIds.Count == 0)
            {
                MessageBox.Show("这本秘籍没有包含武功。", "提示");
                return;
            }
            bool learnedAnyArt = false;
            foreach (var artId in item.ContainedArtIds)
            {
                var art = _config.CreateMartialArt(artId, 1);
                if (art != null)
                {
                    if (p.LearnedArts.Any(a => a.Id == art.Id))
                    {
                        MessageBox.Show($"你已经学过【{art.Name}】了。", "提示");
                        continue;
                    }
                    // 自宫前置条件检查
                    if (!p.CanLearnArt(artId, out var blockReason))
                    {
                        MessageBox.Show($"修炼【{art.Name}】失败：{blockReason}。", "修炼失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }
                    p.LearnArt(art);
                    learnedAnyArt = true;
                    _logBox.AppendSuccess($"你修炼了秘籍【{item.Name}】，学会了【{art.Name}】！");
                    p.AddHistory($"修炼秘籍【{item.Name}】，学会了【{art.Name}】");
                    int manualExp = 30;
                    int manualLv = p.GainJianghuExp(manualExp);
                    _logBox.AppendSuccess($"获得江湖阅历 +{manualExp}（阅历Lv.{p.JianghuLevel}）");
                    if (manualLv > 0) _logBox.AppendSuccess($"阅历提升！等级达到 Lv.{p.JianghuLevel}！");
                }
            }
            // 只有真正学会至少一门武功后才消耗秘籍。前置条件不满足或已学会时保留。
            if (learnedAnyArt)
                p.Inventory.RemoveItem(item.Id, 1);
            RefreshList();
        };
        btnPanel.Controls.Add(learnBtn);
        btnPanel.Controls.Add(equipBtn);

        // 关闭按钮
        var closeBtn = new Button
        {
            Text = "关闭",
            Size = new Size(100, 40),
            Margin = new Padding(3),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        btnPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 商店窗口 ──

    private void ShowShopForm(NPC merchant)
    {
        ShopSystem.InitShopItems(merchant, _config);
        var player = _engine.State.Player;

        var form = new Form
        {
            Text = $"{merchant.Name} - 银两:{player.Gold}",
            Size = new Size(520, 520),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        var goldLabel = new Label
        {
            Text = $"银两：{player.Gold}",
            Location = new Point(10, 10),
            Size = new Size(200, 25),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(goldLabel);

        // Tab按钮
        var buyTabBtn = new Button
        {
            Text = "购买", Location = new Point(10, 40), Size = new Size(80, 30), Cursor = Cursors.Hand
        };
        var sellTabBtn = new Button
        {
            Text = "出售", Location = new Point(95, 40), Size = new Size(80, 30), Cursor = Cursors.Hand
        };
        form.Controls.Add(buyTabBtn);
        form.Controls.Add(sellTabBtn);

        // ── 购买面板 ──
        var buyPanel = new Panel { Location = new Point(0, 75), Size = new Size(520, 410) };
        form.Controls.Add(buyPanel);

        var buyListBox = new ListBox
        {
            Location = new Point(10, 0), Size = new Size(480, 230),
            BackColor = WuxiaTheme.Surface, ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f), IntegralHeight = false
        };
        RarityListItem.SetupOwnerDraw(buyListBox);
        buyPanel.Controls.Add(buyListBox);

        var buyDetailBox = new RichTextBox
        {
            Location = new Point(10, 235), Size = new Size(480, 65),
            BackColor = WuxiaTheme.PanelBackAlt, ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle
        };
        buyPanel.Controls.Add(buyDetailBox);

        void RefreshBuyList()
        {
            buyListBox.Items.Clear();
            foreach (var si in merchant.CurrentShopItems)
            {
                string name = _config.Items.TryGetValue(si.ItemId, out var cfg) ? cfg.Name : si.ItemId;
                string stock = si.Stock < 0 ? "无限" : $"x{si.Stock}";
                string tag = si.IsFixed ? "[固定]" : "[随机]";
                var color = _config.Items.TryGetValue(si.ItemId, out var cfg2) ? Item.GetRarityColor(cfg2.Rarity) : Color.Gray;
                var rarityName = _config.Items.TryGetValue(si.ItemId, out var cfg3) ? Item.GetRarityName(cfg3.Rarity) : "普通";
                buyListBox.Items.Add(new RarityListItem(
                    $"{tag} {name}  {si.Price}银  库存:{stock}", color, rarityName));
            }
            if (buyListBox.Items.Count > 0) buyListBox.SelectedIndex = 0;
        }
        RefreshBuyList();

        buyListBox.SelectedIndexChanged += (_, _) =>
        {
            if (buyListBox.SelectedIndex < 0 || buyListBox.SelectedIndex >= merchant.CurrentShopItems.Count) return;
            var si = merchant.CurrentShopItems[buyListBox.SelectedIndex];
            buyDetailBox.Clear();
            if (_config.Items.TryGetValue(si.ItemId, out var cfg))
            {
                buyDetailBox.AppendText($"【{cfg.Name}】\n{cfg.Description}\n");
                if (cfg.HPRecovery > 0) buyDetailBox.AppendText($"恢复HP: {cfg.HPRecovery}  ");
                if (cfg.MPRecovery > 0) buyDetailBox.AppendText($"恢复MP: {cfg.MPRecovery}  ");
                buyDetailBox.AppendText($"\n价格: {si.Price}银");
            }
        };

        var buyBtn = new Button
        {
            Text = "购买", Location = new Point(10, 310), Size = new Size(100, 38), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(buyBtn, WuxiaTheme.Success);
        buyBtn.Click += (_, _) =>
        {
            if (buyListBox.SelectedIndex < 0 || buyListBox.SelectedIndex >= merchant.CurrentShopItems.Count) return;
            var (success, message) = ShopSystem.BuyItem(merchant, buyListBox.SelectedIndex, player, _config);
            if (success) _logBox.AppendSuccess(message); else _logBox.AppendWarning(message);
            goldLabel.Text = $"银两：{player.Gold}";
            form.Text = $"{merchant.Name} - 银两:{player.Gold}";
            RefreshBuyList();
        };
        buyPanel.Controls.Add(buyBtn);

        var buyCloseBtn = new Button
        {
            Text = "关闭", Location = new Point(380, 310), Size = new Size(100, 38), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(buyCloseBtn);
        buyCloseBtn.Click += (_, _) => form.Close();
        buyPanel.Controls.Add(buyCloseBtn);

        // ── 出售面板 ──
        var sellPanel = new Panel { Location = new Point(0, 75), Size = new Size(520, 410), Visible = false };
        form.Controls.Add(sellPanel);

        var sellListBox = new ListBox
        {
            Location = new Point(10, 0), Size = new Size(480, 230),
            BackColor = WuxiaTheme.Surface, ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f), IntegralHeight = false
        };
        RarityListItem.SetupOwnerDraw(sellListBox);
        sellPanel.Controls.Add(sellListBox);

        var sellDetailBox = new RichTextBox
        {
            Location = new Point(10, 235), Size = new Size(480, 65),
            BackColor = WuxiaTheme.PanelBackAlt, ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle
        };
        sellPanel.Controls.Add(sellDetailBox);

        void RefreshSellList()
        {
            sellListBox.Items.Clear();
            foreach (var item in player.Inventory.Items)
            {
                int sellPrice = Math.Max(1, item.Value / 2);
                string qty = item.Quantity > 1 ? $" x{item.Quantity}" : "";
                sellListBox.Items.Add(new RarityListItem(
                    $"{item.Name}{qty}  售价:{sellPrice}银",
                    item.RarityColor, item.RarityName));
            }
            if (sellListBox.Items.Count > 0) sellListBox.SelectedIndex = 0;
        }

        sellListBox.SelectedIndexChanged += (_, _) =>
        {
            if (sellListBox.SelectedIndex < 0 || sellListBox.SelectedIndex >= player.Inventory.Items.Count) return;
            var item = player.Inventory.Items[sellListBox.SelectedIndex];
            sellDetailBox.Clear();
            sellDetailBox.AppendText($"【{item.Name}】\n{item.Description}\n");
            sellDetailBox.AppendText($"价值: {item.Value}银  售价: {Math.Max(1, item.Value / 2)}银  数量: {item.Quantity}");
        };

        var sellBtn = new Button
        {
            Text = "出售", Location = new Point(10, 310), Size = new Size(100, 38), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(sellBtn, Color.FromArgb(220, 160, 60));
        sellBtn.Click += (_, _) =>
        {
            if (sellListBox.SelectedIndex < 0 || sellListBox.SelectedIndex >= player.Inventory.Items.Count) return;
            var item = player.Inventory.Items[sellListBox.SelectedIndex];
            var (success, message) = ShopSystem.SellItem(player, item.Id, 1, _config);
            if (success) _logBox.AppendSuccess(message); else _logBox.AppendWarning(message);
            goldLabel.Text = $"银两：{player.Gold}";
            form.Text = $"{merchant.Name} - 银两:{player.Gold}";
            RefreshSellList();
        };
        sellPanel.Controls.Add(sellBtn);

        var sellCloseBtn = new Button
        {
            Text = "关闭", Location = new Point(380, 310), Size = new Size(100, 38), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(sellCloseBtn);
        sellCloseBtn.Click += (_, _) => form.Close();
        sellPanel.Controls.Add(sellCloseBtn);

        // ── Tab切换 ──
        void SwitchTab(bool isBuy)
        {
            buyPanel.Visible = isBuy;
            sellPanel.Visible = !isBuy;
            WuxiaTheme.StyleButton(buyTabBtn, isBuy ? WuxiaTheme.Accent : null);
            WuxiaTheme.StyleButton(sellTabBtn, !isBuy ? WuxiaTheme.Accent : null);
            if (!isBuy) RefreshSellList();
        }
        buyTabBtn.Click += (_, _) => SwitchTab(true);
        sellTabBtn.Click += (_, _) => SwitchTab(false);
        SwitchTab(true);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 场景技艺学习 ──

    private void ShowCraftLessonForm()
    {
        var scene = _engine.GetCurrentScene();
        if (scene == null || scene.CraftLessons.Count == 0) return;

        var player = _engine.State.Player;
        var form = new Form
        {
            Text = $"{scene.Name} - 学习技艺",
            Size = new Size(420, 400),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        var goldLabel = new Label
        {
            Text = $"银两：{player.Gold}",
            Location = new Point(10, 10),
            Size = new Size(200, 25),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(goldLabel);

        var listBox = new ListBox
        {
            Location = new Point(10, 40),
            Size = new Size(380, 200),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            IntegralHeight = false
        };
        form.Controls.Add(listBox);

        var detailBox = new RichTextBox
        {
            Location = new Point(10, 245),
            Size = new Size(380, 50),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(detailBox);

        void RefreshCraftList()
        {
            listBox.Items.Clear();
            foreach (var lesson in scene.CraftLessons)
            {
                string skillName = CharacterBase.GetCraftSkillName(lesson.SkillId);
                int currentLevel = player.CraftSkills.GetValueOrDefault(lesson.SkillId, 0);
                listBox.Items.Add($"{skillName}  当前:{currentLevel}  上限:{lesson.MaxLevel}  学费:{lesson.Cost}银");
            }
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
        }
        RefreshCraftList();

        listBox.SelectedIndexChanged += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= scene.CraftLessons.Count) return;
            var lesson = scene.CraftLessons[listBox.SelectedIndex];
            int current = player.CraftSkills.GetValueOrDefault(lesson.SkillId, 0);
            detailBox.Clear();
            detailBox.AppendText($"当前等级: {current} / {lesson.MaxLevel}  每次+{lesson.Gain}经验  学费{lesson.Cost}银\n");
            if (current >= lesson.MaxLevel)
                detailBox.AppendText("已达到此处学习上限！");
        };

        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 305),
            Size = new Size(380, 45),
            FlowDirection = FlowDirection.LeftToRight
        };
        form.Controls.Add(btnPanel);

        var learnBtn = new Button
        {
            Text = "学习", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(learnBtn, WuxiaTheme.Success);
        learnBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= scene.CraftLessons.Count) return;
            var lesson = scene.CraftLessons[listBox.SelectedIndex];
            int current = player.CraftSkills.GetValueOrDefault(lesson.SkillId, 0);
            if (current >= lesson.MaxLevel)
            {
                _logBox.AppendWarning("已达到此处学习上限。");
                return;
            }
            if (player.Gold < lesson.Cost)
            {
                _logBox.AppendWarning($"银两不足（需要{lesson.Cost}两）");
                return;
            }
            player.Gold -= lesson.Cost;
            player.CraftSkills[lesson.SkillId] = current + lesson.Gain;
            string skillName = CharacterBase.GetCraftSkillName(lesson.SkillId);
            _logBox.AppendSuccess($"学习了{skillName}，经验+{lesson.Gain}（当前{current + lesson.Gain}）");
            goldLabel.Text = $"银两：{player.Gold}";
            RefreshCraftList();
        };
        btnPanel.Controls.Add(learnBtn);

        var closeBtn = new Button
        {
            Text = "关闭", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        btnPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 采药 ──

    /// <summary>
    /// 一键连续采集：耗尽所有可用体力(每次10点)循环采集，累计产出后输出汇总日志。
    /// </summary>
    private void RunBatchGather(
        RichTextBox logBox,
        Func<(bool Success, string? ItemName, bool LeveledUp, int NewLevel)> turn,
        double timeCostPerTurn,
        string skillName,
        Action? onBatchComplete = null)
    {
        var player = _engine.State.Player;
        int times = (int)(player.Stamina / 10);
        if (times <= 0)
        {
            logBox.SelectionColor = Color.FromArgb(220, 150, 100);
            logBox.AppendText("体力不足10点，无法连续采集。\n");
            logBox.ScrollToCaret();
            return;
        }

        int success = 0, fail = 0;
        var items = new Dictionary<string, int>();
        bool anyLevelUp = false;
        int finalLevel = 0;
        for (int i = 0; i < times; i++)
        {
            var r = turn();
            _engine.State.GameTime.Advance(timeCostPerTurn);
            if (r.Success)
            {
                success++;
                if (!string.IsNullOrEmpty(r.ItemName))
                    items[r.ItemName] = items.GetValueOrDefault(r.ItemName) + 1;
            }
            else fail++;
            if (r.LeveledUp) { anyLevelUp = true; finalLevel = r.NewLevel; }
        }

        string itemSummary = items.Count > 0
            ? string.Join("、", items.Select(kv => $"{kv.Key}×{kv.Value}"))
            : "无";
        string summary = $"连续{skillName}{times}次：成功{success}次，失败{fail}次，获得：{itemSummary}";
        logBox.SelectionColor = Color.FromArgb(100, 200, 100);
        logBox.AppendText(summary + "\n");
        _logBox.AppendSuccess($"[{skillName}] {summary}");

        if (anyLevelUp)
        {
            logBox.SelectionColor = Color.FromArgb(255, 220, 100);
            logBox.AppendText($"✨ {skillName}技艺提升至 {finalLevel}！\n");
            _logBox.AppendSuccess($"✨ {skillName}技艺提升至 {finalLevel}！");
        }
        logBox.ScrollToCaret();

        onBatchComplete?.Invoke();
    }

    private void ShowHerbGatheringForm()
    {
        var scene = _engine.GetCurrentScene();
        if (scene?.HerbGarden == null) return;

        var garden = scene.HerbGarden;
        var player = _engine.State.Player;
        double successRate = HerbGatheringSystem.GetSuccessRate(player, garden);
        double coefficient = HerbGatheringSystem.GetCoefficient(garden);

        var form = new Form
        {
            Text = $"{scene.Name} - 采药",
            Size = new Size(450, 420),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        string tierName = garden.Tier switch { "normal" => "普通", "medium" => "中级", "high" => "高级", _ => garden.Tier };
        var infoLabel = new Label
        {
            Text = $"【{scene.Name}】药园等级：{tierName}（系数{coefficient:F1}）",
            Location = new Point(10, 10),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(infoLabel);

        int skillLevel = player.GetCraftSkill("gathering");
        int proficiency = player.GetCraftProficiency("gathering");
        var skillLabel = new Label
        {
            Text = $"采药技艺：{skillLevel}    熟练度：{proficiency}/100    成功率：{successRate:P0}",
            Location = new Point(10, 40),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f)
        };
        form.Controls.Add(skillLabel);

        var staminaLabel = new Label
        {
            Text = $"体力：{player.Stamina:F0}/{player.MaxStamina:F0}    每次消耗：10    每次耗时：{garden.TimeCostPerGather}时辰",
            Location = new Point(10, 68),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };
        form.Controls.Add(staminaLabel);

        var herbLabel = new Label
        {
            Text = "可采草药：",
            Location = new Point(10, 98),
            Size = new Size(80, 20),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };
        form.Controls.Add(herbLabel);

        var herbListBox = new ListBox
        {
            Location = new Point(10, 120),
            Size = new Size(410, 120),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            IntegralHeight = false
        };
        form.Controls.Add(herbListBox);

        foreach (var herb in garden.Herbs)
        {
            if (_engine.Config.Items.TryGetValue(herb.ItemId, out var itemCfg))
            {
                string rarityName = herb.Rarity switch
                {
                    "common" => "普通", "uncommon" => "稀有", "rare" => "珍贵",
                    "epic" => "史诗", "legendary" => "传说", _ => herb.Rarity
                };
                herbListBox.Items.Add($"{itemCfg.Name}  [{rarityName}]  价值{itemCfg.Value}银");
            }
        }

        var logBox = new RichTextBox
        {
            Location = new Point(10, 248),
            Size = new Size(410, 80),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(logBox);

        void RefreshHerbInfo()
        {
            skillLevel = player.GetCraftSkill("gathering");
            proficiency = player.GetCraftProficiency("gathering");
            successRate = HerbGatheringSystem.GetSuccessRate(player, garden);
            skillLabel.Text = $"采药技艺：{skillLevel}    熟练度：{proficiency}/100    成功率：{successRate:P0}";
            staminaLabel.Text = $"体力：{player.Stamina:F0}/{player.MaxStamina:F0}    每次消耗：10    每次耗时：{garden.TimeCostPerGather}时辰";
        }

        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 338),
            Size = new Size(410, 45),
            FlowDirection = FlowDirection.LeftToRight
        };
        form.Controls.Add(btnPanel);

        var gatherBtn = new Button
        {
            Text = "采药", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(gatherBtn, Color.FromArgb(120, 180, 120));
        gatherBtn.Click += (_, _) =>
        {
            var result = HerbGatheringSystem.Gather(player, garden, _engine.Config);
            _engine.State.GameTime.Advance(garden.TimeCostPerGather);

            if (result.Success)
            {
                logBox.SelectionColor = Color.FromArgb(100, 200, 100);
                logBox.AppendText($"{result.Message}\n");
                _logBox.AppendSuccess($"[采药] {result.Message}");
            }
            else
            {
                logBox.SelectionColor = Color.FromArgb(220, 150, 100);
                logBox.AppendText($"{result.Message}\n");
                _logBox.AppendWarning($"[采药] {result.Message}");
            }
            logBox.ScrollToCaret();

            if (result.SkillLeveledUp)
            {
                logBox.SelectionColor = Color.FromArgb(255, 220, 100);
                logBox.AppendText($"✨ 采药技艺提升至 {result.NewSkillLevel}！\n");
                _logBox.AppendSuccess($"✨ 采药技艺提升至 {result.NewSkillLevel}！");
                logBox.ScrollToCaret();
            }
            RefreshHerbInfo();
        };
        btnPanel.Controls.Add(gatherBtn);

        var batchGatherBtn = new Button
        {
            Text = "一键采药", Size = new Size(115, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(batchGatherBtn, Color.FromArgb(90, 150, 90));
        batchGatherBtn.Click += (_, _) =>
        {
            RunBatchGather(logBox,
                () => { var r = HerbGatheringSystem.Gather(player, garden, _engine.Config); return (r.Success, r.ObtainedItemName, r.SkillLeveledUp, r.NewSkillLevel); },
                garden.TimeCostPerGather, "采药");
            RefreshHerbInfo();
        };
        btnPanel.Controls.Add(batchGatherBtn);

        var closeBtn = new Button
        {
            Text = "关闭", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        btnPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 挖矿 ──

    private void ShowMiningForm()
    {
        var scene = _engine.GetCurrentScene();
        if (scene?.Mine == null) return;

        var mine = scene.Mine;
        var player = _engine.State.Player;
        double successRate = MiningSystem.GetSuccessRate(player, mine);
        double coefficient = MiningSystem.GetCoefficient(mine);

        var form = new Form
        {
            Text = $"{scene.Name} - 挖矿",
            Size = new Size(450, 420),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        // 矿场信息
        string tierName = mine.Tier switch { "normal" => "普通", "medium" => "中级", "high" => "高级", _ => mine.Tier };
        var infoLabel = new Label
        {
            Text = $"【{scene.Name}】矿场等级：{tierName}（系数{coefficient:F1}）",
            Location = new Point(10, 10),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(infoLabel);

        // 技艺信息
        int skillLevel = player.GetCraftSkill("mining");
        int proficiency = player.GetCraftProficiency("mining");
        var skillLabel = new Label
        {
            Text = $"挖矿技艺：{skillLevel}    熟练度：{proficiency}/100    成功率：{successRate:P0}",
            Location = new Point(10, 40),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f)
        };
        form.Controls.Add(skillLabel);

        // 体力信息
        var staminaLabel = new Label
        {
            Text = $"体力：{player.Stamina:F0}/{player.MaxStamina:F0}    每次消耗：10    每次耗时：{mine.TimeCostPerMine}时辰",
            Location = new Point(10, 68),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };
        form.Controls.Add(staminaLabel);

        // 可挖矿石列表
        var oreLabel = new Label
        {
            Text = "可挖矿石：",
            Location = new Point(10, 98),
            Size = new Size(80, 20),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };
        form.Controls.Add(oreLabel);

        var oreListBox = new ListBox
        {
            Location = new Point(10, 120),
            Size = new Size(410, 120),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            IntegralHeight = false
        };
        form.Controls.Add(oreListBox);

        foreach (var ore in mine.Ores)
        {
            if (_engine.Config.Items.TryGetValue(ore.ItemId, out var itemCfg))
            {
                string rarityName = ore.Rarity switch
                {
                    "common" => "普通", "uncommon" => "稀有", "rare" => "珍贵",
                    "epic" => "史诗", "legendary" => "传说", _ => ore.Rarity
                };
                oreListBox.Items.Add($"{itemCfg.Name}  [{rarityName}]  价值{itemCfg.Value}银");
            }
        }

        // 结果日志
        var logBox = new RichTextBox
        {
            Location = new Point(10, 248),
            Size = new Size(410, 80),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(logBox);

        void RefreshMiningInfo()
        {
            skillLevel = player.GetCraftSkill("mining");
            proficiency = player.GetCraftProficiency("mining");
            successRate = MiningSystem.GetSuccessRate(player, mine);
            skillLabel.Text = $"挖矿技艺：{skillLevel}    熟练度：{proficiency}/100    成功率：{successRate:P0}";
            staminaLabel.Text = $"体力：{player.Stamina:F0}/{player.MaxStamina:F0}    每次消耗：10    每次耗时：{mine.TimeCostPerMine}时辰";
        }

        // 按钮
        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 338),
            Size = new Size(410, 45),
            FlowDirection = FlowDirection.LeftToRight
        };
        form.Controls.Add(btnPanel);

        var mineBtn = new Button
        {
            Text = "挖矿", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(mineBtn, Color.FromArgb(180, 140, 100));
        mineBtn.Click += (_, _) =>
        {
            var result = MiningSystem.Mine(player, mine, _engine.Config);

            // 推进游戏时间
            _engine.State.GameTime.Advance(mine.TimeCostPerMine);

            // 显示结果
            if (result.Success)
            {
                logBox.SelectionColor = Color.FromArgb(100, 200, 100);
                logBox.AppendText($"{result.Message}\n");
                _logBox.AppendSuccess($"[挖矿] {result.Message}");
                // 挖矿成功后尝试自动推进链式任务
                TryAutoAdvanceQuests("mine", _engine.State.CurrentSceneId);
            }
            else
            {
                logBox.SelectionColor = Color.FromArgb(220, 150, 100);
                logBox.AppendText($"{result.Message}\n");
                _logBox.AppendWarning($"[挖矿] {result.Message}");
            }
            logBox.ScrollToCaret();

            if (result.SkillLeveledUp)
            {
                logBox.SelectionColor = Color.FromArgb(255, 220, 100);
                logBox.AppendText($"✨ 挖矿技艺提升至 {result.NewSkillLevel}！\n");
                _logBox.AppendSuccess($"✨ 挖矿技艺提升至 {result.NewSkillLevel}！");
                logBox.ScrollToCaret();
            }

            RefreshMiningInfo();
        };
        btnPanel.Controls.Add(mineBtn);

        var batchMineBtn = new Button
        {
            Text = "一键挖矿", Size = new Size(115, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(batchMineBtn, Color.FromArgb(160, 120, 80));
        batchMineBtn.Click += (_, _) =>
        {
            RunBatchGather(logBox,
                () => { var r = MiningSystem.Mine(player, mine, _engine.Config); return (r.Success, r.ObtainedItemName, r.SkillLeveledUp, r.NewSkillLevel); },
                mine.TimeCostPerMine, "挖矿",
                () => TryAutoAdvanceQuests("mine", _engine.State.CurrentSceneId));
            RefreshMiningInfo();
        };
        btnPanel.Controls.Add(batchMineBtn);

        var closeBtn = new Button
        {
            Text = "关闭", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        btnPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 打猎 ──

    private void ShowHuntingForm()
    {
        var scene = _engine.GetCurrentScene();
        if (scene?.Hunt == null) return;

        var hunt = scene.Hunt;
        var player = _engine.State.Player;
        double successRate = HuntingSystem.GetSuccessRate(player, hunt);

        var form = new Form
        {
            Text = $"{scene.Name} - 打猎",
            Size = new Size(450, 420),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        int skillLevel = player.GetCraftSkill("hunting");
        int proficiency = player.GetCraftProficiency("hunting");

        var skillLabel = new Label
        {
            Text = $"狩猎技艺：{skillLevel}    熟练度：{proficiency}/100    成功率：{successRate:P0}",
            Location = new Point(10, 10),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(10f)
        };
        form.Controls.Add(skillLabel);

        var staminaLabel = new Label
        {
            Text = $"体力：{player.Stamina:F0}/{player.MaxStamina:F0}    每次消耗：10    每次耗时：{hunt.TimeCostPerHunt}时辰",
            Location = new Point(10, 38),
            Size = new Size(410, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f)
        };
        form.Controls.Add(staminaLabel);

        var lootLabel = new Label
        {
            Text = "可获猎物：",
            Location = new Point(10, 68),
            Size = new Size(80, 20),
            ForeColor = WuxiaTheme.Text
        };
        form.Controls.Add(lootLabel);

        var lootBox = new ListBox
        {
            Location = new Point(10, 90),
            Size = new Size(410, 120),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        WuxiaTheme.StyleListBox(lootBox);
        foreach (var loot in hunt.Loots)
        {
            if (_engine.Config.Items.TryGetValue(loot.ItemId, out var cfg))
                lootBox.Items.Add($"{cfg.Name} ({loot.Rarity}) - 价值{cfg.Value}银");
            else
                lootBox.Items.Add($"{loot.ItemId} ({loot.Rarity})");
        }
        form.Controls.Add(lootBox);

        var logBox = new RichTextBox
        {
            Location = new Point(10, 218),
            Size = new Size(410, 110),
            ReadOnly = true,
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(logBox);

        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 335),
            Size = new Size(410, 45),
            FlowDirection = FlowDirection.LeftToRight
        };
        form.Controls.Add(btnPanel);

        void RefreshHuntingInfo()
        {
            skillLevel = player.GetCraftSkill("hunting");
            proficiency = player.GetCraftProficiency("hunting");
            successRate = HuntingSystem.GetSuccessRate(player, hunt);
            skillLabel.Text = $"狩猎技艺：{skillLevel}    熟练度：{proficiency}/100    成功率：{successRate:P0}";
            staminaLabel.Text = $"体力：{player.Stamina:F0}/{player.MaxStamina:F0}    每次消耗：10    每次耗时：{hunt.TimeCostPerHunt}时辰";
        }

        var huntBtn = new Button
        {
            Text = "打猎一次", Size = new Size(140, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(huntBtn, Color.FromArgb(140, 180, 100));
        huntBtn.Click += (_, _) =>
        {
            var result = HuntingSystem.Hunt(player, hunt, _engine.Config);
            logBox.SelectionColor = result.Success ? Color.FromArgb(120, 220, 120) : Color.FromArgb(220, 140, 140);
            logBox.AppendText(result.Message + "\n");
            logBox.ScrollToCaret();

            if (result.SkillLeveledUp)
            {
                logBox.SelectionColor = Color.FromArgb(255, 220, 100);
                logBox.AppendText($"✨ 狩猎技艺提升至 {result.NewSkillLevel}！\n");
                _logBox.AppendSuccess($"✨ 狩猎技艺提升至 {result.NewSkillLevel}！");
                logBox.ScrollToCaret();
            }

            RefreshHuntingInfo();
        };
        btnPanel.Controls.Add(huntBtn);

        var batchHuntBtn = new Button
        {
            Text = "一键打猎", Size = new Size(115, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(batchHuntBtn, Color.FromArgb(110, 150, 80));
        batchHuntBtn.Click += (_, _) =>
        {
            RunBatchGather(logBox,
                () => { var r = HuntingSystem.Hunt(player, hunt, _engine.Config); return (r.Success, r.ObtainedItemName, r.SkillLeveledUp, r.NewSkillLevel); },
                hunt.TimeCostPerHunt, "打猎");
            RefreshHuntingInfo();
        };
        btnPanel.Controls.Add(batchHuntBtn);

        var closeBtn = new Button
        {
            Text = "关闭", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        btnPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 武术馆 ──

    private void ShowMartialLessonForm()
    {
        var scene = _engine.GetCurrentScene();
        if (scene == null || scene.MartialLessons.Count == 0) return;

        var player = _engine.State.Player;
        var form = new Form
        {
            Text = $"{scene.Name} - 武术馆",
            Size = new Size(460, 420),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        var goldLabel = new Label
        {
            Text = $"银两：{player.Gold}",
            Location = new Point(10, 10),
            Size = new Size(200, 25),
            ForeColor = WuxiaTheme.Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(goldLabel);

        var listBox = new ListBox
        {
            Location = new Point(10, 40),
            Size = new Size(420, 220),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            IntegralHeight = false
        };
        form.Controls.Add(listBox);

        var detailBox = new RichTextBox
        {
            Location = new Point(10, 265),
            Size = new Size(420, 55),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(detailBox);

        void RefreshMartialList()
        {
            listBox.Items.Clear();
            foreach (var lesson in scene.MartialLessons)
            {
                string artName = "未知武功";
                if (_config.MartialArts.TryGetValue(lesson.ArtId, out var artCfg))
                    artName = artCfg.Name;
                bool alreadyLearned = player.LearnedArts.Any(a => a.Id == lesson.ArtId);
                string tag = alreadyLearned ? "[已学]" : "";
                listBox.Items.Add($"{tag}{artName}  学费:{lesson.Cost}银");
            }
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
        }
        RefreshMartialList();

        listBox.SelectedIndexChanged += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= scene.MartialLessons.Count) return;
            var lesson = scene.MartialLessons[listBox.SelectedIndex];
            detailBox.Clear();
            if (_config.MartialArts.TryGetValue(lesson.ArtId, out var artCfg))
            {
                string typeLabel = artCfg.Type == "internal" ? "内功" : "外功";
                detailBox.AppendText($"【{artCfg.Name}】{typeLabel}\n{artCfg.Description}");
            }
        };

        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 330),
            Size = new Size(420, 45),
            FlowDirection = FlowDirection.LeftToRight
        };
        form.Controls.Add(btnPanel);

        var learnBtn = new Button
        {
            Text = "学习武功", Size = new Size(110, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(learnBtn, WuxiaTheme.Success);
        learnBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= scene.MartialLessons.Count) return;
            var lesson = scene.MartialLessons[listBox.SelectedIndex];
            if (player.LearnedArts.Any(a => a.Id == lesson.ArtId))
            {
                _logBox.AppendWarning("你已经学过这门武功了。");
                return;
            }
            if (player.Gold < lesson.Cost)
            {
                _logBox.AppendWarning($"银两不足（需要{lesson.Cost}两）");
                return;
            }
            var art = _config.CreateMartialArt(lesson.ArtId, 1);
            if (art == null)
            {
                _logBox.AppendWarning("武功加载失败。");
                return;
            }
            player.Gold -= lesson.Cost;
            player.LearnArt(art);
            _logBox.AppendSuccess($"在武术馆花费{lesson.Cost}银，学会了【{art.Name}】！");
            player.AddHistory($"在{scene.Name}武术馆学会了【{art.Name}】");
            int exp = 30;
            int lvUps = player.GainJianghuExp(exp);
            _logBox.AppendSuccess($"获得江湖阅历 +{exp}");
            if (lvUps > 0) _logBox.AppendSuccess($"阅历提升！等级达到 Lv.{player.JianghuLevel}！");
            goldLabel.Text = $"银两：{player.Gold}";
            RefreshMartialList();
        };
        btnPanel.Controls.Add(learnBtn);

        var closeBtn = new Button
        {
            Text = "关闭", Size = new Size(100, 38),
            Margin = new Padding(3), Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(closeBtn);
        closeBtn.Click += (_, _) => form.Close();
        btnPanel.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 赠送物品 ──

    private void GiftToNPC(NPC npc)
    {
        var giftableItems = _engine.State.Player.Inventory.GetGiftableItems();
        if (giftableItems.Count == 0)
        {
            _logBox.AppendWarning("你没有可以赠送的物品。");
            return;
        }

        // 弹出选择窗口
        var form = new Form
        {
            Text = $"赠送物品给{npc.Name}",
            Size = new Size(430, 580),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var listBox = new ListBox
        {
            Location = new Point(10, 10),
            Size = new Size(400, 260),
            BackColor = WuxiaTheme.PanelBackAlt,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };

        foreach (var item in giftableItems)
        {
            listBox.Items.Add($"{item.Name} x{item.Quantity} - {item.Description}");
        }
        if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
        form.Controls.Add(listBox);

        var infoLabel = new Label
        {
            Location = new Point(10, 278),
            Size = new Size(400, 30),
            ForeColor = WuxiaTheme.TextMuted,
            Text = $"选中物品后点击赠送，NPC会通过AI思考后决定是否接受"
        };
        form.Controls.Add(infoLabel);

        // 显示NPC思考的文本框
        var thinkingBox = new RichTextBox
        {
            Location = new Point(10, 312),
            Size = new Size(400, 140),
            BackColor = Color.FromArgb(30, 30, 40),
            ForeColor = Color.FromArgb(130, 120, 100),
            Font = WuxiaTheme.UiFont(9f, FontStyle.Italic),
            BorderStyle = BorderStyle.None,
            ReadOnly = true
        };
        form.Controls.Add(thinkingBox);

        var giftBtn = new Button
        {
            Text = "赠送",
            Location = new Point(10, 462),
            Size = new Size(195, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(giftBtn, Color.FromArgb(180, 140, 80));
        giftBtn.Click += async (_, _) =>
        {
            if (listBox.SelectedIndex < 0 || listBox.SelectedIndex >= giftableItems.Count)
            {
                MessageBox.Show("请先选择物品", "提示");
                return;
            }
            var item = giftableItems[listBox.SelectedIndex];
            giftBtn.Enabled = false;
            giftBtn.Text = "NPC思考中...";

            var result = await GiftSystem.PlayerGiftToNPC(
                _engine.State.Player, npc, item, _engine.State.GameTime, _engine.AI, _engine.State);

            // 显示NPC思考
            if (!string.IsNullOrEmpty(result.Thinking))
            {
                thinkingBox.Text = $"（心想：{result.Thinking}）";
            }

            _logBox.AppendText(result.Message);
            if (result.FavorChange > 0)
                _logBox.AppendSuccess($"好感度+{result.FavorChange}");
            if (!string.IsNullOrEmpty(result.ReturnMessage))
                _logBox.AppendText(result.ReturnMessage);

            giftBtn.Enabled = true;
            giftBtn.Text = "赠送";

            // 稍等后关闭
            await Task.Delay(1500);
            form.Close();
            RefreshAll();
        };
        form.Controls.Add(giftBtn);

        var cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(215, 462),
            Size = new Size(195, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(cancelBtn);
        cancelBtn.Click += (_, _) => form.Close();
        form.Controls.Add(cancelBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    // ── 存档/读档 ──

    // ── 存档/读档弹窗 ──

    private void ShowSaveDialog()
    {
        var form = new Form
        {
            Text = "保存游戏",
            Size = new Size(400, 350),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        var label = new Label
        {
            Text = "选择存档位：",
            Location = new Point(10, 10),
            Size = new Size(200, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(label);

        var listBox = new ListBox
        {
            Location = new Point(10, 40),
            Size = new Size(360, 200),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };

        for (int i = 1; i <= 5; i++)
        {
            var saveInfo = _engine.GetSaveInfo(i);
            if (saveInfo != null)
                listBox.Items.Add($"存档{i}: {saveInfo.PlayerName} 第{saveInfo.Day}天 [{saveInfo.SceneName}] ({saveInfo.SaveTime:MM-dd HH:mm})");
            else
                listBox.Items.Add($"存档{i}: [空]");
        }
        listBox.SelectedIndex = 0;
        form.Controls.Add(listBox);

        var saveBtn = new Button
        {
            Text = "保存",
            Location = new Point(10, 255),
            Size = new Size(170, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(saveBtn, WuxiaTheme.Success);
        saveBtn.Click += (_, _) =>
        {
            int slot = listBox.SelectedIndex + 1;
            var existing = _engine.GetSaveInfo(slot);
            if (existing != null)
            {
                var result = MessageBox.Show($"存档{slot}已有数据：\n{existing.PlayerName} 第{existing.Day}天\n确定要覆盖吗？",
                    "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            try
            {
                _engine.SaveGame(slot);
                MessageBox.Show("保存成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                form.Close();
                RefreshAll();
            }
            catch (Exception ex)
            {
                GameLogger.Error("存档保存失败", ex);
                MessageBox.Show($"保存失败：\n{ex.Message}\n\n详情请查看日志。", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        form.Controls.Add(saveBtn);

        var cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(200, 255),
            Size = new Size(170, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(cancelBtn);
        cancelBtn.Click += (_, _) => form.Close();
        form.Controls.Add(cancelBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    private void ShowLoadDialog()
    {
        var form = new Form
        {
            Text = "读取存档",
            Size = new Size(400, 350),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = WuxiaTheme.AppBack,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };

        var label = new Label
        {
            Text = "选择要读取的存档：",
            Location = new Point(10, 10),
            Size = new Size(200, 25),
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        form.Controls.Add(label);

        var listBox = new ListBox
        {
            Location = new Point(10, 40),
            Size = new Size(360, 200),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9f)
        };

        bool hasAny = false;
        var slotIndices = new List<int>(); // 记录有数据的存档索引
        for (int i = 1; i <= 5; i++)
        {
            var saveInfo = _engine.GetSaveInfo(i);
            if (saveInfo != null)
            {
                listBox.Items.Add($"存档{i}: {saveInfo.PlayerName} 第{saveInfo.Day}天 [{saveInfo.SceneName}] ({saveInfo.SaveTime:MM-dd HH:mm})");
                slotIndices.Add(i);
                hasAny = true;
            }
            else
            {
                listBox.Items.Add($"存档{i}: [空]");
            }
        }
        form.Controls.Add(listBox);

        if (!hasAny)
        {
            var emptyLabel = new Label
            {
                Text = "没有可用存档。",
                Location = new Point(10, 130),
                Size = new Size(360, 30),
                ForeColor = WuxiaTheme.TextMuted,
                Font = WuxiaTheme.UiFont(10f)
            };
            form.Controls.Add(emptyLabel);
        }
        else
        {
            listBox.SelectedIndex = 0;
        }

        var loadBtn = new Button
        {
            Text = "读取",
            Location = new Point(10, 255),
            Size = new Size(170, 40),
            Cursor = hasAny ? Cursors.Hand : Cursors.No,
            Enabled = hasAny
        };
        WuxiaTheme.StyleButton(loadBtn, WuxiaTheme.Accent);
        loadBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0) return;
            int slot = listBox.SelectedIndex + 1;
            var saveInfo = _engine.GetSaveInfo(slot);
            if (saveInfo == null)
            {
                MessageBox.Show("该存档位为空。", "提示");
                return;
            }
            var result = MessageBox.Show($"确定要读取存档{slot}吗？\n{saveInfo.PlayerName} 第{saveInfo.Day}天",
                "确认读取", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
            if (_engine.LoadGame(slot))
            {
                form.Close();
                RefreshAll();
            }
            else
            {
                MessageBox.Show("读取存档失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        form.Controls.Add(loadBtn);

        var cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(200, 255),
            Size = new Size(170, 40),
            Cursor = Cursors.Hand
        };
        WuxiaTheme.StyleButton(cancelBtn);
        cancelBtn.Click += (_, _) => form.Close();
        form.Controls.Add(cancelBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }
}
