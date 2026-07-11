using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Core;
using AutoWuxia.Items;
using AutoWuxia.World;

namespace AutoWuxia.Systems;

public class DialogueSystem
{
    private readonly AIService _ai;
    private readonly GameState _state;
    private readonly ConfigManager? _config;

    public const double DialogueStaminaCost = 3;
    public const double DialogueTimeCost = 0.5;

    public DialogueSystem(AIService ai, GameState state, ConfigManager? config = null)
    {
        _ai = ai;
        _state = state;
        _config = config;
    }

    public DialogueHistory GetHistory(string npcId)
    {
        if (!_state.DialogueHistories.TryGetValue(npcId, out var history))
        {
            history = new DialogueHistory { NPCId = npcId };
            _state.DialogueHistories[npcId] = history;
        }
        return history;
    }

    /// <summary>
    /// 获取当前场景
    /// </summary>
    private Scene? GetCurrentScene()
    {
        _state.AllScenes.TryGetValue(_state.CurrentSceneId, out var scene);
        return scene;
    }

    /// <summary>
    /// 解析物品ID为中文名:优先玩家背包,其次物品配置。无法解析(幻觉的ID)返回null。
    /// </summary>
    public string? ResolveItemName(string itemId)
    {
        var invItem = _state.Player.Inventory.GetItem(itemId);
        if (invItem != null) return invItem.Name;
        if (_config != null && _config.Items.TryGetValue(itemId, out var cfg) && !string.IsNullOrEmpty(cfg.Name))
            return cfg.Name;
        return null;
    }

    /// <summary>
    /// 检查NPC是否处于对话拒绝冷却中
    /// </summary>
    public bool IsDialogueRefused(NPC npc, GameTime gameTime, out int remainingShiChen)
    {
        remainingShiChen = npc.DialogueRefuseUntilTotalShiChen - gameTime.TotalShiChen;
        return remainingShiChen > 0;
    }

    /// <summary>
    /// 设置NPC拒绝对话的冷却时间（时辰数）
    /// </summary>
    public void SetDialogueRefuse(NPC npc, GameTime gameTime, int shiChens)
    {
        npc.DialogueRefuseUntilTotalShiChen = gameTime.TotalShiChen + shiChens;
    }

    /// <summary>
    /// 开始对话 - AI判断NPC是否愿意对话
    /// </summary>
    public async Task<DialogueResponse> StartDialogue(NPC npc, Player player, GameTime gameTime)
    {
        var relation = npc.GetRelation(player.Id);
        GameLogger.Dialogue($"开始对话: {player.Name} -> {npc.Name} (关系:{relation.GetRelationDescription()}, 好感度:{relation.Favorability})");

        // 检查对话拒绝冷却
        if (IsDialogueRefused(npc, gameTime, out int remaining))
        {
            return new DialogueResponse
            {
                WillingToTalk = false,
                OpeningLine = $"{npc.Name}暂时不想和你对话（剩余{remaining}时辰）",
                Reason = "NPC想休息",
                Thinking = ""
            };
        }

        // 仇敌直接拒绝（不走AI）
        if (relation.Type == RelationType.Enemy)
        {
            return new DialogueResponse
            {
                WillingToTalk = false,
                OpeningLine = $"{npc.Name}怒目而视：\"你我仇深似海，还有什么好说的！\"",
                Reason = "仇敌关系",
                Thinking = "此人是我仇敌，绝无交谈可能。"
            };
        }

        // 尝试AI生成开场白
        try
        {
            var history = GetHistory(npc.Id);
            var scene = GetCurrentScene();
            var prompt = AIPromptBuilder.BuildDialogueStartPrompt(npc, player, gameTime, history, scene, _state.GetFactionName(player.FactionId, ""), _state.AllNPCs.Values, _config, _state.AllNPCs);
            var systemPrompt = AIPromptBuilder.BuildNPCIdentityPrompt(npc, _state.GetFactionName(npc.FactionId));
            GameLogger.Dialogue($"AI开场白请求 - SystemPrompt长度:{systemPrompt.Length}字, UserPrompt长度:{prompt.Length}字");
            var result = await _ai.ChatStructuredAsync<NPCAIOpeningResponse>(
                systemPrompt, prompt);

            if (result != null)
            {
                GameLogger.Dialogue($"AI开场白结果 - WillingToTalk:{result.WillingToTalk}, Thinking:{result.Thinking}, Dialogue:{result.Dialogue}");

                // NPC不想对话 → 设置12时辰拒绝冷却
                if (!result.WillingToTalk)
                {
                    SetDialogueRefuse(npc, gameTime, 12);
                    GameLogger.Dialogue($"NPC拒绝开场对话（{result.Reason}），设置12时辰拒绝冷却");
                }

                return new DialogueResponse
                {
                    WillingToTalk = result.WillingToTalk,
                    OpeningLine = result.Dialogue ?? $"{npc.Name}看了你一眼。",
                    Reason = result.Reason ?? "",
                    Thinking = result.Thinking ?? ""
                };
            }
        }
        catch (Exception ex)
        {
            GameLogger.Dialogue($"AI开场白异常: {ex.Message}");
        }

        // AI不可用时的默认逻辑
        return new DialogueResponse
        {
            WillingToTalk = true,
            OpeningLine = npc.GetGreeting(player),
            Reason = "默认",
            Thinking = ""
        };
    }

    /// <summary>
    /// 发送消息并获取NPC回复（使用统一AI响应格式）
    /// </summary>
    public async Task<DialogueReply> SendMessage(NPC npc, Player player, string message, GameTime gameTime)
    {
        var history = GetHistory(npc.Id);

        // 记录玩家消息
        history.AddRecord(player.Id, player.Name, message, gameTime.Display);

        // 记录NPC今日的社交经历（同一天只记一次，仅在玩家实际开口后写入，避免AI误以为玩家之前来过）
        var day = gameTime.Day;
        if (!npc.LifeEvents.Any(e => e.Day == day && e.Type == LifeEventType.Social))
        {
            npc.AddLifeEvent(day, LifeEventType.Social,
                $"今日{player.Name}前来拜访，与{npc.Name}交谈了一番。");
        }

        string npcReply = "";
        string npcThinking = "";
        string action = "none";
        string? actionTarget = null;
        bool wantsToEnd = false;
        string? endReason = null;
        int favorChange = 0;
        int goldSpent = 0;
        int musicFee = 0;
        int craftFee = 0;

        try
        {
            var scene = GetCurrentScene();
            var prompt = AIPromptBuilder.BuildDialoguePrompt(npc, player, message, history, gameTime, scene, _state.GetFactionName(player.FactionId, ""), _state.AllNPCs.Values, _config, _state.AllNPCs);
            var systemPrompt = AIPromptBuilder.BuildNPCIdentityPrompt(npc, _state.GetFactionName(npc.FactionId));
            GameLogger.Dialogue($"AI回复请求 - 玩家消息: {message}, SystemPrompt:{systemPrompt.Length}字, UserPrompt:{prompt.Length}字");
            var result = await _ai.ChatStructuredAsync<NPCAIResponse>(
                systemPrompt, prompt);

            if (result != null)
            {
                GameLogger.Dialogue($"AI回复结果 - Thinking:{result.Thinking}, Action:{result.Action}, Dialogue:{result.Dialogue}");

                npcReply = result.Dialogue ?? $"{npc.Name}沉默不语。";
                npcThinking = result.Thinking ?? "";
                action = result.Action ?? "none";
                actionTarget = result.ActionTarget;
                wantsToEnd = result.WantsToEnd;
                endReason = result.EndReason;
                favorChange = Math.Clamp(result.FavorChange, -10, 10);
                goldSpent = Math.Clamp(result.GoldSpent, 0, Math.Min(100, npc.Gold));
                // 乐师赏钱:玩家付给NPC,clamp到玩家可承受范围(0~min(玩家银两,500))
                musicFee = action == "play_music" ? Math.Clamp(result.MusicFee, 0, Math.Min(player.Gold, 500)) : 0;
                // 药师/厨师/铁匠工费:玩家付给NPC,clamp到玩家可承受范围(0~min(玩家银两,5000))
                craftFee = (action == "craft_medicine" || action == "craft_food" || action == "craft_forge") ? Math.Clamp(result.CraftFee, 0, Math.Min(player.Gold, 5000)) : 0;
            }
        }
        catch (Exception ex)
        {
            GameLogger.Dialogue($"AI回复异常: {ex.Message}");
        }

        if (string.IsNullOrEmpty(npcReply))
            npcReply = $"{npc.Name}点了点头，没有说话。";

        // 应用好感度变化
        if (favorChange != 0)
        {
            var rel = npc.GetRelation(player.Id);
            rel.ChangeFavorability(favorChange);
            GameLogger.Dialogue($"好感度变化: {(favorChange > 0 ? "+" : "")}{favorChange}, 当前: {rel.Favorability}");
        }

        // NPC想结束对话 → 设置12时辰拒绝冷却
        if (wantsToEnd)
        {
            SetDialogueRefuse(npc, gameTime, 12);
            GameLogger.Dialogue($"NPC想结束对话（{endReason}），设置12时辰拒绝冷却");
        }

        // 处理NPC赠送物品
        string? giftMessage = null;
        if (action == "give_item" && !string.IsNullOrEmpty(actionTarget))
        {
            if (npc.Inventory.HasItem(actionTarget))
            {
                var item = npc.Inventory.GetItem(actionTarget);
                if (item != null)
                {
                    var giftResult = GiftSystem.NPCGiftToPlayer(npc, player, item, gameTime);
                    if (giftResult.Success)
                    {
                        giftMessage = $"\n[{giftResult.Message}]";
                        GameLogger.Dialogue($"NPC赠送: {item.Name}");
                    }
                }
            }
            else
            {
                // 防AI幻觉: 物品不在NPC背包。若为合法配置物品,降级为none(不实际赠予,避免凭空刷物品);
                // 若是编造的ID(拼音/中文名),同样降级,避免向玩家展示原始ID
                GameLogger.Dialogue($"give_item物品NPC未持有或无效,降级为none: {actionTarget}");
                action = "none";
                actionTarget = null;
            }
        }

        // 处理NPC索要物品
        string? askMessage = null;
        if (action == "ask_item" && !string.IsNullOrEmpty(actionTarget))
        {
            var itemName = ResolveItemName(actionTarget);
            if (itemName == null)
            {
                // 无效物品ID(玩家没有且配置中也不存在),降级为none
                GameLogger.Dialogue($"ask_item物品ID无效,降级为none: {actionTarget}");
                action = "none";
                actionTarget = null;
            }
            else
            {
                askMessage = $"\n[{npc.Name}想要你的{itemName}]";
            }
        }

        // NPC开销银两
        if (goldSpent > 0 && npc.Gold >= goldSpent)
        {
            npc.Gold -= goldSpent;
            player.Gold += goldSpent;
            GameLogger.Dialogue($"NPC开销: {goldSpent}银两");
        }

        // 处理乐师演奏: play_music 的实际播放由 UI 层(MainForm)执行,
        // 此处仅校验文件名合法性后带出(防AI幻觉不存在的曲名)
        string? musicMessage = null;
        if (action == "play_music" && !string.IsNullOrEmpty(actionTarget))
        {
            var validFiles = Systems.AudioManager.ListMusicFiles()
                .Select(p => Path.GetFileName(p))
                .ToHashSet();
            if (validFiles.Contains(actionTarget))
            {
                // 收费时不在此预告"演奏了一曲"(需玩家先同意赏钱);免费时照旧在对话流中预告
                if (musicFee == 0)
                    musicMessage = $"\n[{npc.Name}为你演奏了一曲:{actionTarget}]";
                GameLogger.Dialogue($"乐师演奏: {actionTarget}, 赏钱: {musicFee}");
            }
            else
            {
                // AI幻觉了不存在的曲名,降级为 none,不播放(赏钱也清零)
                action = "none";
                musicFee = 0;
                GameLogger.Dialogue($"乐师play_music曲名无效,忽略: {actionTarget}");
            }
        }

        // 记录NPC回复
        history.AddRecord(npc.Id, npc.Name, npcReply, gameTime.Display);

        return new DialogueReply
        {
            Reply = npcReply + (giftMessage ?? "") + (askMessage ?? "") + (musicMessage ?? ""),
            Thinking = npcThinking,
            Action = action,
            ActionTarget = actionTarget,
            WantsToEnd = wantsToEnd,
            EndReason = endReason,
            FavorChange = favorChange,
            GoldSpent = goldSpent,
            MusicFee = musicFee,
            CraftFee = craftFee,
            MusicPlayed = (action == "play_music") ? actionTarget : null
        };
    }

    public bool CanContinueDialogue(Player player)
    {
        return player.Stamina >= DialogueStaminaCost;
    }

    /// <summary>
    /// 消耗对话体力和时间
    /// </summary>
    public bool ConsumeDialogueCost(Player player, GameTime gameTime)
    {
        if (!StaminaSystem.ConsumeStamina(player, DialogueStaminaCost))
            return false;
        gameTime.Advance(DialogueTimeCost);
        return true;
    }
}

public class DialogueReply
{
    public string Reply { get; set; } = "";
    public string? Thinking { get; set; }
    public string Action { get; set; } = "none";
    public string? ActionTarget { get; set; }
    public bool WantsToEnd { get; set; }
    public string? EndReason { get; set; }
    public int FavorChange { get; set; }
    public int GoldSpent { get; set; }

    /// <summary>乐师演奏收取的赏钱(玩家付给NPC),由UI层扣款。仅 play_music 时有效。</summary>
    public int MusicFee { get; set; }

    /// <summary>药师炼药收取的工费(玩家付给NPC),由UI层扣款。仅 craft_medicine 时有效。</summary>
    public int CraftFee { get; set; }

    /// <summary>乐师演奏的曲目文件名(仅当 action=play_music 且曲名合法时非空),由UI层播放。</summary>
    public string? MusicPlayed { get; set; }
}
