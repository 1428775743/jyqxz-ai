using System.Text.Json;
using AutoWuxia.Core;

namespace AutoWuxia.Systems;

public sealed record AchievementDefinition(string Id, string Name, string Description);

public sealed class AchievementProfile
{
    public HashSet<string> UnlockedIds { get; set; } = new();
}

/// <summary>跨存档的结局成就册，供首页浏览。</summary>
public static class AchievementService
{
    private static readonly IReadOnlyList<AchievementDefinition> Definitions = new[]
    {
        new AchievementDefinition("ending_huashan_champion", "华山论剑", "声望达到10000后登顶华山，连续战胜十位绝顶高手，登临天下第一。"),
        new AchievementDefinition("ending_true_huashan", "真·华山论剑", "与作者君好感达到100后，于对话中接受真正的华山之约；连战乔峰、东方不败、张三丰与作者君。"),
        new AchievementDefinition("ending_wealthy_retirement", "富甲一方", "完成鹿鼎记主线，持有十万两银子后向作者君申请归隐。"),
        new AchievementDefinition("ending_wulin_leader", "武林盟主", "声望达到8000后参加武林大会，战胜群雄并接受盟主之位。"),
        new AchievementDefinition("ending_secluded_retirement", "归隐江湖", "声望达到5000，拥有配偶后向作者君申请归隐。"),
        new AchievementDefinition("ending_all_main_stories", "飞雪连天射白鹿，笑书神侠倚碧鸳", "完成各部主线的最终篇章。点开详情可查看每条主线从谁开始。")
    };

    private static string ProfilePath => Path.Combine(AppPaths.UserDataDir, "achievements.json");

    public static IReadOnlyList<AchievementDefinition> GetDefinitions() => Definitions;

    public static AchievementProfile Load()
    {
        try
        {
            if (!File.Exists(ProfilePath)) return new AchievementProfile();
            return JsonSerializer.Deserialize<AchievementProfile>(File.ReadAllText(ProfilePath)) ?? new AchievementProfile();
        }
        catch
        {
            return new AchievementProfile();
        }
    }

    public static void Unlock(string achievementId)
    {
        var profile = Load();
        if (!profile.UnlockedIds.Add(achievementId)) return;
        try
        {
            Directory.CreateDirectory(AppPaths.UserDataDir);
            File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 成就册写入失败不影响当前存档和结局流程。
        }
    }
}
