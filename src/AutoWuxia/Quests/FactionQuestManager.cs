using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;

namespace AutoWuxia.Quests;

public enum FactionQuestAcceptResult
{
    Success,
    NotFound,
    WrongFaction,
    AlreadyAccepted
}

/// <summary>
/// 门派任务管理器
/// 负责发布(添加)/取消(删除)门派任务,维护可领取任务池.
/// 任务被玩家接受后,从池中移除并加入玩家任务日志.
/// 收集类任务的物品在 TrySubmitItems 时通过 IssuerNpcId 转入对应 NPC 背包.
/// </summary>
public class FactionQuestManager
{
    private readonly ConfigManager _config;
    /// <summary>所有门派的可领取任务池, Key=factionId</summary>
    private readonly Dictionary<string, List<FactionQuest>> _byFaction = new();

    public FactionQuestManager(ConfigManager config)
    {
        _config = config;
    }

    /// <summary>启动时从配置目录 data/quests/faction/ 中所有 type=faction 的任务加载到可领取池</summary>
    public void LoadFromConfig()
    {
        foreach (var (id, cfg) in _config.Quests)
        {
            if (!string.Equals(cfg.Type, "faction", StringComparison.OrdinalIgnoreCase)) continue;
            var q = FactionQuest.FromConfig(cfg);
            AddInternal(q);
        }
    }

    private void AddInternal(FactionQuest quest)
    {
        if (string.IsNullOrEmpty(quest.FactionId))
            quest.FactionId = "free";
        if (!_byFaction.TryGetValue(quest.FactionId, out var list))
        {
            list = new List<FactionQuest>();
            _byFaction[quest.FactionId] = list;
        }
        // 防重复
        if (list.Any(q => q.Id == quest.Id)) return;
        list.Add(quest);
    }

    /// <summary>添加 NPC 委托的收集任务</summary>
    public FactionQuest AddCollectionQuest(string factionId, string issuerNpcId,
        string itemId, int quantity, QuestReward? reward = null, string? name = null,
        string? description = null)
    {
        var q = new FactionQuest
        {
            Id = $"fq_collect_{Guid.NewGuid():N}".Substring(0, 20),
            Name = name ?? $"代为搜罗 {itemId} x{quantity}",
            Description = description ?? $"为 {issuerNpcId} 收集 {itemId} 共 {quantity} 件,带回交付。",
            FactionId = factionId,
            IssuerNpcId = issuerNpcId,
            SubType = "collect",
            Difficulty = "normal",
            RequiredItems = { new QuestRequiredItem { ItemId = itemId, Quantity = quantity } },
            Reward = reward ?? new QuestReward { GoldBonus = 30, ReputationBonus = 10, FactionContributionBonus = 10 },
            Steps = { new QuestStep { Id = "submit", Description = $"提交 {itemId} x{quantity}", ActionType = "submit" } }
        };
        AddInternal(q);
        return q;
    }

    /// <summary>添加山贼讨伐任务. difficulty: easy / medium / hard</summary>
    public FactionQuest AddBanditQuest(string factionId, string difficulty = "easy",
        string? name = null, string? description = null)
    {
        var dungeonId = "bandit_" + difficulty;
        if (!_config.Dungeons.ContainsKey(dungeonId))
            throw new InvalidOperationException($"未找到副本配置: {dungeonId}");

        // 难度对应的奖励基线
        var (gold, rep, contrib) = difficulty switch
        {
            "hard" => (200, 80, 60),
            "medium" => (100, 50, 35),
            _ => (50, 30, 20)
        };
        var difficultyName = difficulty switch
        {
            "hard" => "高难", "medium" => "中等", _ => "简单"
        };

        var q = new FactionQuest
        {
            Id = $"fq_bandit_{Guid.NewGuid():N}".Substring(0, 20),
            Name = name ?? $"剿灭周围山贼({difficultyName})",
            Description = description ?? $"近日山贼出没,门派欲请门内弟子前往清剿。难度:{difficultyName}。",
            FactionId = factionId,
            DungeonId = dungeonId,
            SubType = "bandit",
            Difficulty = difficulty,
            Reward = new QuestReward
            {
                GoldBonus = gold,
                ReputationBonus = rep,
                FactionContributionBonus = contrib
            },
            Steps = { new QuestStep { Id = "clear", Description = "清剿山贼", ActionType = "dungeon" } }
        };
        AddInternal(q);
        return q;
    }

    /// <summary>从可领取池中移除任务</summary>
    public bool RemoveQuest(string questId)
    {
        foreach (var list in _byFaction.Values)
        {
            int idx = list.FindIndex(q => q.Id == questId);
            if (idx >= 0)
            {
                list.RemoveAt(idx);
                return true;
            }
        }
        return false;
    }

    public List<FactionQuest> GetAvailableQuests(string factionId)
    {
        if (_byFaction.TryGetValue(factionId, out var list))
            return new List<FactionQuest>(list);
        return new List<FactionQuest>();
    }

    public List<FactionQuest> GetIssuedByNpc(string npcId)
    {
        var result = new List<FactionQuest>();
        foreach (var list in _byFaction.Values)
            result.AddRange(list.Where(q => q.IssuerNpcId == npcId));
        return result;
    }

    public List<FactionQuest> GetAll()
    {
        return _byFaction.Values.SelectMany(l => l).ToList();
    }

    /// <summary>导出任务池快照(用于存档:含配置任务与运行时生成的收集任务)。</summary>
    public Dictionary<string, List<FactionQuest>> ExportPool()
    {
        var snap = new Dictionary<string, List<FactionQuest>>();
        foreach (var (k, list) in _byFaction)
            snap[k] = new List<FactionQuest>(list);
        return snap;
    }

    /// <summary>从存档恢复任务池(替换当前池;已接取的任务存档时已移除,不会重现)。</summary>
    public void RestorePool(Dictionary<string, List<FactionQuest>>? pool)
    {
        _byFaction.Clear();
        if (pool == null) return;
        foreach (var (k, list) in pool)
            _byFaction[k] = new List<FactionQuest>(list);
    }

    /// <summary>玩家接受任务: 从可领取池移除并加到玩家任务日志</summary>
    public FactionQuestAcceptResult AcceptQuest(Player player, string questId)
    {
        if (player.QuestLog.Any(q => q.Id == questId))
            return FactionQuestAcceptResult.AlreadyAccepted;

        foreach (var list in _byFaction.Values)
        {
            int idx = list.FindIndex(q => q.Id == questId);
            if (idx < 0) continue;
            var q = list[idx];
            // 门派任务仅限本门弟子接取(非本门弟子不可领取)
            if (!string.IsNullOrEmpty(q.FactionId) && q.FactionId != "free" && player.FactionId != q.FactionId)
                return FactionQuestAcceptResult.WrongFaction;
            list.RemoveAt(idx);
            player.AddQuest(q);
            return FactionQuestAcceptResult.Success;
        }
        return FactionQuestAcceptResult.NotFound;
    }
}
