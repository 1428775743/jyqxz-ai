namespace AutoWuxia.Forms;

public enum PostCombatChoice
{
    Kill,
    Humiliate,
    Spare
}

/// <summary>生死战胜利后的强制处置弹窗，避免在主窗口临时插入操作按钮。</summary>
public static class PostCombatChoiceBox
{
    public static PostCombatChoice Show(IWin32Window owner, string npcName)
    {
        using var form = new Form
        {
            Text = "战后处置",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            ClientSize = new Size(450, 205),
            BackColor = Color.FromArgb(25, 25, 35)
        };

        var title = new Label
        {
            Text = "战后处置",
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var message = new Label
        {
            Text = $"{npcName}已无再战之力。\n你打算如何处置？",
            Location = new Point(24, 62),
            Size = new Size(402, 55),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(10.5f),
            TextAlign = ContentAlignment.MiddleCenter
        };

        PostCombatChoice choice = PostCombatChoice.Spare;
        bool selected = false;
        Button MakeButton(string text, Color color, int x, PostCombatChoice value)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, 138),
                Size = new Size(120, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.FromArgb(245, 240, 225),
                Font = WuxiaTheme.UiFont(10f),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(224, 139, 43);
            button.Click += (_, _) => { choice = value; selected = true; form.DialogResult = DialogResult.OK; };
            return button;
        }

        form.FormClosing += (_, e) =>
        {
            if (!selected)
                e.Cancel = true;
        };

        form.Controls.Add(title);
        form.Controls.Add(message);
        form.Controls.Add(MakeButton("杀死", Color.FromArgb(120, 50, 50), 27, PostCombatChoice.Kill));
        form.Controls.Add(MakeButton("羞辱", Color.FromArgb(130, 90, 40), 165, PostCombatChoice.Humiliate));
        form.Controls.Add(MakeButton("放过", Color.FromArgb(50, 90, 60), 303, PostCombatChoice.Spare));
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(owner);
        return choice;
    }
}
