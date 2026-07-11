namespace AutoWuxia.Characters;

public enum NPCActionType
{
    Idle,
    Talk,
    Challenge,
    RefuseDialogue,
    Attack,
    Trade,
    Teach
}

public class NPCDecision
{
    public NPCActionType Action { get; set; }
    public string Reason { get; set; } = "";
    public string? DialogueContent { get; set; }
}

public class CharacterAI
{
    public NPCDecision DecideAction(NPC npc, Player player)
    {
        var relation = npc.GetRelation(player.Id);
        int combatPower = npc.GetTotalAttack() + npc.GetTotalDefense();
        int playerPower = player.GetTotalAttack() + player.GetTotalDefense();

        if (relation.Type == RelationType.Enemy && relation.Favorability < -60)
        {
            if (combatPower > playerPower * 0.8)
                return new NPCDecision { Action = NPCActionType.Attack, Reason = "仇敌相见，分外眼红" };
            return new NPCDecision { Action = NPCActionType.RefuseDialogue, Reason = "实力不足，暂避锋芒" };
        }

        if (relation.Type == RelationType.Rival)
        {
            if (npc.IsHiddenPower)
                return new NPCDecision { Action = NPCActionType.Idle, Reason = "隐藏实力" };
            return new NPCDecision { Action = NPCActionType.Challenge, Reason = "想要切磋" };
        }

        if (relation.Favorability > 30)
        {
            return new NPCDecision
            {
                Action = NPCActionType.Talk,
                Reason = "友好关系",
                DialogueContent = GenerateFriendlyDialogue(npc)
            };
        }

        if (npc.IsHiddenPower && playerPower > combatPower * 1.5)
        {
            return new NPCDecision { Action = NPCActionType.RefuseDialogue, Reason = "隐藏实力，不愿交手" };
        }

        return new NPCDecision { Action = NPCActionType.Idle, Reason = "无事发生" };
    }

    private string GenerateFriendlyDialogue(NPC npc)
    {
        string[] greetings =
        [
            $"{npc.Name}说道：\"最近江湖上可不太平，你可要小心。\"",
            $"{npc.Name}说道：\"听说附近有不少高手出没，切磋时记得留个心眼。\"",
            $"{npc.Name}说道：\"兄弟，好久不见，近来可好？\"",
            $"{npc.Name}笑道：\"今日天气不错，不如一同走走？\"",
        ];
        return greetings[Random.Shared.Next(greetings.Length)];
    }
}
