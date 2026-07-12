using System.Text.Json;
using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Core;
using AutoWuxia.Quests;
using AutoWuxia.World;

namespace AutoWuxia.Systems;

/// <summary>
/// 月度 Agent 系统 - 使用 Tool Calling 的 Agent 循环来演化游戏世界
/// </summary>
public class MonthlyUpdateSystem
{
    private readonly AIService _ai;
    private readonly AIConfig _aiConfig;
    private readonly ConfigManager _config;
    private readonly FactionQuestManager? _questManager;

    // Agent 事件回调（UI 订阅）
    public event Action<string>? OnToolCall;
    public event Action<string>? OnToolResult;
    public event Action<string>? OnAgentFinish;
    public event Action<string>? OnAgentError;

    public MonthlyUpdateSystem(AIService ai, AIConfig aiConfig, ConfigManager config,
        FactionQuestManager? questManager = null)
    {
        _ai = ai;
        _aiConfig = aiConfig;
        _config = config;
        _questManager = questManager;
    }

    /// <summary>
    /// 检查是否应该触发月度更新
    /// </summary>
    public bool ShouldTriggerMonthly(GameState state)
    {
        return state.GameTime.IsNewMonth(Math.Max(1, state.LastMonthlyUpdateDay));
    }

    /// <summary>
    /// 执行月度 AI Agent 循环
    /// 返回 Agent 最终的总结文本
    /// </summary>
    public async Task<string> ExecuteMonthlyUpdate(GameState state)
    {
        GameLogger.AI("══════════════════════════════════════════");
        GameLogger.AI("=== 月度 Agent 开始(并行子任务) ===");
        GameLogger.AI($"时间: {state.GameTime.MonthDisplay}，NPC数量: {state.AllNPCs.Count(n => n.Value.IsAlive)}");

        // 共享 context(NPC信息汇总,构建一次,4个子任务复用)
        var context = BuildInitialContext(state);
        OnToolCall?.Invoke("月度演化启动:位置/武功/经历/门派任务 并行处理中...");

        // 4个并行子任务(各自聚焦一方面,独立AI调用,工具子集)
        var subTasks = new[]
        {
            (name: "位置调度", tools: new[]{"set_npc_schedules"},
             prompt: "安排所有NPC下月各时辰(清晨/白天/黄昏/夜晚)的位置。行踪诡秘的流动型NPC(木高峰/平一指等)每月换地区;江湖乐师(musician)应高频巡游不同城镇,几乎每时段都换地方;闭关者留原处。用 set_npc_schedules 一次性批量设置所有NPC。"),
            (name: "武功经验", tools: new[]{"query_martial_arts", "update_npc_skills"},
             prompt: "先用 query_martial_arts 查武功品质,再用 update_npc_skills 为每个NPC设置本月武功熟练度增量(proficiencyGains)和阅历经验(jianghuExpGain)。common武功+20~50,rare/epic+10~30,legendary/mythic+5~20;闭关苦修者阅历100-500,寻常弟子10-40,旅行/经商0-10。不要遗漏任何NPC。"),
            (name: "经历际遇", tools: new[]{"add_npc_life_events", "manage_npc_inventory"},
             prompt: "用 add_npc_life_events 为每个NPC添加1-2条本月经历(武侠风格,每条不超50字,符合其性格门派);可按需用 manage_npc_inventory 调整NPC背包(得宝/失物/购置)。不要遗漏主要NPC。"),
            (name: "门派任务", tools: new[]{"manage_faction_quests"},
             prompt: "用 manage_faction_quests 查看并为各门派刷新任务池:每月每门派至少1-2个新任务(收集/剿匪),移除过期旧任务。任务奖励 karmaBonus 按门派阵营(正派+3~10,邪派−3~10,中立0)。委托人NPC要符合门派特色。"),
        };

        var subResults = new System.Collections.Concurrent.ConcurrentBag<(string name, string summary)>();
        var tasks = subTasks.Select(st => RunSubTaskAsync(state, st.name, st.tools, st.prompt, context, subResults));
        await Task.WhenAll(tasks);

        // 主Agent汇总:基于各子任务结果生成连贯月度总结
        var summary = await GenerateSummary(state, subResults);
        OnAgentFinish?.Invoke(summary);

        // 欠款利息:玩家金为负时收10%利息
        if (state.Player.Gold < 0)
        {
            int debt = Math.Abs(state.Player.Gold);
            int interest = (int)Math.Ceiling(debt * 0.1);
            state.Player.Gold -= interest;
            GameLogger.Economy($"欠款利息：欠银{debt}两，收取利息{interest}两，现欠{Math.Abs(state.Player.Gold)}两");
            summary += $"\n\n---\n月初结账：欠银{debt}两，收取利息{interest}两，当前欠银{Math.Abs(state.Player.Gold)}两。请尽快赚取银两还债！";
        }

        PostProcessNPCs(state);
        GameLogger.AI("=== 月度 Agent 结束 ===");
        return summary;
    }

    /// <summary>
    /// 运行一个并行子任务:独立agent循环,只暴露相关工具,完成触发实时事件。
    /// </summary>
    private async Task RunSubTaskAsync(GameState state, string name, string[] toolNames, string subPrompt,
        string context, System.Collections.Concurrent.ConcurrentBag<(string name, string summary)> results)
    {
        OnToolCall?.Invoke($"【{name}】处理中...");
        var tools = new MonthlyAgentTools(state, _config, _questManager);
        var toolDefs = tools.GetToolDefinitions(toolNames);
        var systemPrompt = BuildSubTaskPrompt(name, subPrompt);
        var messages = new List<ChatMessage> { new() { Role = "user", Content = context } };
        string? subSummary = null;
        int maxIter = Math.Min(_aiConfig.MonthlyMaxIterations, 5);

        for (int it = 0; it < maxIter; it++)
        {
            var resp = await _ai.ChatWithToolsAsync(systemPrompt, messages, toolDefs, modelOverride: null);
            if (resp.FinishReason == "error")
            {
                GameLogger.AI($"[{name}] 错误: {resp.Content}");
                OnAgentError?.Invoke($"【{name}】Agent错误: {resp.Content}");
                break;
            }
            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = resp.Content ?? "",
                ToolCalls = resp.HasToolCalls ? resp.ToolCalls : null
            });
            if (resp.HasToolCalls)
            {
                foreach (var tc in resp.ToolCalls)
                {
                    GameLogger.AI($"[{name}] 调用工具: {tc.FunctionName}({tc.FunctionArguments})");
                    var tr = tools.ExecuteTool(tc.FunctionName, tc.FunctionArguments);
                    GameLogger.AI($"[{name}] 工具结果: {TruncateForLog(tr, 500)}");
                    var narration = FormatToolResult(tc.FunctionName, tr, state);
                    if (!string.IsNullOrEmpty(narration))
                        OnToolResult?.Invoke($"【{name}】{narration}");
                    messages.Add(new ChatMessage { Role = "tool", Content = tr, ToolCallId = tc.Id });
                }
                continue;
            }
            subSummary = resp.Content ?? "";
            break;
        }
        subSummary ??= $"{name}处理完成";
        results.Add((name, subSummary));
        OnToolResult?.Invoke($"【{name}】✓ 完成");
        GameLogger.AI($"[{name}] 子任务完成: {TruncateForLog(subSummary, 300)}");
    }

    /// <summary>子任务聚焦的system prompt(只做这一方面,其他子Agent并行处理)</summary>
    private string BuildSubTaskPrompt(string name, string subPrompt)
    {
        return $"你是金庸武侠世界月度演化的【{name}】子Agent,只负责:{subPrompt}\n\n" +
               "【原则】\n" +
               "1. 只调用提供的工具完成本职责,不要处理其他方面(位置/武功/经历/任务各有专属子Agent并行处理)\n" +
               "2. 变化要合理,尊重NPC性格,符合武侠风格\n" +
               "3. 属性变化限制:攻击/防御每次±20以内,速度±5以内\n" +
               "4. 完成后直接输出本子任务的处理摘要(简洁,说明做了什么),不要加前导套话\n";
    }

    /// <summary>主Agent:基于各子任务结果生成连贯月度总结</summary>
    private async Task<string> GenerateSummary(GameState state,
        System.Collections.Concurrent.ConcurrentBag<(string name, string summary)> subResults)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"当前时间:{state.GameTime.MonthDisplay}。以下是本月各子Agent的处理摘要(供主笔参考,不要照搬细节):");
        foreach (var (name, summary) in subResults)
            sb.AppendLine($"【{name}】{summary}");

        const string systemPrompt = "你是「江湖月报」主笔,负责撰写本月江湖月报(古代邸报风格)。\n" +
            "要求:\n" +
            "1. 用小标题分段(如【门派风云】【江湖际遇】【高手动向】【市井传闻】等),像报纸栏目\n" +
            "2. 门派任务:只点出哪些门派本月颁了新差事或收回旧差事,【绝对不要写具体任务内容细节】(如采什么药、打什么贼、需几样东西)\n" +
            "3. NPC经历:从各子任务结果里挑几件有趣的主要江湖事件加以总结渲染,不必把所有人名都罗列,讲故事的跌宕起伏\n" +
            "4. 修辞可夸张煽情(报纸噱头),武侠风格,生动有趣,像说书人下笔\n" +
            "5. 不要前导套话,直接以第一个小标题开始月报正文\n" +
            "6. 篇幅适中(300-500字),不要太长";

        var userPrompt = sb.ToString() + "\n请据此撰写本月「江湖月报」。";

        try
        {
            var summary = await _ai.ChatAsync(systemPrompt, userPrompt);
            return CleanSummary(summary ?? "月度演化完成，江湖风云变幻。");
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[汇总] AI调用失败,改用拼接: {ex.Message}");
            return string.Join("\n", subResults.Select(r => $"【{r.name}】{r.summary}"));
        }
    }

    /// <summary>
    /// Agent 不可用时的默认月度更新
    /// </summary>
    public string GenerateDefaultUpdate(GameState state)
    {
        int day = state.GameTime.Day;
        foreach (var (_, npc) in state.AllNPCs)
        {
            if (!npc.IsAlive) continue;

            // 默认提升：累加固定熟练度（高品质武功成长慢；装备多本时每本都练）
            foreach (var ea in npc.ActiveExternalArts)
                ea.GainProficiency(Random.Shared.Next(8, 16));
            if (npc.ActiveInternalArt != null)
                npc.ActiveInternalArt.GainProficiency(Random.Shared.Next(8, 16));

            int npcExpGain = Random.Shared.Next(5, 20);
            npc.GainJianghuExp(npcExpGain);

            npc.BaseAttack += Random.Shared.Next(0, 6);
            npc.AddLifeEvent(day, LifeEventType.Monthly,
                $"{npc.Name}这一个月潜心修炼，武功略有精进。");
        }
        return "江湖风平浪静，各门派弟子都在勤修苦练。";
    }

    // ── 内部方法 ──

    private string BuildAgentSystemPrompt()
    {
        return "你是一个金庸武侠世界的月度演化Agent。你的职责是合理安排所有NPC在下个月的变化，让江湖世界生动有趣。\n\n" +
               "【核心原则】\n" +
               "1. 变化要合理：武功提升循序渐进，位置移动符合逻辑，经历描述符合武侠风格\n" +
               "2. 尊重NPC性格：好斗的NPC可能主动切磋，好静的NPC可能闭关修炼\n" +
               "3. 制造故事：适当安排NPC间的互动、冲突、巧合，但不要过于戏剧化\n" +
               "4. 武功成长（重要）：只能通过 proficiencyGains 累加熟练度让NPC武功进步，等级由系统按累计熟练度自动推导\n" +
               "   - common(普通) 武功成长快，每月 +20~50 即可显著进步\n" +
               "   - rare/epic 武功每月 +10~30，进步缓慢\n" +
               "   - legendary/mythic 武功每月 +5~20，需长期苦练\n" +
               "   - 闭关修炼的NPC可给较高值，旅行/经商的NPC给较低值\n" +
               "5. 属性变化限制：攻击/防御每次±20以内，速度±5以内\n" +
               "6. 经历描述：符合武侠风格，每条不超过50字\n" +
               "7. 门派任务管理：每月可为各门派添加新的收集/剿匪任务，保持任务池新鲜\n" +
               "   - 收集任务：选择合适的物品和委托人NPC，描述要符合门派特色\n" +
               "   - 剿匪任务：根据门派实力安排合适难度的副本\n" +
               "   - 可移除已存在太久的旧任务，换上新的\n" +
               "   - 任务奖励设 karmaBonus：按门派阵营,正派门派+(3~10)、邪派门派−(3~10)、中立门派0(未设则系统按门派阵营默认)\n" +
               "8. 阅历经验设置(重要):通过 update_npc_skills 的 jianghuExpGain 为每个NPC设置本月阅历经验增量,需根据其【当前等级】和【本月经历】合理安排:\n" +
               "   - 升级所需经验=50+等级²×2,高等级NPC(50+级)升一级需数千经验,低等级(10级内)只需百余\n" +
               "   - 闭关苦修/得遇奇遇的NPC可给100-500;寻常修炼的弟子10-40;旅行/经商/受伤修养的0-10\n" +
               "   - 不要遗漏任何NPC,否则其本月无阅历成长\n\n" +
               "【工作流程】\n" +
               "1. 先用 query_martial_arts 查询可用武功列表（含品质信息）\n" +
               "2. 根据每个NPC的性格、经历、关系，安排他们下个月的变化\n" +
               "3. 使用工具批量设置位置、累加熟练度、添加经历、调整背包等\n" +
               "4. 用 manage_faction_quests 查看并为各门派添加新任务（每月至少给每个门派1-2个新任务）\n" +
               "5. 所有NPC都处理完后，写一段总结给玩家\n\n" +
               "【重要】\n" +
               "- 你不能直接修改NPC武功的等级数字，只能通过累加熟练度让其自然升级\n" +
               "- 每个NPC都应该有合理的月度变化，不要遗漏\n" +
               "- 行踪诡秘的流动型NPC（如木高峰、平一指等江湖怪客）每月可在不同地区间调度，体现其游走四方、行踪难测的特点，不要固定在一处\n" +
               "- 江湖乐师(musician角色,如琴仙子/笛翁/箫逸人/鼓乐客)云游四方以演奏为生,每月应【更高频率】地在不同城市场景间调度(几乎每个时段都换地方),体现其巡游献艺的特点,务必不要让其久留一处\n" +
               "- 总结要简洁有趣，让玩家感受到江湖的变化\n" +
               "- 当所有安排完成后，直接输出总结正文，不要加任何前导语(如'好的'/'以下是总结'/'所有NPC已处理完毕'等套话)，直接写江湖发生了什么\n";
    }

    private string BuildInitialContext(GameState state)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"当前时间：{state.GameTime.MonthDisplay}（第{state.GameTime.Day}天）");
        sb.AppendLine($"上次月度更新：第{state.LastMonthlyUpdateDay}天");
        sb.AppendLine();

        sb.AppendLine("═══ NPC信息汇总 ═══");
        foreach (var (id, npc) in state.AllNPCs)
        {
            if (!npc.IsAlive) continue;

            sb.AppendLine($"\n【{npc.Name}】(ID:{id})");
            sb.AppendLine($"  性格：{npc.Personality}");
            sb.AppendLine($"  门派：{state.GetFactionName(npc.FactionId, "无")}");
            sb.AppendLine($"  善恶：{npc.Karma}  心情：{npc.Mood}");
            sb.AppendLine($"  攻击：{npc.BaseAttack}  防御：{npc.BaseDefense}  速度：{npc.Speed}  阅历：Lv.{npc.JianghuLevel}");
            sb.AppendLine($"  位置：{npc.DefaultSceneId}" + (npc.NpcRole == "musician" ? "  【巡游乐师,应频繁移动到不同城镇】" : ""));
            sb.AppendLine($"  内功：{npc.ActiveInternalArt?.Name ?? "无"}" +
                          (npc.ActiveInternalArt != null
                              ? $"[{npc.ActiveInternalArt.RarityName}] Lv.{npc.ActiveInternalArt.Level} 熟练度{npc.ActiveInternalArt.Proficiency}"
                              : ""));
            sb.AppendLine($"  外功：{npc.ActiveExternalArt?.Name ?? "无"}" +
                          (npc.ActiveExternalArt != null
                              ? $"[{npc.ActiveExternalArt.RarityName}] Lv.{npc.ActiveExternalArt.Level} 熟练度{npc.ActiveExternalArt.Proficiency}"
                              : ""));

            // 背包概要
            var invSummary = npc.Inventory.IsEmpty ? "空" : npc.Inventory.GetSummary();
            sb.AppendLine($"  背包：{invSummary}");
            sb.AppendLine($"  银两：{npc.Gold}");

            // 最近经历
            var recentEvents = npc.GetRecentLifeEvents(3);
            if (!string.IsNullOrEmpty(recentEvents))
                sb.AppendLine($"  最近经历：{recentEvents}");
        }

        sb.AppendLine("\n═══ 场景列表 ═══");
        foreach (var (id, scene) in state.AllScenes)
        {
            sb.AppendLine($"  {scene.Name}(ID:{id}) [{scene.Region}]");
        }

        // 任务池信息
        if (_questManager != null)
        {
            sb.AppendLine("\n═══ 当前门派任务池 ═══");
            var allQuests = _questManager.GetAll();
            if (allQuests.Count == 0)
            {
                sb.AppendLine("  （无任务）");
            }
            else
            {
                foreach (var q in allQuests)
                {
                    string tag = q.SubType switch { "bandit" => "[剿匪]", "collect" => "[采办]", _ => "[门派]" };
                    sb.AppendLine($"  {tag} {q.Name} (ID:{q.Id}) 门派:{state.GetFactionName(q.FactionId, q.FactionId ?? "")} 难度:{q.Difficulty}");
                }
            }
        }

        sb.AppendLine("\n请开始安排所有NPC下个月的变化。完成后输出一段给玩家的总结。");

        return sb.ToString();
    }

    /// <summary>
    /// 后处理：为未被Agent处理的NPC提供基础成长
    /// </summary>
    private void PostProcessNPCs(GameState state)
    {
        // 阅历成长已交由 AI 通过 update_npc_skills 的 jianghuExpGain 参数设置,此处不重复发放。

        // 作者君首次与玩家相识后便云游四方。放在月度后处理阶段执行，
        // 即使 Agent 未安排到他，也不会长期停留在同一处。
        if (state.AuthorIntroduced && AuthorJunSystem.Relocate(state))
            OnToolResult?.Invoke("【江湖逸闻】作者君又收起稿纸，往别处寻故事去了。");

        // 仇敌寻仇:与玩家为仇敌(Enemy)的NPC,本月主动寻仇至玩家当前场景
        var playerScene = state.CurrentSceneId;
        if (!string.IsNullOrEmpty(playerScene))
        {
            foreach (var (_, npc) in state.AllNPCs)
            {
                if (!npc.IsAlive) continue;
                var rel = npc.GetRelation(state.Player.Id);
                if (rel.Type == RelationType.Enemy)
                {
                    npc.DefaultSceneId = playerScene;
                    if (npc.Schedule != null)
                    {
                        var keys = npc.Schedule.Keys.ToList();
                        foreach (var k in keys) npc.Schedule[k] = playerScene;
                    }
                    GameLogger.AI($"[仇敌寻仇] {npc.Name}本月寻仇至{playerScene}");
                    OnToolResult?.Invoke($"【仇敌寻仇】{npc.Name}循着你的踪迹追来...");
                }
            }
        }
    }

    /// <summary>
    /// 将工具结果格式化为玩家视角的江湖传闻文案(非agent技术日志)。
    /// </summary>
    private string FormatToolResult(string toolName, string resultJson, GameState state)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            if (toolName == "set_npc_schedules" && root.TryGetProperty("results", out var results))
            {
                var names = new List<string>();
                foreach (var r in results.EnumerateArray())
                    if (r.TryGetProperty("npcName", out var name) && r.TryGetProperty("success", out var s) && s.GetBoolean())
                        names.Add(name.GetString() ?? "");
                names = names.Where(n => !string.IsNullOrEmpty(n)).ToList();
                return names.Count > 0 ? $"{string.Join("、", names)}似乎去了别处闯荡..." : "";
            }

            if (toolName == "update_npc_skills" && root.TryGetProperty("results", out var skillResults))
            {
                var parts = new List<string>();
                foreach (var r in skillResults.EnumerateArray())
                {
                    if (r.TryGetProperty("npcName", out var name) && r.TryGetProperty("success", out var s) && s.GetBoolean())
                    {
                        // 从changes里提取升级信息
                        var ups = new List<string>();
                        if (r.TryGetProperty("changes", out var changes))
                            foreach (var c in changes.EnumerateArray())
                            {
                                var t = c.GetString() ?? "";
                                if (t.Contains("Lv") && t.Contains("->")) ups.Add(t);
                            }
                        parts.Add(ups.Count > 0 ? $"{name}修炼有所进境({string.Join(",", ups)})" : $"{name}潜心修练...");
                    }
                }
                return parts.Count > 0 ? string.Join("；", parts) + "。" : "";
            }

            if (toolName == "add_npc_life_events" && root.TryGetProperty("results", out var evtResults))
            {
                var names = new List<string>();
                foreach (var r in evtResults.EnumerateArray())
                    if (r.TryGetProperty("npcName", out var name) && r.TryGetProperty("success", out var s) && s.GetBoolean())
                        names.Add(name.GetString() ?? "");
                names = names.Where(n => !string.IsNullOrEmpty(n)).ToList();
                return names.Count > 0 ? $"{string.Join("、", names)}近来似有些际遇..." : "";
            }

            if (toolName == "manage_faction_quests" && root.TryGetProperty("results", out var qResults))
            {
                var parts = new List<string>();
                foreach (var r in qResults.EnumerateArray())
                {
                    var a = r.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
                    var s = r.TryGetProperty("success", out var sEl) && sEl.GetBoolean();
                    if (a == "add" && s)
                    {
                        var qn = r.TryGetProperty("questName", out var qnEl) ? qnEl.GetString() ?? "" : "";
                        var facId = r.TryGetProperty("factionId", out var facEl) ? facEl.GetString() ?? "" : "";
                        var fac = state.GetFactionName(string.IsNullOrEmpty(facId) ? null : facId, "门派");
                        parts.Add($"「{fac}」似乎在张罗新的差事「{qn}」");
                    }
                    else if (a == "remove" && s)
                        parts.Add("某门派收回了旧差事");
                }
                return parts.Count > 0 ? string.Join("；", parts) + "。" : "";
            }

            if (toolName == "manage_npc_inventory" && root.TryGetProperty("results", out var invResults))
            {
                var parts = new List<string>();
                foreach (var r in invResults.EnumerateArray())
                {
                    if (r.TryGetProperty("npcName", out var name) && r.TryGetProperty("success", out var s) && s.GetBoolean())
                    {
                        var act = r.TryGetProperty("action", out var actEl) ? actEl.GetString() ?? "" : "";
                        var itemId = r.TryGetProperty("itemId", out var itEl) ? itEl.GetString() ?? "" : "";
                        if (act == "add" && !string.IsNullOrEmpty(itemId))
                            parts.Add($"{name}得了一件{itemId}");
                    }
                }
                return parts.Count > 0 ? string.Join("；", parts) + "。" : "";
            }

            // query_martial_arts 是查询,不显示给玩家
            if (toolName == "query_martial_arts") return "";
        }
        catch { }

        return "";
    }

    private static string TruncateForLog(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...(截断)";
    }

    /// <summary>
    /// 清理AI总结开头的前导套话(如"好的，..."、"以下是总结"、"所有NPC已处理完毕"等),
    /// 让玩家直接看到总结正文。
    /// </summary>
    private static string CleanSummary(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var s = raw.Trim();
        // 反复去掉开头的套话行/句,直到剩下正文
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 5)
        {
            changed = false;
            // 去掉以冒号结尾的前导语整行(如"以下是本月江湖演化的总结：")
            var nl = s.IndexOf('\n');
            if (nl >= 0 && nl < 40)
            {
                var firstLine = s[..nl].Trim();
                if (firstLine.EndsWith('：') || firstLine.EndsWith(':'))
                {
                    // 仅当该行像前导语(含"总结/如下/以下/完毕"等)
                    if (firstLine.Contains("总结") || firstLine.Contains("如下") || firstLine.Contains("以下") || firstLine.Contains("完毕"))
                    {
                        s = s[(nl + 1)..].TrimStart();
                        changed = true;
                        continue;
                    }
                }
            }
            // 去掉开头短句套话(到第一个逗号/句号)
            var prefixes = new[] { "好的", "好的！", "好", "明白了", "明白", "收到", "了解" };
            foreach (var p in prefixes)
            {
                if (s.StartsWith(p))
                {
                    var rest = s[p.Length..];
                    // 找到第一个标点(，。！：)后截断
                    int idx = -1;
                    foreach (var ch in new[] { '，', ',', '。', '.', '！', '!', '：', ':' })
                    {
                        int i = rest.IndexOf(ch);
                        if (i >= 0 && (idx < 0 || i < idx)) idx = i;
                    }
                    if (idx >= 0)
                    {
                        s = rest[(idx + 1)..].TrimStart();
                        changed = true;
                    }
                    break;
                }
            }
            // 去掉"所有NPC...完毕"这类前导整句
            if (s.StartsWith("所有") && (s.Contains("完毕") || s.Contains("处理完")) )
            {
                int idx = -1;
                foreach (var ch in new[] { '。', '.', '！', '!', '\n' })
                {
                    int i = s.IndexOf(ch);
                    if (i >= 0 && (idx < 0 || i < idx)) idx = i;
                }
                if (idx >= 0) { s = s[(idx + 1)..].TrimStart(); changed = true; }
            }
        }
        return string.IsNullOrWhiteSpace(s) ? raw.Trim() : s;
    }
}
