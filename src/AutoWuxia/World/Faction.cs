using AutoWuxia.Characters;
using AutoWuxia.Config.Models;

namespace AutoWuxia.World;

public class Faction
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LeaderId { get; set; } = "";
    public string SceneId { get; set; } = "";
    public List<string> AvailableArts { get; set; } = new();
    public int JoinKarmaMin { get; set; } = -100;
    public int JoinKarmaMax { get; set; } = 100;
    public Dictionary<string, string> AlliedFactions { get; set; } = new();
    public Dictionary<string, string> EnemyFactions { get; set; } = new();

    public bool CanPlayerJoin(int playerKarma)
    {
        return playerKarma >= JoinKarmaMin && playerKarma <= JoinKarmaMax;
    }

    public int GetRelationModifier(string? otherFactionId)
    {
        if (otherFactionId == null || otherFactionId == Id) return 10;
        if (AlliedFactions.ContainsKey(otherFactionId)) return 5;
        if (EnemyFactions.ContainsKey(otherFactionId)) return -10;
        return 0;
    }

    public static Faction FromConfig(FactionConfig config)
    {
        return new Faction
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            LeaderId = config.LeaderId,
            SceneId = config.SceneId,
            AvailableArts = config.AvailableArts,
            JoinKarmaMin = config.JoinKarmaMin,
            JoinKarmaMax = config.JoinKarmaMax,
            AlliedFactions = config.AlliedFactions,
            EnemyFactions = config.EnemyFactions
        };
    }

    /// <summary>
    /// 获取当前可学习的武功列表（根据前置武功熟练度>=8）
    /// </summary>
    public List<string> GetLearnableArts(CharacterBase player)
    {
        var learnable = new List<string>();
        for (int i = 0; i < AvailableArts.Count; i++)
        {
            var artId = AvailableArts[i];
            if (i == 0)
            {
                // 第一个武功始终可学
                if (!player.LearnedArts.Any(a => a.Id == artId))
                    learnable.Add(artId);
            }
            else
            {
                // 前一武功等级>=8才能学下一个
                var prevArtId = AvailableArts[i - 1];
                var prevArt = player.LearnedArts.FirstOrDefault(a => a.Id == prevArtId);
                if (prevArt != null && prevArt.Level >= 8)
                {
                    if (!player.LearnedArts.Any(a => a.Id == artId))
                        learnable.Add(artId);
                }
            }
        }
        return learnable;
    }
}
