using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;
using AutoWuxia.Items;

namespace AutoWuxia.Systems;

/// <summary>
/// 打猎结果
/// </summary>
public class HuntResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ObtainedItemId { get; set; }
    public string? ObtainedItemName { get; set; }
    public int ProficiencyGain { get; set; }
    public bool SkillLeveledUp { get; set; }
    public int NewSkillLevel { get; set; }
    public int NewProficiency { get; set; }
}

/// <summary>
/// 打猎系统 - 后山可打猎获取猎物/皮毛/药材
/// 使用 "hunting" 技艺的姐妹属性（planting 作为"狩猎"技艺更合适）
/// 这里复用 mining 技艺作为"狩猎"技艺（减少新系统复杂度），或可新建 "hunting"
/// 实际使用 planting 技艺来代表"山中采集+狩猎"的综合能力。
/// </summary>
public static class HuntingSystem
{
    private static double GetTierCoefficient(string tier) => tier switch
    {
        "normal" => 2.0,
        "medium" => 1.5,
        "high" => 1.0,
        _ => 2.0
    };

    private static readonly Dictionary<string, double> RarityWeights = new()
    {
        { "common", 55 },
        { "uncommon", 30 },
        { "rare", 12 },
        { "epic", 3 }
    };

    public static double GetCoefficient(HuntConfig hunt) => GetTierCoefficient(hunt.Tier);

    public static double GetSuccessRate(Player player, HuntConfig hunt)
    {
        int skill = player.GetCraftSkill("hunting");
        double coefficient = GetTierCoefficient(hunt.Tier);
        return Math.Min(1.0, skill / 100.0 * coefficient);
    }

    /// <summary>
    /// 执行一次打猎
    /// </summary>
    public static HuntResult Hunt(Player player, HuntConfig hunt, ConfigManager configMgr)
    {
        const double staminaCost = 10;  // 副业采集统一每次消耗10点体力
        if (player.Stamina < staminaCost)
            return new HuntResult { Success = false, Message = $"体力不足（需要{staminaCost}，当前{player.Stamina:F0}）" };

        player.ConsumeStamina(staminaCost);

        double successRate = GetSuccessRate(player, hunt);
        bool success = Random.Shared.NextDouble() < successRate;

        var (levelUps, currentProf) = player.ImproveCraftProficiency("hunting", 8);
        int newSkillLevel = player.GetCraftSkill("hunting");

        if (!success)
        {
            return new HuntResult
            {
                Success = false,
                Message = $"打猎空手而归，山中猎物机警，一无所获。（成功率：{successRate:P0}，熟练度：{currentProf}/100）",
                ProficiencyGain = 8,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        string? selectedItemId = SelectLootByRarity(hunt);
        if (selectedItemId == null)
        {
            return new HuntResult
            {
                Success = false,
                Message = "山中转了一圈，什么猎物都没发现。",
                ProficiencyGain = 8,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        if (!configMgr.Items.TryGetValue(selectedItemId, out var itemCfg))
        {
            return new HuntResult
            {
                Success = false,
                Message = "猎物信息异常。",
                ProficiencyGain = 8,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        var item = ConfigManager.ItemFromConfig(itemCfg);
        item.Quantity = 1;
        player.Inventory.AddItem(item);

        string levelUpMsg = levelUps > 0 ? $" 狩猎技艺提升至{newSkillLevel}！" : "";
        return new HuntResult
        {
            Success = true,
            Message = $"打猎成功！猎获【{itemCfg.Name}】x1（价值{itemCfg.Value}银）。{levelUpMsg}（成功率：{successRate:P0}，熟练度：{currentProf}/100）",
            ObtainedItemId = selectedItemId,
            ObtainedItemName = itemCfg.Name,
            ProficiencyGain = 8,
            SkillLeveledUp = levelUps > 0,
            NewSkillLevel = newSkillLevel,
            NewProficiency = currentProf
        };
    }

    private static string? SelectLootByRarity(HuntConfig hunt)
    {
        if (hunt.Loots.Count == 0) return null;

        var rarityGroups = hunt.Loots
            .GroupBy(l => l.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());

        var validWeights = RarityWeights
            .Where(rw => rarityGroups.ContainsKey(rw.Key))
            .ToList();

        if (validWeights.Count == 0) return null;

        double totalWeight = validWeights.Sum(w => w.Value);
        double roll = Random.Shared.NextDouble() * totalWeight;
        double cumulative = 0;
        string selectedRarity = validWeights[0].Key;

        foreach (var (rarity, weight) in validWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                selectedRarity = rarity;
                break;
            }
        }

        var candidates = rarityGroups[selectedRarity];
        return candidates[Random.Shared.Next(candidates.Count)].ItemId;
    }
}
