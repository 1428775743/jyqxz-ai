using System.Text.Json.Serialization;
using AutoWuxia.Systems;

namespace AutoWuxia.Characters;

[JsonDerivedType(typeof(SectLeader), typeDiscriminator: "sectLeader")]
public class NPC : CharacterBase
{
    public string Personality { get; set; } = "";
    public Dictionary<string, string> Schedule { get; set; } = new();
    public string DefaultSceneId { get; set; } = "";
    public bool IsHiddenPower { get; set; }
    public new bool IsAlive { get; set; } = true;
    public bool IsInCombat { get; set; }
    /// <summary>是否为隐藏角色 (山贼/副本敌人,不出现在场景人物列表)</summary>
    public bool IsHidden { get; set; }
    public int BuddhistValue { get; set; } = 0;    // 佛法值
    public string? GiftPreference { get; set; }      // 送礼偏好标签
    public bool IsSectLeader { get; set; }           // 是否为掌门（用于序列化）
    /// <summary>是否为本门派传武护法（非掌门也能教武功）</summary>
    public bool IsTrainer { get; set; }

    /// <summary>
    /// NPC角色类型：wine_merchant/weapon_merchant/medicine_merchant/martial_instructor/craft_teacher
    /// null = 普通NPC
    /// </summary>
    public string? NpcRole { get; set; }

    /// <summary>
    /// 商贩固定商品ID列表
    /// </summary>
    public List<string> ShopFixedItems { get; set; } = new();

    /// <summary>
    /// 商贩随机商品池ID列表
    /// </summary>
    public List<string> ShopRandomItems { get; set; } = new();

    /// <summary>
    /// 当前在售商品（运行时生成，存档中保留）
    /// </summary>
    public List<ShopItem> CurrentShopItems { get; set; } = new();

    /// <summary>
    /// 上次传授武功的游戏天数（-999=从未传授）
    /// </summary>
    public int LastTeachArtDay { get; set; } = -999;

    /// <summary>
    /// NPC拒绝对话直到该时辰（以总时辰数存储，0=不拒绝）
    /// </summary>
    public int DialogueRefuseUntilTotalShiChen { get; set; } = 0;

    /// <summary>
    /// 头像图片路径（相对于 assets/portraits/ 目录）
    /// </summary>
    public string? PortraitPath { get; set; }

    public virtual string GetGreeting(Player player)
    {
        var relation = GetRelation(player.Id);
        return relation.Type switch
        {
            RelationType.Enemy => $"{Name}冷冷地看着你，眼中充满敌意。",
            RelationType.Rival => $"{Name}打量着你，似乎在衡量你的实力。",
            RelationType.CloseFriend => $"{Name}见到你，露出欣喜的笑容。",
            RelationType.Friend => $"{Name}向你拱手致意。",
            RelationType.Master => $"{Name}微微颔首，示意你上前。",
            RelationType.Disciple => $"{Name}恭敬地向你行礼。",
            _ => $"{Name}站在那里，看了你一眼。"
        };
    }

    public virtual bool WillAcceptDialogue(Player player)
    {
        var relation = GetRelation(player.Id);
        if (relation.Type == RelationType.Enemy && Mood < 30) return false;
        return true;
    }

    public virtual bool WillAcceptCombat(Player player)
    {
        var relation = GetRelation(player.Id);
        if (IsHiddenPower && Random.Shared.NextDouble() > 0.3) return false;
        if (relation.Type == RelationType.Enemy) return true;
        if (relation.Favorability < -30) return true;
        return false;
    }

    public string GetCurrentSceneByTime(string timePeriod)
    {
        if (Schedule.TryGetValue(timePeriod, out var sceneId))
            return sceneId;
        return DefaultSceneId;
    }
}
