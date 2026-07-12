using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Quests;

namespace AutoWuxia.Systems;

/// <summary>作者君的首次现身、游历与奖励状态。</summary>
public static class AuthorJunSystem
{
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
                new DialogueLine { Speaker = EndgameSystem.AuthorId, Lines = { "在下作者君，写些江湖闲话糊口。可否借你的故事一用？", "放心，不会改得太离谱……大概。" } },
                new DialogueLine { Speaker = "玩家", Lines = { "江湖人的路，何须旁人落笔？" } },
                new DialogueLine { Speaker = EndgameSystem.AuthorId, Lines = { "正因不须，才最有趣。等你真的走到尽头，我们再好好谈一谈。" } },
                new DialogueLine { Speaker = "旁白", Lines = { "作者君收起纸笔，衣袖一拂，竟已不知要往何处去。" } }
            }
        };
    }
}
