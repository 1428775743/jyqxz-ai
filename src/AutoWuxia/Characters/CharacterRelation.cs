namespace AutoWuxia.Characters;

public enum RelationType
{
    Stranger,
    Acquaintance,
    Friend,
    CloseFriend,
    Master,
    Disciple,
    SwornBrother,
    Spouse,
    Enemy,
    Rival
}

public class CharacterRelation
{
    public string TargetId { get; set; } = "";
    public RelationType Type { get; set; } = RelationType.Stranger;
    public int Favorability { get; set; } = 0;
    public string? FactionId { get; set; }

    public void ChangeFavorability(int amount)
    {
        Favorability = Math.Clamp(Favorability + amount, -100, 100);
        UpdateRelationType();
    }

    private void UpdateRelationType()
    {
        if (Type is RelationType.Master or RelationType.Disciple or
            RelationType.SwornBrother or RelationType.Spouse)
            return;

        if (Favorability >= 80) Type = RelationType.CloseFriend;
        else if (Favorability >= 50) Type = RelationType.Friend;
        else if (Favorability >= 20) Type = RelationType.Acquaintance;
        else if (Favorability <= -50) Type = RelationType.Enemy;
        else if (Favorability <= -20) Type = RelationType.Rival;
        else Type = RelationType.Stranger;
    }

    public string GetRelationDescription()
    {
        return Type switch
        {
            RelationType.Stranger => "素不相识",
            RelationType.Acquaintance => "点头之交",
            RelationType.Friend => "朋友",
            RelationType.CloseFriend => "至交好友",
            RelationType.Master => "师父",
            RelationType.Disciple => "徒弟",
            RelationType.SwornBrother => "结拜兄弟",
            RelationType.Spouse => "夫妻",
            RelationType.Enemy => "仇敌",
            RelationType.Rival => "对手",
            _ => "未知"
        };
    }

    public int GetFactionModifier(string? targetFactionId)
    {
        if (FactionId == null || targetFactionId == null) return 0;
        return FactionId == targetFactionId ? 10 : 0;
    }
}
