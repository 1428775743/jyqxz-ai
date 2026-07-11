using System.Text.Json.Serialization;
using AutoWuxia.Config.Models;

namespace AutoWuxia.MartialArts;

public enum EffectType
{
    IgnoreDefense,
    Knockback,
    DrainMP,
    DoubleStrike,
    Stun,
    Bleed,
    MPRecover,
    MPResist,
    /// <summary>被攻击时概率自动反击一次</summary>
    CounterAttack,
    /// <summary>按比例减少受到的伤害（TimedBuff）</summary>
    DamageReduction,
    /// <summary>将受到伤害的一部分反弹给攻击者（TimedBuff）</summary>
    ReflectDamage,
    /// <summary>追击：攻击后概率额外追加一次攻击</summary>
    ExtraAttack,
    /// <summary>真实伤害：攻击附带一定比例无视防御的伤害</summary>
    TrueDamage,
    /// <summary>蓄力增伤：使用后下次攻击伤害提升</summary>
    NextAttackBoost,
    /// <summary>被动HP恢复：每回合恢复一定比例最大HP</summary>
    HPRecover,
    /// <summary>固定减伤：受到攻击时伤害减少固定值（被动永久生效）</summary>
    FlatDamageReduction,
    /// <summary>每次出手时读条只归 50(永久,内功被动,如葵花宝典)</summary>
    PermanentExtraAction,
    /// <summary>使用后下一次出手时读条只归 50(一次性,外功使用后,如辟邪剑法)</summary>
    ExtraActionNextRound,
    /// <summary>闪避(被动,轻功专用):被攻击时按概率完全闪避</summary>
    Evasion,
    /// <summary>被动减伤(轻功专用):受到攻击时按比例减伤,永久生效</summary>
    PassiveDamageReduction
}

public class ArtEffect
{
    public EffectType Type { get; set; }
    public double Value { get; set; }
    public double Chance { get; set; } = 1.0;
    public string Description { get; set; } = "";

    /// <summary>
    /// 效果激活所需的武功等级（0=无限制）
    /// </summary>
    public int RequiredLevel { get; set; }

    /// <summary>
    /// 判断效果是否可激活（概率检查 + 等级检查）
    /// </summary>
    /// <param name="artLevel">当前武功等级</param>
    public bool TryActivate(int artLevel = 99)
    {
        if (artLevel < RequiredLevel) return false;
        return Random.Shared.NextDouble() <= Chance;
    }

    /// <summary>
    /// 仅检查等级是否达标（不消耗概率）
    /// </summary>
    public bool IsUnlocked(int artLevel) => artLevel >= RequiredLevel;

    public static ArtEffect FromConfig(ArtEffectConfig config)
    {
        return new ArtEffect
        {
            Type = Enum.TryParse<EffectType>(config.Type, true, out var t) ? t : EffectType.IgnoreDefense,
            Value = config.Value,
            Chance = config.Chance > 0 ? config.Chance : 1.0,
            Description = config.Description ?? config.Type,
            RequiredLevel = config.RequiredLevel
        };
    }
}

[JsonDerivedType(typeof(InternalArt), typeDiscriminator: "internal")]
[JsonDerivedType(typeof(ExternalArt), typeDiscriminator: "external")]
[JsonDerivedType(typeof(LightArt), typeDiscriminator: "light")]
public abstract class MartialArtBase
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MaxLevel { get; set; } = 10;
    public List<ArtEffect> Effects { get; set; } = new();

    /// <summary>
    /// 武功品质 common / uncommon / rare / epic / legendary / mythic
    /// 决定升级所需熟练度阈值，颜色显示与物品系统统一
    /// </summary>
    public string Rarity { get; set; } = "common";

    /// <summary>
    /// 累计熟练度（越练越多，永不下降）
    /// 等级、伤害加成、防御加成等都从该值推导
    /// </summary>
    public int Proficiency { get; set; }

    // ── 等级与品质 ──

    /// <summary>
    /// 当前等级（根据累计熟练度推导，1..MaxLevel）
    /// </summary>
    public int Level
    {
        get
        {
            int lv = 1;
            for (int target = 2; target <= MaxLevel; target++)
            {
                if (Proficiency >= GetProficiencyForLevel(target, Rarity))
                    lv = target;
                else break;
            }
            return lv;
        }
    }

    /// <summary>
    /// 升到下一级还需多少累计熟练度（已满级返回0）
    /// </summary>
    public int ProficiencyToNextLevel
    {
        get
        {
            if (Level >= MaxLevel) return 0;
            return GetProficiencyForLevel(Level + 1, Rarity) - Proficiency;
        }
    }

    /// <summary>
    /// 当前等级到下一等级的进度 [0,1]
    /// </summary>
    public double LevelProgress
    {
        get
        {
            if (Level >= MaxLevel) return 1.0;
            int prev = GetProficiencyForLevel(Level, Rarity);
            int next = GetProficiencyForLevel(Level + 1, Rarity);
            int span = Math.Max(1, next - prev);
            return Math.Clamp((double)(Proficiency - prev) / span, 0, 1);
        }
    }

    // ── 战斗加成（按等级） ──
    // 修平衡:让低等级武功也能打出 ~55% 攻击,高等级仍有显著优势(差距 ~2.9x)
    private static readonly double[] LevelDamageMultipliers =
        { 0.55, 0.65, 0.75, 0.85, 0.95, 1.05, 1.15, 1.25, 1.40, 1.60 };

    public double GetLevelDamageMultiplier()
    {
        int idx = Math.Clamp(Level, 1, 10) - 1;
        return LevelDamageMultipliers[idx];
    }

    /// <summary>
    /// 兼容旧调用：等同 GetLevelDamageMultiplier
    /// </summary>
    public double GetProficiencyDamageMultiplier() => GetLevelDamageMultiplier();

    /// <summary>
    /// 累加熟练度（默认每次战斗动作给 +1）
    /// </summary>
    public void GainProficiency(int amount = 1)
    {
        if (amount <= 0) return;
        // 已满级仍可累计但不再用于升级
        Proficiency += amount;
    }

    /// <summary>
    /// 兼容旧调用：每次战斗动作 +5 熟练度点
    /// </summary>
    public void GainCombatProficiency() => GainProficiency(5);

    public string GetProficiencyDescription()
    {
        string[] names = { "初窥门径", "略有小成", "渐入佳境", "小有所成", "登堂入室",
                           "炉火纯青", "出神入化", "返璞归真", "无上妙诣", "大成圆满" };
        int idx = Math.Clamp(Level, 1, 10) - 1;
        if (Level >= MaxLevel)
            return $"Lv.{Level} {names[idx]} (熟练度{Proficiency})";
        return $"Lv.{Level} {names[idx]} ({Proficiency}/{GetProficiencyForLevel(Level + 1, Rarity)})";
    }

    // 技能冷却
    public int Cooldown { get; set; }
    public int CurrentCooldown { get; set; }
    public int MPCost { get; set; }
    public bool IsReady => CurrentCooldown == 0;

    public void UseSkill()
    {
        CurrentCooldown = Cooldown;
    }

    public void TickCooldown()
    {
        if (CurrentCooldown > 0) CurrentCooldown--;
    }

    public abstract string GetSummary();

    // ── 品质静态工具 ──

    /// <summary>
    /// 不同品质升到同一等级，所需基础熟练度倍率（每级线性递增）
    /// 计算公式： Sum_{l=1..L-1} growth × l = growth × L(L-1)/2
    /// </summary>
    public static int GetGrowthFactor(string rarity) => rarity switch
    {
        "common" => 30,
        "uncommon" => 50,
        "rare" => 80,
        "epic" => 120,
        "legendary" => 180,
        "mythic" => 260,
        _ => 30
    };

    /// <summary>
    /// 升到第 level 级所需的累计熟练度（level=1 即 0）
    /// </summary>
    public static int GetProficiencyForLevel(int level, string rarity)
    {
        if (level <= 1) return 0;
        int growth = GetGrowthFactor(rarity);
        return growth * level * (level - 1) / 2;
    }

    /// <summary>
    /// 武功品质显示颜色（复用物品稀有度色板）
    /// </summary>
    public System.Drawing.Color RarityColor => Items.Item.GetRarityColor(Rarity);

    /// <summary>
    /// 武功品质中文名（复用物品稀有度名）
    /// </summary>
    public string RarityName => Items.Item.GetRarityName(Rarity);
}
