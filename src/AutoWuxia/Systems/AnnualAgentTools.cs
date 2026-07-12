using System.Text.Json;
using AutoWuxia.AI;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;
using AutoWuxia.Core;

namespace AutoWuxia.Systems;

/// <summary>
/// 季度 Agent 的剧情编辑器工具集 —— 让 AI 像编辑器一样拼装/修改可完整游玩的长链式剧情任务。
/// 工作流: create_story_quest(建骨架) → add_quest_step(插步骤) → update/remove(打磨) → finalize(定稿入可接取池)。
/// 所有引用(NPC/场景/武功/物品)均经防幻觉校验,失败返回错误让AI重试。
/// 每个工具返回 JSON 含 narration 字段(玩家视角文案),供进度窗体显示。
/// </summary>
public class AnnualAgentTools
{
    private readonly GameState _state;
    private readonly ConfigManager _config;

    /// <summary>正在编辑中的草稿任务(Key=questId)。定稿后从草稿移除,加入 State.RuntimeQuests。</summary>
    private readonly Dictionary<string, QuestConfig> _drafts = new();

    /// <summary>本次季度生成已成功定稿的任务数；季度 Agent 必须恰好生成一条。</summary>
    public int FinalizedQuestCount { get; private set; }

    private static readonly HashSet<string> SupportedActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "talk", "fight", "spar", "kill", "spare", "go", "mine", "submit"
    };

    public AnnualAgentTools(GameState state, ConfigManager config)
    {
        _state = state;
        _config = config;
    }

    // ── 工具定义 ──

    public List<ToolDefinition> GetToolDefinitions()
    {
        return new List<ToolDefinition>
        {
            CreateStoryQuestTool(),
            AddQuestStepTool(),
            RemoveQuestStepTool(),
            UpdateQuestStepTool(),
            FinalizeStoryQuestTool(),
            SetQuestDialogueTool(),
            QueryWorldElementsTool(),
            ListDraftQuestsTool()
        };
    }

    public string ExecuteTool(string toolName, string argumentsJson)
    {
        try
        {
            return toolName switch
            {
                "create_story_quest" => ExecuteCreateStoryQuest(argumentsJson),
                "add_quest_step" => ExecuteAddQuestStep(argumentsJson),
                "remove_quest_step" => ExecuteRemoveQuestStep(argumentsJson),
                "update_quest_step" => ExecuteUpdateQuestStep(argumentsJson),
                "finalize_story_quest" => ExecuteFinalizeStoryQuest(argumentsJson),
                "set_quest_dialogue" => ExecuteSetQuestDialogue(argumentsJson),
                "query_world_elements" => ExecuteQueryWorldElements(argumentsJson),
                "list_draft_quests" => ExecuteListDraftQuests(argumentsJson),
                _ => "{\"error\": \"未知工具: " + toolName + "\"}"
            };
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[季度工具异常] {toolName}: {ex.Message}");
            return $"{{\"error\": \"工具执行失败: {ex.Message}\"}}";
        }
    }

    // ── 1. create_story_quest 建任务骨架 ──

    private ToolDefinition CreateStoryQuestTool()
    {
        return new ToolDefinition
        {
            Name = "create_story_quest",
            Description = "创建本季度唯一的一条剧情任务骨架（空步骤）。后续用 add_quest_step 逐步添加步骤。questId必须全局唯一且以quarter_开头，如quarter_taohua_mystery；创建时必须给出有效最终奖励。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    questId = new { type = "string", description = "任务唯一ID，必须以quarter_开头，如quarter_taohua_mystery" },
                    name = new { type = "string", description = "任务中文名,如 桃花岛之谜" },
                    description = new { type = "string", description = "任务总体描述(剧情背景)" },
                    triggerNpcId = new { type = "string", description = "触发该任务的NPC ID(必须现存,玩家与之对话可接取)" },
                    difficulty = new { type = "string", description = "难度 easy/normal/hard/legendary" },
                    minFavorability = new { type = "integer", description = "接取所需最低好感度（不可高于该NPC当前好感度），默认0" },
                    relatedNpcIds = new { type = "array", items = new { type = "string" }, description = "关联NPC ID列表(对话时知情但不知详情)" },
                    finalReward = new
                    {
                        type = "object",
                        description = "最终奖励(完成全部步骤后)",
                        properties = new
                        {
                            goldBonus = new { type = "integer" },
                            reputationBonus = new { type = "integer" },
                            jianghuExp = new { type = "integer" },
                            martialArtId = new { type = "string", description = "奖励武功ID(须现存,可选)" },
                            items = new { type = "array", items = new { type = "object", properties = new { itemId = new { type = "string" }, quantity = new { type = "integer" } } } }
                        }
                    }
                },
                required = new[] { "questId", "name", "description", "triggerNpcId", "finalReward" }
            }
        };
    }

    private string ExecuteCreateStoryQuest(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var questId = args.GetProperty("questId").GetString() ?? "";
        var name = args.GetProperty("name").GetString() ?? "";
        var desc = args.GetProperty("description").GetString() ?? "";
        var triggerNpcId = args.GetProperty("triggerNpcId").GetString() ?? "";

        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(name))
            return Err("questId和name不能为空");
        if (!questId.StartsWith("quarter_", StringComparison.Ordinal))
            return Err("季度剧情任务的questId必须以quarter_开头");
        if (_drafts.Count > 0 || FinalizedQuestCount > 0)
            return Err("每季度只能创建并定稿一条剧情任务链");
        if (_drafts.ContainsKey(questId) || QuestIdExists(questId))
            return Err($"questId已存在: {questId}");
        if (!NpcExists(triggerNpcId))
            return Err($"触发NPC不存在: {triggerNpcId}(用query_world_elements查可用NPC)");

        var difficulty = args.TryGetProperty("difficulty", out var difficultyEl)
            ? difficultyEl.GetString() ?? "normal" : "normal";
        if (difficulty is not ("easy" or "normal" or "hard" or "legendary"))
            return Err("difficulty只可为 easy/normal/hard/legendary");
        int minFavorability = args.TryGetProperty("minFavorability", out var favorEl) ? favorEl.GetInt32() : 0;
        if (minFavorability is < 0 or > 50)
            return Err("minFavorability必须在0-50之间，避免任务无法接取");
        var currentFavorability = _state.AllNPCs[triggerNpcId].GetRelation(_state.Player.Id).Favorability;
        if (minFavorability > currentFavorability)
            return Err($"minFavorability不能高于触发NPC当前好感度({currentFavorability})，否则任务无法立刻接取");
        if (args.TryGetProperty("prerequisiteQuestIds", out var prereqEl) && prereqEl.ValueKind == JsonValueKind.Array && prereqEl.GetArrayLength() > 0)
            return Err("季度剧情必须独立可接取，不支持前置任务");
        if (args.TryGetProperty("exclusiveWithQuestIds", out var exclusiveEl) && exclusiveEl.ValueKind == JsonValueKind.Array && exclusiveEl.GetArrayLength() > 0)
            return Err("季度剧情不支持互斥任务分支");
        if (!args.TryGetProperty("finalReward", out var rewardEl) || rewardEl.ValueKind != JsonValueKind.Object)
            return Err("finalReward不能为空，季度剧情必须在创建时确定有意义的最终奖励");
        var rewardError = ValidateRewardReferences(rewardEl);
        if (rewardError != null) return Err(rewardError);

        var cfg = new QuestConfig
        {
            Id = questId,
            Name = name,
            Type = "chain",
            Description = desc,
            TriggerNpcId = triggerNpcId,
            Difficulty = difficulty,
            MinFavorabilityToOffer = minFavorability,
            RelatedNpcIds = args.TryGetProperty("relatedNpcIds", out var r) && r.ValueKind == JsonValueKind.Array
                ? r.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                : new List<string>(),
            PrerequisiteQuestIds = new List<string>(),
            ExclusiveWithQuestIds = new List<string>()
        };

        if (cfg.RelatedNpcIds.Any(id => !NpcExists(id)))
            return Err("relatedNpcIds含有不可互动的NPC");
        if (cfg.PrerequisiteQuestIds.Any(id => !QuestReferenceExists(id)))
            return Err("prerequisiteQuestIds含有不存在的任务ID");
        if (cfg.ExclusiveWithQuestIds.Any(id => !QuestReferenceExists(id)))
            return Err("exclusiveWithQuestIds含有不存在的任务ID");

        // 解析最终奖励
        cfg.Reward = ParseReward(rewardEl);
        if (!HasMeaningfulReward(cfg.Reward))
            return Err("finalReward至少要包含一项有效奖励（银两、阅历、声望、武功或物品）");

        _drafts[questId] = cfg;
        GameLogger.AI($"[季度] 创建任务骨架: {name}({questId}) 触发={triggerNpcId}");

        return Json(new
        {
            questId, success = true, name, stepCount = 0,
            narration = $"江湖传闻：「{name}」的序幕似乎拉开了..."
        });
    }

    // ── 2. add_quest_step 添加步骤 ──

    private ToolDefinition AddQuestStepTool()
    {
        return new ToolDefinition
        {
            Name = "add_quest_step",
            Description = "向任务添加一个可实际完成的步骤。actionType仅可为 talk(对话NPC)/fight(战胜NPC)/spar(切磋胜利)/kill(战后杀死)/spare(战后放过)/go(到达场景)/mine(场景挖矿)/submit(提交物品)。talk/fight/spar/kill/spare必须有targetNPC；go/mine必须有targetScene；submit必须有requiredItems且物品须由玩家当前持有。季度剧情任务不支持dungeon或meditate步骤。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    questId = new { type = "string" },
                    stepId = new { type = "string", description = "步骤唯一ID,如 step_meet" },
                    description = new { type = "string", description = "步骤描述(玩家可见)" },
                    actionType = new { type = "string", description = "talk/fight/spar/kill/spare/go/mine/submit" },
                    targetNPC = new { type = "string", description = "目标NPC ID(talk/fight/kill等用,须现存)" },
                    targetScene = new { type = "string", description = "目标场景ID(go/meditate/mine用,须现存)" },
                    aiHint = new { type = "string", description = "给发布者/目标NPC的剧情态度提示(可选)" },
                    requiredItems = new
                    {
                        type = "array",
                        description = "仅 submit 步骤使用：玩家需提交的现存物品，结构[{itemId,quantity}]",
                        items = new { type = "object", properties = new { itemId = new { type = "string" }, quantity = new { type = "integer" } } }
                    },
                    insertIndex = new { type = "integer", description = "插入位置(0=最前,不填=末尾追加)" },
                    reward = new
                    {
                        type = "object",
                        description = "该步骤的节点奖励(可选)",
                        properties = new
                        {
                            goldBonus = new { type = "integer" },
                            reputationBonus = new { type = "integer" },
                            jianghuExp = new { type = "integer" },
                            karmaBonus = new { type = "integer" },
                            martialArtId = new { type = "string" },
                            items = new { type = "array", items = new { type = "object", properties = new { itemId = new { type = "string" }, quantity = new { type = "integer" } } } }
                        }
                    }
                },
                required = new[] { "questId", "stepId", "description", "actionType" }
            }
        };
    }

    private string ExecuteAddQuestStep(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var questId = args.GetProperty("questId").GetString() ?? "";
        if (!_drafts.TryGetValue(questId, out var cfg))
            return Err($"任务草稿不存在: {questId}(先用create_story_quest创建)");

        var stepId = args.GetProperty("stepId").GetString() ?? "";
        var actionType = args.GetProperty("actionType").GetString() ?? "talk";
        var desc = args.GetProperty("description").GetString() ?? "";
        var targetNPC = args.TryGetProperty("targetNPC", out var tn) ? tn.GetString() : null;
        var targetScene = args.TryGetProperty("targetScene", out var ts) ? ts.GetString() : null;

        if (string.IsNullOrWhiteSpace(stepId) || string.IsNullOrWhiteSpace(desc))
            return Err("stepId和description不能为空");
        if (!SupportedActionTypes.Contains(actionType))
            return Err("actionType只可为 talk/fight/spar/kill/spare/go/mine/submit；季度剧情任务不支持dungeon或meditate");
        if (!string.IsNullOrEmpty(targetNPC) && !NpcExists(targetNPC))
            return Err($"targetNPC不存在: {targetNPC}");
        if (!string.IsNullOrEmpty(targetScene) && !SceneExists(targetScene))
            return Err($"targetScene不存在: {targetScene}");
        if (cfg.Steps.Any(s => s.Id == stepId))
            return Err($"stepId已存在: {stepId}");

        var requiredItems = ParseRequiredItems(args, out var requiredItemsError);
        if (requiredItemsError != null) return Err(requiredItemsError);
        var stepValidationError = ValidateStep(actionType, targetNPC, targetScene, requiredItems);
        if (stepValidationError != null) return Err(stepValidationError);
        if (args.TryGetProperty("reward", out var rewardEl) && rewardEl.ValueKind == JsonValueKind.Object)
        {
            var rewardError = ValidateRewardReferences(rewardEl);
            if (rewardError != null) return Err(rewardError);
        }

        var step = new QuestStepConfig
        {
            Id = stepId,
            Description = desc,
            ActionType = actionType,
            TargetNPC = targetNPC,
            TargetScene = targetScene,
            AiHint = args.TryGetProperty("aiHint", out var ah) ? ah.GetString() : null,
            RequiredItems = requiredItems
        };
        if (args.TryGetProperty("reward", out var rw) && rw.ValueKind == JsonValueKind.Object)
            step.Reward = ParseReward(rw);

        if (args.TryGetProperty("insertIndex", out var ii) && ii.ValueKind == JsonValueKind.Number)
        {
            int idx = Math.Clamp(ii.GetInt32(), 0, cfg.Steps.Count);
            cfg.Steps.Insert(idx, step);
        }
        else
        {
            cfg.Steps.Add(step);
        }

        var npcName = !string.IsNullOrEmpty(targetNPC) ? GetNpcName(targetNPC) : "";
        GameLogger.AI($"[季度] {cfg.Name} 添加步骤 {stepId}({actionType}) 共{cfg.Steps.Count}步");

        return Json(new
        {
            questId, stepId, success = true, stepCount = cfg.Steps.Count,
            narration = string.IsNullOrEmpty(npcName)
                ? $"「{cfg.Name}」似乎有了新的进展..."
                : $"{npcName}似乎卷入了「{cfg.Name}」..."
        });
    }

    // ── 3. remove_quest_step 删除步骤 ──

    private ToolDefinition RemoveQuestStepTool()
    {
        return new ToolDefinition
        {
            Name = "remove_quest_step",
            Description = "从任务中删除指定步骤节点。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    questId = new { type = "string" },
                    stepId = new { type = "string" }
                },
                required = new[] { "questId", "stepId" }
            }
        };
    }

    private string ExecuteRemoveQuestStep(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var questId = args.GetProperty("questId").GetString() ?? "";
        var stepId = args.GetProperty("stepId").GetString() ?? "";
        if (!_drafts.TryGetValue(questId, out var cfg))
            return Err($"任务草稿不存在: {questId}");

        int idx = cfg.Steps.FindIndex(s => s.Id == stepId);
        if (idx < 0) return Err($"步骤不存在: {stepId}");
        cfg.Steps.RemoveAt(idx);

        return Json(new
        {
            questId, stepId, success = true, stepCount = cfg.Steps.Count,
            narration = $"某些传闻似乎有了变数..."
        });
    }

    // ── 4. update_quest_step 修改步骤 ──

    private ToolDefinition UpdateQuestStepTool()
    {
        return new ToolDefinition
        {
            Name = "update_quest_step",
            Description = "修改任务中指定步骤的字段。只传需要改的字段,未传的保持不变。便于反复打磨剧情。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    questId = new { type = "string" },
                    stepId = new { type = "string" },
                    description = new { type = "string" },
                    actionType = new { type = "string" },
                    targetNPC = new { type = "string" },
                    targetScene = new { type = "string" },
                    aiHint = new { type = "string" },
                    requiredItems = new { type = "array", items = new { type = "object" } },
                    reward = new { type = "object" }
                },
                required = new[] { "questId", "stepId" }
            }
        };
    }

    private string ExecuteUpdateQuestStep(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var questId = args.GetProperty("questId").GetString() ?? "";
        var stepId = args.GetProperty("stepId").GetString() ?? "";
        if (!_drafts.TryGetValue(questId, out var cfg))
            return Err($"任务草稿不存在: {questId}");

        var step = cfg.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step == null) return Err($"步骤不存在: {stepId}");

        var nextDescription = args.TryGetProperty("description", out var d) ? d.GetString() ?? step.Description : step.Description;
        var nextActionType = args.TryGetProperty("actionType", out var at) ? at.GetString() ?? step.ActionType : step.ActionType;
        var nextTargetNpc = args.TryGetProperty("targetNPC", out var tn) ? tn.GetString() : step.TargetNPC;
        var nextTargetScene = args.TryGetProperty("targetScene", out var ts) ? ts.GetString() : step.TargetScene;
        var nextRequiredItems = new List<QuestItemRequirementConfig>(step.RequiredItems);
        if (args.TryGetProperty("requiredItems", out _))
        {
            nextRequiredItems = ParseRequiredItems(args, out var itemError);
            if (itemError != null) return Err(itemError);
        }
        if (!SupportedActionTypes.Contains(nextActionType))
            return Err("actionType只可为 talk/fight/spar/kill/spare/go/mine/submit");
        if (!string.IsNullOrEmpty(nextTargetNpc) && !NpcExists(nextTargetNpc)) return Err($"targetNPC不存在: {nextTargetNpc}");
        if (!string.IsNullOrEmpty(nextTargetScene) && !SceneExists(nextTargetScene)) return Err($"targetScene不存在: {nextTargetScene}");
        var validationError = ValidateStep(nextActionType, nextTargetNpc, nextTargetScene, nextRequiredItems);
        if (validationError != null) return Err(validationError);
        if (args.TryGetProperty("reward", out var rw) && rw.ValueKind == JsonValueKind.Object)
        {
            var rewardError = ValidateRewardReferences(rw);
            if (rewardError != null) return Err(rewardError);
            step.Reward = ParseReward(rw);
        }

        step.Description = nextDescription;
        step.ActionType = nextActionType;
        step.TargetNPC = nextTargetNpc;
        step.TargetScene = nextTargetScene;
        step.RequiredItems = nextRequiredItems;
        if (args.TryGetProperty("aiHint", out var ah)) step.AiHint = ah.GetString();

        return Json(new { questId, stepId, success = true, narration = $"「{cfg.Name}」的细节似乎在悄然改变..." });
    }

    // ── 5. finalize_story_quest 定稿 ──

    private ToolDefinition FinalizeStoryQuestTool()
    {
        return new ToolDefinition
        {
            Name = "finalize_story_quest",
            Description = "将草稿任务定稿，注册到运行时可接取池。定稿会强制校验：3-6个可达步骤、有效最终奖励、至少两段开场RPG对话、一段结局对话、至少一个带剧情对话的转折步骤，以及全部 NPC/场景/物品/武功引用。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    questId = new { type = "string" }
                },
                required = new[] { "questId" }
            }
        };
    }

    private string ExecuteFinalizeStoryQuest(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var questId = args.GetProperty("questId").GetString() ?? "";
        if (!_drafts.TryGetValue(questId, out var cfg))
            return Err($"任务草稿不存在: {questId}");

        if (cfg.Steps.Count < 3 || cfg.Steps.Count > 6)
            return Err("季度剧情任务必须有3-6个步骤，保证有起承转合且不会拖沓");
        if (string.IsNullOrEmpty(cfg.TriggerNpcId))
            return Err("任务缺少触发NPC");
        if (cfg.Reward == null)
            cfg.Reward = new QuestRewardConfig();
        if (!HasMeaningfulReward(cfg.Reward))
            return Err("任务缺少有效的最终奖励；请设置阅历/银两/声望/武功/物品之一");
        if (cfg.IntroDialogue?.Lines.Count < 2)
            return Err("请先用set_quest_dialogue为intro写至少两段RPG开场对话");
        if (cfg.CompleteDialogue?.Lines.Count < 1)
            return Err("请先用set_quest_dialogue为complete写结局对话");
        if (!cfg.Steps.Skip(1).Take(cfg.Steps.Count - 2).Any(s => s.Dialogue?.Lines.Count > 0))
            return Err("请至少为一个中间转折步骤设置RPG剧情对话");

        // 校验所有步骤的引用仍合法
        foreach (var s in cfg.Steps)
        {
            var stepError = ValidateStep(s.ActionType, s.TargetNPC, s.TargetScene, s.RequiredItems);
            if (stepError != null) return Err($"步骤{s.Id}无效: {stepError}");
            if (!string.IsNullOrEmpty(s.TargetNPC) && !NpcExists(s.TargetNPC))
                return Err($"步骤{s.Id}的targetNPC不可互动: {s.TargetNPC}");
            if (!string.IsNullOrEmpty(s.TargetScene) && !SceneExists(s.TargetScene))
                return Err($"步骤{s.Id}的targetScene不存在: {s.TargetScene}");
        }

        _drafts.Remove(questId);
        _state.RuntimeQuests.Add(cfg);
        FinalizedQuestCount++;

        var triggerName = GetNpcName(cfg.TriggerNpcId);
        GameLogger.AI($"[季度] 任务定稿: {cfg.Name}({questId}) {cfg.Steps.Count}步 触发={triggerName}");

        // 检查是否有武功/物品奖励,生成"现世"文案
        string treasureHint = "";
        if (!string.IsNullOrEmpty(cfg.Reward.MartialArtId))
            treasureHint = $"，似有绝世武功将现世";
        else if (cfg.Reward.Items != null && cfg.Reward.Items.Count > 0)
            treasureHint = $"，似有稀世珍宝将现世";

        return Json(new
        {
            questId, success = true, name = cfg.Name, stepCount = cfg.Steps.Count,
            narration = $"「{cfg.Name}」的传闻已传遍江湖,{triggerName}似乎在等待有缘人{treasureHint}..."
        });
    }

    // ── 6. set_quest_dialogue 设置剧情对话 ──

    private ToolDefinition SetQuestDialogueTool()
    {
        return new ToolDefinition
        {
            Name = "set_quest_dialogue",
            Description = "给任务或步骤设置RPG剧情对话(底部对话框+头像逐句推进)。target: 'intro'(接任务时)/'complete'(领奖时)/步骤stepId(该步完成时)。lines为对话数组[{speaker,lines:[台词...]}],speaker用NPC ID或'旁白'/'玩家'。一次对话可多人轮流。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    questId = new { type = "string" },
                    target = new { type = "string", description = "intro/complete/步骤stepId" },
                    lines = new
                    {
                        type = "array",
                        description = "对话段:多个说话人轮流,结构[{speaker,lines:[...]}]",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                speaker = new { type = "string", description = "NPC ID 或 '旁白'/'玩家'" },
                                lines = new { type = "array", items = new { type = "string" }, description = "该说话人的连续台词(每句不超50字)" }
                            },
                            required = new[] { "speaker", "lines" }
                        }
                    }
                },
                required = new[] { "questId", "target", "lines" }
            }
        };
    }

    private string ExecuteSetQuestDialogue(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var questId = args.GetProperty("questId").GetString() ?? "";
        var target = args.GetProperty("target").GetString() ?? "";
        var cfg = FindQuest(questId);
        if (cfg == null)
            return Err($"任务不存在: {questId}(草稿或已定稿任务中均未找到)");

        if (!args.TryGetProperty("lines", out var linesEl) || linesEl.ValueKind != JsonValueKind.Array)
            return Err("lines必须是对话数组");

        // 解析并防幻觉校验speaker
        var script = new DialogueScriptConfig();
        var invalidSpeakers = new List<string>();
        foreach (var dl in linesEl.EnumerateArray())
        {
            var speaker = dl.TryGetProperty("speaker", out var sp) ? sp.GetString() ?? "" : "";
            var dlLines = dl.TryGetProperty("lines", out var ln) && ln.ValueKind == JsonValueKind.Array
                ? ln.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                : new List<string>();
            if (dlLines.Count == 0) continue;

            // 校验speaker: 现存NPC 或 旁白/玩家
            if (speaker == "旁白" || speaker == "玩家" || NpcExists(speaker))
            {
                script.Lines.Add(new DialogueLineConfig { Speaker = speaker, Lines = dlLines });
            }
            else
            {
                invalidSpeakers.Add(speaker);
            }
        }

        if (script.Lines.Count == 0)
            return Err($"无有效对话行(-speaker校验失败: {string.Join(",", invalidSpeakers)})");

        // 写入目标位置
        if (target == "intro") cfg.IntroDialogue = script;
        else if (target == "complete") cfg.CompleteDialogue = script;
        else
        {
            var step = cfg.Steps.FirstOrDefault(s => s.Id == target);
            if (step == null) return Err($"步骤不存在: {target}(用list_draft_quests查stepId)");
            step.Dialogue = script;
        }

        GameLogger.AI($"[季度] {cfg.Name} 设置对话 target={target} 共{script.Lines.Count}段");
        return Json(new
        {
            questId, target, success = true,
            segmentCount = script.Lines.Count,
            invalidSpeakers = invalidSpeakers.Count > 0 ? invalidSpeakers : null,
            narration = $"「{cfg.Name}」的对话细节似乎丰满了..."
        });
    }

    // ── 7. query_world_elements 查可用元素(防幻觉) ──

    private ToolDefinition QueryWorldElementsTool()
    {
        return new ToolDefinition
        {
            Name = "query_world_elements",
            Description = "查询游戏中现存的NPC/场景/武功/物品的ID和名称清单。生成任务步骤前务必查询,确保引用的ID真实存在。type: npc/scene/martial_art/item/all",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    type = new { type = "string", description = "npc/scene/martial_art/item/all" }
                },
                required = new[] { "type" }
            }
        };
    }

    private string ExecuteQueryWorldElements(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        var type = args.TryGetProperty("type", out var t) ? t.GetString() ?? "all" : "all";
        var result = new List<object>();

        if (type is "npc" or "all")
        {
            foreach (var (id, npc) in _state.AllNPCs)
                if (npc.IsAlive && !npc.IsHidden) result.Add(new { id, name = npc.Name, type = "npc" });
        }
        if (type is "scene" or "all")
        {
            foreach (var (id, s) in _state.AllScenes)
                result.Add(new { id, name = s.Name, type = "scene" });
        }
        if (type is "martial_art" or "all")
        {
            foreach (var (id, a) in _config.MartialArts)
                result.Add(new { id, name = a.Name, type = "martial_art" });
        }
        if (type is "item" or "all")
        {
            foreach (var (id, it) in _config.Items)
                result.Add(new { id, name = it.Name, type = "item" });
        }

        return Json(new { items = result, count = result.Count });
    }

    // ── 7. list_draft_quests 列草稿 ──

    private ToolDefinition ListDraftQuestsTool()
    {
        return new ToolDefinition
        {
            Name = "list_draft_quests",
            Description = "列出当前正在编辑中的所有草稿任务概要(id/名称/步骤数/是否可定稿),方便回顾。",
            Parameters = new
            {
                type = "object",
                properties = new { }
            }
        };
    }

    private string ExecuteListDraftQuests(string argsJson)
    {
        var list = _drafts.Select(kv => new
        {
            questId = kv.Key,
            name = kv.Value.Name,
            stepCount = kv.Value.Steps.Count,
            canFinalize = kv.Value.Steps.Count > 0 && !string.IsNullOrEmpty(kv.Value.TriggerNpcId)
        }).ToList();
        return Json(new { drafts = list, count = list.Count });
    }

    // ── 辅助方法 ──

    private static string Err(string msg) => $"{{\"success\": false, \"error\": \"{msg}\"}}";

    private static string Json(object obj) =>
        JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });

    private bool NpcExists(string id) => _state.AllNPCs.TryGetValue(id, out var npc) && npc.IsAlive && !npc.IsHidden;

    /// <summary>在草稿和已定稿运行时任务中查找任务配置。</summary>
    private QuestConfig? FindQuest(string questId)
    {
        if (_drafts.TryGetValue(questId, out var draft)) return draft;
        return _state.RuntimeQuests.FirstOrDefault(q => q.Id == questId);
    }
    private bool SceneExists(string id) => _state.AllScenes.ContainsKey(id);
    private bool ItemExists(string id) => _config.Items.ContainsKey(id);
    private bool ArtExists(string id) => _config.MartialArts.ContainsKey(id);

    private bool QuestReferenceExists(string id) =>
        _config.Quests.ContainsKey(id) || _state.RuntimeQuests.Any(q => q.Id == id) || _drafts.ContainsKey(id);

    private static string? ValidateStep(string actionType, string? targetNpc, string? targetScene,
        IReadOnlyCollection<QuestItemRequirementConfig> requiredItems)
    {
        if (!SupportedActionTypes.Contains(actionType))
            return "actionType不受支持";
        if (actionType is "talk" or "fight" or "spar" or "kill" or "spare")
            return string.IsNullOrEmpty(targetNpc) ? $"{actionType}步骤必须指定targetNPC" : null;
        if (actionType is "go" or "mine")
            return string.IsNullOrEmpty(targetScene) ? $"{actionType}步骤必须指定targetScene" : null;
        if (actionType == "submit")
            return requiredItems.Count == 0 ? "submit步骤必须指定requiredItems" : null;
        if (requiredItems.Count > 0)
            return "只有submit步骤可以设置requiredItems";
        return null;
    }

    private List<QuestItemRequirementConfig> ParseRequiredItems(JsonElement args, out string? error)
    {
        error = null;
        if (!args.TryGetProperty("requiredItems", out var itemsEl)) return new List<QuestItemRequirementConfig>();
        if (itemsEl.ValueKind != JsonValueKind.Array)
        {
            error = "requiredItems必须是数组";
            return new List<QuestItemRequirementConfig>();
        }

        var result = new List<QuestItemRequirementConfig>();
        foreach (var itemEl in itemsEl.EnumerateArray())
        {
            var itemId = itemEl.TryGetProperty("itemId", out var idEl) ? idEl.GetString() ?? "" : "";
            var quantity = itemEl.TryGetProperty("quantity", out var qtyEl) && qtyEl.ValueKind == JsonValueKind.Number
                ? qtyEl.GetInt32() : 1;
            if (string.IsNullOrEmpty(itemId) || !ItemExists(itemId))
            {
                error = $"requiredItems含有不存在的物品: {itemId}";
                return new List<QuestItemRequirementConfig>();
            }
            if (quantity is < 1 or > 20)
            {
                error = "requiredItems的quantity必须在1-20之间";
                return new List<QuestItemRequirementConfig>();
            }
            if (!_state.Player.Inventory.HasItem(itemId, quantity))
            {
                error = $"季度任务的submit物品必须为玩家当前持有: {itemId} x{quantity}";
                return new List<QuestItemRequirementConfig>();
            }
            result.Add(new QuestItemRequirementConfig { ItemId = itemId, Quantity = quantity });
        }
        return result;
    }

    private string? ValidateRewardReferences(JsonElement reward)
    {
        if (reward.TryGetProperty("martialArtId", out var artEl) && artEl.ValueKind == JsonValueKind.String)
        {
            var artId = artEl.GetString() ?? "";
            if (!string.IsNullOrEmpty(artId) && !ArtExists(artId))
                return $"奖励武功不存在: {artId}";
        }
        if (reward.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemEl in itemsEl.EnumerateArray())
            {
                var itemId = itemEl.TryGetProperty("itemId", out var idEl) ? idEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(itemId) || !ItemExists(itemId))
                    return $"奖励物品不存在: {itemId}";
            }
        }
        return null;
    }

    private static bool HasMeaningfulReward(QuestRewardConfig reward) =>
        reward.GoldBonus != 0 || reward.ReputationBonus != 0 || reward.JianghuExp != 0 ||
        reward.KarmaBonus != 0 || reward.HPBonus != 0 || reward.MPBonus != 0 ||
        reward.AttackBonus != 0 || reward.DefenseBonus != 0 ||
        !string.IsNullOrEmpty(reward.MartialArtId) || reward.Items.Count > 0;

    private string GetNpcName(string id) =>
        _state.AllNPCs.TryGetValue(id, out var n) ? n.Name : id;

    private bool QuestIdExists(string questId)
    {
        if (_config.Quests.ContainsKey(questId)) return true;
        if (_state.RuntimeQuests.Any(q => q.Id == questId)) return true;
        return false;
    }

    /// <summary>解析奖励JSON为QuestRewardConfig,含防幻觉校验(武功/物品ID)。</summary>
    private QuestRewardConfig ParseReward(JsonElement rw)
    {
        var reward = new QuestRewardConfig();
        if (rw.TryGetProperty("goldBonus", out var g) && g.ValueKind == JsonValueKind.Number) reward.GoldBonus = Math.Clamp(g.GetInt32(), 0, 5000);
        if (rw.TryGetProperty("reputationBonus", out var rep) && rep.ValueKind == JsonValueKind.Number) reward.ReputationBonus = Math.Clamp(rep.GetInt32(), 0, 1000);
        if (rw.TryGetProperty("jianghuExp", out var je) && je.ValueKind == JsonValueKind.Number) reward.JianghuExp = Math.Clamp(je.GetInt32(), 0, 10000);
        if (rw.TryGetProperty("karmaBonus", out var kb) && kb.ValueKind == JsonValueKind.Number) reward.KarmaBonus = Math.Clamp(kb.GetInt32(), -20, 20);
        if (rw.TryGetProperty("hpBonus", out var hp) && hp.ValueKind == JsonValueKind.Number) reward.HPBonus = Math.Clamp(hp.GetInt32(), 0, 1000);
        if (rw.TryGetProperty("mpBonus", out var mp) && mp.ValueKind == JsonValueKind.Number) reward.MPBonus = Math.Clamp(mp.GetInt32(), 0, 1000);
        if (rw.TryGetProperty("attackBonus", out var ab) && ab.ValueKind == JsonValueKind.Number) reward.AttackBonus = Math.Clamp(ab.GetInt32(), 0, 80);
        if (rw.TryGetProperty("defenseBonus", out var db) && db.ValueKind == JsonValueKind.Number) reward.DefenseBonus = Math.Clamp(db.GetInt32(), 0, 80);

        if (rw.TryGetProperty("martialArtId", out var ma) && ma.ValueKind == JsonValueKind.String)
        {
            var artId = ma.GetString() ?? "";
            if (string.IsNullOrEmpty(artId) || ArtExists(artId))
                reward.MartialArtId = artId;
            // 不存在的武功ID直接忽略(防幻觉),不报错以保持奖励其余部分
        }

        if (rw.TryGetProperty("items", out var its) && its.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in its.EnumerateArray())
            {
                var itemId = it.TryGetProperty("itemId", out var ii2) ? ii2.GetString() ?? "" : "";
                var qty = it.TryGetProperty("quantity", out var q) ? Math.Clamp(q.GetInt32(), 1, 20) : 1;
                if (!string.IsNullOrEmpty(itemId) && ItemExists(itemId))
                    reward.Items.Add(new RewardItemConfig { ItemId = itemId, Quantity = qty });
            }
        }
        return reward;
    }
}
