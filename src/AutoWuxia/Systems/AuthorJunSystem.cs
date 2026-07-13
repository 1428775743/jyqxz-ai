using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Quests;

namespace AutoWuxia.Systems;

/// <summary>作者君的首次现身、游历与奖励状态。</summary>
public static class AuthorJunSystem
{
    public const int MythicRewardWinRequirement = 10;

    public static bool Relocate(GameState state, bool keepLiujiacun = false)
    {
        if (!state.AllNPCs.TryGetValue(EndgameSystem.AuthorId, out var author) || !author.IsAlive)
            return false;

        var scenes = state.AllScenes.Values
            .Where(scene => !scene.IsSpecial && scene.ConnectedSceneIds.Count > 0)
            .Select(scene => scene.Id)
            .Where(id => keepLiujiacun || id != "liujiacun_scene")
            .Distinct()
            .ToList();
        if (scenes.Count == 0) return false;

        var destination = scenes[Random.Shared.Next(scenes.Count)];
        author.DefaultSceneId = destination;
        foreach (var key in author.Schedule.Keys.ToList())
            author.Schedule[key] = destination;
        author.AddLifeEvent(state.GameTime.Day, LifeEventType.Monthly, "又踏上行囊，往别处寻一段江湖故事。");
        return true;
    }

    public static DialogueScript BuildIntroScript()
    {
        return new DialogueScript
        {
            Lines =
            {
                new DialogueLine { Speaker = "旁白", Lines = { "刘家村的晒谷场旁，一名青年伏在石桌上写写画画。你走近时，他忽然抬头，像早已等候多时。" } },
                new DialogueLine { Speaker = EndgameSystem.AuthorId, Lines = { "在下作者君。说得直白些，这个游戏与这片江湖，正是我落笔写成的。", "不过世界是我写的，你要走哪条路，却得由你自己决定。" } },
                new DialogueLine { Speaker = "玩家", Lines = { "江湖人的路，何须旁人落笔？" } },
                new DialogueLine { Speaker = EndgameSystem.AuthorId, Lines = { "说得好。武功我不会直接传你，机缘还得自己去江湖里寻。", "若能在切磋中累计胜我十次，我倒可以送你一份不一样的奖励。" } },
                new DialogueLine { Speaker = "旁白", Lines = { "作者君收起纸笔，衣袖一拂，竟已不知要往何处去。" } }
            }
        };
    }
}
