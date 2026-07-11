using AutoWuxia.AI;
using AutoWuxia.Config;
using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

public class SettingsForm : Form
{
    private readonly AIService? _aiService;
    private TextBox _endpointBox = null!;
    private TextBox _apiKeyBox = null!;
    private TextBox _modelBox = null!;
    private NumericUpDown _maxIterationsBox = null!;

    private CheckBox _musicEnabledBox = null!;
    private TrackBar _musicVolumeBar = null!;
    private Label _musicVolumeLabel = null!;
    private TrackBar _soundVolumeBar = null!;
    private Label _soundVolumeLabel = null!;

    private ListBox _musicListBox = null!;
    private Button _previewBtn = null!;
    private Button _stopPreviewBtn = null!;
    private Label _nowPlayingLabel = null!;
    private List<string> _musicFiles = new();
    private ComboBox _scaleBox = null!;
    /// <summary>缩放倍率是否被修改(调用方据此决定是否刷新界面)。</summary>
    public bool ScaleChanged { get; private set; }
    private static readonly double[] Scales = { 1.0, 1.25, 1.5, 2.0 };

    private static readonly Color Bg = Color.FromArgb(35, 35, 50);
    private static readonly Color Surface = Color.FromArgb(25, 25, 40);
    private static readonly Color Accent = Color.FromArgb(230, 180, 90);
    private static readonly Color Fg = Color.FromArgb(220, 220, 200);
    private static readonly Color Muted = Color.FromArgb(150, 150, 150);
    private static readonly Font F = WuxiaTheme.UiFont(9f);

    /// <summary>
    /// aiService 可为 null:在首页打开时没有运行中的 AIService,
    /// 保存仅写入 ai_config.json,主界面后续会读取。
    /// </summary>
    public SettingsForm(AIService? aiService)
    {
        _aiService = aiService;
        InitUI();
    }

    private void InitUI()
    {
        Text = "设置";
        Size = new Size(580, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Bg;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tab = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = F,
            Appearance = TabAppearance.Normal
        };
        tab.TabPages.Add(BuildAiTab());
        tab.TabPages.Add(BuildAudioTab());
        tab.TabPages.Add(BuildPreviewTab());
        tab.TabPages.Add(BuildDisplayTab());

        // 底部统一保存按钮
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Bg
        };
        var saveButton = new Button
        {
            Text = "保存全部设置",
            Location = new Point(20, 12),
            Size = new Size(150, 34),
            BackColor = Color.FromArgb(60, 100, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F
        };
        saveButton.Click += SaveAll;
        bottomPanel.Controls.Add(saveButton);

        Controls.Add(tab);
        Controls.Add(bottomPanel);

        // 窗体关闭时停止试听,避免关闭后仍在播放
        FormClosing += (_, _) => AudioManager.Instance.StopPreview();

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
    }

    // ---------------- AI 设置页 ----------------
    private TabPage BuildAiTab()
    {
        var page = new TabPage("AI 设置") { BackColor = Bg };
        var config = AIConfig.Load();

        AddLabel(page, "API Endpoint:", 20, 20);
        _endpointBox = AddTextBox(page, config.ApiEndpoint, 20, 45, 510);

        AddLabel(page, "API Key:", 20, 85);
        _apiKeyBox = AddTextBox(page, config.ApiKey, 20, 110, 510);
        _apiKeyBox.UseSystemPasswordChar = true;

        AddLabel(page, "Model:", 20, 150);
        _modelBox = AddTextBox(page, config.Model, 20, 175, 510);

        AddLabel(page, "月度 Agent 最大轮次:", 20, 215);
        _maxIterationsBox = new NumericUpDown
        {
            Location = new Point(20, 240),
            Size = new Size(120, 25),
            Minimum = 5,
            Maximum = 500,
            Value = config.MonthlyMaxIterations,
            BackColor = Surface,
            ForeColor = Fg,
            Font = F,
            BorderStyle = BorderStyle.FixedSingle
        };
        page.Controls.Add(_maxIterationsBox);

        var hintLabel = new Label
        {
            Text = "(建议50~200,轮次越多NPC变化越丰富,耗时越久)",
            Location = new Point(150, 243),
            AutoSize = true,
            ForeColor = Muted,
            Font = WuxiaTheme.UiFont(8f)
        };
        page.Controls.Add(hintLabel);

        // 预设:点击快速填入 Endpoint 与 Model
        AddLabel(page, "快速预设(点击填入 Endpoint 与 Model):", 20, 280);

        var dsPresetBtn = new Button
        {
            Text = "DeepSeek 预设",
            Location = new Point(20, 305),
            Size = new Size(150, 30),
            BackColor = Color.FromArgb(60, 60, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F
        };
        dsPresetBtn.Click += (_, _) =>
        {
            _endpointBox.Text = "https://api.deepseek.com";
            _modelBox.Text = "deepseek-v4-flash";
        };
        page.Controls.Add(dsPresetBtn);

        var mimoPresetBtn = new Button
        {
            Text = "MiMo 预设",
            Location = new Point(180, 305),
            Size = new Size(150, 30),
            BackColor = Color.FromArgb(100, 60, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F
        };
        mimoPresetBtn.Click += (_, _) =>
        {
            _endpointBox.Text = "https://api.xiaomimimo.com";
            _modelBox.Text = "mimo-v2.5-pro";
        };
        page.Controls.Add(mimoPresetBtn);

        // 推荐 API 服务
        AddLabel(page, "推荐 API 服务(获取 API Key):", 20, 345);

        var mimoLink = new LinkLabel
        {
            Text = "小米 MiMo(推荐,首次注册送5元体验金额): https://platform.xiaomimimo.com/console/recharge",
            Location = new Point(20, 370),
            AutoSize = true,
            Font = WuxiaTheme.UiFont(9f, FontStyle.Underline),
            LinkColor = Color.FromArgb(150, 200, 255),
            ActiveLinkColor = Color.FromArgb(255, 220, 150),
            VisitedLinkColor = Color.FromArgb(200, 180, 220)
        };
        mimoLink.LinkClicked += (_, _) => OpenUrl("https://platform.xiaomimimo.com/console/recharge");
        page.Controls.Add(mimoLink);

        var dsLink = new LinkLabel
        {
            Text = "DeepSeek: https://platform.deepseek.com/",
            Location = new Point(20, 395),
            AutoSize = true,
            Font = WuxiaTheme.UiFont(9f, FontStyle.Underline),
            LinkColor = Color.FromArgb(150, 200, 255),
            ActiveLinkColor = Color.FromArgb(255, 220, 150),
            VisitedLinkColor = Color.FromArgb(200, 180, 220)
        };
        dsLink.LinkClicked += (_, _) => OpenUrl("https://platform.deepseek.com/");
        page.Controls.Add(dsLink);

        var testButton = new Button
        {
            Text = "测试连接",
            Location = new Point(20, 425),
            Size = new Size(120, 34),
            BackColor = Color.FromArgb(60, 60, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F
        };
        testButton.Click += async (_, _) => await TestConnection();
        page.Controls.Add(testButton);

        return page;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开浏览器失败: {ex.Message}\n请手动访问: {url}",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ---------------- 音效设置页 ----------------
    private TabPage BuildAudioTab()
    {
        var page = new TabPage("音效设置") { BackColor = Bg };
        var cfg = AudioManager.Instance.Config;

        _musicEnabledBox = new CheckBox
        {
            Text = "启用背景音乐",
            Location = new Point(20, 25),
            AutoSize = true,
            ForeColor = Fg,
            Font = F,
            Checked = cfg.MusicEnabled
        };
        _musicEnabledBox.CheckedChanged += (_, _) =>
        {
            AudioManager.Instance.SetMusicEnabled(_musicEnabledBox.Checked);
            _musicVolumeBar.Enabled = _musicEnabledBox.Checked;
        };
        page.Controls.Add(_musicEnabledBox);

        AddLabel(page, "背景音乐音量:", 20, 70);
        _musicVolumeBar = new TrackBar
        {
            Location = new Point(20, 95),
            Size = new Size(420, 45),
            Minimum = 0,
            Maximum = 100,
            Value = cfg.MusicVolume,
            TickFrequency = 10,
            Enabled = cfg.MusicEnabled
        };
        _musicVolumeBar.Scroll += (_, _) =>
        {
            _musicVolumeLabel.Text = $"{_musicVolumeBar.Value}%";
            AudioManager.Instance.SetMusicVolume(_musicVolumeBar.Value);
        };
        page.Controls.Add(_musicVolumeBar);

        _musicVolumeLabel = new Label
        {
            Text = $"{cfg.MusicVolume}%",
            Location = new Point(450, 105),
            AutoSize = true,
            ForeColor = Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        page.Controls.Add(_musicVolumeLabel);

        AddLabel(page, "音效音量(预留):", 20, 155);
        _soundVolumeBar = new TrackBar
        {
            Location = new Point(20, 180),
            Size = new Size(420, 45),
            Minimum = 0,
            Maximum = 100,
            Value = cfg.SoundVolume,
            TickFrequency = 10
        };
        _soundVolumeBar.Scroll += (_, _) =>
        {
            _soundVolumeLabel.Text = $"{_soundVolumeBar.Value}%";
        };
        page.Controls.Add(_soundVolumeBar);

        _soundVolumeLabel = new Label
        {
            Text = $"{cfg.SoundVolume}%",
            Location = new Point(450, 190),
            AutoSize = true,
            ForeColor = Accent,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        page.Controls.Add(_soundVolumeLabel);

        var tip = new Label
        {
            Text = "提示:当前版本音效资源尚在筹备,音效音量设置将保留供后续使用。\n背景音乐音量调整即时生效。",
            Location = new Point(20, 245),
            Size = new Size(510, 50),
            ForeColor = Muted,
            Font = WuxiaTheme.UiFont(8f)
        };
        page.Controls.Add(tip);

        return page;
    }

    // ---------------- 音乐试听页 ----------------
    private TabPage BuildPreviewTab()
    {
        var page = new TabPage("音乐试听") { BackColor = Bg };

        AddLabel(page, "音乐列表(assets/music 目录):", 20, 15);

        _musicListBox = new ListBox
        {
            Location = new Point(20, 40),
            Size = new Size(510, 340),
            BackColor = Surface,
            ForeColor = Fg,
            Font = F,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        _musicFiles = AudioManager.ListMusicFiles();
        foreach (var f in _musicFiles)
            _musicListBox.Items.Add(Path.GetFileName(f));
        if (_musicListBox.Items.Count > 0) _musicListBox.SelectedIndex = 0;
        page.Controls.Add(_musicListBox);

        _previewBtn = new Button
        {
            Text = "播放试听",
            Location = new Point(20, 395),
            Size = new Size(120, 34),
            BackColor = Color.FromArgb(60, 100, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F
        };
        _previewBtn.Click += (_, _) =>
        {
            if (_musicListBox.SelectedIndex < 0 || _musicListBox.SelectedIndex >= _musicFiles.Count) return;
            var file = _musicFiles[_musicListBox.SelectedIndex];
            try
            {
                AudioManager.Instance.PlayPreview(file);
                _nowPlayingLabel.Text = "正在试听: " + Path.GetFileName(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放失败: {ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        page.Controls.Add(_previewBtn);

        _stopPreviewBtn = new Button
        {
            Text = "停止",
            Location = new Point(150, 395),
            Size = new Size(90, 34),
            BackColor = Color.FromArgb(100, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = F
        };
        _stopPreviewBtn.Click += (_, _) =>
        {
            AudioManager.Instance.StopPreview();
            _nowPlayingLabel.Text = "已停止";
        };
        page.Controls.Add(_stopPreviewBtn);

        _nowPlayingLabel = new Label
        {
            Text = "未播放",
            Location = new Point(255, 403),
            AutoSize = true,
            ForeColor = Accent,
            Font = F
        };
        page.Controls.Add(_nowPlayingLabel);

        return page;
    }

    // ---------------- 显示设置页 ----------------
    private TabPage BuildDisplayTab()
    {
        var page = new TabPage("显示") { BackColor = Bg };
        var cfg = DisplayConfig.Load();

        AddLabel(page, "界面缩放倍率:", 20, 20);
        _scaleBox = new ComboBox
        {
            Location = new Point(20, 45),
            Size = new Size(120, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Surface,
            ForeColor = Fg,
            Font = F
        };
        foreach (var s in Scales)
            _scaleBox.Items.Add($"{(int)(s * 100)}%");
        int idx = Array.FindIndex(Scales, x => Math.Abs(x - cfg.ScaleFactor) < 0.01);
        _scaleBox.SelectedIndex = idx >= 0 ? idx : 0;
        page.Controls.Add(_scaleBox);

        var scaleTip = new Label
        {
            Text = "用于 4K 等高分辨率显示器上游戏偏小的情况。\n倍率越大界面越大。修改后需重启游戏生效。",
            Location = new Point(20, 80),
            Size = new Size(510, 50),
            ForeColor = Muted,
            Font = WuxiaTheme.UiFont(8f)
        };
        page.Controls.Add(scaleTip);

        return page;
    }

    // ---------------- 通用 helper ----------------
    private static void AddLabel(Control parent, string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Fg,
            Font = F
        };
        parent.Controls.Add(label);
    }

    private static TextBox AddTextBox(Control parent, string text, int x, int y, int width)
    {
        var box = new TextBox
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 25),
            BackColor = Surface,
            ForeColor = Fg,
            Font = F,
            BorderStyle = BorderStyle.FixedSingle
        };
        parent.Controls.Add(box);
        return box;
    }

    private void SaveAll(object? sender, EventArgs e)
    {
        // AI 配置
        var aiConfig = new AIConfig
        {
            ApiEndpoint = _endpointBox.Text,
            ApiKey = _apiKeyBox.Text,
            Model = _modelBox.Text,
            MonthlyMaxIterations = (int)_maxIterationsBox.Value
        };
        aiConfig.Save();
        _aiService?.UpdateConfig(aiConfig);

        // 音频配置:从 AudioManager 单例(已被本窗体即时修改)保存
        var audioCfg = AudioManager.Instance.Config;
        audioCfg.MusicEnabled = _musicEnabledBox.Checked;
        audioCfg.MusicVolume = _musicVolumeBar.Value;
        audioCfg.SoundVolume = _soundVolumeBar.Value;
        audioCfg.Save();

        // 显示配置(缩放倍率)
        if (_scaleBox.SelectedIndex >= 0)
        {
            double oldScale = WuxiaTheme.Scale;
            double newScale = Scales[_scaleBox.SelectedIndex];
            var displayCfg = new DisplayConfig { ScaleFactor = newScale };
            displayCfg.Save();
            WuxiaTheme.Scale = newScale;
            ScaleChanged = Math.Abs(newScale - oldScale) > 0.001;
        }

        MessageBox.Show("设置已保存!\n(界面缩放:首页会自动刷新;游戏主界面需重启游戏生效)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task TestConnection()
    {
        var config = new AIConfig
        {
            ApiEndpoint = _endpointBox.Text,
            ApiKey = _apiKeyBox.Text,
            Model = _modelBox.Text
        };
        var testService = new AIService(config);
        var result = await testService.ChatAsync("你是一个助手", "请回复'连接成功'");
        MessageBox.Show($"测试结果:\n{result}", "测试", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
