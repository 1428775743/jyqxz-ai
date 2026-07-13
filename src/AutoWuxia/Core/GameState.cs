using System.Text.Json;
using System.Text.Json.Serialization;
using AutoWuxia.Characters;
using AutoWuxia.Config.Models;
using AutoWuxia.Items;
using AutoWuxia.MartialArts;
using AutoWuxia.Quests;
using AutoWuxia.World;

namespace AutoWuxia.Core;

public class GameState
{
    public Player Player { get; set; } = new();
    public Dictionary<string, NPC> AllNPCs { get; set; } = new();
    public Dictionary<string, Scene> AllScenes { get; set; } = new();
    public Dictionary<string, Faction> AllFactions { get; set; } = new();

    /// <summary>当前存档版本号。每次有破坏性存档改动(新增NPC/场景/门派、改数据结构)时 +1,并在 GameEngine.MigrateSave 加对应迁移块。</summary>
    public const int CurrentSaveVersion = 6;

    /// <summary>存档创建时的版本号。旧档无此字段,反序列化为 0,加载时由 GameEngine.MigrateSave 逐步迁移到 CurrentSaveVersion。</summary>
    public int SaveVersion { get; set; } = 0;

    public GameTime GameTime { get; set; } = new();
    public string CurrentSceneId { get; set; } = "";
    public int LastMonthlyUpdateDay { get; set; } = 0;

    /// <summary>旧版年度AI更新时间；仅用于旧存档迁移到季度剧情系统。</summary>
    public int LastAnnualUpdateDay { get; set; } = 0;

    /// <summary>上次季度剧情 Agent 更新的天数；旧存档会从 LastAnnualUpdateDay 迁移。</summary>
    public int LastQuarterlyUpdateDay { get; set; } = 0;

    /// <summary>运行时生成的剧情任务池（季度 Agent 生成，玩家可通过对应 NPC 接取）。随存档序列化。</summary>
    public List<QuestConfig> RuntimeQuests { get; set; } = new();

    /// <summary>是否已经触发过华山论剑邀请 (避免重复弹窗)</summary>
    public bool HuashanInvited { get; set; } = false;

    /// <summary>华山论剑是否已通关(终章完成标记,胜后置 true)。</summary>
    public bool HuashanCompleted { get; set; } = false;

    /// <summary>声望达到八千后是否已收到武林大会请柬。</summary>
    public bool WulinConferenceInvited { get; set; } = false;

    /// <summary>是否已与刘家村的作者君正式相识。</summary>
    public bool AuthorIntroduced { get; set; } = false;

    /// <summary>与作者君切磋获胜次数，用于触发神话武学奖励。</summary>
    public int AuthorSparWins { get; set; } = 0;

    /// <summary>作者君的神话武学奖励是否已经领取。</summary>
    public bool AuthorMythicRewardClaimed { get; set; } = false;

    /// <summary>本存档已达成的结局 ID。</summary>
    public List<string> CompletedEndings { get; set; } = new();

    /// <summary>玩家与各NPC的对话历史(Key=NPCId),随存档序列化。</summary>
    public Dictionary<string, DialogueHistory> DialogueHistories { get; set; } = new();

    /// <summary>门派可领取任务池快照(Key=factionId,含月度生成的收集任务),随存档序列化。</summary>
    public Dictionary<string, List<FactionQuest>> FactionQuestPool { get; set; } = new();

    /// <summary>
    /// 将门派ID解析为中文名称
    /// </summary>
    public string GetFactionName(string? factionId, string fallback = "无门无派")
    {
        if (string.IsNullOrEmpty(factionId)) return fallback;
        return AllFactions.TryGetValue(factionId, out var f) ? f.Name : factionId;
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public void Save(string filePath)
    {
        var options = GetJsonOptions();
        try
        {
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            GameLogger.Error($"存档保存失败: {filePath}", ex);
            throw;
        }
    }

    public static GameState? Load(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var json = File.ReadAllText(filePath);
        try
        {
            return JsonSerializer.Deserialize<GameState>(json, GetJsonOptions());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"存档加载失败: {ex.Message}");
            return null;
        }
    }
}
