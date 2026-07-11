using AutoWuxia.Characters;
using AutoWuxia.MartialArts;
using AutoWuxia.World;

namespace AutoWuxia.Systems;

public static class MoodSystem
{
    public static void ChangeMood(CharacterBase character, int amount, string reason = "")
    {
        character.Mood = Math.Clamp(character.Mood + amount, 0, 100);
    }

    public static string GetMoodDescription(int mood)
    {
        return mood switch
        {
            >= 80 => "心情愉悦",
            >= 60 => "心情不错",
            >= 40 => "心情平静",
            >= 20 => "心情低落",
            _ => "心情极差"
        };
    }

    public static double GetMoodModifier(int mood)
    {
        return mood switch
        {
            >= 80 => 1.2,
            >= 60 => 1.1,
            >= 40 => 1.0,
            >= 20 => 0.9,
            _ => 0.8
        };
    }
}

public static class KarmaSystem
{
    public static void ChangeKarma(CharacterBase character, int amount, string reason = "")
    {
        character.Karma = Math.Clamp(character.Karma + amount, -100, 100);
    }

    public static string GetKarmaDescription(int karma)
    {
        return karma switch
        {
            >= 80 => "大侠",
            >= 50 => "正派人士",
            >= 20 => "普通人",
            >= -20 => "亦正亦邪",
            >= -50 => "邪派人物",
            _ => "恶名昭彰"
        };
    }
}

public static class StaminaSystem
{
    public const double TalkCost = 5;
    public const double SparCost = 15;
    public const double CombatCost = 25;
    public const double TravelBaseCost = 10;

    public static bool CanPerformAction(CharacterBase character, double cost)
    {
        return character.Stamina >= cost;
    }

    public static bool ConsumeStamina(CharacterBase character, double cost)
    {
        if (!CanPerformAction(character, cost)) return false;
        character.Stamina -= cost;
        return true;
    }

    public static void RecoverStamina(CharacterBase character, double hours)
    {
        character.Stamina = Math.Min(character.MaxStamina, character.Stamina + hours * 15);
    }

    public static double GetTravelCost(double distance)
    {
        return TravelBaseCost / 4 + distance * 0.5;
    }
}

public static class RelationshipSystem
{
    public static void Interact(CharacterBase a, CharacterBase b, int favorChange)
    {
        var relA = a.GetRelation(b.Id);
        var relB = b.GetRelation(a.Id);
        relA.ChangeFavorability(favorChange);
        relB.ChangeFavorability(favorChange);
    }

    public static void BecomeMasterDisciple(CharacterBase master, CharacterBase disciple)
    {
        var relMaster = master.GetRelation(disciple.Id);
        var relDisciple = disciple.GetRelation(master.Id);
        relMaster.Type = RelationType.Master;
        relDisciple.Type = RelationType.Disciple;
    }

    public static void BecomeSwornBrothers(CharacterBase a, CharacterBase b)
    {
        var relA = a.GetRelation(b.Id);
        var relB = b.GetRelation(a.Id);
        relA.Type = RelationType.SwornBrother;
        relB.Type = RelationType.SwornBrother;
        relA.Favorability = 80;
        relB.Favorability = 80;
    }

    public static void BecomeSpouses(CharacterBase a, CharacterBase b)
    {
        var relA = a.GetRelation(b.Id);
        var relB = b.GetRelation(a.Id);
        relA.Type = RelationType.Spouse;
        relB.Type = RelationType.Spouse;
        relA.Favorability = 100;
        relB.Favorability = 100;
    }
}

public class FactionSystem
{
    private readonly Dictionary<string, World.Faction> _factions;

    public FactionSystem(Dictionary<string, World.Faction> factions)
    {
        _factions = factions;
    }

    public bool TryJoinFaction(Player player, string factionId, out string message)
    {
        message = "";
        if (!_factions.TryGetValue(factionId, out var faction))
        {
            message = "该门派不存在。";
            return false;
        }
        if (player.FactionId != null)
        {
            message = "你已有门派，不可重复加入。";
            return false;
        }
        if (!faction.CanPlayerJoin(player.Karma))
        {
            message = $"{faction.Name}不收你这样善恶值的人。";
            return false;
        }
        player.FactionId = factionId;
        message = $"恭喜！你已加入{faction.Name}！";
        return true;
    }

    public void LeaveFaction(Player player)
    {
        player.FactionId = null;
    }

    public World.Faction? GetFaction(string factionId)
    {
        _factions.TryGetValue(factionId, out var faction);
        return faction;
    }

    public int GetFactionRelationModifier(string? factionId1, string? factionId2)
    {
        if (factionId1 == null || factionId2 == null) return 0;
        if (!_factions.TryGetValue(factionId1, out var faction)) return 0;
        return faction.GetRelationModifier(factionId2);
    }
}

public static class FactionTrainingSystem
{
    /// <summary>
    /// 检查是否可以学习指定武功
    /// </summary>
    public static bool CanLearnArt(Faction faction, CharacterBase player, string artId)
    {
        var learnable = faction.GetLearnableArts(player);
        return learnable.Contains(artId);
    }

    /// <summary>
    /// 获取门派学习进度表
    /// </summary>
    public static List<TrainingProgress> GetTrainingProgress(CharacterBase player, Faction faction)
    {
        var result = new List<TrainingProgress>();
        for (int i = 0; i < faction.AvailableArts.Count; i++)
        {
            var artId = faction.AvailableArts[i];
            var learnedArt = player.LearnedArts.FirstOrDefault(a => a.Id == artId);

            bool isUnlocked = i == 0 ||
                (result.Count > 0 && result[i - 1].ProficiencyLevel >= 8);

            result.Add(new TrainingProgress
            {
                ArtId = artId,
                ArtName = artId, // Will be replaced by actual name in UI
                Order = i + 1,
                IsLearned = learnedArt != null,
                ProficiencyLevel = learnedArt?.Level ?? 0,
                IsUnlocked = isUnlocked,
                RequiredProficiency = 8
            });
        }
        return result;
    }
}

public class TrainingProgress
{
    public string ArtId { get; set; } = "";
    public string ArtName { get; set; } = "";
    public int Order { get; set; }
    public bool IsLearned { get; set; }
    public int ProficiencyLevel { get; set; }
    public bool IsUnlocked { get; set; }
    public int RequiredProficiency { get; set; } = 8;

    public string GetStatusText()
    {
        if (!IsLearned && !IsUnlocked)
            return $"🔒 需要前置武功{RequiredProficiency}层熟练度";
        if (!IsLearned && IsUnlocked)
            return "✓ 可学习";
        if (ProficiencyLevel >= RequiredProficiency)
            return $"✓ 熟练度{ProficiencyLevel}层 - 已掌握";
        return $"熟练度{ProficiencyLevel}/{RequiredProficiency}层";
    }
}
