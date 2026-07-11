using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Items;
using AutoWuxia.World;

namespace AutoWuxia.Systems;

/// <summary>
/// 赠送/交易系统 - AI驱动NPC决策
/// </summary>
public class GiftSystem
{
    /// <summary>
    /// 玩家赠送物品给NPC（AI决策）
    /// </summary>
    public static async Task<GiftResult> PlayerGiftToNPC(
        Player player, NPC npc, Item item, GameTime gameTime, AIService ai, GameState state)
    {
        // 尝试AI决策
        NPCAIGiftResponse? aiResult = null;
        try
        {
            Scene? currentScene = null;
            state.AllScenes.TryGetValue(state.CurrentSceneId, out currentScene);

            var prompt = AIPromptBuilder.BuildGiftPrompt(npc, player, item, gameTime, currentScene);
            GameLogger.Dialogue($"AI赠送决策请求 - 物品:{item.Name}, Prompt长度:{prompt.Length}字");

            aiResult = await ai.ChatStructuredAsync<NPCAIGiftResponse>(
                AIPromptBuilder.BuildGiftSystemPrompt(), prompt);

            if (aiResult != null)
            {
                GameLogger.Dialogue($"AI赠送决策结果 - Accepted:{aiResult.Accepted}, Thinking:{aiResult.Thinking}, Favor:{aiResult.FavorChange}");
            }
        }
        catch (Exception ex)
        {
            GameLogger.Dialogue($"AI赠送决策异常: {ex.Message}");
        }

        // AI可用时使用AI结果，否则使用 fallback
        if (aiResult != null)
        {
            return ApplyAIGiftResult(player, npc, item, gameTime, aiResult);
        }
        else
        {
            return ApplyFallbackGiftResult(player, npc, item, gameTime);
        }
    }

    /// <summary>
    /// 应用AI赠送决策结果
    /// </summary>
    private static GiftResult ApplyAIGiftResult(Player player, NPC npc, Item item, GameTime gameTime, NPCAIGiftResponse aiResult)
    {
        if (!aiResult.Accepted)
        {
            return new GiftResult
            {
                Success = false,
                Message = aiResult.Dialogue ?? $"{npc.Name}摇了摇头。",
                Thinking = aiResult.Thinking ?? "",
                FavorChange = 0
            };
        }

        // 转移物品
        if (!player.Inventory.TransferTo(npc.Inventory, item.Id, 1))
        {
            return new GiftResult { Success = false, Message = "你没有这个物品。", Thinking = "" };
        }

        // 应用好感度变化（AI决定）
        int favorChange = Math.Clamp(aiResult.FavorChange, 0, 20);
        var rel = npc.GetRelation(player.Id);
        rel.ChangeFavorability(favorChange);

        // 记录经历
        npc.AddLifeEvent(gameTime.Day, LifeEventType.Social,
            $"收到{player.Name}赠送的{item.Name}，颇为欢喜。");

        // 处理AI的额外回赠行为
        string? returnMessage = null;
        if (aiResult.ReturnAction == "give_item" && !string.IsNullOrEmpty(aiResult.ReturnActionTarget))
        {
            var returnItem = npc.Inventory.GetItem(aiResult.ReturnActionTarget);
            if (returnItem != null)
            {
                var giftResult = NPCGiftToPlayer(npc, player, returnItem, gameTime);
                if (giftResult.Success)
                    returnMessage = $"\n[{giftResult.Message}]";
            }
        }

        return new GiftResult
        {
            Success = true,
            Message = aiResult.Dialogue ?? $"{npc.Name}微笑收下了。",
            Thinking = aiResult.Thinking ?? "",
            FavorChange = favorChange,
            ReturnMessage = returnMessage
        };
    }

    /// <summary>
    /// AI不可用时的降级方案（原有硬编码逻辑）
    /// </summary>
    private static GiftResult ApplyFallbackGiftResult(Player player, NPC npc, Item item, GameTime gameTime)
    {
        var decision = NPCDecideAcceptGiftFallback(npc, player, item);

        if (!decision.Accepted)
        {
            return new GiftResult
            {
                Success = false,
                Message = decision.RefuseMessage,
                Thinking = "",
                FavorChange = 0
            };
        }

        // 转移物品
        if (!player.Inventory.TransferTo(npc.Inventory, item.Id, 1))
        {
            return new GiftResult { Success = false, Message = "你没有这个物品。", Thinking = "" };
        }

        // 计算好感度变化
        int favorChange = CalculateGiftFavor(npc, item);

        // 改变关系
        var rel = npc.GetRelation(player.Id);
        rel.ChangeFavorability(favorChange);

        // 记录经历
        npc.AddLifeEvent(gameTime.Day, LifeEventType.Social,
            $"收到{player.Name}赠送的{item.Name}，颇为欢喜。");

        return new GiftResult
        {
            Success = true,
            Message = $"{npc.Name}{decision.AcceptMessage}",
            Thinking = "",
            FavorChange = favorChange
        };
    }

    /// <summary>
    /// NPC赠送物品给玩家
    /// </summary>
    public static GiftResult NPCGiftToPlayer(NPC npc, Player player, Item item, GameTime gameTime)
    {
        if (!npc.Inventory.HasItem(item.Id))
            return new GiftResult { Success = false, Message = $"{npc.Name}没有这个物品。" };

        if (!npc.Inventory.TransferTo(player.Inventory, item.Id, 1))
            return new GiftResult { Success = false, Message = "转移失败。" };

        npc.AddLifeEvent(gameTime.Day, LifeEventType.Social,
            $"将{item.Name}赠予了{player.Name}。");

        return new GiftResult
        {
            Success = true,
            Message = $"{npc.Name}将{item.Name}赠予了你。",
            FavorChange = 5
        };
    }

    // ── Fallback: 硬编码决策逻辑 ──

    private static GiftDecision NPCDecideAcceptGiftFallback(NPC npc, Player player, Item item)
    {
        var rel = npc.GetRelation(player.Id);
        int favor = rel.Favorability;

        // 仇敌绝对不收
        if (rel.Type == RelationType.Enemy)
            return GiftDecision.Refuse($"{npc.Name}冷笑道：\"你的东西，我才不要！\"");

        // 好感度太低大概率不收
        if (favor < -20 && Random.Shared.NextDouble() < 0.8)
            return GiftDecision.Refuse($"{npc.Name}摆了摆手：\"无功不受禄，收回去吧。\"");

        // 秘籍类物品：NPC可能觉得太贵重
        if (item.IsManual && item.Value > 1000 && favor < 50)
            return GiftDecision.Refuse($"{npc.Name}连连摆手：\"这等秘籍太过贵重，我岂能收下？\"");

        // 偏好匹配
        if (item.GiftPreference != null && npc.GiftPreference == item.GiftPreference)
        {
            return GiftDecision.Accept($"{npc.Name}眼前一亮：\"这正是我喜欢的，多谢了！\"");
        }

        // 好感度高基本都收
        if (favor >= 30)
            return GiftDecision.Accept($"{npc.Name}微笑收下：\"有心了，多谢。\"");

        // 默认有概率收
        if (Random.Shared.NextDouble() < 0.5)
            return GiftDecision.Accept($"{npc.Name}犹豫了一下，还是收下了：\"那就多谢了。\"");

        return GiftDecision.Refuse($"{npc.Name}摇了摇头：\"不必了，你的心意我领了。\"");
    }

    /// <summary>
    /// 计算赠送带来的好感度变化（fallback用）
    /// </summary>
    private static int CalculateGiftFavor(NPC npc, Item item)
    {
        int baseFavor = item.GiftFavorBonus;

        // 偏好匹配额外加成
        if (item.GiftPreference != null && npc.GiftPreference == item.GiftPreference)
            baseFavor += 5;

        // 贵重物品加成
        if (item.Value >= 500) baseFavor += 3;
        if (item.Value >= 2000) baseFavor += 5;

        return baseFavor;
    }
}

public class GiftResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string Thinking { get; set; } = "";
    public int FavorChange { get; set; }
    public string? ReturnMessage { get; set; }  // 回赠消息
}

internal class GiftDecision
{
    public bool Accepted { get; set; }
    public string AcceptMessage { get; set; } = "";
    public string RefuseMessage { get; set; } = "";

    public static GiftDecision Accept(string msg) => new() { Accepted = true, AcceptMessage = msg };
    public static GiftDecision Refuse(string msg) => new() { Accepted = false, RefuseMessage = msg };
}
