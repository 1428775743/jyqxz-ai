using System.Text.Json;
using AutoWuxia.AI;
using AutoWuxia.Config;
using AutoWuxia.Core;

namespace AutoWuxia.Systems;

/// <summary>
/// 年度 Agent 系统 —— 每游戏年(12个月)触发一次,生成 2-3 个江湖大事件(长链式剧情任务)。
/// 用 AnnualAgentTools 剧情编辑器工具集让 AI 像编辑器一样拼装/修改任务。
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

    /// <summary>检查是否应该触发年度更新(满12个月)</summary>
    public bool ShouldTriggerAnnual(GameState state)
    {
        return state.GameTime.IsNewYear(Math.Max(1, state.LastAnnualUpdateDay));
    }

    /// <summary>执行年度 Agent 循环,生成大事件剧情任务。返回给玩家的总结文本。</summary>
    public async Task<string> ExecuteAnnualUpdate(GameState state)
    {
        GameLogger.AI("══════════════════════════════════════════");
        GameLogger.AI($"=== 年度 Agent 开始 === {state.GameTime.YearDisplay}");

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
            GameLogger.AI($"[年度Agent循环] 第 {iteration + 1}/{maxIterations} 次迭代");
            OnToolCall?.Invoke($"岁末沉思... (第{iteration + 1}轮)");

            var response = await _ai.ChatWithToolsAsync(
                systemPrompt, messages, toolDefs,
                modelOverride: null);

            if (response.FinishReason == "error")
            {
                GameLogger.AI($"[年度Agent] 错误: {response.Content}");
                OnAgentError?.Invoke($"年度Agent错误: {response.Content}");
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
                    GameLogger.AI($"[年度Agent] 调用工具: {toolCall.FunctionName}({toolCall.FunctionArguments})");
                    // 玩家视角: 不显示"调用工具X",只更新状态
                    OnToolCall?.Invoke($"江湖暗流涌动...");

                    var toolResult = tools.ExecuteTool(toolCall.FunctionName, toolCall.FunctionArguments);
                    GameLogger.AI($"[年度Agent] 工具结果: {TruncateForLog(toolResult, 500)}");

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

            finalSummary = CleanSummary(response.Content ?? "");
            GameLogger.AI($"[年度Agent] 完成: {TruncateForLog(finalSummary, 1000)}");
            OnAgentFinish?.Invoke(finalSummary);
            break;
        }

        if (finalSummary == null)
        {
            finalSummary = "这一年江湖风平浪静，并无大事发生。";
            OnAgentFinish?.Invoke(finalSummary);
            GameLogger.AI("[年度Agent] 达到最大迭代次数或出错，使用默认总结");
        }

        GameLogger.AI("=== 年度 Agent 结束 ===");
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
        return "你是一个金庸武侠世界的年度大事件生成Agent。你的职责是基于当前江湖所有人物的经历、属性、关系，构思并生成 2-3 个江湖大事件——即长链式剧情任务(和射雕/神雕/倚天剧情任务同格式)，让玩家可接取游玩。\n\n" +
               "【核心原则】\n" +
               "1. 大事件要有武侠味: 有因果(基于现有人物恩怨/关系)、有冲突(正邪/情仇/夺宝)、有像样的奖励(武功秘籍/珍稀物品/声望)\n" +
               "2. 每个事件 3-8 个步骤,步骤类型用 talk/fight/go/kill/meditate/dungeon 等,节奏起伏(对话→战斗→探索→决战)\n" +
               "3. 引用的NPC/场景/武功/物品ID必须真实存在——先用 query_world_elements 查询确认,不要编造不存在的ID\n" +
               "4. 触发NPC(triggerNpcId)必须是现存活着的NPC,玩家与之对话(满足好感度)即可接取\n" +
               "5. 奖励武功/物品必须是现存的(martialArtId用query_world_elements查),不要编造\n" +
               "6. aiHint字段写该步骤发布者/目标NPC的剧情态度提示,让对话贴合剧情\n" +
               "7. 奖励要设 karmaBonus:正派/行侠任务+(5~15)、邪派/为恶任务−(5~15)。可设计'恶人线分支'(与主角为敌,karmaBonus为负,reward含邪派武功/重宝),用 exclusiveWithQuestIds 指明与对应正线互斥(正线亦指向恶线,接一不可接二)\n\n" +
               "【工作流程】\n" +
               "1. 先用 query_world_elements 查询可用NPC/场景/武功/物品(防幻觉)\n" +
               "2. 用 create_story_quest 创建2-3个任务骨架(每个有独特questId)\n" +
               "3. 用 add_quest_step 为每个任务添加3-8个步骤(可带节点奖励和aiHint)\n" +
               "4. 用 update_quest_step / remove_quest_step 反复打磨剧情,确保连贯合理\n" +
               "5. 用 finalize_story_quest 定稿每个任务(注册到可接取池)\n" +
               "6. 可用 list_draft_quests 回顾正在编辑的任务\n" +
               "7. 全部定稿后,输出一段给玩家的年度总结(江湖传闻风格,如'这一年,某地似有大事件发生...')\n\n" +
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
        sb.AppendLine($"当前时间：{state.GameTime.YearDisplay}（第{state.GameTime.Day}天）");
        sb.AppendLine($"本次年度大事件生成。");
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
            if (!npc.IsAlive) continue;
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

        sb.AppendLine("\n请基于以上江湖人物和世界状态,构思2-3个有武侠味的大事件,用工具集创建并定稿。完成后输出年度总结。");
        return sb.ToString();
    }
}
