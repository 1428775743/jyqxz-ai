using AutoWuxia.MartialArts;
using AutoWuxia.Items;
using AutoWuxia.Core;

namespace AutoWuxia.Characters;

public enum LifeEventType
{
    Background,   // 背景经历
    Combat,       // 战斗相关
    Training,     // 修炼突破
    Social,       // 社交事件
    Travel,       // 行程变动
    Major,        // 重大事件
    Monthly       // 月度变化
}

public class LifeEvent
{
    public int Day { get; set; }
    public LifeEventType Type { get; set; }
    public string Description { get; set; } = "";

    public string Display => $"[{GameTime.FormatYearDay(Day)}] {Description}";
}

public abstract class CharacterBase
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public int MaxHP { get; set; } = 1000;
    public int CurrentHP { get; set; } = 1000;
    public int MaxMP { get; set; } = 500;
    public int CurrentMP { get; set; } = 500;
    public int BaseAttack { get; set; } = 100;
    public int BaseDefense { get; set; } = 50;
    public int Speed { get; set; } = 50;

    public int Mood { get; set; } = 50;
    public int Karma { get; set; } = 50;
    /// <summary>性别("男"/"女"/null未知)。结婚关系需异性。</summary>
    public string? Gender { get; set; }
    public double Stamina { get; set; } = 100;
    public double MaxStamina { get; set; } = 100;

    /// <summary>
    /// 江湖阅历等级 (1~100)
    /// </summary>
    public int JianghuLevel { get; set; } = 1;

    /// <summary>
    /// 当前阅历经验值
    /// </summary>
    public int JianghuExp { get; set; } = 0;

    /// <summary>
    /// 临时Buff（战斗中产生，战斗结束后清空）
    /// Key = buff类型ID, Value = buff数值
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, int> TempBuffs { get; set; } = new();

    /// <summary>
    /// 持续回合数的Buff：Key=buffId, Value=(Buff数值, 剩余回合)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, (int Value, int RemainingRounds)> TimedBuffs { get; set; } = new();

    public string? FactionId { get; set; }
    public string? CurrentSceneId { get; set; }

    public InternalArt? ActiveInternalArt { get; set; }

    /// <summary>
    /// 辅助内功(最多 MaxAuxiliaryInternalArts 本):仅提供 50% 属性加成(HP/MP/攻/防),
    /// 被动效果(回血/减伤/反伤/双倍出手等)与主动技能均不生效。主内功才提供被动。
    /// </summary>
    public List<InternalArt> AuxiliaryInternalArts { get; set; } = new();

    /// <summary>辅助内功上限</summary>
    public const int MaxAuxiliaryInternalArts = 2;

    /// <summary>
    /// 已装备的外功列表(最多 MaxActiveExternalArts 本,战斗中每本都是一个独立技能按钮)。
    /// 旧存档可能用 ActiveExternalArt(单本) 字段,反序列化后会自动合并进此列表。
    /// </summary>
    public List<ExternalArt> ActiveExternalArts { get; set; } = new();

    /// <summary>同时装备的外功上限</summary>
    public const int MaxActiveExternalArts = 3;

    /// <summary>
    /// 兼容性属性:旧代码用 ActiveExternalArt 单本访问。
    /// get 返回列表第一本; set 会替换首位(null 则清空)。旧存档反序列化时会进入这里,
    /// 自动把单本同步到列表。
    /// </summary>
    public ExternalArt? ActiveExternalArt
    {
        get => ActiveExternalArts.Count > 0 ? ActiveExternalArts[0] : null;
        set
        {
            if (value == null)
            {
                ActiveExternalArts.Clear();
                return;
            }
            // 旧存档兼容:若列表里没有此本就插到首位
            int idx = ActiveExternalArts.FindIndex(a => a.Id == value.Id);
            if (idx < 0)
                ActiveExternalArts.Insert(0, value);
            else if (idx > 0)
            {
                ActiveExternalArts.RemoveAt(idx);
                ActiveExternalArts.Insert(0, value);
            }
        }
    }

    public LightArt? ActiveLightArt { get; set; }
    public List<MartialArtBase> LearnedArts { get; set; } = new();

    public Dictionary<string, CharacterRelation> Relations { get; set; } = new();
    public List<string> History { get; set; } = new();
    /// <summary>江湖经历（按游戏天数记录，用于经历查看与AI上下文）。</summary>
    public List<LifeEvent> LifeEvents { get; set; } = new();
    public Inventory Inventory { get; set; } = new();
    public int Gold { get; set; } = 100;

    /// <summary>装备中的武器(提供攻击加成)。</summary>
    public Item? EquippedWeapon { get; set; }
    /// <summary>装备中的防具(提供防御加成)。</summary>
    public Item? EquippedArmor { get; set; }

    /// <summary>
    /// 技艺属性：艺术、锻造、挖矿、种植、医术等
    /// Key = 技艺ID, Value = 技艺等级 (0~100)
    /// </summary>
    public Dictionary<string, int> CraftSkills { get; set; } = new();

    /// <summary>
    /// 技艺熟练度：Key = 技艺ID, Value = 当前熟练度 (0~99，满100则技艺+1并归零)
    /// </summary>
    public Dictionary<string, int> CraftSkillProficiency { get; set; } = new();

    /// <summary>
    /// 获取指定技艺等级，不存在返回0
    /// </summary>
    public int GetCraftSkill(string skillId)
    {
        return CraftSkills.TryGetValue(skillId, out var level) ? level : 0;
    }

    /// <summary>
    /// 设置技艺等级 (0~100)
    /// </summary>
    public void SetCraftSkill(string skillId, int level)
    {
        CraftSkills[skillId] = Math.Clamp(level, 0, 100);
    }

    /// <summary>
    /// 增加技艺经验
    /// </summary>
    public int ImproveCraftSkill(string skillId, int amount)
    {
        int current = GetCraftSkill(skillId);
        int newLevel = Math.Clamp(current + amount, 0, 100);
        CraftSkills[skillId] = newLevel;
        return newLevel - current;
    }

    /// <summary>
    /// 获取指定技艺的熟练度 (0~99)
    /// </summary>
    public int GetCraftProficiency(string skillId)
    {
        return CraftSkillProficiency.TryGetValue(skillId, out var prof) ? prof : 0;
    }

    /// <summary>
    /// 增加技艺熟练度，满100则技艺+1并归零。返回(技艺升级次数, 当前熟练度)
    /// </summary>
    public (int LevelUps, int CurrentProficiency) ImproveCraftProficiency(string skillId, int amount)
    {
        int prof = GetCraftProficiency(skillId);
        prof += amount;
        int levelUps = 0;
        while (prof >= 100)
        {
            prof -= 100;
            ImproveCraftSkill(skillId, 1);
            levelUps++;
        }
        CraftSkillProficiency[skillId] = prof;
        return (levelUps, prof);
    }

    /// <summary>
    /// 技艺ID到中文名称的映射
    /// </summary>
    public static string GetCraftSkillName(string skillId) => skillId switch
    {
        "art" => "艺术",
        "forging" => "锻造",
        "mining" => "挖矿",
        "gathering" => "采集",
        "hunting" => "打猎",
        "medicine" => "医术",
        "cooking" => "厨艺",
        _ => skillId
    };

    /// <summary>
    /// 获取所有技艺的展示文本
    /// </summary>
    public string GetCraftSkillsSummary()
    {
        if (CraftSkills.Count == 0) return "无";
        return string.Join("  ", CraftSkills.Select(kv => $"{GetCraftSkillName(kv.Key)}:{kv.Value}"));
    }

    public int GetTotalAttack()
    {
        int bonus = ActiveInternalArt?.GetAttackBonus() ?? 0;
        int aux = AuxiliaryBonus(a => a.GetAttackBonus());
        int light = ActiveLightArt?.GetAttackBonus() ?? 0;
        int buff = TempBuffs.GetValueOrDefault("attack_boost", 0);
        int equip = EquippedWeapon?.AttackBonus ?? 0;
        return ApplyBeatenDown((int)((BaseAttack + bonus + aux + light + buff + equip) * BuffMultiplier("atk")));
    }

    public int GetTotalDefense()
    {
        int bonus = ActiveInternalArt?.GetDefenseBonus() ?? 0;
        int aux = AuxiliaryBonus(a => a.GetDefenseBonus());
        int light = ActiveLightArt?.GetDefenseBonus() ?? 0;
        int buff = TempBuffs.GetValueOrDefault("defense_boost", 0)
            + GetTimedBuffValue("internal_defense_boost");
        int equip = EquippedArmor?.DefenseBonus ?? 0;
        return ApplyBeatenDown((int)((BaseDefense + bonus + aux + light + buff + equip) * BuffMultiplier("def")));
    }

    /// <summary>战斗读条用的总速度(基础速度 + 当前装备轻功的速度加成)</summary>
    public int GetTotalSpeed()
    {
        int light = ActiveLightArt?.GetSpeedBonus() ?? 0;
        return ApplyBeatenDown((int)((Speed + light) * BuffMultiplier("speed")));
    }

    public int GetTotalMaxHP()
    {
        int bonus = ActiveInternalArt?.GetHPBonus() ?? 0;
        int aux = AuxiliaryBonus(a => a.GetHPBonus());
        int buff = TempBuffs.GetValueOrDefault("maxhp_boost", 0);
        return ApplyBeatenDown((int)((MaxHP + bonus + aux + buff) * BuffMultiplier("maxhp")));
    }

    public int GetTotalMaxMP()
    {
        int bonus = ActiveInternalArt?.GetMPBonus() ?? 0;
        int aux = AuxiliaryBonus(a => a.GetMPBonus());
        int buff = TempBuffs.GetValueOrDefault("maxmp_boost", 0);
        return ApplyBeatenDown((int)((MaxMP + bonus + aux + buff) * BuffMultiplier("maxmp")));
    }

    /// <summary>药buff与食buff合并加成倍率(各自1+Value,独立叠加);非玩家或无buff返回1.0。</summary>
    protected double BuffMultiplier(string buffType)
    {
        double m = 1.0;
        if (this is Player p)
        {
            if (p.MedicineBuff != null) m *= p.MedicineBuff.MultiplierFor(buffType);
            if (p.FoodBuff != null) m *= p.FoodBuff.MultiplierFor(buffType);
        }
        return m;
    }

    /// <summary>辅助内功属性贡献总和的50%(向下取整)。被动不参与。</summary>
    private int AuxiliaryBonus(Func<InternalArt, int> selector)
    {
        if (AuxiliaryInternalArts == null || AuxiliaryInternalArts.Count == 0) return 0;
        return (int)(AuxiliaryInternalArts.Sum(selector) * 0.5);
    }

    /// <summary>
    /// 战败受辱 debuff:玩家身上有 beaten_down 标签时,战斗属性整体 ×0.8(向下取整)。
    /// NPC 无此标签,直接返回原值。
    /// </summary>
    protected int ApplyBeatenDown(int value)
    {
        if (this is Player p && p.HasTag("beaten_down"))
            return value * 4 / 5;   // 80%,向下取整
        return value;
    }

    /// <summary>
    /// 装备一件物品:从背包取出装到对应槽位,旧装备回背包。
    /// 成功返回 null,失败返回原因。
    /// </summary>
    public string? EquipItem(Item item)
    {
        if (item == null || !item.IsEquipment) return "这不是可装备的物品。";
        if (!Inventory.HasItem(item.Id, 1)) return "背包中没有该物品。";

        Inventory.RemoveItem(item.Id, 1);
        var toEquip = item.Clone();
        toEquip.Quantity = 1;

        Item? old = null;
        if (toEquip.Slot == EquipSlot.Weapon)
        {
            old = EquippedWeapon;
            EquippedWeapon = toEquip;
        }
        else if (toEquip.Slot == EquipSlot.Armor)
        {
            old = EquippedArmor;
            EquippedArmor = toEquip;
        }
        else
        {
            // 未知槽位,把取出的放回去
            Inventory.AddItem(toEquip);
            return "未知装备槽位。";
        }

        if (old != null) Inventory.AddItem(old);
        return null;
    }

    /// <summary>
    /// 卸下指定槽位的装备放回背包。成功返回 true,槽位为空返回 false。
    /// </summary>
    public bool UnequipItem(EquipSlot slot)
    {
        Item? current = slot == EquipSlot.Weapon ? EquippedWeapon : EquippedArmor;
        if (current == null) return false;

        if (slot == EquipSlot.Weapon) EquippedWeapon = null;
        else EquippedArmor = null;
        Inventory.AddItem(current);
        return true;
    }

    /// <summary>
    /// 添加临时Buff（叠加）
    /// </summary>
    public void AddTempBuff(string buffId, int value)
    {
        TempBuffs.TryGetValue(buffId, out int current);
        TempBuffs[buffId] = current + value;
    }

    /// <summary>
    /// 清空所有临时Buff
    /// </summary>
    public void ClearTempBuffs()
    {
        TempBuffs.Clear();
        TimedBuffs.Clear();
    }

    /// <summary>
    /// 添加带持续回合数的Buff
    /// </summary>
    /// <param name="buffId">Buff标识</param>
    /// <param name="value">Buff数值</param>
    /// <param name="rounds">持续回合数</param>
    public void AddTimedBuff(string buffId, int value, int rounds)
    {
        TimedBuffs[buffId] = (value, rounds);
    }

    /// <summary>
    /// 每回合递减所有TimedBuff的剩余回合，到期自动移除
    /// </summary>
    public void TickTimedBuffs()
    {
        var expired = new List<string>();
        foreach (var key in TimedBuffs.Keys.ToList())
        {
            var (val, rounds) = TimedBuffs[key];
            rounds--;
            if (rounds <= 0)
                expired.Add(key);
            else
                TimedBuffs[key] = (val, rounds);
        }
        foreach (var key in expired)
            TimedBuffs.Remove(key);
    }

    /// <summary>
    /// 检查是否存在指定的TimedBuff
    /// </summary>
    public bool HasTimedBuff(string buffId) => TimedBuffs.ContainsKey(buffId);

    /// <summary>
    /// 获取TimedBuff的数值（不存在返回0）
    /// </summary>
    public int GetTimedBuffValue(string buffId)
    {
        return TimedBuffs.TryGetValue(buffId, out var buff) ? buff.Value : 0;
    }

    /// <summary>
    /// 获取TimedBuff的剩余回合数（不存在返回0）
    /// </summary>
    public int GetTimedBuffRounds(string buffId)
    {
        return TimedBuffs.TryGetValue(buffId, out var buff) ? buff.RemainingRounds : 0;
    }

    public int TakeDamage(int damage)
    {
        int oldHP = CurrentHP;
        CurrentHP = Math.Max(0, CurrentHP - damage);
        return oldHP - CurrentHP;
    }

    public void Heal(int amount)
    {
        CurrentHP = Math.Min(GetTotalMaxHP(), CurrentHP + amount);
    }

    public void RecoverMP(int amount)
    {
        CurrentMP = Math.Min(GetTotalMaxMP(), CurrentMP + amount);
    }

    /// <summary>恢复到包含当前内功、辅助内功和装备加成后的满气血、满内力。</summary>
    public void RestoreVitalsToFull()
    {
        CurrentHP = GetTotalMaxHP();
        CurrentMP = GetTotalMaxMP();
    }

    public bool IsAlive => CurrentHP > 0;

    public void LearnArt(MartialArtBase art)
    {
        if (LearnedArts.Any(a => a.Id == art.Id))
            return;
        LearnedArts.Add(art);

        if (art is InternalArt internalArt && ActiveInternalArt == null)
            ActiveInternalArt = internalArt;
        else if (art is LightArt lightArt && ActiveLightArt == null)
            ActiveLightArt = lightArt;
        else if (art is ExternalArt externalArt)
        {
            // 已装备的不重复加;未满 3 本时自动装备
            if (!ActiveExternalArts.Any(a => a.Id == externalArt.Id)
                && ActiveExternalArts.Count < MaxActiveExternalArts)
                ActiveExternalArts.Add(externalArt);
        }
    }

    /// <summary>遗忘一门武功，并从所有装备槽中安全移除。</summary>
    public bool ForgetArt(string artId)
    {
        var art = LearnedArts.FirstOrDefault(a => a.Id == artId);
        if (art == null) return false;

        LearnedArts.Remove(art);
        ActiveExternalArts.RemoveAll(a => a.Id == artId);
        AuxiliaryInternalArts.RemoveAll(a => a.Id == artId);

        if (ActiveInternalArt?.Id == artId)
            ActiveInternalArt = LearnedArts.OfType<InternalArt>().FirstOrDefault();
        if (ActiveLightArt?.Id == artId)
            ActiveLightArt = LearnedArts.OfType<LightArt>().FirstOrDefault();

        CurrentHP = Math.Min(CurrentHP, GetTotalMaxHP());
        CurrentMP = Math.Min(CurrentMP, GetTotalMaxMP());
        return true;
    }

    public void AddHistory(string entry)
    {
        History.Add($"[{DateTime.Now:yyyy-MM-dd}] {entry}");
    }

    /// <summary>添加江湖经历（按游戏天数记录，同时写入History）。</summary>
    public void AddLifeEvent(int day, LifeEventType type, string description)
    {
        LifeEvents.Add(new LifeEvent { Day = day, Type = type, Description = description });
        AddHistory(description);
    }

    /// <summary>获取最近N条经历（用于AI上下文）。</summary>
    public string GetRecentLifeEvents(int count = 10)
    {
        var recent = LifeEvents.TakeLast(count);
        return string.Join("\n", recent.Select(e => e.Display));
    }

    public CharacterRelation GetRelation(string targetId)
    {
        if (!Relations.TryGetValue(targetId, out var relation))
        {
            relation = new CharacterRelation { TargetId = targetId };
            Relations[targetId] = relation;
        }
        return relation;
    }

    public void Rest(double hours)
    {
        Stamina = Math.Min(MaxStamina, Stamina + hours * 10);
        Heal((int)(GetTotalMaxHP() * hours * 0.05));
        RecoverMP((int)(GetTotalMaxMP() * hours * 0.08));
    }

    // ── 江湖阅历系统 ──

    /// <summary>
    /// 获取升到下一级所需经验值
    /// 公式: 50 + level^2 * 2
    /// </summary>
    public int GetExpToNextLevel()
    {
        if (JianghuLevel >= 100) return int.MaxValue;
        // 保留原二次方曲线(50+level²×2):高级别需大量经验。
        // 配合战斗经验随对手等级曲线成长(打60级对手给~1810经验),约46场可升到60级。
        return 50 + JianghuLevel * JianghuLevel * 2;
    }

    /// <summary>
    /// 获取阅历经验，自动升级。返回升级次数。
    /// 每次升级: MaxHP+10, MaxMP+5, BaseAttack+1, BaseDefense+1
    /// </summary>
    public int GainJianghuExp(int amount)
    {
        if (amount <= 0 || JianghuLevel >= 100) return 0;

        JianghuExp += amount;
        int levelUps = 0;

        while (JianghuLevel < 100 && JianghuExp >= GetExpToNextLevel())
        {
            JianghuExp -= GetExpToNextLevel();
            JianghuLevel++;
            levelUps++;
            OnLevelUp();
        }

        return levelUps;
    }

    /// <summary>
    /// 升级时的永久属性提升。基类为NPC的缓慢成长(每级+1攻+1防+10HP+5MP);
    /// Player重写为主角的快速成长,使其能在合理等级追上顶尖NPC的属性量级。
    /// </summary>
    protected virtual void OnLevelUp()
    {
        MaxHP += 10;
        MaxMP += 5;
        BaseAttack += 1;
        BaseDefense += 1;
        // 同步增加当前值
        CurrentHP = Math.Min(CurrentHP + 10, GetTotalMaxHP());
        CurrentMP = Math.Min(CurrentMP + 5, GetTotalMaxMP());
    }

    public virtual string GetStatusSummary()
    {
        var baseSummary = $"{Name} | 阅历Lv.{JianghuLevel} | HP:{CurrentHP}/{GetTotalMaxHP()} MP:{CurrentMP}/{GetTotalMaxMP()} " +
               $"攻:{GetTotalAttack()} 防:{GetTotalDefense()} 速:{GetTotalSpeed()} | 体力:{Stamina:F0}/{MaxStamina:F0} " +
               $"心情:{Mood} 善恶:{Karma}";
        if (this is Player p)
        {
            baseSummary += $" | 健康:{p.Health}/{p.MaxHealth}";
            if (p.Tags.Count > 0)
                baseSummary += $" | {p.GetTagsSummary()}";
        }
        return baseSummary;
    }
}
