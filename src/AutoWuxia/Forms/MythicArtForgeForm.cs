using AutoWuxia.MartialArts;

namespace AutoWuxia.Forms;

/// <summary>作者君奖励：在固定词条池中各选三项，铸成一内一外两门神话武学。</summary>
public sealed class MythicArtForgeForm : Form
{
    private sealed record Trait(string Name, EffectType Type, double Value, double Chance, string Description);

    private readonly TextBox _internalName = new() { Text = "天道真解", MaxLength = 12 };
    private readonly TextBox _externalName = new() { Text = "万象归一", MaxLength = 12 };
    private readonly CheckedListBox _internalTraits = new();
    private readonly CheckedListBox _externalTraits = new();

    public InternalArt? ForgedInternalArt { get; private set; }
    public ExternalArt? ForgedExternalArt { get; private set; }

    private static readonly Trait[] InternalTraitPool =
    {
        new("生生不息", EffectType.HPRecover, 0.22, 1, "每回合恢复22%气血"),
        new("万法不侵", EffectType.DamageReduction, 0.28, 1, "永久减免28%受到的伤害"),
        new("真元护体", EffectType.MPShield, 0.55, 1, "以内力抵消55%非真实伤害"),
        new("返照虚空", EffectType.ReflectDamage, 0.42, 0.7, "70%概率反弹42%受到的伤害"),
        new("吐纳归元", EffectType.MPRecover, 0.16, 1, "每回合恢复16%内力"),
        new("不动如山", EffectType.FlatDamageReduction, 280, 1, "受到攻击时固定减伤280")
    };

    private static readonly Trait[] ExternalTraitPool =
    {
        new("破尽万法", EffectType.IgnoreDefense, 0.72, 1, "无视72%防御"),
        new("剑气透骨", EffectType.TrueDamage, 0.28, 1, "附带28%真实伤害"),
        new("连环绝杀", EffectType.DoubleStrike, 1.0, 0.42, "42%概率追加一次完整连击"),
        new("乘势追击", EffectType.ExtraAttack, 0.65, 0.30, "30%概率追加65%伤害的追击"),
        new("后发制人", EffectType.CounterAttack, 0.80, 0.55, "受击时55%概率自动反击80%伤害"),
        new("夺气归元", EffectType.SiphonMPOnHit, 0.18, 1, "命中时吸取目标18%内力")
    };

    public MythicArtForgeForm()
    {
        Text = "作者君 · 神话武学铸成";
        Size = new Size(760, 570);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = WuxiaTheme.PanelBack;
        ForeColor = WuxiaTheme.Text;
        Font = WuxiaTheme.UiFont(10f);

        var title = new Label
        {
            Text = "从固定词条中各选三项，铸成专属于你的神话内功与外功",
            Location = new Point(18, 14), Size = new Size(710, 32),
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(13f, FontStyle.Bold)
        };

        Controls.Add(title);
        AddColumn("神话内功", 18, _internalName, _internalTraits, InternalTraitPool);
        AddColumn("神话外功", 388, _externalName, _externalTraits, ExternalTraitPool);

        var confirm = new Button { Text = "铸成武学", Location = new Point(250, 480), Size = new Size(120, 38) };
        WuxiaTheme.StyleButton(confirm, WuxiaTheme.Success);
        confirm.Click += (_, _) => Forge();
        var cancel = new Button { Text = "暂不铸成", Location = new Point(390, 480), Size = new Size(120, 38) };
        WuxiaTheme.StyleButton(cancel);
        cancel.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { confirm, cancel });

        WuxiaTheme.ApplyScaling(this);
    }

    private void AddColumn(string title, int x, TextBox nameBox, CheckedListBox list, IEnumerable<Trait> traits)
    {
        Controls.Add(new Label
        {
            Text = title, Location = new Point(x, 62), Size = new Size(340, 24),
            ForeColor = WuxiaTheme.Accent, Font = WuxiaTheme.UiFont(12f, FontStyle.Bold)
        });
        Controls.Add(new Label { Text = "名称（可自定）：", Location = new Point(x, 96), Size = new Size(110, 26) });
        nameBox.Location = new Point(x + 112, 92);
        nameBox.Size = new Size(220, 28);
        nameBox.BackColor = WuxiaTheme.Surface;
        nameBox.ForeColor = WuxiaTheme.Text;
        Controls.Add(nameBox);

        Controls.Add(new Label
        {
            Text = "固定词条（必须恰选3项）：", Location = new Point(x, 136), Size = new Size(320, 26),
            ForeColor = WuxiaTheme.TextMuted
        });
        list.Location = new Point(x, 165);
        list.Size = new Size(340, 285);
        list.CheckOnClick = true;
        list.BackColor = WuxiaTheme.Surface;
        list.ForeColor = WuxiaTheme.Text;
        list.BorderStyle = BorderStyle.FixedSingle;
        foreach (var trait in traits)
            list.Items.Add($"{trait.Name}：{trait.Description}", list.Items.Count < 3);
        list.ItemCheck += (_, e) =>
        {
            if (e.NewValue == CheckState.Checked && list.CheckedItems.Count >= 3)
            {
                e.NewValue = CheckState.Unchecked;
                System.Media.SystemSounds.Beep.Play();
            }
        };
        Controls.Add(list);
    }

    private void Forge()
    {
        var internalName = _internalName.Text.Trim();
        var externalName = _externalName.Text.Trim();
        if (string.IsNullOrWhiteSpace(internalName) || string.IsNullOrWhiteSpace(externalName))
        {
            MessageBox.Show(this, "请为两门武学分别命名。", "尚未命名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_internalTraits.CheckedItems.Count != 3 || _externalTraits.CheckedItems.Count != 3)
        {
            MessageBox.Show(this, "内功与外功都必须恰好选择3个固定词条。", "词条数量不符", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedInternal = _internalTraits.CheckedIndices.Cast<int>().Select(i => InternalTraitPool[i]).ToList();
        var selectedExternal = _externalTraits.CheckedIndices.Cast<int>().Select(i => ExternalTraitPool[i]).ToList();
        ForgedInternalArt = new InternalArt
        {
            Id = "author_mythic_internal",
            Name = internalName,
            Description = "作者君见证你自万象中择其三而成的神话内功。",
            MaxLevel = 10,
            Rarity = "mythic",
            Proficiency = MartialArtBase.GetProficiencyForLevel(10, "mythic"),
            Cooldown = 3,
            MPCost = 120,
            HPBonusPerLevel = 520,
            MPBonusPerLevel = 980,
            AttackBonusPerLevel = 58,
            DefenseBonusPerLevel = 62,
            Effects = selectedInternal.Select(ToEffect).ToList()
        };
        ForgedExternalArt = new ExternalArt
        {
            Id = "author_mythic_external",
            Name = externalName,
            Description = "作者君见证你自万象中择其三而成的神话外功。",
            MaxLevel = 10,
            Rarity = "mythic",
            Proficiency = MartialArtBase.GetProficiencyForLevel(10, "mythic"),
            Cooldown = 2,
            MPCost = 110,
            DamageCoefficient = 4.6,
            CritChance = 0.52,
            Effects = selectedExternal.Select(ToEffect).ToList()
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static ArtEffect ToEffect(Trait trait) => new()
    {
        Type = trait.Type,
        Value = trait.Value,
        Chance = trait.Chance,
        Description = trait.Name + "：" + trait.Description
    };
}
