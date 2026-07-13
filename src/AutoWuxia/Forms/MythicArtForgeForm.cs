using AutoWuxia.MartialArts;

namespace AutoWuxia.Forms;

public enum MythicArtKind
{
    Internal,
    External
}

/// <summary>作者君奖励：在神话内功与神话外功中二选一，再从固定词条池中选择三项。</summary>
public sealed class MythicArtForgeForm : Form
{
    private sealed record Trait(string Name, EffectType Type, double Value, double Chance, string Description);

    private readonly RadioButton _internalChoice = new() { Text = "自创神话内功", Checked = true };
    private readonly RadioButton _externalChoice = new() { Text = "自创神话外功" };
    private readonly TextBox _artName = new() { Text = "天道真解", MaxLength = 12 };
    private readonly CheckedListBox _traits = new();

    public MythicArtKind SelectedKind => _internalChoice.Checked ? MythicArtKind.Internal : MythicArtKind.External;
    public MartialArtBase? ForgedArt { get; private set; }

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
        Text = "作者君 · 神话武学二选一";
        Size = new Size(540, 610);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = WuxiaTheme.PanelBack;
        ForeColor = WuxiaTheme.Text;
        Font = WuxiaTheme.UiFont(10f);

        var title = new Label
        {
            Text = "内功与外功只能二选一，选定后不可反悔",
            Location = new Point(18, 14), Size = new Size(490, 32),
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(13f, FontStyle.Bold)
        };
        Controls.Add(title);

        var choiceGroup = new GroupBox
        {
            Text = "选择自创路线",
            Location = new Point(24, 58), Size = new Size(480, 70),
            ForeColor = WuxiaTheme.Accent
        };
        _internalChoice.Location = new Point(42, 28);
        _internalChoice.Size = new Size(170, 28);
        _externalChoice.Location = new Point(270, 28);
        _externalChoice.Size = new Size(170, 28);
        choiceGroup.Controls.AddRange(new Control[] { _internalChoice, _externalChoice });
        Controls.Add(choiceGroup);

        Controls.Add(new Label
        {
            Text = "名称（可自定）：", Location = new Point(28, 145), Size = new Size(120, 28)
        });
        _artName.Location = new Point(150, 141);
        _artName.Size = new Size(345, 30);
        _artName.BackColor = WuxiaTheme.Surface;
        _artName.ForeColor = WuxiaTheme.Text;
        Controls.Add(_artName);

        Controls.Add(new Label
        {
            Text = "固定词条（必须恰选3项）：", Location = new Point(28, 188), Size = new Size(360, 28),
            ForeColor = WuxiaTheme.TextMuted
        });
        _traits.Location = new Point(28, 218);
        _traits.Size = new Size(467, 280);
        _traits.CheckOnClick = true;
        _traits.BackColor = WuxiaTheme.Surface;
        _traits.ForeColor = WuxiaTheme.Text;
        _traits.BorderStyle = BorderStyle.FixedSingle;
        _traits.ItemCheck += (_, e) =>
        {
            if (e.NewValue == CheckState.Checked && _traits.CheckedItems.Count >= 3)
            {
                e.NewValue = CheckState.Unchecked;
                System.Media.SystemSounds.Beep.Play();
            }
        };
        Controls.Add(_traits);

        var confirm = new Button { Text = "铸成这一门", Location = new Point(142, 516), Size = new Size(120, 38) };
        WuxiaTheme.StyleButton(confirm, WuxiaTheme.Success);
        confirm.Click += (_, _) => Forge();
        var cancel = new Button { Text = "暂不铸成", Location = new Point(282, 516), Size = new Size(120, 38) };
        WuxiaTheme.StyleButton(cancel);
        cancel.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { confirm, cancel });

        _internalChoice.CheckedChanged += (_, _) => { if (_internalChoice.Checked) RefreshTraitPool(); };
        _externalChoice.CheckedChanged += (_, _) => { if (_externalChoice.Checked) RefreshTraitPool(); };
        RefreshTraitPool();
        WuxiaTheme.ApplyScaling(this);
    }

    private void RefreshTraitPool()
    {
        _artName.Text = SelectedKind == MythicArtKind.Internal ? "天道真解" : "万象归一";
        var pool = SelectedKind == MythicArtKind.Internal ? InternalTraitPool : ExternalTraitPool;
        _traits.Items.Clear();
        foreach (var trait in pool)
            _traits.Items.Add($"{trait.Name}：{trait.Description}", _traits.Items.Count < 3);
    }

    private void Forge()
    {
        var artName = _artName.Text.Trim();
        if (string.IsNullOrWhiteSpace(artName))
        {
            MessageBox.Show(this, "请为自创武学命名。", "尚未命名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_traits.CheckedItems.Count != 3)
        {
            MessageBox.Show(this, "必须恰好选择3个固定词条。", "词条数量不符", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var pool = SelectedKind == MythicArtKind.Internal ? InternalTraitPool : ExternalTraitPool;
        var selectedTraits = _traits.CheckedIndices.Cast<int>().Select(i => pool[i]).ToList();
        ForgedArt = SelectedKind == MythicArtKind.Internal
            ? CreateInternalArt(artName, selectedTraits)
            : CreateExternalArt(artName, selectedTraits);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static InternalArt CreateInternalArt(string name, List<Trait> traits) => new()
    {
        Id = "author_mythic_internal",
        Name = name,
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
        Effects = traits.Select(ToEffect).ToList()
    };

    private static ExternalArt CreateExternalArt(string name, List<Trait> traits) => new()
    {
        Id = "author_mythic_external",
        Name = name,
        Description = "作者君见证你自万象中择其三而成的神话外功。",
        MaxLevel = 10,
        Rarity = "mythic",
        Proficiency = MartialArtBase.GetProficiencyForLevel(10, "mythic"),
        Cooldown = 2,
        MPCost = 110,
        DamageCoefficient = 4.6,
        CritChance = 0.52,
        Effects = traits.Select(ToEffect).ToList()
    };

    private static ArtEffect ToEffect(Trait trait) => new()
    {
        Type = trait.Type,
        Value = trait.Value,
        Chance = trait.Chance,
        Description = trait.Name + "：" + trait.Description
    };

    /// <summary>旧存档若同时拥有两门奖励，必须选择保留其中一门。</summary>
    public static MythicArtKind ChooseLegacyArt(IWin32Window owner, string internalName, string externalName)
    {
        MythicArtKind selected = MythicArtKind.Internal;
        using var form = new Form
        {
            Text = "自创武学规则调整",
            Size = new Size(560, 290),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ControlBox = false,
            ShowInTaskbar = false,
            BackColor = WuxiaTheme.PanelBack,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(10f)
        };
        form.Controls.Add(new Label
        {
            Text = "旧版曾同时发放自创内功与外功。\n新版奖励改为二选一，请选择永久保留的一门：",
            Location = new Point(30, 25), Size = new Size(490, 70),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(11f, FontStyle.Bold)
        });

        var keepInternal = new Button
        {
            Text = $"保留内功\n《{internalName}》",
            Location = new Point(55, 120), Size = new Size(200, 80)
        };
        var keepExternal = new Button
        {
            Text = $"保留外功\n《{externalName}》",
            Location = new Point(305, 120), Size = new Size(200, 80)
        };
        WuxiaTheme.StyleButton(keepInternal, WuxiaTheme.Success);
        WuxiaTheme.StyleButton(keepExternal, WuxiaTheme.Accent);
        keepInternal.Click += (_, _) => { selected = MythicArtKind.Internal; form.DialogResult = DialogResult.OK; form.Close(); };
        keepExternal.Click += (_, _) => { selected = MythicArtKind.External; form.DialogResult = DialogResult.OK; form.Close(); };
        form.Controls.AddRange(new Control[] { keepInternal, keepExternal });
        WuxiaTheme.ApplyScaling(form);
        form.ShowDialog(owner);
        return selected;
    }
}
