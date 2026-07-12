using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Quests;

namespace AutoWuxia.Systems;

public enum EndingType
{
    HuashanChampion,
    TrueHuashan,
    WulinLeader,
    WealthyRetirement,
    SecludedRetirement,
    AllMainStories
}

public sealed record EndingDefinition(string Id, string Title, string Subtitle, string AchievementId);

/// <summary>
/// 终局条件的唯一入口。所有结局都写入存档，同时同步到首页的全局成就册。
/// </summary>
public static class EndgameSystem
{
    public const string AuthorId = "author_jun";

    private static readonly IReadOnlyDictionary<EndingType, EndingDefinition> Definitions =
        new Dictionary<EndingType, EndingDefinition>
        {
            [EndingType.HuashanChampion] = new("huashan_champion", "江湖终章", "华山之巅 · 力挫群雄 · 天下第一", "ending_huashan_champion"),
            [EndingType.TrueHuashan] = new("true_huashan", "真·华山论剑", "四绝问道 · 武学尽头 · 此心无挂", "ending_true_huashan"),
            [EndingType.WulinLeader] = new("wulin_leader", "武林盟主", "群贤推举 · 执掌盟约 · 侠义为先", "ending_wulin_leader"),
            [EndingType.WealthyRetirement] = new("wealthy_retirement", "富甲一方", "功名如寄 · 千金归隐 · 笑看江湖", "ending_wealthy_retirement"),
            [EndingType.SecludedRetirement] = new("secluded_retirement", "归隐江湖", "携侣归山 · 尘缘已了 · 此生安宁", "ending_secluded_retirement"),
            [EndingType.AllMainStories] = new("all_main_stories", "飞雪连天射白鹿，笑书神侠倚碧鸳", "群书既毕 · 江湖圆满 · 侠名永传", "ending_all_main_stories")
        };

    // 每部作品以一条最终主线为准；存在善恶分歧时，任一分支完成即可。
    private static readonly string[][] MainStoryCompletionGroups =
    {
        new[] { "tutorial_liujiacun" },
        new[] { "bixue_jinshe" },
        new[] { "fuwei_revenge" },
        new[] { "gumu_story" },
        new[] { "liancheng_final" },
        new[] { "luding_shenlong", "luding_evil" },
        new[] { "shediao_main" },
        new[] { "shendiao_yangguo", "shendiao_evil" },
        new[] { "tianlong_shaoshi", "tianlong_shaoshi_evil" },
        new[] { "xiake_island" },
        new[] { "xiaojiao_challenge_dongfang", "riyue_dongfang_test", "xiaojiao_evil" },
        new[] { "yitian_tushi", "yitian_evil" }
    };

    public static EndingDefinition GetDefinition(EndingType type) => Definitions[type];

    public static bool CanChallengeTrueHuashan(GameState state)
    {
        if (!state.AllNPCs.TryGetValue(AuthorId, out var author) || !author.IsAlive)
            return false;
        return author.GetRelation(state.Player.Id).Favorability >= 100;
    }

    public static bool CanRetireWealthily(Player player)
        => (HasFinishedQuest(player, "luding_shenlong") || HasFinishedQuest(player, "luding_evil"))
           && player.Gold >= 100_000;

    public static bool CanRetireWithSpouse(GameState state)
        => state.Player.Reputation >= 5_000 && HasSpouse(state);

    public static bool CanUnlockAllMainStoriesEnding(Player player)
        => MainStoryCompletionGroups.All(group => group.Any(id => HasFinishedQuest(player, id)));

    public static bool HasSpouse(GameState state)
    {
        if (state.Player.Relations.Values.Any(r => r.Type == RelationType.Spouse))
            return true;
        return state.AllNPCs.Values.Any(npc =>
            npc.Relations.TryGetValue(state.Player.Id, out var relation) && relation.Type == RelationType.Spouse);
    }

    public static bool HasFinishedQuest(Player player, string questId)
        => player.QuestLog.Any(q => q.Id == questId && q.Status is QuestStatus.Completed or QuestStatus.Rewarded);

    public static bool IsEndingCompleted(GameState state, EndingType type)
        => state.CompletedEndings.Contains(GetDefinition(type).Id);

    public static void CompleteEnding(GameState state, EndingType type)
    {
        var definition = GetDefinition(type);
        if (!state.CompletedEndings.Contains(definition.Id))
            state.CompletedEndings.Add(definition.Id);
        if (!string.IsNullOrEmpty(definition.AchievementId))
            AchievementService.Unlock(definition.AchievementId);
        state.Player.AddLifeEvent(state.GameTime.Day, LifeEventType.Major, $"达成结局：{definition.Title}");
    }
}
