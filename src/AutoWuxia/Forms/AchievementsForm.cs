using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

/// <summary>首页可查看的跨存档成就册。</summary>
public sealed class AchievementsForm : Form
{
    private readonly AchievementProfile _profile;
    private readonly ListBox _list = new();
    private readonly RichTextBox _details = new();

    public AchievementsForm()
    {
        _profile = AchievementService.Load();
        Text = "江湖成就";
        Size = new Size(760, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(40, 32, 24);
        ForeColor = Color.FromArgb(230, 180, 90);
        Font = WuxiaTheme.UiFont(10f);

        var title = new Label
        {
            Text = $"江湖成就 · 已解锁 {_profile.UnlockedIds.Count}/{AchievementService.GetDefinitions().Count}",
            Location = new Point(16, 14), Size = new Size(710, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = WuxiaTheme.UiFont(14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 220, 150)
        };
        _list.Location = new Point(16, 62);
        _list.Size = new Size(270, 385);
        _list.BackColor = Color.FromArgb(58, 42, 30);
        _list.ForeColor = Color.FromArgb(230, 180, 90);
        _list.BorderStyle = BorderStyle.FixedSingle;

        _details.Location = new Point(304, 62);
        _details.Size = new Size(420, 385);
        _details.ReadOnly = true;
        _details.BackColor = Color.FromArgb(58, 42, 30);
        _details.ForeColor = Color.FromArgb(240, 225, 195);
        _details.BorderStyle = BorderStyle.None;
        _details.Font = WuxiaTheme.UiFont(10f);

        foreach (var achievement in AchievementService.GetDefinitions())
        {
            var unlocked = _profile.UnlockedIds.Contains(achievement.Id);
            _list.Items.Add(new AchievementListItem(achievement, unlocked));
        }
        _list.SelectedIndexChanged += (_, _) => ShowDetails();
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;

        var close = new Button { Text = "关闭", Location = new Point(624, 458), Size = new Size(100, 32) };
        WuxiaTheme.StyleButton(close);
        close.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { title, _list, _details, close });
        WuxiaTheme.ApplyScaling(this);
    }

    private void ShowDetails()
    {
        _details.Clear();
        if (_list.SelectedItem is not AchievementListItem item) return;
        _details.SelectionColor = item.Unlocked ? WuxiaTheme.Success : Color.FromArgb(190, 170, 135);
        _details.SelectionFont = WuxiaTheme.UiFont(13f, FontStyle.Bold);
        _details.AppendText((item.Unlocked ? "已解锁 · " : "未解锁 · ") + item.Definition.Name + "\n\n");
        _details.SelectionColor = Color.FromArgb(240, 225, 195);
        _details.SelectionFont = WuxiaTheme.UiFont(10f);
        _details.AppendText(item.Definition.Description + "\n\n");
        if (item.Definition.Id == "ending_all_main_stories")
            _details.AppendText(MainStoryGuide.BuildStartNpcGuide());
    }

    private sealed record AchievementListItem(AchievementDefinition Definition, bool Unlocked)
    {
        public override string ToString() => (Unlocked ? "★ " : "◇ ") + Definition.Name;
    }
}
