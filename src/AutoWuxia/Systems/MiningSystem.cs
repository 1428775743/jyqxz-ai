using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;
using AutoWuxia.Items;

namespace AutoWuxia.Systems;

/// <summary>
/// 挖矿结果
/// </summary>
public class MiningResult
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
/// 挖矿系统
/// </summary>
public static class MiningSystem
{
    /// <summary>
    /// 矿场等级对应的成功率系数
    /// </summary>
    private static double GetTierCoefficient(string tier) => tier switch
    {
        "normal" => 2.0,
        "medium" => 1.5,
        "high" => 1.0,
        _ => 2.0
    };

    /// <summary>
    /// 稀有度权重
    /// </summary>
    private static readonly Dictionary<string, double> RarityWeights = new()
    {
        { "common", 50 },
        { "uncommon", 25 },
        { "rare", 15 },
        { "epic", 8 },
        { "legendary", 2 }
    };

    /// <summary>
    /// 获取矿场系数
    /// </summary>
    public static double GetCoefficient(MineConfig mine) => GetTierCoefficient(mine.Tier);

    /// <summary>
    /// 计算当前成功率
    /// </summary>
    public static double GetSuccessRate(Player player, MineConfig mine)
    {
        int miningSkill = player.GetCraftSkill("mining");
        double coefficient = GetTierCoefficient(mine.Tier);
        return Math.Min(1.0, miningSkill / 100.0 * coefficient);
    }

    /// <summary>
    /// 执行一次挖矿
    /// </summary>
    public static MiningResult Mine(Player player, MineConfig mine, ConfigManager configMgr)
    {
        const double staminaCost = 10;  // 副业采集统一每次消耗10点体力
        // 1. 检查体力
        if (player.Stamina < staminaCost)
            return new MiningResult { Success = false, Message = $"体力不足（需要{staminaCost}，当前{player.Stamina:F0}）" };

        // 2. 消耗体力
        player.ConsumeStamina(staminaCost);

        // 3. 判定成功/失败
        double successRate = GetSuccessRate(player, mine);
        bool success = Random.Shared.NextDouble() < successRate;

        // 4. 增加熟练度（无论成败都增加）
        var (levelUps, currentProf) = player.ImproveCraftProficiency("mining", 10);
        int newSkillLevel = player.GetCraftSkill("mining");

        if (!success)
        {
            return new MiningResult
            {
                Success = false,
                Message = $"挖矿失败！一无所获。（成功率：{successRate:P0}，挖矿技艺：{newSkillLevel}，熟练度：{currentProf}/100）",
                ProficiencyGain = 10,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        // 5. 成功 - 按稀有度权重随机出矿石
        string? selectedItemId = SelectOreByRarity(mine);
        if (selectedItemId == null)
        {
            return new MiningResult
            {
                Success = false,
                Message = "矿洞空空如也，什么也没找到。",
                ProficiencyGain = 10,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        // 6. 获取矿石配置并加入背包
        if (!configMgr.Items.TryGetValue(selectedItemId, out var itemCfg))
        {
            return new MiningResult
            {
                Success = false,
                Message = "矿石信息异常。",
                ProficiencyGain = 10,
                SkillLeveledUp = levelUps > 0,
                NewSkillLevel = newSkillLevel,
                NewProficiency = currentProf
            };
        }

        var item = ConfigManager.ItemFromConfig(itemCfg);
        item.Quantity = 1;
        player.Inventory.AddItem(item);

        string levelUpMsg = levelUps > 0 ? $" 挖矿技艺提升至{newSkillLevel}！" : "";
        return new MiningResult
        {
            Success = true,
            Message = $"挖矿成功！获得【{itemCfg.Name}】x1（价值{itemCfg.Value}银）。{levelUpMsg}（成功率：{successRate:P0}，熟练度：{currentProf}/100）",
            ObtainedItemId = selectedItemId,
            ObtainedItemName = itemCfg.Name,
            ProficiencyGain = 10,
            SkillLeveledUp = levelUps > 0,
            NewSkillLevel = newSkillLevel,
            NewProficiency = currentProf
        };
    }

    /// <summary>
    /// 按稀有度权重从矿场矿石池中随机选取一个矿石
    /// </summary>
    private static string? SelectOreByRarity(MineConfig mine)
    {
        if (mine.Ores.Count == 0) return null;

        // 按稀有度分组
        var rarityGroups = mine.Ores
            .GroupBy(o => o.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 计算有效稀有度权重（只包含矿池中存在的稀有度）
        var validWeights = RarityWeights
            .Where(rw => rarityGroups.ContainsKey(rw.Key))
            .ToList();

        if (validWeights.Count == 0) return null;

        // 按权重随机选择稀有度
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

        // 从该稀有度的矿石中随机选一个
        var candidates = rarityGroups[selectedRarity];
        return candidates[Random.Shared.Next(candidates.Count)].ItemId;
    }
}
