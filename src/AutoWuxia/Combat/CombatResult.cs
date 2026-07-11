using AutoWuxia.Characters;

namespace AutoWuxia.Combat;

public enum CombatOutcome
{
    InProgress,
    PlayerWin,
    NPCWin,
    Fled,
    Draw,
    Surrendered
}

public class CombatResult
{
    public CombatOutcome Outcome { get; set; } = CombatOutcome.InProgress;
    public int TotalRounds { get; set; }
    public int PlayerDamageDealt { get; set; }
    public int PlayerDamageReceived { get; set; }
    public bool IsSpar { get; set; }
    public List<string> CombatLog { get; set; } = new();

    public void AddLog(string message)
    {
        CombatLog.Add(message);
    }
}
