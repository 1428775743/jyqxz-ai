using AutoWuxia.Config.Models;

namespace AutoWuxia.Quests;

/// <summary>
/// 副本失败处罚类型
/// </summary>
public enum DungeonFailType
{
    DeductGold,   // 扣金钱
    DeductHP,     // HP 设为 1
    GameOver      // 游戏结束 (回到上次存档)
}

/// <summary>
/// 副本运行时模型 - 多轮战斗,每轮可有多个对手
/// </summary>
public class Dungeon
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "bandit";
    public List<DungeonRound> Rounds { get; set; } = new();
    public DungeonReward Reward { get; set; } = new();
    public DungeonFailPenalty OnFail { get; set; } = new();
    public int StaminaCost { get; set; } = 20;
    public double TimeCostHours { get; set; } = 2;

    public static Dungeon FromConfig(DungeonConfig config)
    {
        var d = new Dungeon
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            Type = config.Type,
            StaminaCost = config.StaminaCost,
            TimeCostHours = config.TimeCostHours,
            Reward = new DungeonReward
            {
                Gold = config.Reward.Gold,
                Reputation = config.Reward.Reputation,
                FactionContribution = config.Reward.FactionContribution,
                FactionId = config.Reward.FactionId
            },
            OnFail = new DungeonFailPenalty
            {
                Type = ParseFailType(config.OnFail.Type),
                Amount = config.OnFail.Amount
            }
        };
        foreach (var r in config.Rounds)
        {
            d.Rounds.Add(new DungeonRound
            {
                OpponentCharacterId = r.OpponentCharacterId,
                OpponentPool = new List<string>(r.OpponentPool),
                Count = Math.Max(1, r.Count),
                TriggerDialogue = r.TriggerDialogue
            });
        }
        return d;
    }

    private static DungeonFailType ParseFailType(string s) => s?.ToLowerInvariant() switch
    {
        "deducthp" => DungeonFailType.DeductHP,
        "gameover" => DungeonFailType.GameOver,
        _ => DungeonFailType.DeductGold
    };
}

public class DungeonRound
{
    public string? OpponentCharacterId { get; set; }
    public List<string> OpponentPool { get; set; } = new();
    public int Count { get; set; } = 1;
    public bool TriggerDialogue { get; set; } = true;

    /// <summary>
    /// 根据配置抽取本轮的对手 ID 列表(若有 OpponentPool 则从池中随机抽取 Count 个)
    /// </summary>
    public List<string> RollOpponents()
    {
        var result = new List<string>();
        if (!string.IsNullOrEmpty(OpponentCharacterId))
        {
            for (int i = 0; i < Count; i++) result.Add(OpponentCharacterId);
            return result;
        }
        if (OpponentPool.Count == 0) return result;
        for (int i = 0; i < Count; i++)
            result.Add(OpponentPool[Random.Shared.Next(OpponentPool.Count)]);
        return result;
    }
}

public class DungeonReward
{
    public int Gold { get; set; }
    public int Reputation { get; set; }
    public int FactionContribution { get; set; }
    public string? FactionId { get; set; }
}

public class DungeonFailPenalty
{
    public DungeonFailType Type { get; set; } = DungeonFailType.DeductGold;
    public int Amount { get; set; }
}
