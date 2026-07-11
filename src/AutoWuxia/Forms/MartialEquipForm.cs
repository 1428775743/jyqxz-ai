using AutoWuxia.Characters;
using AutoWuxia.MartialArts;

namespace AutoWuxia.Forms;

/// <summary>
/// 武学装备窗口:玩家在战斗外切换/装备已学武功。
/// 规则:外功最多 MaxActiveExternalArts 本;主内功单选(被动全开);辅助内功最多2本(仅+50%属性,被动不生效);轻功单选。
/// </summary>
public class MartialEquipForm : Form
{
    private readonly Player _player;
    private CheckedListBox _externalList = null!;
    private ListBox _internalList = null!;
    private CheckedListBox _auxInternalList = null!;
    private ListBox _lightList = null!;
    private Label _summaryLabel = null!;

    public MartialEquipForm(Player player)
    {
        _player = player;
        InitUI();
    }

    private void InitUI()
    {
        Text = "武学装备";
        Size = new Size(560, 690);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = WuxiaTheme.AppBack;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = WuxiaTheme.UiFont(9f);

        var titleLabel = new Label
        {
            Text = "── 武学装备 ──",
            Location = new Point(15, 10),
            Size = new Size(520, 28),
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(titleLabel);

        // 外功
        var extGroup = new GroupBox
        {
            Text = $"外功 (最多{CharacterBase.MaxActiveExternalArts}本同时装备)",
            Location = new Point(15, 45),
            Size = new Size(520, 175),
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(9.5f, FontStyle.Bold)
        };
        Controls.Add(extGroup);

        _externalList = new CheckedListBox
        {
            Location = new Point(8, 22),
            Size = new Size(504, 143),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            IntegralHeight = false
        };
        extGroup.Controls.Add(_externalList);

        // 主内功
        var intGroup = new GroupBox
        {
            Text = "主内功 (单选,被动全开)",
            Location = new Point(15, 225),
            Size = new Size(255, 155),
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(9.5f, FontStyle.Bold)
        };
        Controls.Add(intGroup);

        _internalList = new ListBox
        {
            Location = new Point(8, 22),
            Size = new Size(239, 123),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        intGroup.Controls.Add(_internalList);

        // 轻功
        var lightGroup = new GroupBox
        {
            Text = "身法/轻功 (单选)",
            Location = new Point(280, 225),
            Size = new Size(255, 155),
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(9.5f, FontStyle.Bold)
        };
        Controls.Add(lightGroup);

        _lightList = new ListBox
        {
            Location = new Point(8, 22),
            Size = new Size(239, 123),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        lightGroup.Controls.Add(_lightList);

        // 辅助内功
        var auxGroup = new GroupBox
        {
            Text = $"辅助内功 (最多{CharacterBase.MaxAuxiliaryInternalArts}本,仅+50%属性,被动不生效;不可与主内功重复)",
            Location = new Point(15, 385),
            Size = new Size(520, 145),
            ForeColor = WuxiaTheme.AccentSoft,
            Font = WuxiaTheme.UiFont(9.5f, FontStyle.Bold)
        };
        Controls.Add(auxGroup);

        _auxInternalList = new CheckedListBox
        {
            Location = new Point(8, 22),
            Size = new Size(504, 113),
            BackColor = WuxiaTheme.Surface,
            ForeColor = WuxiaTheme.Text,
            Font = WuxiaTheme.UiFont(9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            IntegralHeight = false
        };
        auxGroup.Controls.Add(_auxInternalList);

        // 当前合计
        _summaryLabel = new Label
        {
            Location = new Point(15, 538),
            Size = new Size(520, 65),
            ForeColor = WuxiaTheme.Text,
            BackColor = WuxiaTheme.Surface,
            Font = WuxiaTheme.UiFont(9f),
            Padding = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_summaryLabel);

        // 按钮
        var okBtn = new Button
        {
            Text = "确定装备",
            Location = new Point(265, 612),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = WuxiaTheme.UiFont(10f, FontStyle.Bold)
        };
        WuxiaTheme.StyleButton(okBtn, WuxiaTheme.Success);
        okBtn.Click += (_, _) => ApplyAndClose();
        Controls.Add(okBtn);

        var cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(395, 612),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = WuxiaTheme.UiFont(10f)
        };
        WuxiaTheme.StyleButton(cancelBtn);
        cancelBtn.Click += (_, _) => Close();
        Controls.Add(cancelBtn);
        WuxiaTheme.ApplyScaling(this);

        PopulateLists();
        RefreshSummary();

        // 事件
        _externalList.ItemCheck += (s, e) =>
        {
            int currentChecked = _externalList.CheckedItems.Count;
            if (e.NewValue == CheckState.Checked && currentChecked >= CharacterBase.MaxActiveExternalArts)
            {
                e.NewValue = CheckState.Unchecked;
                MessageBox.Show(this, $"外功最多同时装备 {CharacterBase.MaxActiveExternalArts} 本。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginInvoke(new Action(RefreshSummary));
        };
        _auxInternalList.ItemCheck += (s, e) =>
        {
            // 不可与主内功重复
            if (e.NewValue == CheckState.Checked && e.Index >= 0 && e.Index < _allInternals.Count
                && _internalList.SelectedIndex >= 0
                && _allInternals[e.Index].Id == _allInternals[_internalList.SelectedIndex].Id)
            {
                e.NewValue = CheckState.Unchecked;
                MessageBox.Show(this, "此内功已作为主内功,不能同时设为辅助。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // 最多2本
            int currentChecked = _auxInternalList.CheckedItems.Count;
            if (e.NewValue == CheckState.Checked && currentChecked >= CharacterBase.MaxAuxiliaryInternalArts)
            {
                e.NewValue = CheckState.Unchecked;
                MessageBox.Show(this, $"辅助内功最多 {CharacterBase.MaxAuxiliaryInternalArts} 本。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginInvoke(new Action(RefreshSummary));
        };
        _internalList.SelectedIndexChanged += (_, _) =>
        {
            // 主内功切换后,若辅助槽勾选了同一本,自动取消
            SyncAuxCheckedAgainstMain();
            RefreshSummary();
        };
        _lightList.SelectedIndexChanged += (_, _) => RefreshSummary();
    }

    private List<ExternalArt> _allExternals = new();
    private List<InternalArt> _allInternals = new();
    private List<LightArt> _allLights = new();

    private void PopulateLists()
    {
        _allExternals = _player.LearnedArts.OfType<ExternalArt>().ToList();
        _allInternals = _player.LearnedArts.OfType<InternalArt>().ToList();
        _allLights = _player.LearnedArts.OfType<LightArt>().ToList();

        foreach (var ea in _allExternals)
        {
            bool active = _player.ActiveExternalArts.Any(a => a.Id == ea.Id);
            _externalList.Items.Add($"{ea.Name}  [{ea.RarityName}]  Lv.{ea.Level}", active);
        }
        foreach (var ia in _allInternals)
            _internalList.Items.Add($"{ia.Name}  [{ia.RarityName}]  Lv.{ia.Level}");
        foreach (var la in _allLights)
            _lightList.Items.Add($"{la.Name}  [{la.RarityName}]  Lv.{la.Level}");

        // 默认选中当前主内功
        if (_player.ActiveInternalArt != null)
        {
            int idx = _allInternals.FindIndex(a => a.Id == _player.ActiveInternalArt.Id);
            if (idx >= 0) _internalList.SelectedIndex = idx;
        }
        if (_player.ActiveLightArt != null)
        {
            int idx = _allLights.FindIndex(a => a.Id == _player.ActiveLightArt.Id);
            if (idx >= 0) _lightList.SelectedIndex = idx;
        }

        // 辅助内功:勾选当前已装备的(跳过主内功)
        for (int i = 0; i < _allInternals.Count; i++)
        {
            bool isAux = _player.AuxiliaryInternalArts.Any(a => a.Id == _allInternals[i].Id);
            _auxInternalList.Items.Add($"{_allInternals[i].Name}  [{_allInternals[i].RarityName}]  Lv.{_allInternals[i].Level}", isAux);
        }

        // 列表为空提示
        if (_allExternals.Count == 0) _externalList.Items.Add("(未学习任何外功)");
        if (_allInternals.Count == 0) { _internalList.Items.Add("(未学习任何内功)"); _auxInternalList.Items.Add("(未学习任何内功)"); }
        if (_allLights.Count == 0) _lightList.Items.Add("(未学习任何身法)");
    }

    /// <summary>主内功切换后,取消辅助槽中与之重复的勾选。</summary>
    private void SyncAuxCheckedAgainstMain()
    {
        if (_internalList.SelectedIndex < 0 || _internalList.SelectedIndex >= _allInternals.Count) return;
        string mainId = _allInternals[_internalList.SelectedIndex].Id;
        for (int i = 0; i < _allInternals.Count; i++)
        {
            if (_allInternals[i].Id == mainId && _auxInternalList.GetItemChecked(i))
                _auxInternalList.SetItemChecked(i, false);
        }
    }

    private void RefreshSummary()
    {
        int hp = 0, mp = 0, atk = 0, def = 0, spd = 0;

        // 主内功(满属性)
        if (_internalList.SelectedIndex >= 0 && _internalList.SelectedIndex < _allInternals.Count)
        {
            var ia = _allInternals[_internalList.SelectedIndex];
            hp += ia.GetHPBonus();
            mp += ia.GetMPBonus();
            atk += ia.GetAttackBonus();
            def += ia.GetDefenseBonus();
        }
        // 辅助内功(50%属性)
        int auxHp = 0, auxMp = 0, auxAtk = 0, auxDef = 0;
        var auxNames = new List<string>();
        for (int i = 0; i < _allInternals.Count; i++)
        {
            if (_auxInternalList.GetItemChecked(i))
            {
                var ia = _allInternals[i];
                auxHp += ia.GetHPBonus();
                auxMp += ia.GetMPBonus();
                auxAtk += ia.GetAttackBonus();
                auxDef += ia.GetDefenseBonus();
                auxNames.Add(ia.Name);
            }
        }
        hp += auxHp / 2;
        mp += auxMp / 2;
        atk += auxAtk / 2;
        def += auxDef / 2;

        // 轻功
        if (_lightList.SelectedIndex >= 0 && _lightList.SelectedIndex < _allLights.Count)
        {
            var la = _allLights[_lightList.SelectedIndex];
            spd += la.GetSpeedBonus();
            atk += la.GetAttackBonus();
            def += la.GetDefenseBonus();
        }

        var checkedExtNames = new List<string>();
        for (int i = 0; i < _allExternals.Count; i++)
        {
            if (_externalList.GetItemChecked(i))
                checkedExtNames.Add(_allExternals[i].Name);
        }
        string extText = checkedExtNames.Count > 0 ? string.Join("、", checkedExtNames) : "无";
        string auxText = auxNames.Count > 0 ? string.Join("、", auxNames) : "无";

        _summaryLabel.Text =
            $"当前选中:\n" +
            $"  内/身加成(含辅助50%): HP+{hp}  MP+{mp}  攻+{atk}  防+{def}  速+{spd}\n" +
            $"  已选外功({checkedExtNames.Count}/{CharacterBase.MaxActiveExternalArts}): {extText}\n" +
            $"  辅助内功({auxNames.Count}/{CharacterBase.MaxAuxiliaryInternalArts}): {auxText}";
    }

    private void ApplyAndClose()
    {
        // 主内功
        if (_internalList.SelectedIndex >= 0 && _internalList.SelectedIndex < _allInternals.Count)
            _player.ActiveInternalArt = _allInternals[_internalList.SelectedIndex];

        // 辅助内功(跳过与主重复的)
        string? mainId = _player.ActiveInternalArt?.Id;
        _player.AuxiliaryInternalArts.Clear();
        for (int i = 0; i < _allInternals.Count; i++)
        {
            if (_player.AuxiliaryInternalArts.Count >= CharacterBase.MaxAuxiliaryInternalArts) break;
            if (!_auxInternalList.GetItemChecked(i)) continue;
            if (_allInternals[i].Id == mainId) continue;
            _player.AuxiliaryInternalArts.Add(_allInternals[i]);
        }

        // 轻功
        if (_lightList.SelectedIndex >= 0 && _lightList.SelectedIndex < _allLights.Count)
            _player.ActiveLightArt = _allLights[_lightList.SelectedIndex];

        // 外功
        var newExt = new List<ExternalArt>();
        for (int i = 0; i < _allExternals.Count; i++)
        {
            if (_externalList.GetItemChecked(i))
                newExt.Add(_allExternals[i]);
        }
        if (newExt.Count > CharacterBase.MaxActiveExternalArts)
            newExt = newExt.Take(CharacterBase.MaxActiveExternalArts).ToList();
        _player.ActiveExternalArts.Clear();
        _player.ActiveExternalArts.AddRange(newExt);

        DialogResult = DialogResult.OK;
        Close();
    }
}
