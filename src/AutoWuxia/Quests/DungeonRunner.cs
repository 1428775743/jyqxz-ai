using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Combat;
using AutoWuxia.Config;
using AutoWuxia.Core;
using AutoWuxia.World;

namespace AutoWuxia.Quests;

public enum DungeonOutcome
{
    Running,
    Victory,
    Defeat
}

public class DungeonBattleSnapshot
{
    public int RoundIndex { get; set; }
    public int RoundCount { get; set; }
    public int OpponentIndex { get; set; }
    public int OpponentTotalInRound { get; set; }
    public NPC? Opponent { get; set; }
    public bool TriggerDialogue { get; set; }
}

/// <summary>
/// 副本运行时调度器
/// 多轮战斗 → 每场胜利触发战后 AI 对话 → 全胜结算奖励 / 失败应用处罚
/// </summary>
public class DungeonRunner
{
    public Dungeon Dungeon { get; }
    public Player Player { get; }
    public DungeonOutcome Outcome { get; private set; } = DungeonOutcome.Running;
    public CombatEngine? CurrentEngine { get; private set; }
    public NPC? CurrentOpponent { get; private set; }
    public int RoundIndex { get; private set; } = -1;          // 0-based, -1 表示未开始
    public int OpponentIndexInRound { get; private set; } = -1;

    /// <summary>是否为华山论剑终章副本(影响 UI 收尾与是否进入结束画面)。</summary>
    public bool IsHuashanLunjian { get; set; }

    /// <summary>扁平对手列表(华山论剑用):非空时 Start() 以此作为单轮 10 人,忽略配置 Rounds。</summary>
    public List<NPC>? FlatOpponents { get; set; }

    /// <summary>本副本中已被玩家击败的对手(华山论剑结束画面总结用)。</summary>
    public IReadOnlyList<NPC> DefeatedOpponents => _defeatedOpponents;

    private readonly ConfigManager _config;
    private readonly AIService _ai;
    private readonly GameTime _gameTime;
    private readonly Scene? _scene;
    private readonly List<List<NPC>> _opponentsByRound = new();
    private readonly List<NPC> _defeatedOpponents = new();
    private bool _started;

    public int TotalRounds => Dungeon.Rounds.Count;

    public DungeonRunner(Dungeon dungeon, Player player, ConfigManager config,
        AIService ai, GameTime gameTime, Scene? scene)
    {
        Dungeon = dungeon;
        Player = player;
        _config = config;
        _ai = ai;
        _gameTime = gameTime;
        _scene = scene;
    }

    /// <summary>预先把所有轮的对手 NPC 实例 roll 出来</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _opponentsByRound.Clear();

        // 华山论剑:扁平 10 人列表作为单轮(顺序由 HuashanLunjianBuilder 已排好:弱→强)
        if (FlatOpponents != null)
        {
            _opponentsByRound.Add(new List<NPC>(FlatOpponents));
        }
        else
        {
            foreach (var round in Dungeon.Rounds)
            {
                var opponents = new List<NPC>();
                foreach (var charId in round.RollOpponents())
                {
                    try
                    {
                        var npc = _config.CreateNPC(charId);
                        npc.IsHidden = true;          // 临时副本对手不污染场景
                        opponents.Add(npc);
                    }
                    catch
                    {
                        // 跳过无效 ID
                    }
                }
                _opponentsByRound.Add(opponents);
            }
        }
        EventSystem.Instance.Publish(GameEvents.DungeonStarted,
            new Dictionary<string, object?> { { "dungeonId", Dungeon.Id } });
    }

    /// <summary>开启下一场战斗,返回 CombatEngine。null 表示副本结束</summary>
    public CombatEngine? StartNextBattle()
    {
        if (Outcome != DungeonOutcome.Running) return null;

        // 推进索引
        OpponentIndexInRound++;
        if (RoundIndex < 0 || OpponentIndexInRound >= _opponentsByRound[RoundIndex].Count)
        {
            RoundIndex++;
            OpponentIndexInRound = 0;
        }

        if (RoundIndex >= _opponentsByRound.Count)
        {
            Outcome = DungeonOutcome.Victory;
            return null;
        }

        var roundList = _opponentsByRound[RoundIndex];
        if (roundList.Count == 0)
        {
            // 跳过空轮
            return StartNextBattle();
        }

        CurrentOpponent = roundList[OpponentIndexInRound];
        // 副本不算切磋(可杀 NPC,不需要隐藏实力机制干扰)
        CurrentEngine = new CombatEngine(Player, CurrentOpponent, isSpar: false);
        return CurrentEngine;
    }

    /// <summary>当前对战结束后调用 (UI 在 CombatEngine.IsCombatOver 后调)</summary>
    public void OnCurrentBattleEnded()
    {
        if (CurrentEngine == null) return;

        var result = CurrentEngine.Result.Outcome;
        if (result == CombatOutcome.PlayerWin)
        {
            // 玩家胜:记录败将(华山论剑结束画面总结用),继续打下一个
            if (CurrentOpponent != null) _defeatedOpponents.Add(CurrentOpponent);
        }
        else
        {
            // 玩家负/逃/降 都视为副本失败
            Outcome = DungeonOutcome.Defeat;
        }
    }

    public DungeonRound? GetCurrentRoundConfig()
    {
        if (RoundIndex < 0 || RoundIndex >= Dungeon.Rounds.Count) return null;
        return Dungeon.Rounds[RoundIndex];
    }

    public DungeonBattleSnapshot GetSnapshot()
    {
        return new DungeonBattleSnapshot
        {
            RoundIndex = RoundIndex,
            RoundCount = TotalRounds,
            OpponentIndex = OpponentIndexInRound,
            OpponentTotalInRound = RoundIndex >= 0 && RoundIndex < _opponentsByRound.Count
                ? _opponentsByRound[RoundIndex].Count : 0,
            Opponent = CurrentOpponent,
            // 华山论剑每场胜后都让败将说一句
            TriggerDialogue = IsHuashanLunjian || (GetCurrentRoundConfig()?.TriggerDialogue ?? false)
        };
    }

    /// <summary>
    /// 玩家刚击败 CurrentOpponent 后异步获取战后对话.
    /// 利用 BuildDialoguePrompt 复用已有对话 prompt 模板。
    /// </summary>
    public async Task<string?> GetPostBattleDialogueAsync()
    {
        var npc = CurrentOpponent;
        if (npc == null) return null;
        var roundCfg = GetCurrentRoundConfig();
        // 华山论剑无 Rounds 配置,默认每场触发战后对话
        if (!IsHuashanLunjian && (roundCfg == null || !roundCfg.TriggerDialogue)) return null;

        // 复用对话 prompt: 模拟玩家"刚刚战胜"语境,让 NPC 自然反应
        var systemPrompt = AIPromptBuilder.BuildNPCIdentityPrompt(npc);
        var userPrompt = AIPromptBuilder.BuildDialoguePrompt(npc, Player,
            $"(刚刚在【{Dungeon.Name}】中败于你手)", new DialogueHistory(), _gameTime, _scene);
        var raw = await _ai.ChatAsync(systemPrompt, userPrompt);
        if (string.IsNullOrEmpty(raw) || raw.StartsWith("[")) return null;

        // 试图提取 dialogue 字段;失败则把整段当对话
        try
        {
            int s = raw.IndexOf('{');
            int e = raw.LastIndexOf('}');
            if (s >= 0 && e > s)
            {
                var json = raw.Substring(s, e - s + 1);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("dialogue", out var d))
                    return d.GetString();
            }
        }
        catch { }
        return raw;
    }

    /// <summary>全胜后将副本奖励应用到玩家</summary>
    public string ApplyVictoryReward()
    {
        var r = Dungeon.Reward;
        var parts = new List<string>();
        if (r.Gold != 0) { Player.Gold += r.Gold; parts.Add($"银两+{r.Gold}"); }
        if (r.Reputation != 0) { Player.AddReputation(r.Reputation); parts.Add($"声望+{r.Reputation}"); }
        if (r.FactionContribution != 0 && !string.IsNullOrEmpty(r.FactionId))
        {
            Player.AddFactionContribution(r.FactionId, r.FactionContribution);
            parts.Add($"{r.FactionId}贡献+{r.FactionContribution}");
        }
        EventSystem.Instance.Publish(GameEvents.DungeonFinished,
            new Dictionary<string, object?>
            {
                { "dungeonId", Dungeon.Id },
                { "outcome", "victory" }
            });
        return parts.Count == 0 ? "" : string.Join(", ", parts);
    }

    /// <summary>
    /// 失败时按副本配置的处罚类型施加.
    /// 返回值: (是否需要 Game Over, 描述文本)
    /// </summary>
    public (bool gameOver, string message) ApplyDefeatPenalty()
    {
        var p = Dungeon.OnFail;
        EventSystem.Instance.Publish(GameEvents.DungeonFinished,
            new Dictionary<string, object?>
            {
                { "dungeonId", Dungeon.Id },
                { "outcome", "defeat" }
            });
        switch (p.Type)
        {
            case DungeonFailType.DeductGold:
                int actualGold = Math.Min(Player.Gold, Math.Max(0, p.Amount));
                Player.Gold -= actualGold;
                return (false, $"你倒在了【{Dungeon.Name}】之中,被劫去 {actualGold} 两银钱。");
            case DungeonFailType.DeductHP:
                Player.CurrentHP = 1;
                return (false, $"你倒在了【{Dungeon.Name}】之中,只余一口气。");
            case DungeonFailType.GameOver:
                return (true, "胜败乃兵家常事,英雄请重新来过。");
            default:
                return (false, "你失败了。");
        }
    }
}
