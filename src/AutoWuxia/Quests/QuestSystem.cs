using System.Text.Json.Serialization;
using AutoWuxia.Characters;
using AutoWuxia.Config.Models;

namespace AutoWuxia.Quests;

/// <summary>
/// 任务状态机
/// InProgress 进行中 → Completed 已完成(可领奖) → Rewarded 已结束(已归档,可查看历史)
/// Failed     失败
/// </summary>
public enum QuestStatus
{
    InProgress,
    Completed,
    Rewarded,
    Failed
}

public class QuestRequiredItem
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int Submitted { get; set; }   // 已提交数量

    public bool IsFulfilled => Submitted >= Quantity;
}

public class RewardItem
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$questType")]
[JsonDerivedType(typeof(MainQuest), typeDiscriminator: "main")]
[JsonDerivedType(typeof(SideQuest), typeDiscriminator: "side")]
[JsonDerivedType(typeof(FactionQuest), typeDiscriminator: "faction")]
[JsonDerivedType(typeof(DungeonQuest), typeDiscriminator: "dungeon")]
[JsonDerivedType(typeof(DynamicQuest), typeDiscriminator: "dynamic")]
[JsonDerivedType(typeof(ChainQuest), typeDiscriminator: "chain")]
public abstract class QuestBase
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CurrentStepIndex { get; set; }
    public QuestStatus Status { get; set; } = QuestStatus.InProgress;
    public List<QuestStep> Steps { get; set; } = new();
    public QuestReward? Reward { get; set; }

    /// <summary>接取任务时播放的剧情对话(可选)</summary>
    public DialogueScript? IntroDialogue { get; set; }
    /// <summary>领取最终奖励时播放的剧情对话(可选,可衔接下一任务)</summary>
    public DialogueScript? CompleteDialogue { get; set; }

    /// <summary>委托NPC (收集任务必填,提交物品流向该NPC)</summary>
    public string? IssuerNpcId { get; set; }
    /// <summary>所属门派</summary>
    public string? FactionId { get; set; }
    /// <summary>关联副本ID</summary>
    public string? DungeonId { get; set; }
    /// <summary>难度</summary>
    public string Difficulty { get; set; } = "normal";
    /// <summary>需要提交的物品</summary>
    public List<QuestRequiredItem> RequiredItems { get; set; } = new();

    /// <summary>接取该任务所需的最低好感度（默认0=无要求）</summary>
    public int MinFavorabilityToOffer { get; set; }

    /// <summary>接取该任务所需玩家任一武功最低等级（默认0=无要求）</summary>
    public int MinAnyArtLevel { get; set; }

    /// <summary>关联人NPC ID列表（对话时知道"发布者委托玩家处理本任务"，但不知详情）</summary>
    public List<string> RelatedNpcIds { get; set; } = new();

    public QuestStep? CurrentStep => CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

    // ── 兼容旧代码 ──
    public bool IsCompleted => Status == QuestStatus.Completed || Status == QuestStatus.Rewarded;
    public bool IsFailed => Status == QuestStatus.Failed;

    public virtual bool TryAdvanceStep(Player player)
    {
        if (Status != QuestStatus.InProgress) return false;
        if (CurrentStep == null) return false;
        if (!CurrentStep.CheckConditions(player)) return false;
        CurrentStepIndex++;
        if (CurrentStepIndex >= Steps.Count)
            Status = QuestStatus.Completed;
        return true;
    }

    /// <summary>
    /// 提交物品 (收集任务): 从玩家背包扣物品,若有 IssuerNpc 则物品转入 NPC 背包,
    /// 全部满足后状态转为 Completed
    /// </summary>
    public bool TrySubmitItems(Player player, NPC? issuerNpc, out string message)
    {
        message = "";
        if (Status != QuestStatus.InProgress)
        {
            message = "任务不在进行中";
            return false;
        }
        if (RequiredItems.Count == 0)
        {
            message = "此任务无需提交物品";
            return false;
        }

        // 校验玩家持有
        foreach (var req in RequiredItems)
        {
            int need = req.Quantity - req.Submitted;
            if (need <= 0) continue;
            if (!player.Inventory.HasItem(req.ItemId, need))
            {
                message = $"物品不足: 需要 {req.ItemId} x{need}";
                return false;
            }
        }

        // 转移
        foreach (var req in RequiredItems)
        {
            int need = req.Quantity - req.Submitted;
            if (need <= 0) continue;
            if (issuerNpc != null)
                player.Inventory.TransferTo(issuerNpc.Inventory, req.ItemId, need);
            else
                player.Inventory.RemoveItem(req.ItemId, need);
            req.Submitted += need;
        }

        if (RequiredItems.All(r => r.IsFulfilled))
            Status = QuestStatus.Completed;

        message = "物品已提交";
        return true;
    }

    /// <summary>
    /// 领取奖励,状态转为 Rewarded (已结束)
    /// </summary>
    public bool ClaimReward(Player player, AutoWuxia.Config.ConfigManager? config, out string message)
    {
        message = "";
        if (Status != QuestStatus.Completed)
        {
            message = "任务尚未完成";
            return false;
        }
        GrantReward(player, config);
        Status = QuestStatus.Rewarded;
        message = "奖励已领取";
        return true;
    }

    public void GrantReward(Player player, AutoWuxia.Config.ConfigManager? config = null)
    {
        if (Reward == null) return;
        player.MaxHP += Reward.HPBonus;
        player.MaxMP += Reward.MPBonus;
        player.BaseAttack += Reward.AttackBonus;
        player.BaseDefense += Reward.DefenseBonus;
        if (Reward.KarmaBonus != 0)
            player.Karma = Math.Clamp(player.Karma + Reward.KarmaBonus, -100, 100);
        if (Reward.JianghuExp > 0)
            player.GainJianghuExp(Reward.JianghuExp);

        if (Reward.GoldBonus != 0) player.Gold += Reward.GoldBonus;
        if (Reward.ReputationBonus != 0) player.AddReputation(Reward.ReputationBonus);
        if (Reward.FactionContributionBonus != 0)
        {
            var fac = Reward.FactionIdForContribution ?? FactionId;
            if (!string.IsNullOrEmpty(fac))
                player.AddFactionContribution(fac, Reward.FactionContributionBonus);
        }
        if (config != null && Reward.Items.Count > 0)
        {
            foreach (var ri in Reward.Items)
            {
                var item = config.CreateItem(ri.ItemId);
                if (item == null) continue;
                item.Quantity = ri.Quantity;
                player.Inventory.AddItem(item);
            }
        }

        // 奖励武功秘籍
        if (!string.IsNullOrEmpty(Reward.MartialArtId) && config != null)
        {
            if (!player.LearnedArts.Any(a => a.Id == Reward.MartialArtId))
            {
                var art = config.CreateMartialArt(Reward.MartialArtId);
                if (art != null)
                {
                    player.LearnArt(art);
                    Core.GameLogger.Info($"[任务奖励] 学会武功: {art.Name}");
                }
            }
        }
    }
}

public class QuestStep
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public string? TargetScene { get; set; }
    public string? TargetNPC { get; set; }
    public string ActionType { get; set; } = "talk";
    public Dictionary<string, string> Conditions { get; set; } = new();

    /// <summary>给发布者AI的剧情上下文提示（当NPC作为该任务发布者对话时注入prompt）</summary>
    public string? AiHint { get; set; }

    /// <summary>该步骤的节点奖励（链式任务中每步可有独立奖励）</summary>
    public QuestReward? Reward { get; set; }

    /// <summary>该步骤完成时播放的剧情对话(可选)</summary>
    public DialogueScript? Dialogue { get; set; }

    /// <summary>该步骤需要提交的物品（链式任务中的收集节点）</summary>
    public List<QuestRequiredItem> RequiredItems { get; set; } = new();

    public bool CheckConditions(Player player)
    {
        foreach (var (key, value) in Conditions)
        {
            switch (key)
            {
                case "minLevel" when int.TryParse(value, out int lv) && player.LearnedArts.Count < lv:
                    return false;
                case "faction" when player.FactionId != value:
                    return false;
                case "minKarma" when int.TryParse(value, out int mk) && player.Karma < mk:
                    return false;
                case "maxKarma" when int.TryParse(value, out int xk) && player.Karma > xk:
                    return false;
            }
        }
        return true;
    }
}

// ── 剧情对话(运行时) ──

public class DialogueLine
{
    public string Speaker { get; set; } = "";
    public List<string> Lines { get; set; } = new();
}

public class DialogueScript
{
    public List<DialogueLine> Lines { get; set; } = new();

    public bool HasContent => Lines != null && Lines.Count > 0;

    public static DialogueScript? FromConfig(Config.Models.DialogueScriptConfig? cfg)
    {
        if (cfg?.Lines == null || cfg.Lines.Count == 0) return null;
        return new DialogueScript
        {
            Lines = cfg.Lines.Select(l => new DialogueLine
            {
                Speaker = l.Speaker ?? "",
                Lines = l.Lines?.ToList() ?? new List<string>()
            }).Where(l => l.Lines.Count > 0).ToList()
        };
    }
}

public class QuestReward
{
    public int HPBonus { get; set; }
    public int MPBonus { get; set; }
    public int AttackBonus { get; set; }
    public int DefenseBonus { get; set; }
    public string? MartialArtId { get; set; }
    public int KarmaBonus { get; set; }
    public int JianghuExp { get; set; }
    public int GoldBonus { get; set; }
    public int ReputationBonus { get; set; }
    public int FactionContributionBonus { get; set; }
    public string? FactionIdForContribution { get; set; }
    public List<RewardItem> Items { get; set; } = new();

    public static QuestReward? FromConfig(QuestRewardConfig? config)
    {
        if (config == null) return null;
        return new QuestReward
        {
            HPBonus = config.HPBonus,
            MPBonus = config.MPBonus,
            AttackBonus = config.AttackBonus,
            DefenseBonus = config.DefenseBonus,
            MartialArtId = config.MartialArtId,
            KarmaBonus = config.KarmaBonus,
            JianghuExp = config.JianghuExp,
            GoldBonus = config.GoldBonus,
            ReputationBonus = config.ReputationBonus,
            FactionContributionBonus = config.FactionContributionBonus,
            FactionIdForContribution = config.FactionIdForContribution,
            Items = config.Items.Select(i => new RewardItem { ItemId = i.ItemId, Quantity = i.Quantity }).ToList()
        };
    }

    public string GetSummary(AutoWuxia.Config.ConfigManager? config = null)
    {
        var parts = new List<string>();
        if (GoldBonus > 0) parts.Add($"银两+{GoldBonus}");
        if (ReputationBonus > 0) parts.Add($"声望+{ReputationBonus}");
        if (FactionContributionBonus > 0) parts.Add($"门派贡献+{FactionContributionBonus}");
        if (JianghuExp > 0) parts.Add($"阅历+{JianghuExp}");
        if (HPBonus > 0) parts.Add($"HP+{HPBonus}");
        if (MPBonus > 0) parts.Add($"MP+{MPBonus}");
        if (AttackBonus > 0) parts.Add($"攻击+{AttackBonus}");
        if (DefenseBonus > 0) parts.Add($"防御+{DefenseBonus}");
        if (KarmaBonus != 0) parts.Add($"善恶{(KarmaBonus > 0 ? "+" : "")}{KarmaBonus}");
        if (!string.IsNullOrEmpty(MartialArtId))
        {
            var artName = (config != null && config.MartialArts.TryGetValue(MartialArtId, out var ac)) ? ac.Name : MartialArtId;
            parts.Add($"武功秘籍: {artName}");
        }
        foreach (var it in Items)
        {
            var itemName = (config != null && config.Items.TryGetValue(it.ItemId, out var ic)) ? ic.Name : it.ItemId;
            parts.Add($"{itemName} x{it.Quantity}");
        }
        return parts.Count == 0 ? "无奖励" : string.Join(", ", parts);
    }

    /// <summary>直接发放奖励给玩家（节点奖励/不依赖config创建物品）</summary>
    public void GrantRewardDirect(Player player, AutoWuxia.Config.ConfigManager? config = null)
    {
        player.MaxHP += HPBonus;
        player.MaxMP += MPBonus;
        player.BaseAttack += AttackBonus;
        player.BaseDefense += DefenseBonus;
        if (KarmaBonus != 0)
            player.Karma = Math.Clamp(player.Karma + KarmaBonus, -100, 100);
        if (JianghuExp > 0)
            player.GainJianghuExp(JianghuExp);
        if (GoldBonus != 0) player.Gold += GoldBonus;
        if (ReputationBonus != 0) player.AddReputation(ReputationBonus);
        if (FactionContributionBonus != 0)
        {
            var fac = FactionIdForContribution;
            if (!string.IsNullOrEmpty(fac))
                player.AddFactionContribution(fac, FactionContributionBonus);
        }
        if (config != null && Items.Count > 0)
        {
            foreach (var ri in Items)
            {
                var item = config.CreateItem(ri.ItemId);
                if (item == null) continue;
                item.Quantity = ri.Quantity;
                player.Inventory.AddItem(item);
            }
        }
        // 奖励武功秘籍
        if (!string.IsNullOrEmpty(MartialArtId) && config != null)
        {
            if (!player.LearnedArts.Any(a => a.Id == MartialArtId))
            {
                var art = config.CreateMartialArt(MartialArtId);
                if (art != null)
                {
                    player.LearnArt(art);
                    Core.GameLogger.Info($"[任务奖励] 学会武功: {art.Name}");
                }
            }
        }
    }
}

// ── 任务子类型 ──

public class MainQuest : QuestBase
{
    public static MainQuest FromConfig(QuestConfig config) => Populate(new MainQuest(), config);

    internal static T Populate<T>(T quest, QuestConfig config) where T : QuestBase
    {
        quest.Id = config.Id;
        quest.Name = config.Name;
        quest.Description = config.Description;
        quest.Reward = QuestReward.FromConfig(config.Reward);
        quest.IntroDialogue = DialogueScript.FromConfig(config.IntroDialogue);
        quest.CompleteDialogue = DialogueScript.FromConfig(config.CompleteDialogue);
        quest.IssuerNpcId = config.IssuerNpcId;
        quest.FactionId = config.FactionId;
        quest.DungeonId = config.DungeonId;
        quest.Difficulty = config.Difficulty;
        quest.MinFavorabilityToOffer = config.MinFavorabilityToOffer;
        quest.MinAnyArtLevel = config.MinAnyArtLevel;
        quest.RelatedNpcIds = new List<string>(config.RelatedNpcIds);
        quest.RequiredItems = config.RequiredItems
            .Select(r => new QuestRequiredItem { ItemId = r.ItemId, Quantity = r.Quantity })
            .ToList();
        quest.Steps = config.Steps.Select(s => new QuestStep
        {
            Id = s.Id,
            Description = s.Description,
            TargetScene = s.TargetScene,
            TargetNPC = s.TargetNPC,
            ActionType = s.ActionType,
            Conditions = s.Conditions,
            AiHint = s.AiHint,
            Reward = QuestReward.FromConfig(s.Reward),
            Dialogue = DialogueScript.FromConfig(s.Dialogue),
            RequiredItems = s.RequiredItems
                .Select(r => new QuestRequiredItem { ItemId = r.ItemId, Quantity = r.Quantity })
                .ToList()
        }).ToList();
        return quest;
    }
}

public class SideQuest : QuestBase
{
    public static SideQuest FromConfig(QuestConfig config) => MainQuest.Populate(new SideQuest(), config);
}

/// <summary>
/// 门派任务 - 收集物品 / 讨伐山贼,奖励含门派贡献
/// </summary>
public class FactionQuest : QuestBase
{
    /// <summary>子类型: collect / bandit</summary>
    public string SubType { get; set; } = "collect";

    public static FactionQuest FromConfig(QuestConfig config)
    {
        var q = MainQuest.Populate(new FactionQuest(), config);
        q.SubType = string.IsNullOrEmpty(config.DungeonId) ? "collect" : "bandit";
        return q;
    }
}

/// <summary>
/// 副本任务 - 仅一步:进入副本完成 (例如华山论剑)
/// </summary>
public class DungeonQuest : QuestBase
{
    public static DungeonQuest FromConfig(QuestConfig config) => MainQuest.Populate(new DungeonQuest(), config);
}

public class DynamicQuest : QuestBase
{
    public string GeneratedBy { get; set; } = "ai";
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string? TriggerCondition { get; set; }
}

/// <summary>
/// 链式任务 - 多步骤节点任务，每个节点有独立奖励，
/// 最终节点的奖励作为整条链的终极奖励。
/// </summary>
public class ChainQuest : QuestBase
{
    /// <summary>发布该链式任务的NPC（任务发起者）</summary>
    public string? QuestTriggerNpcId { get; set; }

    public static ChainQuest FromConfig(QuestConfig config)
    {
        var q = MainQuest.Populate(new ChainQuest(), config);
        q.QuestTriggerNpcId = config.TriggerNpcId;

        // 填充每个步骤的节点奖励
        for (int i = 0; i < q.Steps.Count && i < config.Steps.Count; i++)
        {
            var stepReward = QuestReward.FromConfig(config.Steps[i].Reward);
            q.Steps[i].Reward = stepReward;
        }
        return q;
    }

    /// <summary>
    /// 重写步骤推进：每完成一步自动发放该步的节点奖励
    /// </summary>
    public override bool TryAdvanceStep(Player player)
    {
        if (Status != QuestStatus.InProgress) return false;
        if (CurrentStep == null) return false;
        if (!CurrentStep.CheckConditions(player)) return false;

        // 发放当前步骤的节点奖励
        if (CurrentStep.Reward != null)
        {
            CurrentStep.Reward.GrantRewardDirect(player);
        }

        CurrentStepIndex++;
        if (CurrentStepIndex >= Steps.Count)
            Status = QuestStatus.Completed;
        return true;
    }

    /// <summary>
    /// 提交当前步骤的物品（链式任务专用，检查当前步骤的RequiredItems）
    /// </summary>
    public bool TrySubmitStepItems(Player player, out string message)
    {
        message = "";
        if (Status != QuestStatus.InProgress)
        {
            message = "任务不在进行中";
            return false;
        }
        var step = CurrentStep;
        if (step == null || step.RequiredItems.Count == 0)
        {
            message = "当前步骤无需提交物品";
            return false;
        }

        // 校验玩家持有
        foreach (var req in step.RequiredItems)
        {
            int need = req.Quantity - req.Submitted;
            if (need <= 0) continue;
            if (!player.Inventory.HasItem(req.ItemId, need))
            {
                message = $"物品不足: 需要 {req.ItemId} x{need}";
                return false;
            }
        }

        // 扣物品
        foreach (var req in step.RequiredItems)
        {
            int need = req.Quantity - req.Submitted;
            if (need <= 0) continue;
            player.Inventory.RemoveItem(req.ItemId, need);
            req.Submitted += need;
        }

        if (step.RequiredItems.All(r => r.IsFulfilled))
        {
            // 物品全部提交，推进步骤
            TryAdvanceStep(player);
            message = "物品已提交，步骤完成！";
        }
        else
        {
            message = "物品已部分提交";
        }
        return true;
    }
}
