using AutoWuxia.Config.Models;

namespace AutoWuxia.MartialArts;

/// <summary>
/// 轻功(身法) - 第三种武功类型。
/// 装备的轻功提供被动 Speed/Attack/Defense 加成,以及闪避、减伤等战斗被动。
/// 每个角色同时只能装备一种轻功(ActiveLightArt)。
/// </summary>
public class LightArt : MartialArtBase
{
    /// <summary>速度加成(每级递增)。例如凌波微步 SpeedBonusPerLevel=5,Lv10→+50</summary>
    public int SpeedBonusPerLevel { get; set; }
    /// <summary>攻击加成(每级递增,如武当轻功)</summary>
    public int AttackBonusPerLevel { get; set; }
    /// <summary>防御加成(每级递增,如少林轻功)</summary>
    public int DefenseBonusPerLevel { get; set; }

    public int GetSpeedBonus() => SpeedBonusPerLevel * Level;
    public int GetAttackBonus() => AttackBonusPerLevel * Level;
    public int GetDefenseBonus() => DefenseBonusPerLevel * Level;

    /// <summary>
    /// 获取闪避概率(被动效果中类型为 Evasion 的 Value 之和)
    /// </summary>
    public double GetEvasionChance()
    {
        double total = 0;
        foreach (var eff in Effects)
        {
            if (eff.Type == EffectType.Evasion && eff.IsUnlocked(Level))
                total += eff.Value;
        }
        return total;
    }

    /// <summary>
    /// 获取被动减伤比例(类型为 FlatPercentReduction 的 Value 之和)
    /// </summary>
    public double GetPassiveDamageReduction()
    {
        double total = 0;
        foreach (var eff in Effects)
        {
            if (eff.Type == EffectType.PassiveDamageReduction && eff.IsUnlocked(Level))
                total += eff.Value;
        }
        return total;
    }

    public static LightArt FromConfig(MartialArtConfig config, int level = 1)
    {
        var art = new LightArt
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            MaxLevel = config.MaxLevel,
            Rarity = string.IsNullOrEmpty(config.Rarity) ? "common" : config.Rarity,
            Cooldown = config.Cooldown,
            MPCost = config.MPCost,
            SpeedBonusPerLevel = config.SpeedBonusPerLevel,
            AttackBonusPerLevel = config.AttackBonusPerLevel,
            DefenseBonusPerLevel = config.DefenseBonusPerLevel,
            Effects = config.Effects.Select(ArtEffect.FromConfig).ToList()
        };
        int target = Math.Clamp(level, 1, art.MaxLevel);
        art.Proficiency = GetProficiencyForLevel(target, art.Rarity);
        return art;
    }

    public override string GetSummary()
    {
        var parts = new List<string>();
        if (GetSpeedBonus() > 0) parts.Add($"速+{GetSpeedBonus()}");
        if (GetAttackBonus() > 0) parts.Add($"攻+{GetAttackBonus()}");
        if (GetDefenseBonus() > 0) parts.Add($"防+{GetDefenseBonus()}");
        var statText = parts.Count > 0 ? " | " + string.Join(" ", parts) : "";
        return $"{Name}[{RarityName}] Lv.{Level}{statText} | 熟练度{Proficiency}";
    }
}
