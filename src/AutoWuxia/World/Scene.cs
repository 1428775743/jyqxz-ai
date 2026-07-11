using AutoWuxia.Characters;
using AutoWuxia.Config.Models;

namespace AutoWuxia.World;

public class Scene
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Region { get; set; } = "";
    public string? BackgroundImagePath { get; set; }
    public List<string> ConnectedSceneIds { get; set; } = new();
    public string? FactionId { get; set; }
    public bool IsSpecial { get; set; }
    public List<NPC> PresentNPCs { get; set; } = new();
    public List<SceneCraftLesson> CraftLessons { get; set; } = new();
    public List<MartialLesson> MartialLessons { get; set; } = new();
    public MineConfig? Mine { get; set; }
    public HuntConfig? Hunt { get; set; }
    public HerbGardenConfig? HerbGarden { get; set; }

    public void UpdateNPCsByTime(string timePeriod, Dictionary<string, NPC> allNPCs)
    {
        PresentNPCs.Clear();
        foreach (var npc in allNPCs.Values)
        {
            if (!npc.IsAlive) continue;
            var targetScene = npc.GetCurrentSceneByTime(timePeriod);
            if (targetScene == Id)
                PresentNPCs.Add(npc);
        }
    }

    public string GetSceneDescription()
    {
        var npcList = PresentNPCs.Count > 0
            ? $"此处有：{string.Join("、", PresentNPCs.Select(n => n.Name))}"
            : "此处空无一人。";
        return $"【{Name}】\n{Description}\n{npcList}";
    }

    public static Scene FromConfig(SceneConfig config)
    {
        return new Scene
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            Region = config.Region,
            BackgroundImagePath = config.BackgroundImagePath,
            ConnectedSceneIds = config.ConnectedScenes,
            FactionId = config.FactionId,
            IsSpecial = config.IsSpecial,
            CraftLessons = config.CraftLessons,
            MartialLessons = config.MartialLessons,
            Mine = config.Mine,
            Hunt = config.Hunt,
            HerbGarden = config.HerbGarden
        };
    }
}
