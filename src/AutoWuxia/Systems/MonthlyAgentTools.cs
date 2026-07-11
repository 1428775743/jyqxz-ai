using System.Text.Json;
using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;
using AutoWuxia.Core;
using AutoWuxia.MartialArts;
using AutoWuxia.Quests;

namespace AutoWuxia.Systems;

/// <summary>
/// 月度 Agent 的工具定义 + 执行逻辑（6个工具）
/// </summary>
public class MonthlyAgentTools
{
    private readonly GameState _state;
    private readonly ConfigManager _config;
    private readonly FactionQuestManager? _questManager;

    public MonthlyAgentTools(GameState state, ConfigManager config, FactionQuestManager? questManager = null)
    {
        _state = state;
        _config = config;
        _questManager = questManager;
    }

    // ── 工具定义列表 ──

    private static readonly Dictionary<string, Func<MonthlyAgentTools, ToolDefinition>> ToolFactories = new()
    {
        ["query_martial_arts"] = t => t.QueryMartialArtsTool(),
        ["set_npc_schedules"] = t => t.SetNpcSchedulesTool(),
        ["update_npc_skills"] = t => t.UpdateNpcSkillsTool(),
        ["add_npc_life_events"] = t => t.AddNpcLifeEventsTool(),
        ["manage_npc_inventory"] = t => t.ManageNpcInventoryTool(),
        ["manage_faction_quests"] = t => t.ManageFactionQuestsTool(),
    };

    /// <summary>
    /// 获取工具定义。toolNames 传 null 返回全部;传子集仅返回指定工具
    /// (并行子任务时各子Agent只暴露相关工具,聚焦不乱调)。
    /// </summary>
    public List<ToolDefinition> GetToolDefinitions(IEnumerable<string>? toolNames = null)
    {
        var names = toolNames ?? ToolFactories.Keys;
        var defs = new List<ToolDefinition>();
        foreach (var name in names)
        {
            if (name == "manage_faction_quests" && _questManager == null) continue;
            if (ToolFactories.TryGetValue(name, out var factory))
                defs.Add(factory(this));
        }
        return defs;
    }

    /// <summary>
    /// 执行工具调用，返回 JSON 字符串作为 tool result
    /// </summary>
    public string ExecuteTool(string toolName, string argumentsJson)
    {
        try
        {
            return toolName switch
            {
                "query_martial_arts" => ExecuteQueryMartialArts(argumentsJson),
                "set_npc_schedules" => ExecuteSetNpcSchedules(argumentsJson),
                "update_npc_skills" => ExecuteUpdateNpcSkills(argumentsJson),
                "add_npc_life_events" => ExecuteAddNpcLifeEvents(argumentsJson),
                "manage_npc_inventory" => ExecuteManageNpcInventory(argumentsJson),
                "manage_faction_quests" => ExecuteManageFactionQuests(argumentsJson),
                _ => "{\"error\": \"未知工具: " + toolName + "\"}"
            };
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[工具执行异常] {toolName}: {ex.Message}");
            return $"{{\"error\": \"工具执行失败: {ex.Message}\"}}";
        }
    }

    // ── 1. query_martial_arts ──

    private ToolDefinition QueryMartialArtsTool()
    {
        return new ToolDefinition
        {
            Name = "query_martial_arts",
            Description = "查询可用的武功列表，返回所有武功的ID、名称、类型和最大等级。可按类型筛选。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    artType = new
                    {
                        type = "string",
                        description = "筛选类型: 'internal'(内功), 'external'(外功), 'all'(全部)，默认'all'",
                        @enum = new[] { "internal", "external", "all" }
                    }
                }
            }
        };
    }

    private string ExecuteQueryMartialArts(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson);
        var artType = "all";
        if (args.RootElement.TryGetProperty("artType", out var typeEl))
            artType = typeEl.GetString() ?? "all";

        var results = new List<object>();
        foreach (var (id, config) in _config.MartialArts)
        {
            if (artType != "all" && config.Type != artType) continue;
            results.Add(new
            {
                id = config.Id,
                name = config.Name,
                type = config.Type,
                rarity = string.IsNullOrEmpty(config.Rarity) ? "common" : config.Rarity,
                maxLevel = config.MaxLevel,
                description = config.Description
            });
        }

        return JsonSerializer.Serialize(new { arts = results, count = results.Count });
    }

    // ── 2. set_npc_schedules ──

    private ToolDefinition SetNpcSchedulesTool()
    {
        return new ToolDefinition
        {
            Name = "set_npc_schedules",
            Description = "批量设置NPC的位置/日程安排。可以设置NPC在各个时段（清晨、上午、中午、下午、傍晚、夜晚）所在的场景。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    updates = new
                    {
                        type = "array",
                        description = "NPC位置更新列表",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                npcId = new { type = "string", description = "NPC的ID" },
                                schedule = new
                                {
                                    type = "object",
                                    description = "时段->场景ID的映射，如 {\"清晨\": \"sceneId1\", \"白天\": \"sceneId2\"}"
                                },
                                defaultSceneId = new { type = "string", description = "默认场景ID（不按时段时使用）" }
                            },
                            required = new[] { "npcId" }
                        }
                    }
                },
                required = new[] { "updates" }
            }
        };
    }

    private string ExecuteSetNpcSchedules(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson);
        var updates = args.RootElement.GetProperty("updates");
        var results = new List<object>();
        int day = _state.GameTime.Day;

        foreach (var update in updates.EnumerateArray())
        {
            var npcId = update.GetProperty("npcId").GetString() ?? "";
            if (!_state.AllNPCs.TryGetValue(npcId, out var npc))
            {
                results.Add(new { npcId, success = false, error = "NPC不存在" });
                continue;
            }

            // 更新 schedule
            if (update.TryGetProperty("schedule", out var scheduleEl))
            {
                foreach (var prop in scheduleEl.EnumerateObject())
                {
                    var timePeriod = prop.Name;
                    var sceneId = prop.Value.GetString() ?? "";
                    if (_state.AllScenes.ContainsKey(sceneId))
                    {
                        npc.Schedule[timePeriod] = sceneId;
                    }
                }
            }

            // 更新 defaultSceneId
            if (update.TryGetProperty("defaultSceneId", out var defaultEl))
            {
                var sceneId = defaultEl.GetString() ?? "";
                if (_state.AllScenes.ContainsKey(sceneId))
                {
                    var oldScene = npc.DefaultSceneId;
                    npc.DefaultSceneId = sceneId;

                    if (oldScene != sceneId)
                    {
                        var sceneName = _state.AllScenes.TryGetValue(sceneId, out var s) ? s.Name : sceneId;
                        npc.AddLifeEvent(day, LifeEventType.Travel, $"前往{sceneName}");
                    }
                }
            }

            results.Add(new { npcId, npcName = npc.Name, success = true });
        }

        return JsonSerializer.Serialize(new { results });
    }

    // ── 3. update_npc_skills ──

    private ToolDefinition UpdateNpcSkillsTool()
    {
        return new ToolDefinition
        {
            Name = "update_npc_skills",
            Description = "批量为NPC累加武功熟练度、调整技艺和属性。武功等级由累计熟练度自动推导，不能直接设置等级。" +
                          "若需让NPC学习新武功，请通过 learnArts 让其加入并附带初始熟练度。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    updates = new
                    {
                        type = "array",
                        description = "NPC技能更新列表",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                npcId = new { type = "string", description = "NPC的ID" },
                                proficiencyGains = new
                                {
                                    type = "object",
                                    description = "对NPC已学武功累加熟练度，key=武功ID，value=本月增加的熟练度点数(建议5-50)。" +
                                                   "高品质武功升级所需熟练度更多，因此对legendary/mythic武功可以多给一些。"
                                },
                                learnArts = new
                                {
                                    type = "array",
                                    description = "本月让NPC新学的武功ID列表（仅记录习得，等级=1，不附带熟练度）",
                                    items = new { type = "string" }
                                },
                                craftSkillChanges = new
                                {
                                    type = "object",
                                    description = "技艺等级变更，key=技艺ID，value=变化量。如 {\"forging\": 1}"
                                },
                                attackChange = new { type = "integer", description = "攻击力变化" },
                                defenseChange = new { type = "integer", description = "防御力变化" },
                                speedChange = new { type = "integer", description = "速度变化" },
                                jianghuExpGain = new { type = "integer", description = "本月阅历经验增量,根据NPC当前等级和本月经历设置。升级所需经验=50+等级²×2(如60级升61级需7250经验)。闭关苦修/得遇奇遇的高手可给100-500;寻常修炼的弟子10-40;旅行/经商/受伤修养的0-10。高等级NPC需更多经验才能升级。" }
                            },
                            required = new[] { "npcId" }
                        }
                    }
                },
                required = new[] { "updates" }
            }
        };
    }

    private string ExecuteUpdateNpcSkills(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson);
        var updates = args.RootElement.GetProperty("updates");
        var results = new List<object>();
        int day = _state.GameTime.Day;

        foreach (var update in updates.EnumerateArray())
        {
            var npcId = update.GetProperty("npcId").GetString() ?? "";
            if (!_state.AllNPCs.TryGetValue(npcId, out var npc))
            {
                results.Add(new { npcId, success = false, error = "NPC不存在" });
                continue;
            }

            var changes = new List<string>();

            // 累加武功熟练度
            if (update.TryGetProperty("proficiencyGains", out var gainsEl))
            {
                foreach (var prop in gainsEl.EnumerateObject())
                {
                    var artId = prop.Name;
                    var gain = Math.Clamp(prop.Value.GetInt32(), 0, 200);
                    var art = npc.LearnedArts.FirstOrDefault(a => a.Id == artId);
                    if (art == null) continue;
                    int oldLv = art.Level;
                    art.GainProficiency(gain);
                    int newLv = art.Level;
                    if (newLv > oldLv)
                        changes.Add($"{art.Name}熟练+{gain} Lv{oldLv}→{newLv}");
                    else
                        changes.Add($"{art.Name}熟练+{gain}({art.Proficiency})");
                }
            }

            // 让NPC学习新武功
            if (update.TryGetProperty("learnArts", out var learnEl))
            {
                foreach (var artIdEl in learnEl.EnumerateArray())
                {
                    var artId = artIdEl.GetString() ?? "";
                    if (npc.LearnedArts.Any(a => a.Id == artId)) continue;
                    var newArt = _config.CreateMartialArt(artId, 1);
                    if (newArt != null)
                    {
                        npc.LearnArt(newArt);
                        changes.Add($"习得{newArt.Name}[{newArt.RarityName}]");
                    }
                }
            }

            // 技艺变更
            if (update.TryGetProperty("craftSkillChanges", out var craftEl))
            {
                foreach (var prop in craftEl.EnumerateObject())
                {
                    var skillId = prop.Name;
                    var amount = prop.Value.GetInt32();
                    npc.ImproveCraftSkill(skillId, amount);
                    changes.Add($"{CharacterBase.GetCraftSkillName(skillId)}+{amount}={npc.GetCraftSkill(skillId)}");
                }
            }

            // 属性变化
            if (update.TryGetProperty("attackChange", out var atkEl))
            {
                var change = Math.Clamp(atkEl.GetInt32(), -20, 20);
                npc.BaseAttack += change;
                if (change != 0) changes.Add($"攻击{(change > 0 ? "+" : "")}{change}");
            }
            if (update.TryGetProperty("defenseChange", out var defEl))
            {
                var change = Math.Clamp(defEl.GetInt32(), -20, 20);
                npc.BaseDefense += change;
                if (change != 0) changes.Add($"防御{(change > 0 ? "+" : "")}{change}");
            }
            if (update.TryGetProperty("speedChange", out var spdEl))
            {
                var change = Math.Clamp(spdEl.GetInt32(), -5, 5);
                npc.Speed += change;
                if (change != 0) changes.Add($"速度{(change > 0 ? "+" : "")}{change}");
            }

            // NPC阅历成长:由AI根据NPC当前等级和本月经历设置(不再硬编码随机)
            int npcExpGain = update.TryGetProperty("jianghuExpGain", out var expEl)
                ? Math.Clamp(expEl.GetInt32(), 0, 1000) : 0;
            if (npcExpGain > 0)
            {
                int npcLevelUps = npc.GainJianghuExp(npcExpGain);
                if (npcLevelUps > 0)
                {
                    npc.AddLifeEvent(day, LifeEventType.Training, $"江湖阅历提升，达到Lv.{npc.JianghuLevel}");
                    changes.Add($"阅历Lv.{npc.JianghuLevel}(+{npcExpGain}经验)");
                }
                else
                {
                    changes.Add($"阅历经验+{npcExpGain}");
                }
            }

            results.Add(new { npcId, npcName = npc.Name, success = true, changes });
        }

        return JsonSerializer.Serialize(new { results });
    }

    // ── 4. add_npc_life_events ──

    private ToolDefinition AddNpcLifeEventsTool()
    {
        return new ToolDefinition
        {
            Name = "add_npc_life_events",
            Description = "批量为NPC添加经历事件。记录NPC这个月发生的事情，如旅行、修炼、社交等。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    events = new
                    {
                        type = "array",
                        description = "经历事件列表",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                npcId = new { type = "string", description = "NPC的ID" },
                                type = new
                                {
                                    type = "string",
                                    description = "事件类型",
                                    @enum = new[] { "Travel", "Training", "Social", "Combat", "Major", "Monthly" }
                                },
                                description = new { type = "string", description = "事件描述（不超过50字，符合武侠风格）" }
                            },
                            required = new[] { "npcId", "type", "description" }
                        }
                    }
                },
                required = new[] { "events" }
            }
        };
    }

    private string ExecuteAddNpcLifeEvents(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson);
        var events = args.RootElement.GetProperty("events");
        var results = new List<object>();
        int day = _state.GameTime.Day;

        foreach (var evt in events.EnumerateArray())
        {
            var npcId = evt.GetProperty("npcId").GetString() ?? "";
            if (!_state.AllNPCs.TryGetValue(npcId, out var npc))
            {
                results.Add(new { npcId, success = false, error = "NPC不存在" });
                continue;
            }

            var typeStr = evt.GetProperty("type").GetString() ?? "Monthly";
            var desc = evt.GetProperty("description").GetString() ?? "";

            if (!Enum.TryParse<LifeEventType>(typeStr, true, out var eventType))
                eventType = LifeEventType.Monthly;

            npc.AddLifeEvent(day, eventType, desc);
            results.Add(new { npcId, npcName = npc.Name, success = true, eventType = typeStr });
        }

        return JsonSerializer.Serialize(new { results, count = results.Count });
    }

    // ── 5. manage_npc_inventory ──

    private ToolDefinition ManageNpcInventoryTool()
    {
        return new ToolDefinition
        {
            Name = "manage_npc_inventory",
            Description = "批量操作NPC的背包物品，支持新增、删除、设置数量。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    operations = new
                    {
                        type = "array",
                        description = "背包操作列表",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                npcId = new { type = "string", description = "NPC的ID" },
                                action = new
                                {
                                    type = "string",
                                    description = "操作类型",
                                    @enum = new[] { "add", "remove", "set" }
                                },
                                itemId = new { type = "string", description = "物品ID" },
                                quantity = new { type = "integer", description = "数量" }
                            },
                            required = new[] { "npcId", "action", "itemId" }
                        }
                    }
                },
                required = new[] { "operations" }
            }
        };
    }

    private string ExecuteManageNpcInventory(string argsJson)
    {
        var args = JsonDocument.Parse(argsJson);
        var operations = args.RootElement.GetProperty("operations");
        var results = new List<object>();

        foreach (var op in operations.EnumerateArray())
        {
            var npcId = op.GetProperty("npcId").GetString() ?? "";
            if (!_state.AllNPCs.TryGetValue(npcId, out var npc))
            {
                results.Add(new { npcId, success = false, error = "NPC不存在" });
                continue;
            }

            var action = op.GetProperty("action").GetString() ?? "";
            var itemId = op.GetProperty("itemId").GetString() ?? "";
            var quantity = op.TryGetProperty("quantity", out var qtyEl) ? qtyEl.GetInt32() : 1;

            switch (action)
            {
                case "add":
                    var existing = npc.Inventory.GetItem(itemId);
                    if (existing != null)
                    {
                        existing.Quantity += quantity;
                    }
                    else
                    {
                        // 尝试从配置创建物品
                        var item = _config.CreateItem(itemId);
                        if (item != null)
                        {
                            item.Quantity = quantity;
                            npc.Inventory.AddItem(item);
                        }
                        else
                        {
                            results.Add(new { npcId, action, itemId, success = false, error = "物品不存在" });
                            continue;
                        }
                    }
                    break;

                case "remove":
                    var itemToRemove = npc.Inventory.GetItem(itemId);
                    if (itemToRemove != null)
                    {
                        itemToRemove.Quantity -= quantity;
                        if (itemToRemove.Quantity <= 0)
                            npc.Inventory.RemoveItem(itemId);
                    }
                    break;

                case "set":
                    var itemToSet = npc.Inventory.GetItem(itemId);
                    if (itemToSet != null)
                    {
                        itemToSet.Quantity = quantity;
                        if (quantity <= 0)
                            npc.Inventory.RemoveItem(itemId);
                    }
                    else if (quantity > 0)
                    {
                        var newItem = _config.CreateItem(itemId);
                        if (newItem != null)
                        {
                            newItem.Quantity = quantity;
                            npc.Inventory.AddItem(newItem);
                        }
                    }
                    break;
            }

            results.Add(new { npcId, npcName = npc.Name, action, itemId, quantity, success = true });
        }

        return JsonSerializer.Serialize(new { results });
    }

    // ── 6. manage_faction_quests ──

    /// <summary>
    /// 动态生成物品列表（从配置中读取，不再硬编码）
    /// </summary>
    private string BuildItemList()
    {
        var parts = new List<string>();
        foreach (var (id, cfg) in _config.Items)
        {
            parts.Add($"{id}({cfg.Name})");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "无";
    }

    private ToolDefinition ManageFactionQuestsTool()
    {
        return new ToolDefinition
        {
            Name = "manage_faction_quests",
            Description = "管理门派任务池。可添加新任务（收集/剿匪）、移除任务、或查看当前任务池。\n" +
                          "门派ID(所有可加入门派,每月应为每个门派维护任务池): shaolin(少林), wudang(武当), huashan(华山), mingjiao(明教), quanzhen(全真), gumu(古墓), xiaoyao(逍遥), dali_duan(大理段氏), riyue(日月神教)。\n" +
                          "收集任务(subType=collect)需指定 itemId + quantity + issuerNpcId；\n" +
                          "剿匪任务(subType=bandit)需指定 dungeonId(easy/medium/hard),不需委托NPC,所有门派均可发布。\n" +
                          $"可用物品ID: {BuildItemList()}\n" +
                          "可用委托人NPC(收集任务): 各门派掌门 kong_zhi/zhang_sanfeng/yue_buqun/zhang_wuji/qiu_chuji/xiao_long_nv/wu_yazi/duan_zhengchun/yang_liantin, " +
                          "弟子 shaolin_disciple_hui(慧明)/wudang_disciple_qing(清风)/mingjiao_warrior(韦一笑), " +
                          "通用 chen_medicine(陈大夫)/liu_hunter(猎人老刘头)/zhou_grocery(周大嫂)/wen_bookseller(文秀才)。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    actions = new
                    {
                        type = "array",
                        description = "任务操作列表",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                action = new
                                {
                                    type = "string",
                                    description = "操作类型: add(添加)/remove(移除)/list(查看)",
                                    @enum = new[] { "add", "remove", "list" }
                                },
                                factionId = new { type = "string", description = "门派ID (add/list 必填)" },
                                questId = new { type = "string", description = "任务ID (remove 必填)" },
                                subType = new
                                {
                                    type = "string",
                                    description = "任务子类型 (add 必填): collect(收集) 或 bandit(剿匪)",
                                    @enum = new[] { "collect", "bandit" }
                                },
                                name = new { type = "string", description = "任务名称 (add 必填，武侠风不超过15字)" },
                                description = new { type = "string", description = "任务描述 (add 必填，武侠风不超过50字)" },
                                itemId = new { type = "string", description = "收集物品ID (subType=collect 时必填)" },
                                quantity = new { type = "integer", description = "收集数量 (subType=collect 时必填，建议2-5)" },
                                issuerNpcId = new { type = "string", description = "委托NPC ID (subType=collect 时必填)" },
                                dungeonId = new
                                {
                                    type = "string",
                                    description = "副本ID (subType=bandit 时必填): bandit_easy, bandit_medium, bandit_hard",
                                    @enum = new[] { "bandit_easy", "bandit_medium", "bandit_hard" }
                                },
                                difficulty = new
                                {
                                    type = "string",
                                    description = "难度 (add 必填): easy/normal/medium/hard",
                                    @enum = new[] { "easy", "normal", "medium", "hard" }
                                },
                                goldBonus = new { type = "integer", description = "金钱奖励(默认50)" },
                                reputationBonus = new { type = "integer", description = "声望奖励(默认20)" },
                                contributionBonus = new { type = "integer", description = "门派贡献奖励(默认15)" },
                                jianghuExpBonus = new { type = "integer", description = "阅历奖励(默认5)" },
                                karmaBonus = new { type = "integer", description = "善恶奖励:正派/行侠任务+(3~10)、邪派/为恶任务−(3~10)、中立任务0(默认0)" }
                            },
                            required = new[] { "action" }
                        }
                    }
                },
                required = new[] { "actions" }
            }
        };
    }

    private string ExecuteManageFactionQuests(string argsJson)
    {
        if (_questManager == null)
            return "{\"error\": \"任务管理器不可用\"}";

        var args = JsonDocument.Parse(argsJson);
        var actions = args.RootElement.GetProperty("actions");
        var results = new List<object>();

        foreach (var act in actions.EnumerateArray())
        {
            var action = act.GetProperty("action").GetString() ?? "";

            switch (action)
            {
                case "list":
                {
                    var factionId = act.TryGetProperty("factionId", out var fEl) ? fEl.GetString() ?? "all" : "all";
                    var quests = factionId == "all"
                        ? _questManager.GetAll()
                        : _questManager.GetAvailableQuests(factionId);
                    var items = quests.Select(q => new
                    {
                        id = q.Id,
                        name = q.Name,
                        factionId = q.FactionId,
                        subType = q.SubType,
                        difficulty = q.Difficulty,
                        issuerNpcId = q.IssuerNpcId
                    }).ToList();
                    results.Add(new { action = "list", factionId, quests = items, count = items.Count });
                    break;
                }

                case "remove":
                {
                    var questId = act.TryGetProperty("questId", out var qEl) ? qEl.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(questId))
                    {
                        results.Add(new { action = "remove", success = false, error = "缺少 questId" });
                    }
                    else
                    {
                        bool ok = _questManager.RemoveQuest(questId);
                        results.Add(new { action = "remove", questId, success = ok });
                    }
                    break;
                }

                case "add":
                {
                    var factionId = act.TryGetProperty("factionId", out var fEl2) ? fEl2.GetString() ?? "" : "";
                    var subType = act.TryGetProperty("subType", out var stEl) ? stEl.GetString() ?? "collect" : "collect";
                    var name = act.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
                    var desc = act.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
                    var difficulty = act.TryGetProperty("difficulty", out var diffEl) ? diffEl.GetString() ?? "normal" : "normal";

                    int gold = act.TryGetProperty("goldBonus", out var gEl) ? gEl.GetInt32() : 50;
                    int rep = act.TryGetProperty("reputationBonus", out var rEl) ? rEl.GetInt32() : 20;
                    int contrib = act.TryGetProperty("contributionBonus", out var cEl) ? cEl.GetInt32() : 15;
                    int jexp = act.TryGetProperty("jianghuExpBonus", out var jEl) ? jEl.GetInt32() : 5;
                    int karma = act.TryGetProperty("karmaBonus", out var kEl) ? kEl.GetInt32() : 0;
                    // 未显式设善恶时,按门派阵营默认(正派+5/邪派−5/中立0)
                    if (karma == 0 && _config.Factions.TryGetValue(factionId, out var fac) && !string.IsNullOrEmpty(fac.Alignment))
                        karma = fac.Alignment == "邪派" ? -5 : fac.Alignment == "正派" ? 5 : 0;

                    var reward = new QuestReward
                    {
                        GoldBonus = gold,
                        ReputationBonus = rep,
                        FactionContributionBonus = contrib,
                        FactionIdForContribution = factionId,
                        JianghuExp = jexp,
                        KarmaBonus = karma
                    };

                    FactionQuest? quest = null;

                    if (subType == "bandit")
                    {
                        var dungeonId = act.TryGetProperty("dungeonId", out var dgEl) ? dgEl.GetString() ?? "bandit_easy" : "bandit_easy";
                        if (!_config.Dungeons.ContainsKey(dungeonId))
                        {
                            results.Add(new { action = "add", success = false, error = $"副本不存在: {dungeonId}" });
                            continue;
                        }
                        quest = _questManager.AddBanditQuest(factionId, dungeonId.Replace("bandit_", ""),
                            name: name, description: desc);
                    }
                    else // collect
                    {
                        var itemId = act.TryGetProperty("itemId", out var itEl) ? itEl.GetString() ?? "" : "";
                        var quantity = act.TryGetProperty("quantity", out var qtyEl) ? qtyEl.GetInt32() : 3;
                        var issuerNpcId = act.TryGetProperty("issuerNpcId", out var issEl) ? issEl.GetString() ?? "" : "";

                        if (string.IsNullOrEmpty(itemId))
                        {
                            results.Add(new { action = "add", success = false, error = "收集任务缺少 itemId" });
                            continue;
                        }

                        quest = _questManager.AddCollectionQuest(
                            factionId, issuerNpcId, itemId, quantity,
                            reward: reward, name: name, description: desc);
                    }

                    if (quest != null)
                        results.Add(new { action = "add", success = true, questId = quest.Id, questName = quest.Name, factionId });
                    break;
                }

                default:
                    results.Add(new { action, success = false, error = $"未知操作: {action}" });
                    break;
            }
        }

        return JsonSerializer.Serialize(new { results });
    }
}
