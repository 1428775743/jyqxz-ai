using System.Text.Json;
using AutoWuxia.AI;
using AutoWuxia.Config;
using AutoWuxia.Core;

namespace AutoWuxia.Systems;

/// <summary>
/// 季度 Agent 系统 —— 每游戏季度（3个月）触发一次，生成一条完整的江湖剧情任务链。
/// 用 AnnualAgentTools 剧情编辑器工具集让 AI 像编辑器一样拼装、校验并定稿任务。
/// 工具调用日志用玩家视角(narration字段),非agent视角。
/// </summary>
public class AnnualUpdateSystem
{
    private readonly AIService _ai;
    private readonly AIConfig _aiConfig;
    private readonly ConfigManager _config;

    public event Action<string>? OnToolCall;
    public event Action<string>? OnToolResult;
    public event Action<string>? OnAgentFinish;
    public event Action<string>? OnAgentError;

    public AnnualUpdateSystem(AIService ai, AIConfig aiConfig, ConfigManager config)
    {
        _ai = ai;
        _aiConfig = aiConfig;
        _config = config;
    }

    /// <summary>检查是否应该触发季度剧情更新（每三个月）。</summary>
    public bool ShouldTriggerQuarterly(GameState state)
    {
        return state.GameTime.IsNewQuarter(Math.Max(1, state.LastQuarterlyUpdateDay));
    }

    /// <summary>执行季度 Agent 循环，生成一条完整的剧情任务链。</summary>
    public async Task<string> ExecuteQuarterlyUpdate(GameState state)
    {
        GameLogger.AI("══════════════════════════════════════════");
        GameLogger.AI($"=== 季度 Agent 开始 === {state.GameTime.QuarterDisplay}");

        var tools = new AnnualAgentTools(state, _config);
        var toolDefs = tools.GetToolDefinitions();
        var maxIterations = _aiConfig.MonthlyMaxIterations;

        var systemPrompt = BuildAgentSystemPrompt();
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = BuildInitialContext(state) }
        };

        string? finalSummary = null;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            GameLogger.AI($"[季度Agent循环] 第 {iteration + 1}/{maxIterations} 次迭代");
            OnToolCall?.Invoke($"季末筹谋... (第{iteration + 1}轮)");

            var response = await _ai.ChatWithToolsAsync(
                systemPrompt, messages, toolDefs,
                modelOverride: null);

            if (response.FinishReason == "error")
            {
                GameLogger.AI($"[季度Agent] 错误: {response.Content}");
                OnAgentError?.Invoke($"季度Agent错误: {response.Content}");
                break;
            }

            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response.Content ?? "",
                ToolCalls = response.HasToolCalls ? response.ToolCalls : null
            });

            if (response.HasToolCalls)
            {
                foreach (var toolCall in response.ToolCalls)
                {
                    GameLogger.AI($"[季度Agent] 调用工具: {toolCall.FunctionName}({toolCall.FunctionArguments})");
                    // 玩家视角: 不显示"调用工具X",只更新状态
                    OnToolCall?.Invoke($"江湖暗流涌动...");

                    var toolResult = tools.ExecuteTool(toolCall.FunctionName, toolCall.FunctionArguments);
                    GameLogger.AI($"[季度Agent] 工具结果: {TruncateForLog(toolResult, 500)}");

                    // 玩家视角: 从工具结果提取 narration 字段显示
                    OnToolResult?.Invoke(ExtractNarration(toolResult));

                    messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = toolResult,
                        ToolCallId = toolCall.Id
                    });
                }
                continue;
            }

            if (tools.FinalizedQuestCount != 1)
            {
                GameLogger.AI("[季度Agent] 未完成定稿，要求继续补全任务链");
                messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = "你尚未成功定稿一条季度剧情任务链。请继续调用工具，补齐所有校验要求并执行 finalize_story_quest；定稿成功后再输出季度江湖传闻。"
                });
                continue;
            }

            finalSummary = CleanSummary(response.Content ?? "");
            GameLogger.AI($"[季度Agent] 完成: {TruncateForLog(finalSummary, 1000)}");
            OnAgentFinish?.Invoke(finalSummary);
            break;
        }

        if (finalSummary == null)
        {
            finalSummary = "这一季江湖风平浪静，并无新的故事线展开。";
            OnAgentFinish?.Invoke(finalSummary);
            GameLogger.AI("[季度Agent] 达到最大迭代次数或出错，使用默认总结");
        }

        GameLogger.AI("=== 季度 Agent 结束 ===");
        return finalSummary;
    }

    /// <summary>从工具返回JSON提取 narration 字段(玩家视角文案);无则返回空。</summary>
    private static string ExtractNarration(string toolResultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);
            if (doc.RootElement.TryGetProperty("narration", out var n) && n.ValueKind == JsonValueKind.String)
                return n.GetString() ?? "";
        }
        catch { }
        return "";
    }

    private static string TruncateForLog(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...(截断)";

    /// <summary>清理AI总结开头的前导套话,让玩家直接看到正文。与月度同逻辑。</summary>
    private static string CleanSummary(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var s = raw.Trim();
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 5)
        {
            changed = false;
            var nl = s.IndexOf('\n');
            if (nl >= 0 && nl < 40)
            {
                var firstLine = s[..nl].Trim();
                if (firstLine.EndsWith('：') || firstLine.EndsWith(':'))
                {
                    if (firstLine.Contains("总结") || firstLine.Contains("如下") || firstLine.Contains("以下") || firstLine.Contains("完毕") || firstLine.Contains("纪事"))
                    {
                        s = s[(nl + 1)..].TrimStart();
                        changed = true;
                        continue;
                    }
                }
            }
            var prefixes = new[] { "好的", "好的！", "好", "明白了", "明白", "收到", "了解" };
            foreach (var p in prefixes)
            {
                if (s.StartsWith(p))
                {
                    var rest = s[p.Length..];
                    int idx = -1;
                    foreach (var ch in new[] { '，', ',', '。', '.', '！', '!', '：', ':' })
                    {
                        int i = rest.IndexOf(ch);
                        if (i >= 0 && (idx < 0 || i < idx)) idx = i;
                    }
                    if (idx >= 0) { s = rest[(idx + 1)..].TrimStart(); changed = true; }
                    break;
                }
            }
            if (s.StartsWith("所有") && (s.Contains("完毕") || s.Contains("生成") || s.Contains("定稿")))
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

    private string BuildAgentSystemPrompt()
    {
        return "你是一个金庸武侠世界的季度剧情总编 Agent。你的职责是基于当前江湖人物的经历、属性、关系，策划并完整落地 1 条可游玩的剧情任务链。季度内容宁可少而精：必须有因果、冲突、转折、收束与可获得的奖励。\n\n" +
               "【核心原则】\n" +
               "1. 必须围绕当前可互动人物的恩怨、关系或近期经历，写出明确动机、矛盾升级、一次反转与收束；不得只拼接跑腿和战斗。\n" +
               "2. 任务固定为3-6步：推荐“触发对话→调查/赴地→冲突或收集→转折对话→收束”。仅可使用 talk/fight/spar/kill/spare/go/mine/submit；不要使用 dungeon 或 meditate。\n" +
               "3. 引用的NPC/场景/武功/物品ID必须真实存在——先用 query_world_elements 查询确认,不要编造不存在的ID\n" +
               "4. 触发NPC(triggerNpcId)必须是现存活着的NPC，任务不设前置/互斥条件；玩家与之对话即可立即接取\n" +
               "5. 奖励武功/物品必须是现存的(martialArtId用query_world_elements查),不要编造\n" +
               "6. 必须用 set_quest_dialogue 设置至少两段开场对话、一个转折步骤对话和一段结局对话；台词须使用真实 NPC ID、“旁白”或“玩家”。\n" +
               "7. aiHint字段写该步骤发布者/目标NPC的剧情态度提示,让对话贴合剧情\n" +
               "8. 奖励必须克制且有意义：最终奖励可在银两、阅历、声望、现有武功、现有物品中组合；善恶奖励控制在±20。不要设计互斥分支，单季度只制作一条完整主链。\n\n" +
               "【工作流程】\n" +
               "1. 先用 query_world_elements 查询可用NPC/场景/武功/物品(防幻觉)\n" +
               "2. 用 create_story_quest 创建1个任务骨架（questId 以 quarter_ 开头，创建时必须带有意义的 finalReward）\n" +
               "3. 用 add_quest_step 添加3-6个可达步骤；submit 步骤必须带 requiredItems，且只能提交玩家当前持有的物品\n" +
               "4. 用 set_quest_dialogue 写 intro、一个转折 stepId 与 complete 的RPG台词\n" +
               "5. 用 list_draft_quests 自查，再用 finalize_story_quest 定稿；若工具报错必须修正后重试\n" +
               "6. 定稿后输出一段给玩家的季度江湖传闻，不剧透关键反转。\n\n" +
               "【重要】\n" +
               "- 你生成的是运行时任务,会加入游戏可接取池,玩家真能玩到,务必认真设计\n" +
               "- 大事件应基于当前世界状态: 若两派仇敌正在某地,可设计冲突事件;若某NPC有未解恩怨,可设计了断事件\n" +
               "- 不要重复已有任务,要创新剧情\n" +
               "- 总结要简洁有悬念,不要剧透任务全部细节,留待玩家探索\n" +
               "- 当所有任务定稿后,直接输出总结正文,不要加任何前导语(如'好的'/'以下是总结'/'所有任务已生成'等套话),直接写江湖传闻\n";
    }

    private string BuildInitialContext(GameState state)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"当前时间：{state.GameTime.QuarterDisplay}（第{state.GameTime.Day}天）");
        sb.AppendLine("本次季度剧情任务生成：只定稿一条完整、可游玩的任务链。");
        sb.AppendLine();

        // 玩家状态
        var p = state.Player;
        sb.AppendLine("═══ 玩家状态 ═══");
        sb.AppendLine($"姓名：{p.Name} | 门派：{state.GetFactionName(p.FactionId, "无")} | 善恶：{p.Karma} | 声望：{p.Reputation}");
        sb.AppendLine($"攻击：{p.GetTotalAttack()} 防御：{p.GetTotalDefense()} 速度：{p.GetTotalSpeed()}");
        sb.AppendLine($"武功：{(p.LearnedArts.Count > 0 ? string.Join(", ", p.LearnedArts.Select(a => $"{a.Name}Lv{a.Level}")) : "无")}");
        sb.AppendLine($"当前场景：{state.CurrentSceneId}");
        sb.AppendLine($"已接任务：{(p.QuestLog.Count > 0 ? string.Join(", ", p.QuestLog.Select(q => q.Name)) : "无")}");
        sb.AppendLine();

        // 所有NPC详情(姓名/性格/门派/善恶/位置/武功/关系)
        sb.AppendLine("═══ 江湖人物 ═══");
        foreach (var (id, npc) in state.AllNPCs)
        {
            if (!npc.IsAlive || npc.IsHidden) continue;
            sb.AppendLine($"\n【{npc.Name}】(ID:{id})");
            sb.AppendLine($"  性格：{npc.Personality}");
            sb.AppendLine($"  门派：{state.GetFactionName(npc.FactionId, "无")}  善恶：{npc.Karma}  位置：{npc.DefaultSceneId}");
            sb.AppendLine($"  武功：{(npc.LearnedArts.Count > 0 ? string.Join(", ", npc.LearnedArts.Select(a => $"{a.Name}Lv{a.Level}")) : "无")}");
            var recentEvents = npc.GetRecentLifeEvents(3);
            if (!string.IsNullOrEmpty(recentEvents))
                sb.AppendLine($"  近况：{recentEvents}");
        }

        // 现存场景ID清单(供AI引用)
        sb.AppendLine("\n═══ 现存场景ID清单(供targetScene引用) ═══");
        sb.AppendLine(string.Join(", ", state.AllScenes.Keys));

        // 现存武功ID清单
        sb.AppendLine("\n═══ 现存武功ID清单(供奖励martialArtId引用,可用query_world_elements查详情) ═══");
        sb.AppendLine(string.Join(", ", _config.MartialArts.Keys));

        // 现存物品ID清单
        sb.AppendLine("\n═══ 现存物品ID清单(供奖励items引用) ═══");
        sb.AppendLine(string.Join(", ", _config.Items.Keys));

        // 内置剧情任务格式模板(2个范例)
        sb.AppendLine("\n═══ 剧情任务格式范例(参照此格式生成) ═══");
        if (_config.Quests.TryGetValue("fuwei_revenge", out var tpl1))
            sb.AppendLine($"范例1 福威镖局灭门案:\n{JsonSerializer.Serialize(tpl1, new JsonSerializerOptions { WriteIndented = true })}");
        if (_config.Quests.TryGetValue("shediao_main", out var tpl2))
            sb.AppendLine($"范例2 射雕英雄传:\n{JsonSerializer.Serialize(tpl2, new JsonSerializerOptions { WriteIndented = true })}");

        sb.AppendLine("\n请基于以上江湖人物和世界状态，创作一条3-6步的完整剧情链。先查询世界元素，再创建、添加步骤、写开场/转折/结局对话、定稿。所有步骤必须可在现有玩法中完成；完成后输出季度江湖传闻。");
        return sb.ToString();
    }
}
