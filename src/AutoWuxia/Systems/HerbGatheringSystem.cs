using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;
using AutoWuxia.Items;

namespace AutoWuxia.Systems;

/// <summary>
/// 采药结果
/// </summary>
public class HerbGatherResult
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
/// 采药系统 - 用 planting(种植/采药)技艺采集草药材料。结构与 MiningSystem 平行。
/// </summary>
public static class HerbGatheringSystem
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
        { "common", 50 },
        { "uncommon", 25 },
        { "rare", 15 },
        { "epic", 8 },
        { "legendary", 2 }
    };

    public static double GetCoefficient(HerbGardenConfig garden) => GetTierCoefficient(garden.Tier);

    public static double GetSuccessRate(Player player, HerbGardenConfig garden)
    {
        int skill = player.GetCraftSkill("gathering");
        double coefficient = GetTierCoefficient(garden.Tier);
        return Math.Min(1.0, skill / 100.0 * coefficient);
    }

    /// <summary>执行一次采药</summary>
    public static HerbGatherResult Gather(Player player, HerbGardenConfig garden, ConfigManager configMgr)
    {
        const double staminaCost = 10;  // 副业采集统一每次消耗10点体力
        if (player.Stamina < staminaCost)
            return new HerbGatherResult { Success = false, Message = $"体力不足（需要{staminaCost}，当前{player.Stamina:F0}）" };

        player.ConsumeStamina(staminaCost);

        double successRate = GetSuccessRate(player, garden);
        bool success = Random.Shared.NextDouble() < successRate;

        var (levelUps, currentProf) = player.ImproveCraftProficiency("gathering", 8);
        int newSkillLevel = player.GetCraftSkill("gathering");

        if (!success)
        {
            return new HerbGatherResult
            {
                Success = false,
                Message = $"采药空手而归，山中草木虽多却无所获。（成功率：{successRate:P0}，熟练度：{currentProf}/100）",
                ProficiencyGain = 8,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        string? selectedItemId = SelectHerbByRarity(garden);
        if (selectedItemId == null)
        {
            return new HerbGatherResult
            {
                Success = false,
                Message = "药园转了一圈，未发现可采之药。",
                ProficiencyGain = 8,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        if (!configMgr.Items.TryGetValue(selectedItemId, out var itemCfg))
        {
            return new HerbGatherResult
            {
                Success = false,
                Message = "草药信息异常。",
                ProficiencyGain = 8,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        var item = ConfigManager.ItemFromConfig(itemCfg);
        item.Quantity = 1;
        player.Inventory.AddItem(item);

        string levelUpMsg = levelUps > 0 ? $" 采药技艺提升至{newSkillLevel}！" : "";
        return new HerbGatherResult
        {
            Success = true,
            Message = $"采药成功！获得【{itemCfg.Name}】x1（价值{itemCfg.Value}银）。{levelUpMsg}（成功率：{successRate:P0}，熟练度：{currentProf}/100）",
            ObtainedItemId = selectedItemId,
            ObtainedItemName = itemCfg.Name,
            ProficiencyGain = 8,
            SkillLeveledUp = levelUps > 0,
            NewSkillLevel = newSkillLevel,
            NewProficiency = currentProf
        };
    }

    /// <summary>按稀有度权重从药园草药池中随机选取一个</summary>
    private static string? SelectHerbByRarity(HerbGardenConfig garden)
    {
        if (garden.Herbs.Count == 0) return null;

        var rarityGroups = garden.Herbs
            .GroupBy(h => h.Rarity)
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
