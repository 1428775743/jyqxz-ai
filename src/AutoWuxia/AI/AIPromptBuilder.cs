using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Core;
using AutoWuxia.Items;
using AutoWuxia.Quests;
using AutoWuxia.World;

namespace AutoWuxia.AI;

public class AIPromptBuilder
{
    public static string BuildMonthlySystemPrompt()
    {
        return "你是一个金庸武侠世界的江湖演化系统。你需要根据所有NPC的当前状态、经历和关系，合理安排每个NPC下个月的变化。\n" +
               "规则：\n" +
               "1. 属性变化要合理，每次攻击/防御变化不超过±20，速度不超过±5\n" +
               "2. 武功熟练度每月最多提升2层，且不能超出当前武功最大熟练度\n" +
               "3. 位置变动要合理，不能跳跃太远，且要符合NPC身份\n" +
               "4. 经历描述要符合武侠风格，不超过50字\n" +
               "5. 如果有江湖大事，可以写worldEvent，不超过80字\n" +
               "请始终以JSON格式返回结果。";
    }

    public static string BuildSystemPrompt()
    {
        return "你是一个金庸武侠世界的NPC行为决策系统。你需要根据NPC的性格、履历、当前关系、武力值等信息，做出合理的行为决策。请始终以JSON格式返回你的决策结果。";
    }

    // ── 对话系统 Prompt（缓存优化版）──

    /// <summary>
    /// 构建 NPC 身份系统提示（静态，放在 system prompt，可被 prefix cache 复用）
    /// 包含：对话规则 + NPC 身份信息（姓名、性格、门派、description）
    /// </summary>
    public static string BuildNPCIdentityPrompt(NPC npc, string? factionName = null)
    {
        var factionDisplay = factionName ?? npc.FactionId ?? "无门无派";
        var desc = string.IsNullOrEmpty(npc.Description) ? "一位江湖中人" : npc.Description;

        return "你是一个金庸武侠世界的NPC扮演系统。你需要完全代入指定的NPC角色，根据其性格、经历、关系、当前状态来\"思考\"和\"说话\"。\n\n" +
               $"【你扮演的NPC】\n" +
               $"- 姓名：{npc.Name}\n" +
               $"- 性格：{npc.Personality}\n" +
               $"- 门派：{factionDisplay}\n" +
               $"- 简介：{desc}\n\n" +
               "【核心要求】\n" +
               "1. 必须先写出NPC的内心独白(thinking)，展现其思考过程：考虑与玩家的关系、最近经历、自身性格、当前场景\n" +
               "2. 然后生成NPC的对话(dialogue)，简洁有力，符合武侠小说风格，每次不超过100字\n" +
               "3. 根据情境决定是否触发行为(action)\n\n" +
               "【行为类型 action】\n" +
               "- \"none\": 仅对话，无额外行为（大多数情况）\n" +
               "- \"spar\": NPC主动提出切磋。条件：好感度>0，且NPC性格好战或武痴\n" +
               "- \"attack\": NPC主动攻击。条件：仇敌关系，或被严重冒犯，或善恶值极低\n" +
               "- \"give_item\": NPC赠送物品给玩家。条件：好感度>30，且NPC背包中有该物品。actionTarget必须填【NPC背包】中列出的物品ID（如healing_pill_small），不可用中文名或拼音\n" +
               "- \"ask_item\": NPC向玩家索要物品。条件：NPC确实需要且好感度>20。actionTarget必须填【玩家背包】中列出的物品ID\n" +
               "- \"teach_art\": NPC传授武功。【非常严格】必须同时满足：\n" +
               "  1. 好感度>80（极高信任）\n" +
               "  2. 同门派，且NPC是掌门或资深前辈\n" +
               "  3. 传授的武功必须是NPC会的且玩家不会的（参考武功列表）\n" +
               "  4. 名门正派（少林/武当）要求更高：好感度>90，且玩家善恶值符合门派要求\n" +
               "  5. NPC性格谨慎/保守的更难传授，性格豪爽/师者风范的较容易\n" +
               "  6. 如果teach_art_cooldown_active为true，绝对不能传授，但可以表达将来愿意教的意思\n" +
               "  7. 大多数对话中NPC不应该主动传授武功，除非关系已非常密切\n" +
               "- \"end_dialogue\": NPC想结束对话。条件：好感度低、有事在身、时间已晚想休息、或被冒犯\n" +
               "- \"heal\": NPC治疗玩家。仅限医者类NPC（medicine_merchant/imperial_doctor/wandering_doctor）\n" +
               "  治疗规则：\n" +
               "  - medicine_merchant：只能治疗普通中毒(poison)，不能治剧毒(severe_poison)和重伤(heavy_injury)\n" +
               "  - imperial_doctor：可治剧毒和重伤，但需好感度>20或玩家声望>500\n" +
               "  - wandering_doctor：可治一切问题，包括移除阉人标签恢复身躯\n" +
               "  - 治疗时NPC会在对话中说明治疗方案，然后行动\n" +
               "  - actionTarget为要治疗的标签ID: poison/severe_poison/heavy_injury/eunuch\n" +
               "- \"castrate\": 自宫手术。仅限eunuch_surgeon角色\n" +
               "  - 当玩家请求自宫时执行，会加上永久阉人标签\n" +
               "  - NPC会劝诫玩家三思，如果玩家坚持则执行\n" +
               "- \"query_location\": 查询某NPC的下落。仅限百晓阁门人(baixiao_informer角色)\n" +
               "  - 当玩家打听某人在哪里时，必须立即触发 query_location，不要先进行多轮报价、收钱对话\n" +
               "  - actionTarget 必须是被查询NPC的ID(从【可查询的江湖人物】列表中选)\n" +
               "  - 收费按目标江湖等级浮动(50~500两)，后端会弹出付费确认并自动扣款，不要要求玩家另行付钱\n" +
               "  - NPC只需简短报出目标姓名(用中文名)，后端会完成报价、扣款并补充真实位置\n" +
               "  - 若玩家银两不足,NPC应拒绝并提示攒够再来\n\n" +
               "【对话内容约束 - 非常重要】\n" +
               "- NPC只能聊自己description中提到的已知事件、人物、经历，不能编造description以外的奇遇、宝藏、神秘武器、古墓等\n" +
               "- 禁止主动提及任何奇遇、宝藏、神秘武功秘籍、隐藏地点等信息（这些由任务系统单独管理）\n" +
               "- NPC讲述的故事必须基于金庸武侠世界中的真实人物和事件\n" +
               "- 如果玩家追问NPC不知道的事情，NPC应该坦诚说不知道或只是听过传闻，不能编造细节\n" +
               "- NPC可以聊日常生活、对时局的看法、对来往江湖人物的评价、过去的真实经历\n\n" +
               "【限制】\n" +
               "- favorChange: -10~+10，根据对话内容判断。夸奖/帮助→正，冒犯/威胁→负，默认0\n" +
               "- give_item/ask_item 的 actionTarget 必须是上方【背包】中列出的物品ID（如healing_pill_small），严禁使用中文名、拼音或编造的ID\n" +
               "- teach_art 的 actionTarget 必须是NPC会且玩家不会的武功ID\n" +
               "- 行为要符合NPC性格，不能随意赠送贵重物品或传授高阶武功\n" +
               "- goldSpent: NPC请客/打赏的银两，不超过NPC拥有的银两\n" +
               "- musicFee: 仅 action=play_music 时填写,乐师向玩家收取的赏钱(玩家付给你)。默认10~30两;仅好感度很高时免费(0);知名乐师可收50~100两。须不超过玩家持有银两\n" +
               "- craftFee: 仅 action=craft_medicine 时填写,药师炼药收取的工费(玩家付给你)。按配方feeRange区间定;好感越高收费越低;可免费(0)。须不超过玩家持有银两\n\n" +
               "【JSON格式】\n" +
               "{\"thinking\": \"内心独白\", \"dialogue\": \"NPC说的话\", \"action\": \"none\", \"actionTarget\": null, \"favorChange\": 0, \"goldSpent\": 0, \"musicFee\": 0, \"craftFee\": 0, \"wantsToEnd\": false, \"endReason\": null}";
    }

    /// <summary>
    /// 构建动态上下文（放在 user message 前部，每次调用都变化）
    /// 包含：时间、场景、NPC/玩家当前状态、关系、对话历史
    /// </summary>
    public static string BuildDynamicContext(NPC npc, Player player, GameTime gameTime, DialogueHistory history, Scene? currentScene = null, string? playerFactionName = null, IEnumerable<NPC>? allNpcsForQuery = null, ConfigManager? config = null, IDictionary<string, NPC>? allNpcs = null)
    {
        var relation = npc.GetRelation(player.Id);
        bool playerHasSpouse = player.Relations.Values.Any(r => r.Type == RelationType.Spouse);
        bool playerHasMaster = player.Relations.Values.Any(r => r.Type == RelationType.Disciple);
        string npcGender = npc.Gender ?? "未知";
        string playerGender = player.Gender ?? "未知";
        string marryStatus = playerHasSpouse ? "已婚" : "未婚";
        string masterStatus = playerHasMaster ? "已拜师" : "无师父";
        var playerFactionDisplay = playerFactionName ?? player.FactionId ?? "无";

        // NPC动态状态
        var npcArtsText = BuildArtsText(npc);
        // 背包同时列出物品ID与中文名,供 give_item 的 actionTarget 使用(防AI用拼音/中文名幻觉出不存在的ID)
        var npcInvText = npc.Inventory.IsEmpty ? "空" : string.Join(", ", npc.Inventory.Items.Select(i => $"{i.Id}({i.Name})x{i.Quantity}"));
        var lifeEvents = npc.GetRecentLifeEvents(10);
        var lifeEventsText = string.IsNullOrEmpty(lifeEvents) ? "无特殊经历" : lifeEvents;

        // NPC会的武功ID列表（用于teach_art）
        var npcArtIds = string.Join(", ", npc.LearnedArts.Select(a => $"{a.Id}(Lv{a.Level})"));

        // 可传授的武功（NPC会且玩家不会）
        var playerArtIdSet = new HashSet<string>(player.LearnedArts.Select(a => a.Id));
        var teachableArts = npc.LearnedArts
            .Where(a => !playerArtIdSet.Contains(a.Id))
            .Select(a => $"{a.Id}({a.Name})");
        var teachableArtsText = teachableArts.Any() ? string.Join(", ", teachableArts) : "无（玩家已学会NPC所有武功）";

        // 传授冷却状态
        int daysSinceLastTeach = gameTime.Day - npc.LastTeachArtDay;
        bool teachCooldownActive = daysSinceLastTeach < 5;
        var teachCooldownText = teachCooldownActive ? $"是（距上次传授仅{daysSinceLastTeach}天，需等5天）" : "否（可传授）";

        // 玩家动态状态
        var playerArtsText = BuildArtsText(player);
        // 玩家背包同样列出ID,供 ask_item 的 actionTarget 使用
        var playerInvText = player.Inventory.IsEmpty ? "空" : string.Join(", ", player.Inventory.Items.Select(i => $"{i.Id}({i.Name})x{i.Quantity}"));
        var playerArtIds = string.Join(", ", player.LearnedArts.Select(a => a.Id));

        // 场景信息
        var sceneText = currentScene != null
            ? $"{currentScene.Name} - {currentScene.Description}"
            : "未知场景";

        // 对话历史
        var historyText = history.TotalMessages > 0
            ? $"\n【最近对话记录】\n{history.GetRecentContext(10)}"
            : "\n【最近对话记录】这是第一次对话。";

        var sb = new System.Text.StringBuilder();
        sb.Append($"【当前时间】{gameTime.Display}\n");
        sb.Append($"【当前场景】{sceneText}\n\n");
        sb.Append($"【NPC当前状态】\n");
        sb.Append($"善恶值：{npc.Karma} | 心情：{npc.Mood}\n");
        sb.Append($"性别：{npcGender}\n");
        sb.Append($"生命：{npc.CurrentHP}/{npc.GetTotalMaxHP()} | 内力：{npc.CurrentMP}/{npc.GetTotalMaxMP()}\n");
        sb.Append($"攻击：{npc.GetTotalAttack()} | 防御：{npc.GetTotalDefense()} | 速度：{npc.GetTotalSpeed()}\n");
        sb.Append($"武功：{npcArtsText}\n");
        sb.Append($"武功ID列表：{npcArtIds}\n");
        sb.Append($"可传授武功（NPC会且玩家不会）：{teachableArtsText}\n");
        sb.Append($"传授冷却中：{teachCooldownText}\n");
        sb.Append($"银两：{npc.Gold}\n");
        sb.Append($"背包：{npcInvText}\n");
        sb.Append($"最近经历：\n{lifeEventsText}\n\n");
        sb.Append($"【玩家当前状态】\n");
        sb.Append($"姓名：{player.Name} | 性别：{playerGender} | 门派：{playerFactionDisplay} | 善恶值：{player.Karma}\n");
        sb.Append($"婚姻/师徒：{marryStatus} | {masterStatus}\n");
        sb.Append($"健康度：{player.Health}/{player.MaxHealth}\n");
        sb.Append($"标签：{player.GetTagsSummary()}\n");
        sb.Append($"攻击：{player.GetTotalAttack()} | 防御：{player.GetTotalDefense()} | 速度：{player.GetTotalSpeed()}\n");
        sb.Append($"武功：{playerArtsText}\n");
        sb.Append($"武功ID列表：{playerArtIds}\n");
        sb.Append($"银两：{player.Gold}\n");
        sb.Append($"背包：{playerInvText}\n\n");
        sb.Append($"【双方关系】{relation.GetRelationDescription()}（好感度：{relation.Favorability}）\n");
        sb.Append($"{historyText}\n");

        // 百晓阁门人:注入可查询的江湖人物列表
        if (npc.NpcRole == "baixiao_informer" && allNpcsForQuery != null)
        {
            sb.Append("\n【你的特殊能力 - 百晓阁门人】\n");
            sb.Append("你是百晓阁门人,江湖百晓阁专门收集武林人物行踪。玩家可付费请你打听某人在哪里。\n");
            sb.Append("收费按目标江湖等级浮动:江湖等级1~10收费50两,11~25收费100~200两,26~50收费300两,50以上收费500两。\n");
            sb.Append("流程:玩家说出要查询的人名后,必须在本次回复立即使用 query_location,actionTarget 填该NPC的ID。后端会负责报价、弹出付费确认并自动扣款,不要让玩家在对话中反复确认或手动付钱。\n");
            sb.Append("若玩家银两不足,拒绝查询并提示攒够再来。\n\n");
            sb.Append("【可查询的江湖人物】(ID|姓名|江湖等级)\n");
            foreach (var n in allNpcsForQuery)
            {
                if (!n.IsAlive) continue;
                if (n.Id == npc.Id) continue;
                if (n.IsHidden) continue;
                sb.Append($"{n.Id} | {n.Name} | Lv{n.JianghuLevel}\n");
            }
            sb.Append("\n注意:actionTarget 必须从上面列表中选一个NPC的ID。\n");
        }

        // 任务上下文（发布者→全量进度；关联人→仅"发布者+任务名"）
        var questContext = BuildQuestContext(player, npc, config, allNpcs);
        if (!string.IsNullOrEmpty(questContext))
            sb.Append(questContext);

        // 乐师专属能力: play_music 技能说明 + 可演奏曲目列表(仅 musician 角色注入,
        // 不污染通用 prompt。仿百晓阁"你的特殊能力"块)
        if (npc.NpcRole == "musician")
        {
            sb.Append("\n【你的特殊能力 - 江湖乐师】\n");
            sb.Append("你是云游四方的江湖乐师,以演奏为生。你拥有专属技能 play_music——为玩家演奏一曲。\n");
            sb.Append("触发条件:好感度>-10(非仇敌)。当玩家请你演奏/唱曲/赏乐,或你心情好主动献艺时触发。\n");
            sb.Append("规则:\n");
            sb.Append("- action 必须为 \"play_music\"\n");
            sb.Append("- actionTarget 必须是下列文件名之一(直接用文件名,不要加路径)\n");
            sb.Append("- 根据你的性格与曲目风格自主挑选,不必每次都播同一首\n");
            sb.Append("- 演奏通常收取随缘赏钱(musicFee,玩家付给你)作为云游盘缠:默认收10~30两;仅当与玩家好感度很高(知交/知音)或对方确有难处时才免费(0);江湖知名乐师可收50~100两。须不超过玩家持有银两。在dialogue中自然带出价码(如\"一曲十两,请了\"或\"今日与你投缘,免费奏一曲\")\n");
            sb.Append("【可演奏曲目列表】\n");
            var musicFiles = AutoWuxia.Systems.AudioManager.ListMusicFiles();
            foreach (var f in musicFiles)
            {
                sb.Append($"- {Path.GetFileName(f)}\n");
            }
        }

        // 药师专属能力: craft_medicine(制药) + 配方清单
        // 触发角色: pharmacist(专职药师) 或顶级医者 wandering_doctor/imperial_doctor(既治伤又制药)
        if ((npc.NpcRole == "pharmacist" || npc.NpcRole == "wandering_doctor" || npc.NpcRole == "imperial_doctor") && config != null)
        {
            int medSkill = npc.GetCraftSkill("medicine");
            sb.Append("\n【你的特殊能力 - 制药】\n");
            sb.Append($"你精通药理(医术{medSkill}),可替玩家炼制丹药。专属技能 craft_medicine。\n");
            sb.Append("触发条件:玩家请你炼药/制丹/解毒,且你愿意接单(视好感与材料而定)。\n");
            sb.Append("规则:\n");
            sb.Append("- action 必须为 \"craft_medicine\",actionTarget 必须是下方【可炼配方】中的配方ID(直接用ID,不要中文名)\n");
            sb.Append("- craftFee 为你收取的工费(玩家付给你),在配方feeRange区间内定;好感越高可越低,知交可免费(0)。须不超过玩家持有银两\n");
            sb.Append("- 玩家需自备材料。玩家背包实际持有量见上方玩家状态(格式:物品ID(中文名)x数量),须以实际持有量为准,勿与下方配方需求量混淆。若材料不足,你不应接单,可在对话中告知缺什么\n");
            sb.Append("- 好感不足(minFavorability)或你的医术不够时,可委婉拒绝\n");
            sb.Append("- 在dialogue中自然透露你能炼什么药、需何材料、收多少工费\n");
            sb.Append("【可炼配方】(仅列出你医术可达的)\n");
            foreach (var r in config.MedicineRecipes.Values)
            {
                if (r.RequiredMedicineSkill > medSkill) continue;
                if (r.AllowedPharmacists != null && r.AllowedPharmacists.Count > 0 && !r.AllowedPharmacists.Contains(npc.Id)) continue;
                var mats = string.Join("、", r.Materials.Select(m =>
                    $"{(config.Items.TryGetValue(m.ItemId, out var it) ? it.Name : m.ItemId)}×{m.Quantity}"));
                string fee = r.FeeRange.Count >= 2 ? $"{r.FeeRange[0]}~{r.FeeRange[1]}" : "面议";
                sb.Append($"- {r.Name}({r.Tier}, 配方ID:{r.Id}): 材料 {mats}; 工费{fee}两; 需好感≥{r.MinFavorability}\n");
            }
        }

        // 厨师专属能力: craft_food(做菜) + 菜谱清单
        if (npc.NpcRole == "chef" && config != null)
        {
            int cookSkill = npc.GetCraftSkill("cooking");
            sb.Append("\n【你的特殊能力 - 烹饪】\n");
            sb.Append($"你精通厨艺(厨艺{cookSkill}),可替玩家烹制菜肴。专属技能 craft_food。\n");
            sb.Append("触发条件:玩家请你做菜/烹饪/备宴,且你愿意接单(视好感与材料而定)。\n");
            sb.Append("规则:\n");
            sb.Append("- action 必须为 \"craft_food\",actionTarget 必须是下方【可做菜谱】中的配方ID(直接用ID,不要中文名)\n");
            sb.Append("- craftFee 为你收取的工费(玩家付给你),在配方feeRange区间内定;好感越高可越低,知交可免费(0)。须不超过玩家持有银两\n");
            sb.Append("- 玩家需自备食材。玩家背包实际持有量见上方玩家状态(格式:物品ID(中文名)x数量),须以实际持有量为准,勿与下方配方需求量混淆。若食材不足,你不应接单,可在对话中告知缺什么\n");
            sb.Append("- 好感不足(minFavorability)或你的厨艺不够时,可委婉拒绝\n");
            sb.Append("- 在dialogue中自然透露你能做什么菜、需何食材、收多少工费\n");
            sb.Append("【可做菜谱】(仅列出你厨艺可达的)\n");
            foreach (var r in config.FoodRecipes.Values)
            {
                if (r.RequiredCookingSkill > cookSkill) continue;
                if (r.AllowedChefs != null && r.AllowedChefs.Count > 0 && !r.AllowedChefs.Contains(npc.Id)) continue;
                var mats = string.Join("、", r.Materials.Select(m =>
                    $"{(config.Items.TryGetValue(m.ItemId, out var it) ? it.Name : m.ItemId)}×{m.Quantity}"));
                string fee = r.FeeRange.Count >= 2 ? $"{r.FeeRange[0]}~{r.FeeRange[1]}" : "面议";
                sb.Append($"- {r.Name}({r.Tier}, 配方ID:{r.Id}): 食材 {mats}; 工费{fee}两; 需好感≥{r.MinFavorability}\n");
            }
        }

        // 铁匠专属能力: craft_forge(打造装备) + 配方清单
        // 触发角色: blacksmith(铁匠) 或 weapon_merchant(武器商,既卖兵器也接打造)
        if ((npc.NpcRole == "blacksmith" || npc.NpcRole == "weapon_merchant") && config != null)
        {
            int forgeSkill = npc.GetCraftSkill("forging");
            sb.Append("\n【你的特殊能力 - 打造】\n");
            sb.Append($"你精通锻造(锻造{forgeSkill}),可替玩家打造装备。专属技能 craft_forge。\n");
            sb.Append("触发条件:玩家请你打铁/锻造/打造兵器,且你愿意接单(视好感与材料而定)。\n");
            sb.Append("规则:\n");
            sb.Append("- action 必须为 \"craft_forge\",actionTarget 必须是下方【可造配方】中的配方ID(直接用ID,不要中文名)\n");
            sb.Append("- craftFee 为你收取的工费(玩家付给你),在配方feeRange区间内定;好感越高可越低,知交可免费(0)。须不超过玩家持有银两\n");
            sb.Append("- 玩家需自备矿石/材料。玩家背包实际持有量见上方玩家状态(格式:物品ID(中文名)x数量),须以实际持有量为准,勿与下方配方需求量混淆。若材料不足,你不应接单,可在对话中告知缺什么\n");
            sb.Append("- 好感不足(minFavorability)或你的锻造不够时,可委婉拒绝\n");
            sb.Append("- 你能打造上至稀有品阶的兵器(精钢/寒铁/烈焰/陨星,攻防最高约80),但珍贵/传说级神兵(倚天/屠龙/玄铁剑等剧情至宝)非你所能,可在对话中明示\n");
            sb.Append("- 在dialogue中自然透露你能打什么、需何材料、收多少工费\n");
            sb.Append("【可造配方】(仅列出你锻造可达的)\n");
            foreach (var r in config.ForgeRecipes.Values)
            {
                if (r.RequiredForgingSkill > forgeSkill) continue;
                if (r.AllowedBlacksmiths != null && r.AllowedBlacksmiths.Count > 0 && !r.AllowedBlacksmiths.Contains(npc.Id)) continue;
                var mats = string.Join("、", r.Materials.Select(m =>
                    $"{(config.Items.TryGetValue(m.ItemId, out var it) ? it.Name : m.ItemId)}×{m.Quantity}"));
                string fee = r.FeeRange.Count >= 2 ? $"{r.FeeRange[0]}~{r.FeeRange[1]}" : "面议";
                sb.Append($"- {r.Name}({r.Tier}, 配方ID:{r.Id}): 材料 {mats}; 工费{fee}两; 需好感≥{r.MinFavorability}\n");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建该NPC相关的任务上下文文本。
    /// - 若npc是某任务的发布者(TriggerNpcId): 注入任务名/描述/当前进度(步骤逐条✅⏳⬜)/奖励 → AI可主动询问/反馈进展
    /// - 若npc在某任务的RelatedNpcIds中(关联人): 仅注入"{发布者名}委托玩家处理「{任务名}」" → AI只能基于此寒暄,不会剧透
    /// 仅扫描玩家QuestLog中Status==InProgress的任务。
    /// </summary>
    public static string BuildQuestContext(Player player, NPC npc, ConfigManager? config, IDictionary<string, NPC>? allNpcs)
    {
        if (config == null) return "";
        var issuerLines = new List<string>();
        var relatedLines = new List<string>();
        var failedIssuerLines = new List<string>();
        var participantLines = new List<string>();

        foreach (var quest in player.QuestLog)
        {
            if (!config.Quests.TryGetValue(quest.Id, out var cfg)) continue;

            bool isIssuer = !string.IsNullOrEmpty(cfg.TriggerNpcId) && cfg.TriggerNpcId == npc.Id;
            bool isRelated = cfg.RelatedNpcIds != null && cfg.RelatedNpcIds.Contains(npc.Id);

            // 任务失败时,发布者仍需知道结果(影响态度)
            if (quest.Status == QuestStatus.Failed && isIssuer)
            {
                // 收集所有有 AiHint 的步骤(包括失败前未完成的)
                var hints = new List<string>();
                foreach (var s in quest.Steps)
                    if (!string.IsNullOrWhiteSpace(s.AiHint)) hints.Add(s.AiHint);
                var hintText = hints.Count > 0 ? "\n  剧情提示:\n    - " + string.Join("\n    - ", hints) : "";
                failedIssuerLines.Add(
                    $"- 任务名：{quest.Name} (已失败)\n" +
                    $"  描述：{quest.Description}\n" +
                    $"  失败原因:玩家违背了任务约束(如本应放过却杀死,本应到达却未到等)。{hintText}");
                continue;
            }

            if (quest.Status != QuestStatus.InProgress) continue;

            if (isIssuer)
            {
                var stepsSb = new System.Text.StringBuilder();
                var hints = new List<string>();
                for (int i = 0; i < quest.Steps.Count; i++)
                {
                    var s = quest.Steps[i];
                    string marker;
                    if (i < quest.CurrentStepIndex) marker = "✅";
                    else if (i == quest.CurrentStepIndex) marker = "⏳";
                    else marker = "⬜";
                    stepsSb.Append($"  {marker} 步骤{i + 1}: {s.Description}\n");
                    // 已完成步骤 + 当前步骤的 AiHint 都注入(过去与当下都构成你对玩家的认知)
                    if (i <= quest.CurrentStepIndex && !string.IsNullOrWhiteSpace(s.AiHint))
                        hints.Add(s.AiHint);
                }
                var rewardText = quest.Reward != null ? quest.Reward.GetSummary(config) : "无";
                var hintText = hints.Count > 0 ? "\n  剧情态度提示(必须遵循):\n    - " + string.Join("\n    - ", hints) : "";
                issuerLines.Add(
                    $"- 任务名：{quest.Name}\n" +
                    $"  描述：{quest.Description}\n" +
                    $"  当前进度：\n{stepsSb}" +
                    $"  最终奖励：{rewardText}" +
                    hintText);
            }
            else
            {
                // 通用剧情机制: 该NPC是否是任务某步骤的交互目标(战斗对手/谈话对象)?
                // 若是, 注入其参与步骤的 AiHint, 使其对话贴合剧情态度(适用于任意剧情任务)
                var participantHints = new List<string>();
                var participantSteps = new List<string>();
                for (int i = 0; i < quest.Steps.Count; i++)
                {
                    var s = quest.Steps[i];
                    if (!string.IsNullOrEmpty(s.TargetNPC) && s.TargetNPC == npc.Id && i <= quest.CurrentStepIndex)
                    {
                        participantSteps.Add(s.Description);
                        if (!string.IsNullOrWhiteSpace(s.AiHint))
                            participantHints.Add(s.AiHint);
                    }
                }
                if (participantSteps.Count > 0)
                {
                    var hintText = participantHints.Count > 0
                        ? "\n  你的处境与态度(必须遵循):\n    - " + string.Join("\n    - ", participantHints) : "";
                    participantLines.Add(
                        $"- 任务名：{quest.Name}\n" +
                        $"  你与此事的关联：你是「{string.Join("、", participantSteps)}」的当事人{hintText}");
                }
                else if (isRelated)
                {
                    // 仅听闻未直接参与: 保持一句话, 避免剧透
                    string issuerName = cfg.TriggerNpcId ?? "";
                    if (allNpcs != null && !string.IsNullOrEmpty(cfg.TriggerNpcId)
                        && allNpcs.TryGetValue(cfg.TriggerNpcId, out var issuerNpc))
                    {
                        issuerName = issuerNpc.Name;
                    }
                    relatedLines.Add($"- {issuerName} 委托玩家处理「{quest.Name}」");
                }
            }
        }

        if (issuerLines.Count == 0 && relatedLines.Count == 0
            && failedIssuerLines.Count == 0 && participantLines.Count == 0) return "";

        var sb = new System.Text.StringBuilder();
        if (issuerLines.Count > 0)
        {
            sb.Append("\n【你发布给玩家的任务（你完全知情，可主动询问进度/反馈结果。务必遵循『剧情态度提示』）】\n");
            foreach (var line in issuerLines) sb.Append(line).Append('\n');
        }
        if (failedIssuerLines.Count > 0)
        {
            sb.Append("\n【你发布的任务已失败(玩家违背约束)】\n");
            foreach (var line in failedIssuerLines) sb.Append(line).Append('\n');
        }
        if (participantLines.Count > 0)
        {
            sb.Append("\n【你参与的江湖事（你是此事的当事人——或是对手、或是对话对象。务必遵循『你的处境与态度』来演绎，但不要主动剧透任务全貌或奖励）】\n");
            foreach (var line in participantLines) sb.Append(line).Append('\n');
        }
        if (relatedLines.Count > 0)
        {
            sb.Append("\n【你听闻的江湖委托（你只听过这一句话，不知任何细节，不要主动剧透或编造内容）】\n");
            foreach (var line in relatedLines) sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// 构建开场白请求的 user message（动态上下文 + 开场白指令）
    /// </summary>
    public static string BuildDialogueStartPrompt(NPC npc, Player player, GameTime gameTime, DialogueHistory history, Scene? currentScene = null, string? playerFactionName = null, IEnumerable<NPC>? allNpcsForQuery = null, ConfigManager? config = null, IDictionary<string, NPC>? allNpcs = null)
    {
        var context = BuildDynamicContext(npc, player, gameTime, history, currentScene, playerFactionName, allNpcsForQuery, config, allNpcs);
        return context +
               "\n请决定NPC是否愿意与玩家对话，并生成开场白。返回JSON：\n" +
               "{\"thinking\": \"NPC内心思考\", \"willingToTalk\": true/false, \"dialogue\": \"开场白内容\", \"reason\": \"原因\"}";
    }

    /// <summary>
    /// 构建对话回复请求的 user message（动态上下文 + 玩家消息 + 回复指令）
    /// </summary>
    public static string BuildDialoguePrompt(NPC npc, Player player, string message, DialogueHistory history, GameTime gameTime, Scene? currentScene = null, string? playerFactionName = null, IEnumerable<NPC>? allNpcsForQuery = null, ConfigManager? config = null, IDictionary<string, NPC>? allNpcs = null)
    {
        var context = BuildDynamicContext(npc, player, gameTime, history, currentScene, playerFactionName, allNpcsForQuery, config, allNpcs);
        return context +
               $"\n【玩家刚说】{message}\n\n" +
               "请以NPC身份回复，注意：\n" +
               "1. 先写thinking（内心独白），结合关系、经历、性格、场景来思考\n" +
               "2. 再写dialogue（对话内容），简洁有力\n" +
               "3. 判断是否触发行为（大多数情况action为none）\n\n" +
               "返回JSON：\n" +
               "{\"thinking\": \"内心独白\", \"dialogue\": \"NPC的回复\", \"action\": \"none\", \"actionTarget\": null, \"favorChange\": 0, \"goldSpent\": 0, \"wantsToEnd\": false, \"endReason\": null}";
    }

    // ── 赠送系统 Prompt ──

    /// <summary>
    /// 赠送系统静态规则（放在 system prompt）
    /// </summary>
    public static string BuildGiftSystemPrompt()
    {
        return "你是一个金庸武侠世界的NPC扮演系统。玩家正在向你赠送物品，你需要根据NPC的性格、关系、经历、需求来决定是否接受。\n\n" +
               "【核心要求】\n" +
               "1. 先写thinking（内心独白）：思考是否该接受、与玩家关系如何、物品对自己是否有用\n" +
               "2. 生成dialogue（回应话语）：符合武侠风格\n" +
               "3. 决定accepted（是否接受）\n" +
               "4. 决定favorChange（好感度变化）：接受时根据物品价值和关系决定，通常+3~+15\n" +
               "5. 可选returnAction（接受后的额外行为）：\n" +
               "   - \"none\": 无\n" +
               "   - \"give_item\": 回赠物品（actionTarget为NPC背包中的物品ID）\n" +
               "   - \"teach_art\": 传授武功作为感谢（actionTarget为武功ID，需好感度>60）\n\n" +
               "【判断逻辑参考】\n" +
               "- 仇敌关系：绝对拒绝\n" +
               "- 好感度<-20：大概率拒绝\n" +
               "- 偏好匹配（如武者收到好酒、少林NPC收到佛经）：欣然接受\n" +
               "- 贵重秘籍且好感度不高：可能觉得太贵重而推辞\n" +
               "- 好感度高：基本都收，且好感度加成更多\n\n" +
               "【JSON格式】\n" +
               "{\"thinking\": \"内心独白\", \"accepted\": true/false, \"dialogue\": \"NPC的回应\", \"favorChange\": 0, \"returnAction\": \"none\", \"returnActionTarget\": null}";
    }

    /// <summary>
    /// 构建赠送请求的 user message（动态上下文 + 赠送物品信息）
    /// </summary>
    public static string BuildGiftPrompt(NPC npc, Player player, Item item, GameTime gameTime, Scene? currentScene = null)
    {
        var relation = npc.GetRelation(player.Id);
        var lifeEvents = npc.GetRecentLifeEvents(5);
        var lifeEventsText = string.IsNullOrEmpty(lifeEvents) ? "无特殊经历" : lifeEvents;

        var sceneText = currentScene != null ? $"{currentScene.Name}" : "未知场景";
        var npcArtIds = string.Join(", ", npc.LearnedArts.Select(a => $"{a.Id}(Lv{a.Level})"));
        var npcItemIds = npc.Inventory.IsEmpty ? "无" : string.Join(", ", npc.Inventory.Items.Select(i => $"{i.Id}x{i.Quantity}"));

        return $"【当前时间】{gameTime.Display}\n" +
               $"【当前场景】{sceneText}\n\n" +
               $"【NPC当前状态】\n" +
               $"善恶值：{npc.Karma} | 心情：{npc.Mood}\n" +
               $"偏好标签：{npc.GiftPreference ?? "无特殊偏好"}\n" +
               $"武功ID：{npcArtIds}\n" +
               $"背包物品：{npcItemIds}\n" +
               $"最近经历：\n{lifeEventsText}\n\n" +
               $"【玩家】姓名：{player.Name} | 门派：{player.FactionId ?? "无"} | 善恶值：{player.Karma}\n\n" +
               $"【双方关系】{relation.GetRelationDescription()}（好感度：{relation.Favorability}）\n\n" +
               $"【赠送物品】\n" +
               $"- 名称：{item.Name}\n" +
               $"- 类型：{item.Type}\n" +
               $"- 描述：{item.Description}\n" +
               $"- 价值：{item.Value}银两\n" +
               $"- 偏好标签：{item.GiftPreference ?? "无"}\n\n" +
               "请决定NPC是否接受这份礼物，返回JSON：\n" +
               "{\"thinking\": \"内心独白\", \"accepted\": true/false, \"dialogue\": \"NPC的回应\", \"favorChange\": 0, \"returnAction\": \"none\", \"returnActionTarget\": null}";
    }

    // ── NPC 决策 Prompt（非对话场景） ──

    public static string BuildNPCDecisionPrompt(NPC npc, Player player, GameTime gameTime, string? npcFactionName = null, string? playerFactionName = null)
    {
        var relation = npc.GetRelation(player.Id);
        var factionDisplay = npcFactionName ?? npc.FactionId ?? "无";
        var playerFactionDisplay = playerFactionName ?? player.FactionId ?? "无";

        return $"当前时间：{gameTime.Display}\n" +
               $"NPC信息：\n" +
               $"- 姓名：{npc.Name}\n" +
               $"- 性格：{npc.Personality}\n" +
               $"- 门派：{factionDisplay}\n" +
               $"- 善恶值：{npc.Karma}\n" +
               $"- 心情：{npc.Mood}\n" +
               $"- 攻击力：{npc.GetTotalAttack()}\n" +
               $"- 防御力：{npc.GetTotalDefense()}\n" +
               $"- 履历：{string.Join("；", npc.History.TakeLast(5))}\n\n" +
               $"玩家信息：\n" +
               $"- 姓名：{player.Name}\n" +
               $"- 门派：{playerFactionDisplay}\n" +
               $"- 善恶值：{player.Karma}\n" +
               $"- 攻击力：{player.GetTotalAttack()}\n" +
               $"- 防御力：{player.GetTotalDefense()}\n\n" +
               $"关系：{relation.GetRelationDescription()}（好感度：{relation.Favorability}）\n\n" +
               "请根据以上信息，决定NPC的行为，返回JSON格式：\n" +
               "{\"action\": \"talk/challenge/refuse/attack/idle\", \"reason\": \"原因\", \"dialogue\": \"如果action是talk，这里写对话内容\"}";
    }

    public static string BuildNPCSchedulePrompt(List<NPC> npcs, GameTime gameTime)
    {
        var npcInfo = string.Join("\n", npcs.Select(n =>
            $"- {n.Name}（性格：{n.Personality}，当前场景：{n.CurrentSceneId}）"));

        return $"请为以下NPC安排今天（{gameTime.Display}）各时辰的位置。\n" +
               $"NPC列表：\n{npcInfo}\n\n" +
               "请返回JSON格式：\n" +
               "{\"schedules\": [{\"npcId\": \"xxx\", \"location\": \"场景ID\", \"timePeriod\": \"morning/afternoon/evening\"}]}";
    }

    public static string BuildDynamicQuestPrompt(Player player, GameTime gameTime)
    {
        var factionDisplay = player.FactionId ?? "无";
        return $"当前时间：{gameTime.Display}\n" +
               $"玩家：{player.Name}，门派：{factionDisplay}，善恶值：{player.Karma}\n" +
               $"当前场景：{player.CurrentSceneId}\n\n" +
               "请生成一个合理的动态支线任务，返回JSON格式：\n" +
               "{\"questId\": \"dynamic_xxx\", \"name\": \"任务名称\", \"description\": \"任务描述\", " +
               "\"steps\": [{\"description\": \"步骤描述\", \"targetScene\": \"目标场景\", \"targetNPC\": \"目标NPC\"}]}";
    }

    // ── 门派审批 Prompt ──

    /// <summary>
    /// 构建门派加入审批的 System Prompt
    /// </summary>
    public static string BuildFactionJoinSystemPrompt()
    {
        return "你是一个金庸武侠世界的NPC掌门人扮演系统。有一位江湖人士请求拜入你的门下，你需要根据自身门派特点、玩家阅历和善恶值来决定是否收留。\n" +
               "\n【审批原则】\n" +
               "1. 根据门派特色和价值观判断：少林重佛缘善行，武当重道心悟性，明教重豪情义气\n" +
               "2. 玩家阅历等级(JianghuLevel)越高，说明江湖历练越丰富，越值得收留\n" +
               "3. 善恶值(Karma)要符合门派要求\n" +
               "4. 玩家历史经历中有不良记录（如背叛、偷窃）应谨慎考虑\n" +
               "5. 掌门人性格也会影响决定\n\n" +
               "【JSON格式】\n" +
               "{\"approved\": true/false, \"reason\": \"拒绝/同意的原因(30字内)\", \"dialogue\": \"掌门说的话(60字内)\"}";
    }

    /// <summary>
    /// 构建门派加入审批的 User Message
    /// </summary>
    public static string BuildFactionJoinPrompt(NPC npc, Player player, Faction faction)
    {
        var playerHistory = player.History.Count > 0
            ? string.Join("；", player.History.TakeLast(10))
            : "初出茅庐，尚无江湖经历";

        var playerArtsText = BuildArtsText(player);
        var playerCraftSkills = player.GetCraftSkillsSummary();

        return $"【你（掌门）】\n" +
               $"姓名：{npc.Name}\n" +
               $"性格：{npc.Personality}\n" +
               $"门派：{faction.Name}\n" +
               $"门派简介：{faction.Description}\n\n" +
               $"【申请人】\n" +
               $"姓名：{player.Name}\n" +
               $"阅历等级：{player.JianghuLevel}\n" +
               $"善恶值：{player.Karma}（门派要求：{faction.JoinKarmaMin}~{faction.JoinKarmaMax}）\n" +
               $"攻击：{player.GetTotalAttack()} | 防御：{player.GetTotalDefense()}\n" +
               $"武功：{playerArtsText}\n" +
               $"技艺：{playerCraftSkills}\n" +
               $"声望：{player.Reputation}\n" +
               $"健康度：{player.Health}/{player.MaxHealth}\n" +
               $"标签：{player.GetTagsSummary()}\n" +
               $"江湖经历：{playerHistory}\n\n" +
               $"请决定是否收留此人，返回JSON：\n" +
               "{\"approved\": true/false, \"reason\": \"原因(30字内)\", \"dialogue\": \"掌门说的话(60字内)\"}";
    }

    // ── NPC 战胜玩家后的处置决策 ──

    public static string BuildNPCVictorySystemPrompt()
    {
        return "你是一个金庸武侠世界的NPC扮演系统。你刚刚在生死搏杀中击败了这位玩家，现在要决定如何处置败者。\n" +
               "\n【决策原则】\n" +
               "1. 是否下杀手取决于:你与对方的关系(仇敌/敌对更可能杀)、自身善恶(恶人更易杀)、性格(嗜杀/狠辣/仁慈)、对方是否对你有利用价值\n" +
               "2. 心存仁慈或尚有利用价值时,可饶其性命,但应羞辱并索取赎金\n" +
               "3. 赎金(ransom)要符合江湖常理,依对方财力而定,通常在几十到几百两;不索要则填0\n" +
               "4. dialogue 为你此刻对败者说的话,武侠风格,符合自身性格,不超过40字\n" +
               "5. kill=true 表示你当场杀死对方(游戏结束),务必慎重\n\n" +
               "【JSON格式】\n" +
               "{\"kill\": true/false, \"ransom\": 整数, \"dialogue\": \"你的话(40字内)\"}";
    }

    public static string BuildNPCVictoryPrompt(NPC npc, Player player)
    {
        var rel = npc.GetRelation(player.Id);
        var relDesc = rel.GetRelationDescription();
        var playerHistory = player.History.Count > 0
            ? string.Join("；", player.History.TakeLast(8))
            : "初出茅庐,尚无江湖经历";

        return $"【你(胜利者)】\n" +
               $"姓名:{npc.Name}\n" +
               $"性格:{npc.Personality}\n" +
               $"善恶值:{npc.Karma}({Systems.KarmaSystem.GetKarmaDescription(npc.Karma)})\n" +
               $"门派:{npc.FactionId ?? "无门无派"}\n\n" +
               $"【败者(玩家)】\n" +
               $"姓名:{player.Name}\n" +
               $"阅历等级:{player.JianghuLevel}\n" +
               $"善恶值:{player.Karma}({Systems.KarmaSystem.GetKarmaDescription(player.Karma)})\n" +
               $"与你关系:{relDesc}(好感度{rel.Favorability})\n" +
               $"持有银两:{player.Gold}\n" +
               $"声望:{player.Reputation}\n" +
               $"江湖经历:{playerHistory}\n\n" +
               $"你已将其击败,生死由你定夺。请返回JSON:\n" +
               "{\"kill\": true/false, \"ransom\": 整数, \"dialogue\": \"你的话(40字内)\"}";
    }

    // ── 辅助方法 ──

    private static string BuildArtsText(Characters.CharacterBase character)
    {
        var parts = new List<string>();
        if (character.ActiveInternalArt != null)
            parts.Add($"内功:{character.ActiveInternalArt.Name}(Lv{character.ActiveInternalArt.Level})");
        if (character.ActiveExternalArt != null)
            parts.Add($"外功:{character.ActiveExternalArt.Name}(Lv{character.ActiveExternalArt.Level})");
        if (character.ActiveLightArt != null)
            parts.Add($"身法:{character.ActiveLightArt.Name}(Lv{character.ActiveLightArt.Level})");
        foreach (var art in character.LearnedArts)
        {
            if (art != character.ActiveInternalArt && art != character.ActiveExternalArt && art != character.ActiveLightArt)
                parts.Add($"{art.Name}(Lv{art.Level})");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "无";
    }
}

/// <summary>
/// NPC 对门派加入请求的审批结果
/// </summary>
public class FactionJoinDecision
{
    public bool Approved { get; set; }
    public string Reason { get; set; } = "";
    public string Dialogue { get; set; } = "";
}

/// <summary>
/// NPC 战胜玩家后的处置决策:是否杀死,索取赎金,以及对白。
/// </summary>
public class NPCVictoryDecision
{
    /// <summary>是否杀死玩家(true=游戏结束)</summary>
    public bool Kill { get; set; }
    /// <summary>索取的赎金(银两)。不杀时生效,0=不索要。会在玩家持有范围内扣取。</summary>
    public int Ransom { get; set; }
    /// <summary>NPC 的对白(武侠风,不超过40字)</summary>
    public string Dialogue { get; set; } = "";
}
