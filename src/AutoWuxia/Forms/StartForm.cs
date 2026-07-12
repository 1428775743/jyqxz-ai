using System.Text.Json;
using AutoWuxia.AI;
using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

/// <summary>
/// 游戏首页:四个竖排古风按钮(新游戏/读档/未启用/退出)。
/// 关闭时通过 Action 返回 -1=退出 / 0=新游戏 / 1~MaxSlots=读档槽位。
/// </summary>
public class StartForm : Form
{
    private const int MaxSaveSlots = 5;
    private Image? _backgroundImage;

    /// <summary>选择结果:null=未做选择(关闭窗体), -1=退出, 0=新游戏, ≥1=读档槽位</summary>
    public int? Selection { get; private set; }

    /// <summary>缩放变更后需重新加载首页(由 Program 循环读取)。</summary>
    public bool RestartRequested { get; private set; }

    public StartForm()
    {
        Text = "金庸群侠传-AI";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 22, 16);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        _backgroundImage = ImageAssetLoader.Load("assets/ui/start-wuxia-ink.jpg");
        if (_backgroundImage != null)
        {
            BackgroundImage = _backgroundImage;
            BackgroundImageLayout = ImageLayout.Stretch;
        }

        BuildLayout();
        FormClosed += (_, _) =>
        {
            BackgroundImage = null;
            _backgroundImage?.Dispose();
        };

        // 首页播放大地图 BGM
        Shown += (_, _) =>
        {
            var bgm = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music", "大地图 - 华语群星.mp3");
            Systems.AudioManager.Instance.PlayMusic(bgm, true);
        };
    }

    private void BuildLayout()
    {
        // 标题:横排"金庸群侠传"五个大字
        var titleLabel = new Label
        {
            Text = "金庸群侠传-AI",
            Font = WuxiaTheme.UiFont(36f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 180, 90),
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(880, 90),
            Location = new Point(0, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(titleLabel);

        // 副标题
        var subtitleLabel = new Label
        {
            Text = "～江湖路远，剑气长存～",
            Font = WuxiaTheme.UiFont(12f, FontStyle.Italic),
            ForeColor = Color.FromArgb(180, 140, 80),
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(880, 30),
            Location = new Point(0, 140),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(subtitleLabel);

        // 四个按钮:竖排古风,每个按钮文字"上下排列"(竖排)。
        // 由于 Windows 文本竖排支持不完美,实现方式:每个按钮文字用 "字\n字\n字" 的方式做出"竖排"视觉效果。
        var tooltip = new ToolTip
        {
            ForeColor = Color.FromArgb(230, 180, 90),
            BackColor = Color.FromArgb(40, 32, 24),
            AutoPopDelay = 8000,
            InitialDelay = 200,
            ReshowDelay = 100
        };

        var buttons = new (string text, string tip, EventHandler onClick)[]
        {
            ("天\n下\n风\n云\n出\n我\n辈", "新游戏",  (_, _) => { if (CheckAIBeforeStart()) Choose(0); }),
            ("一\n入\n江\n湖\n岁\n月\n催", "读档",    (_, _) => { if (CheckAIBeforeStart()) OpenLoadDialog(); }),
            ("皇\n图\n霸\n业\n谈\n笑\n中", "游戏介绍", (_, _) => ShowGameIntro()),
            ("不\n胜\n人\n生\n一\n场\n醉", "退出游戏", (_, _) => Choose(-1)),
        };

        const int btnW = 80;
        const int btnH = 290;
        const int gap = 28;
        int totalW = btnW * 4 + gap * 3;
        int startX = (ClientSize.Width - totalW) / 2;
        int btnY = 210;

        for (int i = 0; i < buttons.Length; i++)
        {
            var (text, tip, onClick) = buttons[i];
            var btn = new Controls.WuxiaMenuButton
            {
                Text = text,
                Location = new Point(startX + i * (btnW + gap), btnY),
                Size = new Size(btnW, btnH),
                ForeColor = Color.FromArgb(230, 180, 90),
                Font = WuxiaTheme.UiFont(16f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                AccessibleName = tip
            };
            btn.Click += onClick;
            tooltip.SetToolTip(btn, tip);
            Controls.Add(btn);
        }

        var footer = new Label
        {
            Text = "© AutoWuxia",
            Font = WuxiaTheme.UiFont(8f),
            ForeColor = Color.FromArgb(100, 80, 50),
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(880, 22),
            Location = new Point(0, 540),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(footer);

        // 右上角设置图标按钮(齿轮)
        var settingsBtn = new Button
        {
            Text = "⚙",
            Location = new Point(840, 12),
            Size = new Size(44, 44),
            BackColor = Color.FromArgb(58, 42, 30),
            ForeColor = Color.FromArgb(230, 180, 90),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(18f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };
        settingsBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 70);
        settingsBtn.FlatAppearance.BorderSize = 1;
        settingsBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 62, 42);
        settingsBtn.Click += (_, _) =>
        {
            using var sf = new SettingsForm(null);
            sf.ShowDialog(this);
            if (sf.ScaleChanged)
            {
                RestartRequested = true;
                Close();  // 关闭首页,Program 循环重新创建(应用新缩放)
            }
        };
        tooltip.SetToolTip(settingsBtn, "设置 AI 模型");
        Controls.Add(settingsBtn);

        var achievementsBtn = new Button
        {
            Text = "成就",
            Location = new Point(755, 18),
            Size = new Size(76, 32),
            BackColor = Color.FromArgb(58, 42, 30),
            ForeColor = Color.FromArgb(230, 180, 90),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        achievementsBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 70);
        achievementsBtn.Click += (_, _) =>
        {
            using var form = new AchievementsForm();
            form.ShowDialog(this);
        };
        tooltip.SetToolTip(achievementsBtn, "查看已解锁的江湖结局成就");
        Controls.Add(achievementsBtn);

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
    }

    /// <summary>检查 AI 是否已配置(endpoint + apiKey + model 均非空)。</summary>
    private static bool IsAIConfigured()
    {
        var cfg = AIConfig.Load();
        return !string.IsNullOrWhiteSpace(cfg.ApiEndpoint)
            && !string.IsNullOrWhiteSpace(cfg.ApiKey)
            && !string.IsNullOrWhiteSpace(cfg.Model);
    }

    /// <summary>
    /// 开始游戏/读档前校验 AI 配置:未设置则弹提示并打开设置窗口,
    /// 保存后再次检查,仍未配置则不进游戏。
    /// </summary>
    private bool CheckAIBeforeStart()
    {
        if (IsAIConfigured()) return true;

        var r = MessageBox.Show(this,
            "尚未配置 AI 模型。游戏中的对话、NPC 行为决策、月度演化等核心玩法均依赖 AI。\n\n是否现在打开设置?",
            "需要配置 AI", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return false;

        using (var sf = new SettingsForm(null))
        {
            sf.ShowDialog(this);
        }

        if (IsAIConfigured()) return true;

        MessageBox.Show(this, "AI 仍未配置,无法进入游戏。请在设置中填写 Endpoint / API Key / Model 后再开始。",
            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private void Choose(int result)
    {
        Selection = result;
        Close();
    }

    /// <summary>读档对话框:扫描 saves 目录列出可用存档。</summary>
    private void OpenLoadDialog()
    {
        var dir = Core.AppPaths.SavesDir;
        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this, "没有存档。", "读档", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new Form
        {
            Text = "选择存档",
            Size = new Size(440, 360),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(40, 32, 24)
        };

        var listBox = new ListBox
        {
            Location = new Point(15, 15),
            Size = new Size(395, 250),
            BackColor = Color.FromArgb(58, 42, 30),
            ForeColor = Color.FromArgb(230, 180, 90),
            Font = WuxiaTheme.UiFont(10f),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        form.Controls.Add(listBox);

        var slotIds = new List<int>();
        for (int i = 1; i <= MaxSaveSlots; i++)
        {
            var info = ReadSaveInfo(dir, i);
            if (info != null)
            {
                listBox.Items.Add($"存档{i}: {info.Value.player} 第{info.Value.day}天 [{info.Value.scene}] ({info.Value.time:MM-dd HH:mm})");
                slotIds.Add(i);
            }
            else
            {
                listBox.Items.Add($"存档{i}: [空]");
                slotIds.Add(-i); // 负数标记空槽
            }
        }

        var okBtn = new Button
        {
            Text = "读取",
            Location = new Point(220, 280),
            Size = new Size(90, 32),
            BackColor = Color.FromArgb(80, 56, 36),
            ForeColor = Color.FromArgb(230, 180, 90),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(10f)
        };
        okBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 70);
        okBtn.Click += (_, _) =>
        {
            if (listBox.SelectedIndex < 0) return;
            int marker = slotIds[listBox.SelectedIndex];
            if (marker < 0)
            {
                MessageBox.Show(form, "该存档位为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Selection = marker;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };
        form.Controls.Add(okBtn);

        var cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(320, 280),
            Size = new Size(90, 32),
            BackColor = Color.FromArgb(80, 56, 36),
            ForeColor = Color.FromArgb(230, 180, 90),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(10f)
        };
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 70);
        cancelBtn.Click += (_, _) => form.Close();
        form.Controls.Add(cancelBtn);

        WuxiaTheme.ApplyScaling(form);
        if (form.ShowDialog(this) == DialogResult.OK)
            Close();
    }

    /// <summary>游戏介绍窗口。</summary>
    private void ShowGameIntro()
    {
        var form = new Form
        {
            Text = "江湖序 · 游戏介绍",
            Size = new Size(720, 640),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(40, 32, 24)
        };

        var box = new RichTextBox
        {
            Location = new Point(15, 15),
            Size = new Size(675, 545),
            BackColor = Color.FromArgb(58, 42, 30),
            ForeColor = Color.FromArgb(230, 180, 90),
            Font = WuxiaTheme.UiFont(11f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        box.Text = @"【金庸群侠传-AI】

这是一方由 AI 驱动的金庸武侠江湖。你以一介小虾米之身踏入武林，于刀光剑影、恩怨情仇中闯荡，习武、结交、行侠或为恶，闯出属于你的传说。

【自由江湖】
大地图纵横九州，城镇、山门、秘境皆可前往。各方人物各有性格、行踪与日程，江湖因你而动，亦自行演化——每月众生各有机缘，每年风云际会。

【武学体系】
内修一门主内功、两门辅内功，外练最多三本外功，再配一门轻功身法。武功分品阶，熟练度循序渐进，搭配出招决定战斗风格——刚猛、阴毒、防守反击，皆由你选。

【读条战斗】
采用读条制回合交锋，速度高者先手。防御以百分比减伤，真实伤害可绕过防御。连击、反击、追击、暴击、流血、眩晕，招招皆有计较。

【门派师承】
可拜入各大门派，积贡献、学绝艺；亦可云游四方，向各派高手请教切磋。送礼结交、论道比武，关系亲疏影响你所获。

【生活技艺】
采药、挖矿、打猎、锻造、烹饪、医药——江湖不只是打打杀杀。药材可制丹，食材可烹佳肴，矿石可锻神兵，各有增益。

【善恶由心】
一举一动皆系善恶。行侠仗义得人心，为非作歹启恶缘。善恶分流，江湖看待你的目光随之而变。

【任务经历】
环环相扣的江湖使命等待你去完成，你所经历的每一场战斗、每一次突破、每一段际遇，皆记入生平经历，供你回望。

【华山论剑】
声望赫赫之时，华山之巅的邀请自会到来。天下英雄齐聚，谁能问鼎武林，便看这一路修行。

江湖路远，剑气长存。";
        form.Controls.Add(box);

        var closeBtn = new Button
        {
            Text = "关闭",
            Location = new Point(300, 570),
            Size = new Size(120, 36),
            BackColor = Color.FromArgb(80, 56, 36),
            ForeColor = Color.FromArgb(230, 180, 90),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(10f)
        };
        closeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 70);
        closeBtn.Click += (_, _) => form.Close();
        form.Controls.Add(closeBtn);

        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(this);
    }

    /// <summary>读取存档元信息(无需GameEngine)。</summary>
    private static (string player, int day, string scene, DateTime time)? ReadSaveInfo(string dir, int slot)
    {
        var path = Path.Combine(dir, $"save_{slot}.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            string player = "未知";
            int day = 0;
            string scene = "未知";

            if (root.TryGetProperty("Player", out var p) && p.TryGetProperty("Name", out var n))
                player = n.GetString() ?? player;
            if (root.TryGetProperty("GameTime", out var gt) && gt.TryGetProperty("Day", out var d))
                day = d.GetInt32();
            string sceneId = root.TryGetProperty("CurrentSceneId", out var sId) ? (sId.GetString() ?? "") : "";
            if (root.TryGetProperty("AllScenes", out var allScenes)
                && !string.IsNullOrEmpty(sceneId)
                && allScenes.TryGetProperty(sceneId, out var sObj)
                && sObj.TryGetProperty("Name", out var sName))
            {
                scene = sName.GetString() ?? scene;
            }

            return (player, day, scene, File.GetLastWriteTime(path));
        }
        catch
        {
            return null;
        }
    }
}
