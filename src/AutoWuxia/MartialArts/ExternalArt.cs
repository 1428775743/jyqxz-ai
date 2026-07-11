using AutoWuxia.Config.Models;

namespace AutoWuxia.MartialArts;

public class ExternalArt : MartialArtBase
{
    public double DamageCoefficient { get; set; } = 1.0;
    public double CritChance { get; set; }

    /// <summary>等级解锁加成(如太极拳Lv5/10提升伤害系数)</summary>
    public Dictionary<string, LevelBonusConfig>? LevelBonuses { get; set; }

    public int CalculateBaseDamage(int attack)
    {
        // 暴击×1.5由DamageCalculator统一判断应用,此处仅算基础伤害(避免双重随机不同步导致"显示暴击但伤害没加成")
        return (int)(attack * GetEffectiveDamageCoefficient());
    }

    /// <summary>当前等级下的有效伤害系数(应用 levelBonuses 中最高已解锁阈值的覆盖值)。</summary>
    public double GetEffectiveDamageCoefficient()
    {
        double coef = DamageCoefficient;
        if (LevelBonuses == null || LevelBonuses.Count == 0) return coef;
        int bestThreshold = -1;
        foreach (var kv in LevelBonuses)
        {
            if (int.TryParse(kv.Key, out int threshold) && Level >= threshold && threshold > bestThreshold
                && kv.Value?.DamageCoefficient != null)
            {
                bestThreshold = threshold;
                coef = kv.Value.DamageCoefficient.Value;
            }
        }
        return coef;
    }

    public double GetEffectiveIgnoreDefense()
    {
        return Effects
            .Where(e => e.Type == EffectType.IgnoreDefense && e.TryActivate(Level))
            .Sum(e => e.Value);
    }

    public static ExternalArt FromConfig(MartialArtConfig config, int level = 1)
    {
        var art = new ExternalArt
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            MaxLevel = config.MaxLevel,
            Rarity = string.IsNullOrEmpty(config.Rarity) ? "common" : config.Rarity,
            DamageCoefficient = config.DamageCoefficient,
            CritChance = config.CritChance,
            LevelBonuses = config.LevelBonuses,
            Cooldown = config.Cooldown,
            MPCost = config.MPCost,
            Effects = config.Effects.Select(ArtEffect.FromConfig).ToList()
        };
        int target = Math.Clamp(level, 1, art.MaxLevel);
        art.Proficiency = GetProficiencyForLevel(target, art.Rarity);
        return art;
    }

    public override string GetSummary()
    {
        return $"{Name}[{RarityName}] Lv.{Level} | 伤害{GetEffectiveDamageCoefficient():F1} 暴击{CritChance:P0} CD{Cooldown}回 MP{MPCost} | 熟练度{Proficiency} | {Description}";
    }
}
