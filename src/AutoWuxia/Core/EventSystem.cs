namespace AutoWuxia.Core;

public class GameEventArgs : EventArgs
{
    public string EventType { get; }
    public Dictionary<string, object?> Data { get; }

    public GameEventArgs(string eventType, Dictionary<string, object?>? data = null)
    {
        EventType = eventType;
        Data = data ?? new Dictionary<string, object?>();
    }
}

public class EventSystem
{
    private static readonly Lazy<EventSystem> _instance = new(() => new EventSystem());
    public static EventSystem Instance => _instance.Value;

    private readonly Dictionary<string, List<EventHandler<GameEventArgs>>> _handlers = new();

    public void Subscribe(string eventType, EventHandler<GameEventArgs> handler)
    {
        if (!_handlers.ContainsKey(eventType))
            _handlers[eventType] = new List<EventHandler<GameEventArgs>>();
        _handlers[eventType].Add(handler);
    }

    public void Unsubscribe(string eventType, EventHandler<GameEventArgs> handler)
    {
        if (_handlers.ContainsKey(eventType))
            _handlers[eventType].Remove(handler);
    }

    public void Publish(string eventType, Dictionary<string, object?>? data = null)
    {
        var args = new GameEventArgs(eventType, data);
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.ToList())
                handler(this, args);
        }
    }

    public void Clear() => _handlers.Clear();
}

public static class GameEvents
{
    public const string TimeAdvanced = "time.advanced";
    public const string NewDay = "time.newday";
    public const string SceneChanged = "scene.changed";
    public const string CombatStarted = "combat.started";
    public const string CombatEnded = "combat.ended";
    public const string DialogStarted = "dialog.started";
    public const string DialogEnded = "dialog.ended";
    public const string CharacterHPChanged = "character.hp.changed";
    public const string CharacterDied = "character.died";
    public const string QuestStarted = "quest.started";
    public const string QuestCompleted = "quest.completed";
    public const string QuestSubmitted = "quest.submitted";
    public const string QuestRewarded = "quest.rewarded";
    public const string DungeonStarted = "dungeon.started";
    public const string DungeonFinished = "dungeon.finished";
    public const string ReputationChanged = "player.reputation.changed";
    public const string FactionContributionChanged = "player.faction_contribution.changed";
    public const string FactionJoined = "faction.joined";
    public const string MartialArtLearned = "martialart.learned";
    public const string LogMessage = "ui.log";
    public const string PlayerAction = "player.action";
    public const string NPCAction = "npc.action";
    public const string HealthChanged = "player.health.changed";
    public const string TagChanged = "player.tag.changed";
    public const string PlayerDied = "player.died";
}
