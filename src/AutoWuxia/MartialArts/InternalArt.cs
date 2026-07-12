using AutoWuxia.Config.Models;

namespace AutoWuxia.MartialArts;

public class InternalArt : MartialArtBase
{
    public int HPBonusPerLevel { get; set; }
    public int MPBonusPerLevel { get; set; }
    public int AttackBonusPerLevel { get; set; }
    public int DefenseBonusPerLevel { get; set; }

    /// <summary>所有内功的气血收益统一提高约 10%。</summary>
    public int GetHPBonus() => (int)Math.Round(HPBonusPerLevel * Level * 1.10, MidpointRounding.AwayFromZero);
    public int GetMPBonus() => MPBonusPerLevel * Level;
    public int GetAttackBonus() => AttackBonusPerLevel * Level;
    public int GetDefenseBonus() => DefenseBonusPerLevel * Level;

    public static InternalArt FromConfig(MartialArtConfig config, int level = 1)
    {
        var art = new InternalArt
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            MaxLevel = config.MaxLevel,
            Rarity = string.IsNullOrEmpty(config.Rarity) ? "common" : config.Rarity,
            Cooldown = config.Cooldown,
            MPCost = config.MPCost,
            MPCostPercent = config.MPCostPercent,
            HPBonusPerLevel = config.HPBonusPerLevel,
            MPBonusPerLevel = config.MPBonusPerLevel,
            AttackBonusPerLevel = config.AttackBonusPerLevel,
            DefenseBonusPerLevel = config.DefenseBonusPerLevel,
            Effects = config.Effects.Select(ArtEffect.FromConfig).ToList()
        };
        // 通过累计熟练度回填到目标等级起点
        int target = Math.Clamp(level, 1, art.MaxLevel);
        art.Proficiency = GetProficiencyForLevel(target, art.Rarity);
        return art;
    }

    public int GetMPCost(int totalMaxMP)
        => Math.Max(MPCost, (int)Math.Ceiling(totalMaxMP * MPCostPercent));

    public string GetMPCostDescription()
        => MPCostPercent > 0
            ? $"至少{MPCost}（或最大内力的{MPCostPercent:P0}，取较高值）"
            : MPCost.ToString();

    public override string GetSummary()
    {
        return $"{Name}[{RarityName}] Lv.{Level} | 血+{GetHPBonus()} 内+{GetMPBonus()} 攻+{GetAttackBonus()} 防+{GetDefenseBonus()} | CD{Cooldown}回 MP{GetMPCostDescription()} | 熟练度{Proficiency}";
    }
}
