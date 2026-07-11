using AutoWuxia.Core;
using AutoWuxia.Items;
using AutoWuxia.MartialArts;
using AutoWuxia.Quests;

namespace AutoWuxia.Characters;

public class Player : CharacterBase
{
    /// <summary>玩家头像图片路径（相对于 assets/portraits/）。</summary>
    public string PortraitPath { get; set; } = "player_01_buyi.png";

    public int BuddhistValue { get; set; } = 0;  // 佛法值
    public List<QuestBase> QuestLog { get; set; } = new();
    public string? CurrentLocationId { get; set; }

    /// <summary>健康度 (0=死亡, 上限100)</summary>
    public int Health { get; set; } = 100;
    /// <summary>健康度上限</summary>
    public int MaxHealth { get; set; } = 100;
    /// <summary>长期标签/Buff（重伤、中毒、阉人、称号等）</summary>
    public List<PlayerTag> Tags { get; set; } = new();

    /// <summary>当前药物增益(药buff)。同时仅1个,吃新药覆盖;按天持续,每日tick递减。</summary>
    public MedicineBuff? MedicineBuff { get; set; }

    /// <summary>当前食物增益(食buff)。同时仅1个,吃新食物覆盖;与药buff独立共存;按天持续,每日tick递减。</summary>
    public FoodBuff? FoodBuff { get; set; }

    /// <summary>江湖声望 (≥10000 触发华山论剑邀请)</summary>
    public int Reputation { get; set; } = 0;

    /// <summary>各门派贡献度 Key=factionId Value=贡献值</summary>
    public Dictionary<string, int> FactionContributions { get; set; } = new();

    /// <summary>申请入派被拒记录 Key=factionId Value=被拒当天Day;10天内不可再申请该门派。</summary>
    public Dictionary<string, int> FactionJoinRejections { get; set; } = new();

    /// <summary>任务私有进度数据（如思过崖面壁起始日: "siguoyai_start_day"）</summary>
    public Dictionary<string, int> QuestProgressData { get; set; } = new();

    /// <summary>
    /// 悟性(1~10):影响武功熟练度的获取速度。
    /// 公式 multiplier = 0.5 + Talent * 0.1
    ///   Talent=1: 0.6x | Talent=5: 1.0x | Talent=8: 1.3x | Talent=10: 1.5x
    /// 战斗中出招、闪避、战斗结束结算时都按此倍率应用。
    /// </summary>
    public int Talent { get; set; } = 5;

    /// <summary>悟性熟练度倍率: 0.5 + Talent * 0.1</summary>
    public double TalentMultiplier => 0.5 + Math.Clamp(Talent, 1, 10) * 0.1;

    /// <summary>
    /// 修炼速度额外加成(如"勤学苦练"天赋 +0.5)。0=无加成。
    /// 与悟性倍率叠加,最终熟练度获取倍率 = TalentMultiplier * (1 + TrainingSpeedBonus)。
    /// </summary>
    public double TrainingSpeedBonus { get; set; } = 0;

    /// <summary>实际武功熟练度获取倍率(悟性 × 修炼加成)。</summary>
    public double EffectiveTrainingMultiplier => TalentMultiplier * (1 + TrainingSpeedBonus);

    /// <summary>
    /// 玩家升级成长(重写):每级 +3攻 +3防 +40HP +15MP。
    /// 攻防成长适中,HP偏厚增强肉度;MP沿用基类。使玩家能在50~60级接近五绝级高手。
    /// </summary>
    protected override void OnLevelUp()
    {
        MaxHP += 40;
        MaxMP += 15;
        BaseAttack += 3;
        BaseDefense += 3;
        CurrentHP = Math.Min(CurrentHP + 40, GetTotalMaxHP());
        CurrentMP = Math.Min(CurrentMP + 15, GetTotalMaxMP());
    }

    public Player()
    {
        Id = "player";
        Name = "小虾米";
        Description = "一位初出茅庐的江湖新人";
        MaxHP = 500;
        CurrentHP = 500;
        MaxMP = 200;
        CurrentMP = 200;
        BaseAttack = 50;
        BaseDefense = 30;
        Stamina = 100;
        MaxStamina = 100;
        Mood = 50;
        Karma = 20;
        Gold = 1000;
        CraftSkills = new Dictionary<string, int>
        {
            { "art", 20 }, { "forging", 20 }, { "mining", 20 },
            { "gathering", 20 }, { "hunting", 20 }
        };
    }

    public bool ConsumeStamina(double amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    public void AddQuest(QuestBase quest)
    {
        if (QuestLog.Any(q => q.Id == quest.Id)) return;
        QuestLog.Add(quest);
        EventSystem.Instance.Publish(GameEvents.QuestStarted,
            new Dictionary<string, object?> { { "questId", quest.Id }, { "questName", quest.Name } });
    }

    public QuestBase? GetActiveQuest(string questId)
    {
        return QuestLog.FirstOrDefault(q => q.Id == questId && q.Status == QuestStatus.InProgress);
    }

    /// <summary>增加声望并发布事件</summary>
    public void AddReputation(int amount)
    {
        if (amount == 0) return;
        Reputation = Math.Max(0, Reputation + amount);
        EventSystem.Instance.Publish(GameEvents.ReputationChanged,
            new Dictionary<string, object?> { { "delta", amount }, { "total", Reputation } });
    }

    /// <summary>增加门派贡献并发布事件</summary>
    public void AddFactionContribution(string factionId, int amount)
    {
        if (string.IsNullOrEmpty(factionId) || amount == 0) return;
        FactionContributions.TryGetValue(factionId, out int cur);
        FactionContributions[factionId] = Math.Max(0, cur + amount);
        EventSystem.Instance.Publish(GameEvents.FactionContributionChanged,
            new Dictionary<string, object?>
            {
                { "factionId", factionId },
                { "delta", amount },
                { "total", FactionContributions[factionId] }
            });
    }

    public int GetFactionContribution(string factionId)
        => FactionContributions.TryGetValue(factionId, out int c) ? c : 0;

    // ── 标签系统 ──

    /// <summary>添加标签（如已存在同ID则刷新剩余天数）</summary>
    public void AddTag(PlayerTag tag)
    {
        var existing = Tags.FirstOrDefault(t => t.TagId == tag.TagId);
        if (existing != null)
        {
            // 刷新：取更长的剩余天数
            if (tag.RemainingDays > existing.RemainingDays)
                existing.RemainingDays = tag.RemainingDays;
            if (tag.DailyHealthLoss > existing.DailyHealthLoss)
            {
                existing.DailyHealthLoss = tag.DailyHealthLoss;
                existing.DailyHealthLossMax = tag.DailyHealthLossMax;
            }
        }
        else
        {
            Tags.Add(tag);
        }
        EventSystem.Instance.Publish(GameEvents.TagChanged,
            new Dictionary<string, object?> { { "action", "add" }, { "tagId", tag.TagId }, { "tagName", tag.Name } });
    }

    /// <summary>移除标签</summary>
    public bool RemoveTag(string tagId)
    {
        var tag = Tags.FirstOrDefault(t => t.TagId == tagId);
        if (tag == null) return false;
        Tags.Remove(tag);
        EventSystem.Instance.Publish(GameEvents.TagChanged,
            new Dictionary<string, object?> { { "action", "remove" }, { "tagId", tagId }, { "tagName", tag.Name } });
        return true;
    }

    /// <summary>检查是否拥有指定标签</summary>
    public bool HasTag(string tagId) => Tags.Any(t => t.TagId == tagId);

    /// <summary>获取标签摘要</summary>
    public string GetTagsSummary()
    {
        if (Tags.Count == 0) return "无";
        return string.Join(" ", Tags.Select(t => t.GetSummary()));
    }

    /// <summary>修改健康度并发布事件</summary>
    public void ChangeHealth(int amount, string reason = "")
    {
        if (amount == 0) return;
        Health = Math.Clamp(Health + amount, 0, MaxHealth);
        EventSystem.Instance.Publish(GameEvents.HealthChanged,
            new Dictionary<string, object?> { { "delta", amount }, { "current", Health }, { "max", MaxHealth }, { "reason", reason } });
    }

    /// <summary>需要自宫才能修炼的武功ID集合</summary>
    private static readonly HashSet<string> _eunuchRequiredArts = new()
    {
        "kuihua_baodian", "bixie_jianfa"
    };

    /// <summary>检查是否可以学习指定武功（考虑自宫等前置条件）</summary>
    public bool CanLearnArt(string artId, out string? reason)
    {
        reason = null;
        if (_eunuchRequiredArts.Contains(artId) && !HasTag("eunuch"))
        {
            reason = "此武功需先自宫方可修炼";
            return false;
        }
        return true;
    }
}

