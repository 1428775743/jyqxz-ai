using AutoWuxia.Characters;

namespace AutoWuxia.Forms;

/// <summary>天赋选择</summary>
public enum TalentChoice
{
    None,
    /// <summary>小虾米来也:强制名字小虾米,血量+1000 攻击+20 防御+20(简单模式)</summary>
    XiaoXiaMi,
    /// <summary>轻功大师:初始速度+20</summary>
    QingGongDaShi,
    /// <summary>勤学苦练:武功修炼速度+50%</summary>
    QinXueKuLian
}

/// <summary>角色创建结果,由 CharacterCreateForm 产出,MainForm 应用到 Player。</summary>
public class CharacterCreationData
{
    public string Name { get; set; } = "小虾米";
    public string PortraitPath { get; set; } = "player_01_buyi.png";
    public int MaxHP { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    /// <summary>悟性(1~10),影响武功熟练度获取速度。</summary>
    public int Wuxing { get; set; } = 5;
    public Dictionary<string, int> CraftSkills { get; set; } = new();
    public TalentChoice Talent { get; set; } = TalentChoice.None;
    /// <summary>性别"男"/"女"</summary>
    public string Gender { get; set; } = "男";
}

/// <summary>
/// 新游戏角色创建窗:roll 血量/攻击/防御(可无限重投)、roll 技艺并自由分配剩余点数、3 选 1 天赋。
/// </summary>
public class CharacterCreateForm : Form
{
    private static readonly Color Bg = Color.FromArgb(35, 35, 50);
    private static readonly Color Surface = Color.FromArgb(25, 25, 40);
    private static readonly Color Accent = Color.FromArgb(230, 180, 90);
    private static readonly Color Fg = Color.FromArgb(220, 220, 200);
    private static readonly Color Muted = Color.FromArgb(150, 150, 150);
    private static readonly Font F = WuxiaTheme.UiFont(9f);
    private static readonly Font Fb = WuxiaTheme.UiFont(10f, FontStyle.Bold);

    private static readonly string[] CraftIds = { "art", "forging", "mining", "gathering", "hunting" };
    private static readonly (string Path, string Label)[] PortraitOptions =
    {
        ("player_01_buyi.png", "布衣少侠"),
        ("player_02_qingshan.png", "青衫剑客"),
        ("player_03_langke.png", "黑衣浪客"),
        ("player_04_hongyi.png", "红衣女侠"),
        ("player_05_baiyi.png", "白衣侠女"),
        ("player_06_youxia.png", "劲装游侠")
    };
    private const int CraftCapPerSkill = 15;          // 单项技艺上限
    private const int CraftTotalCap = 75;             // 5 * 15

    private readonly Random _rng = new();

    private TextBox _nameBox = null!;
    private RadioButton _genderMale = null!;
    private RadioButton _genderFemale = null!;
    private readonly Dictionary<string, Panel> _portraitCards = new();
    private string _selectedPortraitPath = PortraitOptions[0].Path;
    private Label _hpLabel = null!;
    private Label _atkLabel = null!;
    private Label _defLabel = null!;
    private Label _wuxingLabel = null!;

    private readonly Dictionary<string, Label> _craftLabels = new();
    private readonly Dictionary<string, int> _craftPoints = new();
    private readonly Dictionary<string, int> _craftBase = new();   // roll随机基础值(不可减少,只能往上+;+加的可减回)
    private Label _remainLabel = null!;

    private readonly Dictionary<TalentChoice, RadioButton> _talentRadios = new();
    private Label _talentDescLabel = null!;

    /// <summary>确认时填充;取消为 null。</summary>
    public CharacterCreationData? Result { get; private set; }

    // roll 基础值(天赋加成应用前)
    private int _hp, _atk, _def, _wuxing;

    public CharacterCreateForm()
    {
        Text = "初入江湖 · 角色创建";
        Size = new Size(720, 890);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        InitUI();
        RollAllAttributes();
        RollAllCrafts();
        RefreshAllDisplay();

        // 角色创建界面播放"侠客风云传"BGM(循环)
        Shown += (_, _) =>
        {
            var bgm = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music", "侠客风云传 - 贾爱国.mp3");
            Systems.AudioManager.Instance.PlayMusic(bgm, true);
        };
    }

    private void InitUI()
    {
        int y = 15;

        // 标题
        var title = new Label
        {
            Text = "～ 命由天定,运由己造 ～",
            Font = WuxiaTheme.UiFont(14f, FontStyle.Bold),
            ForeColor = Accent,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(680, 35),
            Location = new Point(20, y),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(title);
        y += 45;

        // ---- 名字 ----
        AddLabel("姓名:", 20, y + 5);
        _nameBox = new TextBox
        {
            Text = "小虾米",
            Location = new Point(80, y),
            Size = new Size(200, 25),
            BackColor = Surface,
            ForeColor = Fg,
            Font = F,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_nameBox);
        y += 40;

        // ---- 性别 ----
        AddLabel("性别:", 20, y + 5);
        _genderMale = new RadioButton { Text = "男", Location = new Point(80, y), Size = new Size(50, 25), ForeColor = Fg, Font = F, Checked = true };
        _genderFemale = new RadioButton { Text = "女", Location = new Point(140, y), Size = new Size(50, 25), ForeColor = Fg, Font = F };
        Controls.Add(_genderMale);
        Controls.Add(_genderFemale);
        y += 35;

        // ---- 头像 ----
        AddLabel("【选择头像】", 20, y);
        y += 25;

        var portraitPanel = new FlowLayoutPanel
        {
            Location = new Point(20, y),
            Size = new Size(650, 78),
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4)
        };
        foreach (var (path, label) in PortraitOptions)
        {
            var card = new Panel
            {
                Size = new Size(102, 68),
                BackColor = Surface,
                Cursor = Cursors.Hand,
                Margin = new Padding(2)
            };
            var picture = new PictureBox
            {
                Location = new Point(23, 2),
                Size = new Size(56, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = PortraitHelper.GetPortraitOrDefault(path, label, 56),
                Cursor = Cursors.Hand
            };
            var caption = new Label
            {
                Text = label,
                Location = new Point(2, 51),
                Size = new Size(98, 16),
                ForeColor = Fg,
                Font = WuxiaTheme.UiFont(7.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            void SelectCard(object? _, EventArgs __) => SelectPortrait(path);
            card.Click += SelectCard;
            picture.Click += SelectCard;
            caption.Click += SelectCard;
            card.Controls.Add(picture);
            card.Controls.Add(caption);
            _portraitCards[path] = card;
            portraitPanel.Controls.Add(card);
        }
        Controls.Add(portraitPanel);
        SelectPortrait(_selectedPortraitPath);
        y += 88;

        // ---- 属性 roll 区 ----
        AddLabel("【天生资质】(可反复重投,直到满意为止)", 20, y);
        y += 28;

        var rerollAllBtn = MakeBtn("全部重投", 540, y, 130, 30, Color.FromArgb(80, 60, 100));
        rerollAllBtn.Click += (_, _) => { RollAllAttributes(); RefreshAttrDisplay(); };
        Controls.Add(rerollAllBtn);

        _hpLabel = AddRollRow("血  量", 20, y);
        y += 38;
        _atkLabel = AddRollRow("攻  击", 20, y);
        y += 38;
        _defLabel = AddRollRow("防  御", 20, y);
        y += 38;
        _wuxingLabel = AddRollRow("悟  性", 20, y);
        y += 48;

        // ---- 技艺 roll 区 ----
        AddLabel("【技艺出身】(总值上限75,单项不限;先roll基础值,剩余点数自由分配)", 20, y);
        y += 28;

        var rerollCraftBtn = MakeBtn("重投技艺", 540, y, 130, 30, Color.FromArgb(80, 60, 100));
        rerollCraftBtn.Click += (_, _) => { RollAllCrafts(); RefreshCraftDisplay(); };
        Controls.Add(rerollCraftBtn);

        _remainLabel = new Label
        {
            Location = new Point(20, y),
            Size = new Size(300, 25),
            ForeColor = Accent,
            Font = Fb,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_remainLabel);
        y += 32;

        foreach (var id in CraftIds)
        {
            _craftPoints[id] = 0;
            _craftBase[id] = 0;
            AddLabel(CharacterBase.GetCraftSkillName(id), 20, y + 5);
            var valLabel = new Label
            {
                Location = new Point(90, y + 3),
                Size = new Size(60, 25),
                ForeColor = Accent,
                Font = Fb,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _craftLabels[id] = valLabel;
            Controls.Add(valLabel);

            var minusBtn = MakeBtn("－", 150, y, 32, 28, Color.FromArgb(100, 60, 60));
            minusBtn.Click += (_, _) => AdjustCraft(id, -1);
            Controls.Add(minusBtn);

            var plusBtn = MakeBtn("＋", 190, y, 32, 28, Color.FromArgb(60, 100, 60));
            plusBtn.Click += (_, _) => AdjustCraft(id, +1);
            Controls.Add(plusBtn);

            y += 36;
        }
        y += 8;

        // ---- 天赋区 ----
        AddLabel("【天赋异禀】(三选一)", 20, y);
        y += 28;

        var talents = new (TalentChoice t, string name, string desc)[]
        {
            (TalentChoice.XiaoXiaMi, "小虾米来也",
                "小虾米来体验生活,属于简单模式啦。\n强制姓名「小虾米」,血量+1000、攻击+20、防御+20。"),
            (TalentChoice.QingGongDaShi, "轻功大师",
                "快就是快。\n初始速度+20(基础50→70),先发制人。"),
            (TalentChoice.QinXueKuLian, "勤学苦练",
                "一分耕耘一分收获。\n武功修炼速度+50%,熟练度获取更快。")
        };

        _talentDescLabel = new Label
        {
            Location = new Point(280, y),
            Size = new Size(390, 90),
            ForeColor = Fg,
            Font = F,
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_talentDescLabel);

        foreach (var (t, name, desc) in talents)
        {
            var rb = new RadioButton
            {
                Text = name,
                Location = new Point(20, y),
                Size = new Size(240, 28),
                ForeColor = Fg,
                Font = Fb,
                BackColor = Color.Transparent,
                Checked = t == TalentChoice.None
            };
            rb.CheckedChanged += (_, _) =>
            {
                if (rb.Checked)
                {
                    _talentDescLabel.Text = desc;
                    UpdateNameLock();
                }
            };
            _talentRadios[t] = rb;
            Controls.Add(rb);
            y += 32;
        }
        // 默认不选天赋
        foreach (var rb in _talentRadios.Values) rb.Checked = false;
        _talentDescLabel.Text = "（未选择天赋）";
        y += 12;

        // ---- 底部按钮 ----
        var startBtn = MakeBtn("踏入江湖", 220, y, 130, 38, Color.FromArgb(60, 100, 60));
        startBtn.Font = Fb;
        startBtn.Click += OnConfirm;
        Controls.Add(startBtn);

        var cancelBtn = MakeBtn("返回", 370, y, 110, 38, Color.FromArgb(100, 60, 60));
        cancelBtn.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(cancelBtn);

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
    }

    private Label AddRollRow(string name, int x, int y)
    {
        AddLabel(name, x, y + 5);
        var valLabel = new Label
        {
            Location = new Point(x + 70, y + 3),
            Size = new Size(120, 25),
            ForeColor = Accent,
            Font = Fb,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(valLabel);
        return valLabel;
    }

    private void AddLabel(string text, int x, int y)
    {
        var l = new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Fg,
            Font = F
        };
        Controls.Add(l);
    }

    private static Button MakeBtn(string text, int x, int y, int w, int h, Color back)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = back,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 70);
        return b;
    }

    // ---------------- roll 逻辑 ----------------
    private int RollAttr(int min, int max) => _rng.Next(min, max + 1);

    private void RollAllAttributes()
    {
        _hp = RollAttr(400, 600);
        _atk = RollAttr(30, 60);
        _def = RollAttr(15, 35);
        _wuxing = RollAttr(1, 10);
    }

    private void RollAllCrafts()
    {
        foreach (var id in CraftIds)
        {
            _craftBase[id] = _rng.Next(0, CraftCapPerSkill + 1);
            _craftPoints[id] = _craftBase[id];
        }
    }

    private void AdjustCraft(string id, int delta)
    {
        int cur = _craftPoints[id];
        int used = _craftPoints.Values.Sum();
        if (delta > 0)
        {
            if (used >= CraftTotalCap) return;       // 总值已满,单项不设上限
            _craftPoints[id] = cur + 1;
        }
        else if (delta < 0 && cur > _craftBase[id])   // 只能减回+加的,不能低于随机基础值
        {
            _craftPoints[id] = cur - 1;
        }
        RefreshCraftDisplay();
    }

    // ---------------- 显示刷新 ----------------
    private void RefreshAllDisplay()
    {
        RefreshAttrDisplay();
        RefreshCraftDisplay();
    }

    private void SelectPortrait(string portraitPath)
    {
        _selectedPortraitPath = portraitPath;
        foreach (var (path, card) in _portraitCards)
            card.BackColor = path == portraitPath ? Accent : Surface;
    }

    private void RefreshAttrDisplay()
    {
        _hpLabel.Text = $"{_hp}";
        _atkLabel.Text = $"{_atk}";
        _defLabel.Text = $"{_def}";
        _wuxingLabel.Text = $"{_wuxing}";
    }

    private void RefreshCraftDisplay()
    {
        foreach (var id in CraftIds)
            _craftLabels[id].Text = $"{_craftPoints[id]}";
        int used = _craftPoints.Values.Sum();
        _remainLabel.Text = $"剩余可分配点数: {CraftTotalCap - used}";
    }

    private TalentChoice SelectedTalent()
    {
        foreach (var kv in _talentRadios)
            if (kv.Value.Checked) return kv.Key;
        return TalentChoice.None;
    }

    /// <summary>选了"小虾米来也"则锁定名字。</summary>
    private void UpdateNameLock()
    {
        if (SelectedTalent() == TalentChoice.XiaoXiaMi)
        {
            _nameBox.Text = "小虾米";
            _nameBox.Enabled = false;
        }
        else
        {
            _nameBox.Enabled = true;
        }
    }

    private void OnConfirm(object? sender, EventArgs e)
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "小虾米";

        var talent = SelectedTalent();
        if (talent == TalentChoice.XiaoXiaMi) name = "小虾米"; // 强制

        Result = new CharacterCreationData
        {
            Name = name,
            PortraitPath = _selectedPortraitPath,
            MaxHP = _hp,
            BaseAttack = _atk,
            BaseDefense = _def,
            Wuxing = _wuxing,
            CraftSkills = CraftIds.ToDictionary(id => id, id => _craftPoints[id]),
            Talent = talent,
            Gender = _genderFemale.Checked ? "女" : "男"
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
