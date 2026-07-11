using AutoWuxia.Characters;
using AutoWuxia.Core;

namespace AutoWuxia.Quests;

/// <summary>
/// 任务自动推进系统：根据游戏事件自动检查并推进进行中的链式/节点任务步骤。
/// 支持的事件类型：
///   talk     - 与某NPC对话结束后
///   fight    - 与某NPC切磋/战斗胜利后
///   kill     - 战后玩家选择"杀死"对方
///   spare    - 战后玩家选择"放过"对方
///   meditate - 满足面壁条件后(场景+天数)
///   mine     - 挖矿成功后
///   go       - 到达某场景后
///   submit   - 提交物品后（仍走原有QuestBase.TrySubmitItems）
/// </summary>
public static class QuestAutoAdvance
{
    /// <summary>
    /// 尝试自动推进所有进行中的任务步骤。
    /// 返回触发推进的任务日志列表（用于UI提示）。
    /// </summary>
    public static (List<string> logs, List<DialogueScript> dialogues) TryAdvanceAll(Player player, string actionType, string? targetId, Config.ConfigManager? config = null)
    {
        var logs = new List<string>();
        var dialogues = new List<DialogueScript>();

        // 收集需要在循环外触发的副作用(避免循环中修改 QuestLog)
        var spareFailures = new List<QuestBase>();   // "放过田伯光"导致师门考验失败

        foreach (var quest in player.QuestLog)
        {
            if (quest.Status != QuestStatus.InProgress) continue;
            if (quest.CurrentStep == null) continue;

            var step = quest.CurrentStep;

            // ── 放过目标NPC: 若当前步骤要求"杀死"该NPC,则任务失败 ──
            if (actionType == "spare"
                && step.ActionType == "kill"
                && step.TargetNPC == targetId)
            {
                quest.Status = QuestStatus.Failed;
                logs.Add($"[{quest.Name}] 你放走了{targetId},未完成师门交代,任务失败!");
                spareFailures.Add(quest);
                continue;
            }

            // 检查 actionType 是否匹配当前步骤
            if (!IsStepMatch(step, actionType, targetId)) continue;

            // 尝试推进
            bool advanced = quest.TryAdvanceStep(player, config);
            if (advanced)
            {
                // 节点奖励提示
                if (step.Reward != null)
                {
                    string rewardSummary = step.Reward.GetSummary(config);
                    logs.Add($"[{quest.Name}] 步骤完成：{step.Description}（奖励：{rewardSummary}）");
                }
                else
                {
                    logs.Add($"[{quest.Name}] 步骤完成：{step.Description}");
                }

                // 该步骤完成时播放剧情对话(若有)
                if (step.Dialogue != null && step.Dialogue.HasContent)
                    dialogues.Add(step.Dialogue);

                // ── 副作用:推进到 meet_fengqingyang 步骤时,把风清扬从隐藏状态显形 ──
                if (quest.CurrentStep?.Id == "meet_fengqingyang")
                {
                    TryRevealFengQingyang(logs);
                }

                // ── 副作用:魔教任务推进到 final_choice 步骤时,触发黑木崖之变剧情 ──
                if (quest.Id == "riyue_dongfang_test" && quest.CurrentStep?.Id == "final_choice")
                {
                    EventSystem.Instance.Publish("quest.riyue_choice");
                    logs.Add("（崖后突然传来一阵狂笑——任我行、向问天和令狐冲三人破空而至!）");
                }

                // 任务整体完成提示
                if (quest.Status == QuestStatus.Completed)
                {
                    logs.Add($"[{quest.Name}] 全部步骤完成，可领取奖励！");
                    EventSystem.Instance.Publish(GameEvents.QuestCompleted,
                        new Dictionary<string, object?> { { "questId", quest.Id } });
                }
                else if (quest.CurrentStep != null)
                {
                    logs.Add($"[{quest.Name}] 下一步：{quest.CurrentStep.Description}");
                }
            }
        }

        // ── 善后:师门考验失败时,自动接取"思过崖面壁"任务 ──
        foreach (var failed in spareFailures)
        {
            if (failed.Id == "huashan_test" && config != null
                && !player.QuestLog.Any(q => q.Id == "siguoyai_meditation")
                && config.Quests.TryGetValue("siguoyai_meditation", out var meditationCfg))
            {
                var meditationQuest = ChainQuest.FromConfig(meditationCfg);
                player.AddQuest(meditationQuest);
                logs.Add($"[{meditationQuest.Name}] 师父震怒,罚你到思过崖面壁三十日!");
                if (meditationQuest.CurrentStep != null)
                    logs.Add($"[{meditationQuest.Name}] 当前步骤:{meditationQuest.CurrentStep.Description}");
            }
        }

        return (logs, dialogues);
    }

    /// <summary>
    /// 推进到 meet_fengqingyang 步骤时, 让风清扬出现在思过崖
    /// </summary>
    private static void TryRevealFengQingyang(List<string> logs)
    {
        EventSystem.Instance.Publish("quest.reveal_npc",
            new Dictionary<string, object?> { { "npcId", "feng_qingyang" } });
        logs.Add("（一道身影自思过崖石壁后走出——剑魔风清扬现身！）");
    }

    /// <summary>
    /// 检查步骤是否与当前事件匹配
    /// </summary>
    private static bool IsStepMatch(QuestStep step, string actionType, string? targetId)
    {
        return actionType switch
        {
            "talk" => step.ActionType == "talk" && step.TargetNPC == targetId,
            "fight" => (step.ActionType == "fight" || step.ActionType == "spar") && step.TargetNPC == targetId,
            "kill" => step.ActionType == "kill" && step.TargetNPC == targetId,
            "spare" => step.ActionType == "spare" && step.TargetNPC == targetId,
            "meditate" => step.ActionType == "meditate" && (step.TargetScene == targetId || string.IsNullOrEmpty(step.TargetScene)),
            "mine" => step.ActionType == "mine" && (step.TargetScene == targetId || string.IsNullOrEmpty(step.TargetScene)),
            "go" => step.ActionType == "go" && step.TargetScene == targetId,
            _ => false
        };
    }
}
