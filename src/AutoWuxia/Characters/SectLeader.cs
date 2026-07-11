namespace AutoWuxia.Characters;

public class SectLeader : NPC
{
    public bool CanAcceptDisciple(Player player)
    {
        if (player.FactionId != null) return false;
        if (player.Karma < 30) return false;
        var relation = GetRelation(player.Id);
        return relation.Favorability >= 0;
    }

    public string GetRecruitmentResponse(Player player)
    {
        if (player.FactionId != null)
            return $"{Name}摇了摇头：\"你已有门派，不可再入我门。\"";
        if (player.Karma < 30)
            return $"{Name}皱眉道：\"你身上煞气太重，不适合入我门派。\"";
        return $"{Name}抚须微笑：\"好，从今日起，你便是我门下弟子了。\"";
    }

    public override string GetGreeting(Player player)
    {
        if (player.FactionId == FactionId)
            return $"{Name}看着门下的你，微微点头：\"回来了。\"";
        return base.GetGreeting(player);
    }
}
