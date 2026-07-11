using AutoWuxia.Characters;
using AutoWuxia.Core;

namespace AutoWuxia.Combat;

public enum CombatActionType
{
    Attack,
    Defend,
    UseInternalArt,
    UseExternalArt,
    UseItem,
    Flee,
    Surrender
}

public class CombatAction
{
    public CombatActionType Type { get; set; }
    public CharacterBase Actor { get; set; } = null!;
    public CharacterBase? Target { get; set; }
    public string? ArtId { get; set; }
    public string Description { get; set; } = "";
}
