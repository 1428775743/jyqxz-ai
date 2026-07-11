using AutoWuxia.AI;
using AutoWuxia.Characters;
using AutoWuxia.Combat;
using AutoWuxia.Config;
using AutoWuxia.Systems;
using AutoWuxia.World;

namespace AutoWuxia.Core;

public enum PostCombatPhase
{
    None,
    PlayerWinChoice,
    NPCWinConsequence
}

public class PostCombatContext
{
    public PostCombatPhase Phase { get; set; }
    public string DefeatedNPCId { get; set; } = "";
    public CombatOutcome Outcome { get; set; }
    public bool WasSpar { get; set; }
    public bool OpponentHiddenPower { get; set; }
}

public class GameEngine
{
    public GameState State { get; private set; } = new();
    public ConfigManager Config { get; }
    public AIService AI { get; private set; }
    public NPCLocationManager LocationManager { get; } = new();
    public FactionSystem FactionSystem { get; private set; } = null!;
    public DialogueSystem DialogueSystem { get; private set; } = null!;
    public MonthlyUpdateSystem MonthlyUpdateSystem { get; private set; } = null!;
    public AnnualUpdateSystem AnnualUpdateSystem { get; private set; } = null!;
    public Quests.FactionQuestManager FactionQuests { get; private set; } = null!;
    public CombatEngine? CurrentCombat { get; private set; }
    public WorldMap? WorldMap { get; private set; }
    public PostCombatContext? PostCombat { get; private set; }

    /// <summary>
    /// 战斗中 NPC 胜利后,需异步由 AI 决策处置(杀/赎金/羞辱)。
    /// EndCombat 同步阶段只记录对手ID,由 CombatForm 关闭前调用 ProcessNPCVictoryAsync 完成。
    /// </summary>
    public string? PendingNPCVictoryOpponentId { get; private set; }

    public bool IsInCombat => CurrentCombat != null;
    public bool HasPostCombatChoices => PostCombat != null && PostCombat.Phase != PostCombatPhase.None;
    /// <summary>玩家是否已死亡（健康度耗尽）</summary>
    public bool PlayerIsDead { get; set; } = false;

    public event Action<string>? OnLog;

    private const int MaxSaveSlots = 5;

    public GameEngine(ConfigManager config)
    {
        Config = config;
        AI = new AIService(AIConfig.Load());
    }

    public void Initialize()
    {
        Logger.Info("GameEngine.Initialize 开始");
        Config.LoadAll();
        Logger.Info($"加载配置: {Config.Characters.Count}角色, {Config.Scenes.Count}场景, {Config.MartialArts.Count}武功, {Config.Factions.Count}门派");

        State.Player = Config.CreatePlayer();
        foreach (var (id, _) in Config.Characters)
        {
            if (id == "player_default") continue;
            try { State.AllNPCs[id] = Config.CreateNPC(id); }
            catch { }
        }

        // 初始化商贩商品
        foreach (var npc in State.AllNPCs.Values)
        {
            if (ShopSystem.IsMerchant(npc.NpcRole))
                ShopSystem.InitShopItems(npc, Config);
        }

        foreach (var (id, sceneConfig) in Config.Scenes)
            State.AllScenes[id] = Scene.FromConfig(sceneConfig);

        foreach (var (id, _) in Config.Factions)
        {
            var faction = Config.CreateFaction(id);
            if (faction != null) State.AllFactions[id] = faction;
        }

        FactionSystem = new FactionSystem(State.AllFactions);
        DialogueSystem = new DialogueSystem(AI, State, Config);
        FactionQuests = new Quests.FactionQuestManager(Config);
        FactionQuests.LoadFromConfig();
        MonthlyUpdateSystem = new MonthlyUpdateSystem(AI, AIConfig.Load(), Config, FactionQuests);
        AnnualUpdateSystem = new AnnualUpdateSystem(AI, AIConfig.Load(), Config);

        if (Config.WorldMap != null)
            WorldMap = AutoWuxia.World.WorldMap.FromConfig(Config.WorldMap, State.AllScenes);

        var firstScene = State.AllScenes.Values.FirstOrDefault();
        // 优先从刘家村开始
        if (State.AllScenes.TryGetValue("liujiacun_scene", out var startScene))
            firstScene = startScene;
        if (firstScene != null)
        {
            State.CurrentSceneId = firstScene.Id;
            State.Player.CurrentSceneId = firstScene.Id;
        }

        // 新档直接标记为当前版本(无需迁移)
        State.SaveVersion = GameState.CurrentSaveVersion;

        UpdateSceneNPCs();

        // 初始化NPC基础经历
        InitNPCLifeEvents();

        // 订阅任务剧情事件: 解除隐藏NPC的 isHidden 标记
        EventSystem.Instance.Subscribe("quest.reveal_npc", OnQuestRevealNpc);

        Log("═══════════════════════════════");
        Log("       【金庸群侠传】");
        Log("═══════════════════════════════");
        Log("欢迎来到武侠世界！");
        Log($"当前：{State.GameTime.Display}");
        Log("");
    }

    public void Log(string message) => OnLog?.Invoke(message);

    /// <summary>
    /// 任务剧情事件: 让指定的隐藏NPC现身于场景中
    /// </summary>
    private void OnQuestRevealNpc(object? sender, GameEventArgs e)
    {
        if (!e.Data.TryGetValue("npcId", out var idObj) || idObj is not string npcId) return;
        if (!State.AllNPCs.TryGetValue(npcId, out var npc)) return;
        if (!npc.IsHidden) return;
        npc.IsHidden = false;
        Logger.Info($"剧情解锁NPC: {npc.Name} (defaultScene={npc.DefaultSceneId})");
        UpdateSceneNPCs();
    }

    public Scene? GetCurrentScene()
    {
        State.AllScenes.TryGetValue(State.CurrentSceneId, out var scene);
        return scene;
    }

    /// <summary>根据副本ID构建一个 DungeonRunner. 找不到返回 null.</summary>
    public Quests.DungeonRunner? CreateDungeonRunner(string dungeonId)
    {
        if (!Config.Dungeons.TryGetValue(dungeonId, out var dCfg)) return null;
        var dungeon = Quests.Dungeon.FromConfig(dCfg);
        return new Quests.DungeonRunner(dungeon, State.Player, Config, AI, State.GameTime, GetCurrentScene());
    }

    /// <summary>
    /// 构建华山论剑终章副本运行器:按玩家善恶动态选取 10 位绝顶高手(关系优先 + 随机 + 阅历升序),
    /// 进入前一次性满血满蓝(决战公平起手;之后 10 连战之间不回血),对手属性按配置倍率放大。
    /// 找不到配置返回 null。
    /// </summary>
    public Quests.DungeonRunner? CreateHuashanRunner()
    {
        if (!Config.Dungeons.TryGetValue("huashan_lunjian", out var dCfg)) return null;
        var dungeon = Quests.Dungeon.FromConfig(dCfg);

        // 进入论剑:一次性满血满蓝(决战公平起手;10 连战之间不回血,只能靠战斗中嗑药——未来制药系统)
        var p = State.Player;
        p.CurrentHP = p.GetTotalMaxHP();
        p.CurrentMP = p.GetTotalMaxMP();

        var opponents = Quests.HuashanLunjianBuilder.Build(p, Config, dCfg.OpponentStatMultiplier);
        return new Quests.DungeonRunner(dungeon, p, Config, AI, State.GameTime, GetCurrentScene())
        {
            IsHuashanLunjian = true,
            FlatOpponents = opponents
        };
    }

    public void EnterScene(string sceneId, bool fromTravel = false)
    {
        Logger.Info($"EnterScene: {sceneId} (fromTravel={fromTravel})");
        if (PostCombat != null) return;

        if (!State.AllScenes.TryGetValue(sceneId, out var scene))
        {
            Logger.Warn($"场景不存在: {sceneId}");
            Log("该场景不存在。");
            return;
        }

        State.CurrentSceneId = sceneId;
        State.Player.CurrentSceneId = sceneId;

        if (!fromTravel)
        {
            if (!StaminaSystem.ConsumeStamina(State.Player, StaminaSystem.TalkCost))
            {
                Logger.Info("体力不足，无法进入场景");
                Log("你太累了，需要休息。");
                return;
            }
        }

        UpdateSceneNPCs();
        Logger.Info($"场景NPC更新完成, 当前NPC数: {scene.PresentNPCs.Count}");
        Log(scene.GetSceneDescription());
        EventSystem.Instance.Publish(GameEvents.SceneChanged, new() { ["sceneId"] = sceneId });

        try { ProcessNPCInteractions(); }
        catch (Exception ex) { Logger.Error("场景交互异常", ex); Log($"(场景交互出错: {ex.Message})"); }
    }

    /// <summary>
    /// 本地场景导航（同城镇内步行，免费，只消耗少量时间和体力）
    /// </summary>
    public void TravelToScene(string targetSceneId)
    {
        Logger.Info($"TravelToScene: {targetSceneId} (当前: {State.CurrentSceneId})");
        if (PostCombat != null) return;

        // 本地导航：固定低消耗
        double timeCost = 0.3;
        double staminaCost = 5;

        if (!StaminaSystem.ConsumeStamina(State.Player, staminaCost))
        {
            Log("体力不足，无法前往。");
            return;
        }

        int prevDay = State.GameTime.Day;
        State.GameTime.Advance(timeCost);
        Log($"你步行前往，花费了{timeCost:F1}个时辰。");

        if (State.GameTime.IsNewDay(prevDay))
            OnNewDay();

        EnterScene(targetSceneId, fromTravel: true);
    }

    /// <summary>
    /// 城镇间旅行（大地图，自动寻最短路径，扣银两+时间+体力）
    /// </summary>
    public bool TravelToLocation(string targetLocationId)
    {
        Logger.Info($"TravelToLocation: {targetLocationId} (当前场景: {State.CurrentSceneId})");
        if (PostCombat != null) return false;
        if (WorldMap == null) return false;

        var currentLoc = WorldMap.GetLocationByScene(State.CurrentSceneId);
        if (currentLoc == null)
        {
            Log("你当前所在的位置不在地图路线上。");
            return false;
        }

        if (!WorldMap.Locations.TryGetValue(targetLocationId, out var targetLoc))
        {
            Log("目标城镇不存在。");
            return false;
        }

        if (currentLoc.Id == targetLocationId)
        {
            Log("你已经在这里了。");
            return false;
        }

        // 使用Dijkstra最短路径
        var pathResult = WorldMap.FindShortestPath(currentLoc.Id, targetLocationId);
        if (pathResult == null)
        {
            Log($"从{currentLoc.Name}无法到达{targetLoc.Name}。");
            return false;
        }

        var (totalDist, totalTime, path) = pathResult.Value;
        int goldCost = (int)(totalDist * 0.5);
        double staminaCost = totalDist / 5.0;

        // 允许欠款（负数金），不再检查余额

        if (!StaminaSystem.ConsumeStamina(State.Player, staminaCost))
        {
            Log("体力不足，无法远行。请先休息。");
            return false;
        }

        State.Player.Gold -= goldCost;
        if (State.Player.Gold < 0)
            Log($"注意：你现在欠银 {Math.Abs(State.Player.Gold)} 两，过月将收取10%利息！");

        int prevDay = State.GameTime.Day;
        State.GameTime.Advance(totalTime);

        // 显示途经信息
        Log($"═══ 乘坐马车前往{targetLoc.Name} ═══");
        if (path.Count > 2)
        {
            var pathNames = path.Select(id => WorldMap.Locations.TryGetValue(id, out var l) ? l.Name : id);
            Log($"路线：{string.Join(" → ", pathNames)}");
        }
        Log($"花费{goldCost}银，耗时{totalTime:F1}个时辰。");

        if (State.GameTime.IsNewDay(prevDay))
            OnNewDay();

        // 进入目标城镇的第一个场景
        if (targetLoc.SceneIds.Count > 0)
            EnterScene(targetLoc.SceneIds[0], fromTravel: true);

        return true;
    }

    /// <summary>
    /// 获取当前所在城镇
    /// </summary>
    public Location? GetCurrentLocation()
    {
        return WorldMap?.GetLocationByScene(State.CurrentSceneId);
    }

    // ── 战斗系统 ──

    public CombatEngine? StartCombat(string npcId, bool isSpar = false)
    {
        Logger.Info($"StartCombat: {npcId} isSpar={isSpar}");
        if (!State.AllNPCs.TryGetValue(npcId, out var npc) || !npc.IsAlive)
            return null;

        double cost = isSpar ? StaminaSystem.SparCost : StaminaSystem.CombatCost;
        if (!StaminaSystem.ConsumeStamina(State.Player, cost))
        {
            Log("体力不足，无法战斗。");
            return null;
        }

        double timeCost = isSpar ? 1 : 2;
        State.GameTime.Advance(timeCost);

        // 切磋:战前快照 HP/MP,战后恢复(切磋不掉血)
        if (isSpar)
        {
            _sparSnapshotPlayerHP = State.Player.CurrentHP;
            _sparSnapshotPlayerMP = State.Player.CurrentMP;
            _sparSnapshotOpponentHP = npc.CurrentHP;
            _sparSnapshotOpponentMP = npc.CurrentMP;
        }

        CurrentCombat = new CombatEngine(State.Player, npc, isSpar);

        string combatType = isSpar ? "切磋" : "战斗";
        Log($"═══ {combatType}开始：{State.Player.Name} VS {npc.Name} ═══");
        Log($"行动顺序：{CurrentCombat.GetActionOrderDisplay()}");

        return CurrentCombat;
    }

    // 切磋战前状态快照(切磋结束后恢复双方 HP/MP)
    private int _sparSnapshotPlayerHP;
    private int _sparSnapshotPlayerMP;
    private int _sparSnapshotOpponentHP;
    private int _sparSnapshotOpponentMP;

    /// <summary>
    /// 执行一个大回合，返回战报日志列表
    /// </summary>
    public List<string>? ExecuteCombatRound(int skillIndex)
    {
        if (CurrentCombat == null) return null;

        var logs = CurrentCombat.ExecuteRound(skillIndex);
        foreach (var log in logs)
            Log(log);

        if (CurrentCombat.IsCombatOver)
            EndCombat();

        return logs;
    }

    private void EndCombat()
    {
        if (CurrentCombat == null) return;
        var result = CurrentCombat.Result;
        var opponent = CurrentCombat.Opponent as NPC;
        bool wasSpar = CurrentCombat.IsSpar;
        bool hiddenPower = CurrentCombat.OpponentHiddenPower;

        // 切磋:从快照恢复双方 HP/MP(纯交流武艺,不真正伤害)
        if (wasSpar)
        {
            State.Player.CurrentHP = _sparSnapshotPlayerHP;
            State.Player.CurrentMP = _sparSnapshotPlayerMP;
            if (opponent != null)
            {
                opponent.CurrentHP = _sparSnapshotOpponentHP;
                opponent.CurrentMP = _sparSnapshotOpponentMP;
            }
        }

        CurrentCombat.RestoreOpponentPower();
        CurrentCombat.CalculateExpRewards();

        // 战后熟练度结算: 玩家装备的内/外/轻功一并获得熟练度
        var profGains = CurrentCombat.SettleCombatProficiency();
        foreach (var (art, gained) in profGains)
        {
            string typeTag = art is MartialArts.InternalArt ? "内功" : art is MartialArts.LightArt ? "身法" : "外功";
            Log($"【{typeTag}·{art.Name}】熟练度 +{gained} (当前 {art.Proficiency})");
        }

        // 应用阅历经验
        int playerExp = CurrentCombat.PlayerExpGained;
        int opponentExp = CurrentCombat.OpponentExpGained;

        if (playerExp > 0)
        {
            int levelUps = State.Player.GainJianghuExp(playerExp);
            Log($"获得江湖阅历 +{playerExp}（阅历Lv.{State.Player.JianghuLevel} 经验{State.Player.JianghuExp}/{State.Player.GetExpToNextLevel()}）");
            if (levelUps > 0)
            {
                Log($"🌟 阅历提升！等级达到 Lv.{State.Player.JianghuLevel}！（攻击+{levelUps} 防御+{levelUps} HP+{levelUps * 10} MP+{levelUps * 5}）");
                State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Training, $"江湖阅历突破至 Lv.{State.Player.JianghuLevel}");
            }
        }

        if (opponentExp > 0 && opponent != null)
        {
            opponent.GainJianghuExp(opponentExp);
        }

        // 清理临时Buff
        CurrentCombat.CleanupCombat();

        string combatType = wasSpar ? "切磋" : "战斗";
        Log($"═══ {combatType}结束！共 {result.TotalRounds} 回合 ═══");

        if (wasSpar)
            EndSpar(result, opponent, hiddenPower);
        else
            EndRealCombat(result, opponent);

        // 记录战斗经历
        if (opponent != null)
            RecordCombatLifeEvent(opponent, wasSpar, result.Outcome);

        CurrentCombat = null;
    }

    private void EndSpar(CombatResult result, NPC? opponent, bool hiddenPower)
    {
        switch (result.Outcome)
        {
            case CombatOutcome.PlayerWin:
                Log("切磋胜利！你技高一筹。");
                if (opponent != null)
                {
                    RelationshipSystem.Interact(State.Player, opponent, 5);
                    if (hiddenPower)
                        Log($"{opponent.Name}笑道：\"阁下武功不错，我只是随意比划了几招。\"");
                    else
                        Log($"{opponent.Name}拱手道：\"佩服佩服！\"");
                }
                break;
            case CombatOutcome.NPCWin:
                Log("切磋落败。");
                if (opponent != null)
                {
                    if (hiddenPower)
                        Log($"{opponent.Name}似乎并未使出全力...");
                    RelationshipSystem.Interact(State.Player, opponent, 3);
                }
                State.Player.CurrentHP = Math.Max(1, State.Player.CurrentHP);
                break;
            case CombatOutcome.Surrendered:
                Log("你认输了。");
                if (opponent != null)
                    Log($"{opponent.Name}抱拳道：\"承让了。\"");
                State.Player.CurrentHP = Math.Max(1, State.Player.CurrentHP);
                break;
        }
    }

    private void EndRealCombat(CombatResult result, NPC? opponent)
    {
        switch (result.Outcome)
        {
            case CombatOutcome.PlayerWin:
                Log("战斗胜利！");
                PostCombat = new PostCombatContext
                {
                    Phase = PostCombatPhase.PlayerWinChoice,
                    DefeatedNPCId = opponent?.Id ?? "",
                    Outcome = result.Outcome,
                    WasSpar = false,
                    OpponentHiddenPower = false
                };
                Log("你可以选择：杀死对方 / 羞辱对方 / 放过对方");
                break;

            case CombatOutcome.NPCWin:
                // 不在此同步处理:AI 决策需异步,仅标记待处理,由 CombatForm 调用 ProcessNPCVictoryAsync
                PendingNPCVictoryOpponentId = opponent?.Id;
                break;

            case CombatOutcome.Fled:
                Log("你成功逃脱了！");
                KarmaSystem.ChangeKarma(State.Player, -2);
                break;
        }
    }

    /// <summary>
    /// NPC 胜利后异步处理:由 AI 决策是否杀玩家、索取赎金、羞辱。
    /// 杀 → 游戏结束;不杀 → 羞辱 + 赎金(扣玩家银两给NPC) + 战败受辱debuff(战斗属性-20%,持续7天)。
    /// AI 不可用/失败时回退到规则判定。
    /// </summary>
    public async Task ProcessNPCVictoryAsync()
    {
        var npcId = PendingNPCVictoryOpponentId;
        PendingNPCVictoryOpponentId = null;
        if (string.IsNullOrEmpty(npcId)) return;

        if (!State.AllNPCs.TryGetValue(npcId, out var npc) || npc == null)
        {
            Log("你被击败了...");
            ApplyDefeatConsequence(null, 0);
            return;
        }

        bool kill = false;
        int ransom = 0;
        string dialogue = "";

        try
        {
            var sys = AIPromptBuilder.BuildNPCVictorySystemPrompt();
            var user = AIPromptBuilder.BuildNPCVictoryPrompt(npc, State.Player);
            var decision = await AI.ChatStructuredAsync<NPCVictoryDecision>(sys, user);
            if (decision != null)
            {
                kill = decision.Kill;
                ransom = Math.Max(0, decision.Ransom);
                dialogue = decision.Dialogue ?? "";
                GameLogger.AI($"[NPC胜利处置] {npc.Name} kill={kill} ransom={ransom} dialogue={dialogue}");
            }
            else
            {
                GameLogger.AI("[NPC胜利处置] AI未返回决策,回退规则判定");
                (kill, ransom) = FallbackNPCVictoryDecision(npc);
            }
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[NPC胜利处置] AI异常: {ex.Message},回退规则判定");
            (kill, ransom) = FallbackNPCVictoryDecision(npc);
        }

        if (string.IsNullOrWhiteSpace(dialogue))
            dialogue = kill ? "今天就是你的死期！" : "就这点本事也敢来送死？滚吧！";

        Log($"{npc.Name}冷笑道：\"{dialogue}\"");

        if (kill)
        {
            KillPlayer(npc);
            return;
        }

        // 羞辱
        Log("你战败受辱,颜面尽失...");
        MoodSystem.ChangeMood(State.Player, -30);
        KarmaSystem.ChangeKarma(State.Player, -5);
        RelationshipSystem.Interact(State.Player, npc, -15);

        ApplyDefeatConsequence(npc, ransom);
    }

    /// <summary>AI 不可用时的规则回退:基于关系/善恶判定是否杀,赎金随机。</summary>
    private (bool kill, int ransom) FallbackNPCVictoryDecision(NPC npc)
    {
        var rel = npc.GetRelation(State.Player.Id);
        bool kill = false;
        if (rel.Favorability <= -50 || npc.Karma < -30)
            kill = Random.Shared.NextDouble() < 0.4;

        int ransom = 0;
        if (!kill)
            ransom = Math.Min(State.Player.Gold, Random.Shared.Next(50, 301));
        return (kill, ransom);
    }

    /// <summary>
    /// 战败后果(未被杀时):扣赎金、加战败受辱debuff、HP/MP归1、送回出生场景。
    /// 不再扣除基础攻击力。
    /// </summary>
    private void ApplyDefeatConsequence(NPC? npc, int ransom)
    {
        var player = State.Player;

        if (ransom > 0)
        {
            int actual = Math.Min(ransom, player.Gold);
            if (actual > 0)
            {
                player.Gold -= actual;
                if (npc != null) npc.Gold += actual;
                Log($"{npc?.Name ?? "对方"}搜走了你身上{actual}两银钱。");
            }
        }

        // 战败受辱 debuff:战斗中所有属性 -20%,持续7天
        player.AddTag(PlayerTag.CreateBeatenDown(State.GameTime.Day, 7));
        Log("你身受内伤,战败受辱——接下来7日内战斗中所有属性降低20%。");

        player.CurrentHP = 1;
        player.CurrentMP = 0;
    }

    private void KillPlayer(NPC killer)
    {
        Log($"{killer.Name}杀死了你！");
        Log("═══ 游戏结束 ═══");
        Log($"你在江湖中行走了{State.GameTime.Day}天。");
        Log("你的故事到此结束...");

        State.Player.CurrentHP = 0;
        State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Combat, $"被{killer.Name}杀死");

        RelationshipSystem.Interact(State.Player, killer, -50);
        PlayerIsDead = true;
    }

    // ── 战后选择 ──

    public void PostCombatKill()
    {
        if (PostCombat?.Phase != PostCombatPhase.PlayerWinChoice) return;
        var npcId = PostCombat.DefeatedNPCId;
        if (!State.AllNPCs.TryGetValue(npcId, out var npc)) { ClearPostCombat(); return; }

        npc.IsAlive = false;
        npc.CurrentHP = 0;
        KarmaSystem.ChangeKarma(State.Player, -25, "杀死对手");
        MoodSystem.ChangeMood(State.Player, -15);
        Log($"你杀死了{npc.Name}。");
        Log($"善恶值-25（当前：{State.Player.Karma}，{KarmaSystem.GetKarmaDescription(State.Player.Karma)}）");

        foreach (var otherNpc in State.AllNPCs.Values)
        {
            var rel = otherNpc.GetRelation(npcId);
            if (rel.Type is RelationType.Spouse or RelationType.SwornBrother or RelationType.Disciple or RelationType.Master)
            {
                otherNpc.GetRelation(State.Player.Id).ChangeFavorability(-40);
                Log($"{otherNpc.Name}得知此事，对你怀恨在心。");
            }
        }

        State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Combat, $"杀死了{npc.Name}");

        // 任务挂钩: 通知 QuestAutoAdvance(若有任务要求"放过"此 NPC,则任务失败)
        var (killLogs, killDialogues) = Quests.QuestAutoAdvance.TryAdvanceAll(State.Player, "kill", npcId, Config);
        foreach (var l in killLogs) Log(l);

        ClearPostCombat();
    }

    public void PostCombatHumiliate()
    {
        if (PostCombat?.Phase != PostCombatPhase.PlayerWinChoice) return;
        var npcId = PostCombat.DefeatedNPCId;
        if (!State.AllNPCs.TryGetValue(npcId, out var npc)) { ClearPostCombat(); return; }

        KarmaSystem.ChangeKarma(State.Player, -10, "羞辱对手");
        MoodSystem.ChangeMood(State.Player, 10);
        RelationshipSystem.Interact(State.Player, npc, -30);

        string[] humiliations =
        [
            $"你把{npc.Name}的兵器折断，扔在地上。",
            $"你在{npc.Name}脸上写了一个\"输\"字。",
            $"你让{npc.Name}当众跪下磕头。",
            $"你扯下{npc.Name}的外衣，扬长而去。"
        ];
        Log(humiliations[Random.Shared.Next(humiliations.Length)]);
        Log($"{npc.Name}受辱，眼中满是怨恨。");

        State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Social, $"羞辱了{npc.Name}");
        ClearPostCombat();
    }

    public void PostCombatSpare()
    {
        if (PostCombat?.Phase != PostCombatPhase.PlayerWinChoice) return;
        var npcId = PostCombat.DefeatedNPCId;
        if (!State.AllNPCs.TryGetValue(npcId, out var npc)) { ClearPostCombat(); return; }

        KarmaSystem.ChangeKarma(State.Player, 10, "放过对手");
        RelationshipSystem.Interact(State.Player, npc, 15);

        npc.CurrentHP = (int)(npc.GetTotalMaxHP() * 0.3);

        Log($"你放过了{npc.Name}。");
        Log($"{npc.Name}抱拳道：\"多谢不杀之恩。\"");
        Log($"善恶值+10（当前：{State.Player.Karma}，{KarmaSystem.GetKarmaDescription(State.Player.Karma)}）");

        State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Social, $"放过了{npc.Name}");

        // 任务挂钩: "放过"动作推进 spare 步骤
        var (spareLogs, spareDialogues) = Quests.QuestAutoAdvance.TryAdvanceAll(State.Player, "spare", npcId, Config);
        foreach (var l in spareLogs) Log(l);

        ClearPostCombat();
    }

    private void ClearPostCombat()
    {
        PostCombat = null;
        UpdateSceneNPCs();
    }

    // ── 门派系统 ──

    /// <summary>
    /// 尝试加入门派（NPC AI 审批）
    /// </summary>
    public async Task<bool> TryJoinFactionAsync(string factionId, NPC approverNpc)
    {
        var faction = FactionSystem.GetFaction(factionId);
        if (faction == null)
        {
            Log("该门派不存在。");
            return false;
        }
        if (State.Player.FactionId != null)
        {
            Log("你已有门派，不可重复加入。");
            return false;
        }

        // 基础善恶值检查
        if (!faction.CanPlayerJoin(State.Player.Karma))
        {
            Log($"{faction.Name}的善恶值要求与你不符，掌门不会见你。");
            return false;
        }

        // 申请冷却:被该门派拒绝后10天内不可再申请
        if (State.Player.FactionJoinRejections.TryGetValue(factionId, out var rejectDay))
        {
            int daysSinceReject = State.GameTime.Day - rejectDay;
            if (daysSinceReject < 10)
            {
                int remaining = 10 - daysSinceReject;
                Log($"你上次申请{faction.Name}被拒，{remaining}天内不可再申请。");
                return false;
            }
        }

        // AI 审批
        Log($"你向{approverNpc.Name}表达了拜入{faction.Name}的意愿……");
        GameLogger.AI($"[门派审批] {approverNpc.Name} 审核玩家加入 {faction.Name}");

        var systemPrompt = AIPromptBuilder.BuildFactionJoinSystemPrompt();
        var userMessage = AIPromptBuilder.BuildFactionJoinPrompt(approverNpc, State.Player, faction);

        var decision = await AI.ChatStructuredAsync<FactionJoinDecision>(systemPrompt, userMessage);

        if (decision == null)
        {
            Log("掌门沉思良久，最终还是摇了摇头。");
            return false;
        }

        GameLogger.AI($"[门派审批] 结果: approved={decision.Approved}, reason={decision.Reason}");

        if (!decision.Approved)
        {
            // 拒绝:记录10天冷却
            State.Player.FactionJoinRejections[factionId] = State.GameTime.Day;
            Log($"{approverNpc.Name}说道：“{decision.Dialogue}”");
            Log($"（原因：{decision.Reason}，10天内不可再申请{faction.Name}）");
            return false;
        }

        // 同意
        State.Player.FactionId = factionId;
        Log($"{approverNpc.Name}说道：“{decision.Dialogue}”");
        Log($"恭喜！你已加入{faction.Name}！");
        State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Major, $"加入了{faction.Name}，掌门{approverNpc.Name}亲自接纳");

        // 记录掌门收徒经历
        approverNpc.AddLifeEvent(State.GameTime.Day, Characters.LifeEventType.Major,
            $"接纳{State.Player.Name}拜入{faction.Name}");
        RecordFactionJoinLifeEvent(approverNpc, faction.Name);

        EventSystem.Instance.Publish(GameEvents.FactionJoined);
        return true;
    }

    /// <summary>
    /// 同步版本（兼容旧调用）
    /// </summary>
    public bool TryJoinFaction(string factionId)
    {
        if (FactionSystem.TryJoinFaction(State.Player, factionId, out var message))
        {
            var faction = FactionSystem.GetFaction(factionId);
            Log(message);
            State.Player.AddLifeEvent(State.GameTime.Day, LifeEventType.Major, $"加入了{faction?.Name}");

            // 记录掌门收徒经历
            if (faction != null)
            {
                var sectLeader = State.AllNPCs.Values
                    .FirstOrDefault(n => n.FactionId == factionId && n is SectLeader);
                if (sectLeader != null)
                    RecordFactionJoinLifeEvent(sectLeader, faction.Name);
            }

            EventSystem.Instance.Publish(GameEvents.FactionJoined);
            return true;
        }
        Log(message);
        return false;
    }

    // ── 休息 ──

    public void Rest()
    {
        if (PostCombat != null) return;

        State.Player.Rest(8);
        State.Player.BaseAttack += 2;

        // NPC也恢复状态
        foreach (var npc in State.AllNPCs.Values)
        {
            if (npc.IsAlive)
                npc.Rest(8);
        }

        int prevDay = State.GameTime.Day;
        State.GameTime.SleepUntilNextDay();
        Log("你休息了一晚，体力恢复，伤势好转。");
        OnNewDay();
    }

    /// <summary>休息6个时辰：恢复量为一日(8时辰)的一半，时间推进6时辰。</summary>
    public void Rest6ShiChen()
    {
        if (PostCombat != null) return;
        // 一日为 Rest(8)，半日即 Rest(4)：HP/MP/体力各恢复一日的一半
        State.Player.Rest(4);
        foreach (var npc in State.AllNPCs.Values)
        {
            if (npc.IsAlive)
                npc.Rest(4);
        }
        int prevDay = State.GameTime.Day;
        State.GameTime.Advance(6);
        Log("你打坐调息六个时辰，体力略有恢复。");
        if (State.GameTime.Day != prevDay) OnNewDay();
    }

    /// <summary>休息3天：气血内力完全恢复，时间推进3天。</summary>
    public void Rest3Days()
    {
        if (PostCombat != null) return;
        for (int i = 0; i < 3; i++)
        {
            State.GameTime.SleepUntilNextDay();
            OnNewDay();
        }
        var p = State.Player;
        p.CurrentHP = p.GetTotalMaxHP();
        p.CurrentMP = p.GetTotalMaxMP();
        p.Stamina = p.MaxStamina;
        foreach (var npc in State.AllNPCs.Values)
        {
            if (!npc.IsAlive) continue;
            npc.CurrentHP = npc.GetTotalMaxHP();
            npc.CurrentMP = npc.GetTotalMaxMP();
            npc.Stamina = npc.MaxStamina;
        }
        Log("你闭关修养三天，气血充盈，精神焕发，伤势痊愈。");
    }

    private void OnNewDay()
    {
        Log($"--- 新的一天：{State.GameTime.Display} ---");
        ProcessDailyTags();
        ProcessSiguoyaiMeditation();
        UpdateSceneNPCs();
        EventSystem.Instance.Publish(GameEvents.NewDay);
    }

    /// <summary>
    /// 处理思过崖面壁:若玩家当前在思过崖且任务步骤是 meditate_siguoyai,
    /// 记录起始日并在满30天后触发推进。
    /// </summary>
    private void ProcessSiguoyaiMeditation()
    {
        var player = State.Player;
        var quest = player.QuestLog.FirstOrDefault(q =>
            q.Id == "siguoyai_meeting" && q.Status == Quests.QuestStatus.InProgress);
        if (quest == null || quest.CurrentStep == null) return;
        if (quest.CurrentStep.Id != "meditate_siguoyai") return;
        if (State.CurrentSceneId != "siguoyai") return;

        const string key = "siguoyai_start_day";
        if (!player.QuestProgressData.TryGetValue(key, out int startDay))
        {
            player.QuestProgressData[key] = State.GameTime.Day;
            Log("（你盘膝坐于思过崖石室之前,开始面壁思过...）");
            return;
        }

        int daysPassed = State.GameTime.Day - startDay;
        if (daysPassed >= 30)
        {
            Log($"（思过崖面壁已满{daysPassed}天。）");
            // 若令狐冲好感度足够,触发推进
            if (State.AllNPCs.TryGetValue("linghu_chong", out var linghu))
            {
                var rel = linghu.GetRelation(player.Id);
                if (rel.Favorability >= 50)
                {
                    Log("（远远望见令狐冲沿石阶而上,身后跟着一位白衣老者——）");
                    var (logs, meditateDialogues) = Quests.QuestAutoAdvance.TryAdvanceAll(player, "meditate", "siguoyai", Config);
                    foreach (var l in logs) Log(l);
                }
                else
                {
                    Log("（你与令狐冲的交情尚浅,无人引荐,面壁继续...）");
                }
            }
        }
        else
        {
            Log($"（思过崖面壁第{daysPassed}/30天...）");
        }
    }

    /// <summary>
    /// 每日标签Tick：扣减健康度、推进剩余天数、移除过期标签、检查死亡
    /// </summary>
    private void ProcessDailyTags()
    {
        var player = State.Player;

        // 药buff每日tick(与标签独立,Tags 为空也需处理)
        if (player.MedicineBuff != null)
        {
            player.MedicineBuff.TickDay();
            if (player.MedicineBuff.IsExpired)
            {
                Log($"[{player.MedicineBuff.Name}] 药效已过");
                player.MedicineBuff = null;
            }
        }

        // 食buff每日tick(与药buff独立共存)
        if (player.FoodBuff != null)
        {
            player.FoodBuff.TickDay();
            if (player.FoodBuff.IsExpired)
            {
                Log($"[{player.FoodBuff.Name}] 食效已过");
                player.FoodBuff = null;
            }
        }

        if (player.Tags.Count == 0) return;

        var expired = new List<string>();
        int totalLoss = 0;

        foreach (var tag in player.Tags)
        {
            // 扣健康度
            int loss = tag.GetDailyLoss();
            if (loss > 0)
            {
                totalLoss += loss;
                Log($"[{tag.Name}] 健康度 -{loss}");
            }

            // 推进天数
            tag.TickDay();
            if (tag.IsExpired)
                expired.Add(tag.TagId);
        }

        // 扣除总健康度
        if (totalLoss > 0)
        {
            player.ChangeHealth(-totalLoss, "标签每日损耗");
            Log($"健康度：{player.Health}/{player.MaxHealth}");
        }

        // 移除过期标签
        foreach (var tagId in expired)
        {
            var tag = player.Tags.FirstOrDefault(t => t.TagId == tagId);
            if (tag != null)
            {
                player.Tags.Remove(tag);
                Log($"[{tag.Name}] 已痊愈/失效");
                EventSystem.Instance.Publish(GameEvents.TagChanged,
                    new Dictionary<string, object?> { { "action", "expired" }, { "tagId", tagId }, { "tagName", tag.Name } });
            }
        }

        // 检查死亡
        if (player.Health <= 0)
        {
            Log("═══ 油尽灯枯，驾鹤西去... ═══");
            PlayerIsDead = true;
            EventSystem.Instance.Publish(GameEvents.PlayerDied);
        }
    }

    private void UpdateSceneNPCs()
    {
        var timePeriod = State.GameTime.GetTimePeriod();
        LocationManager.UpdateAllSceneNPCs(State.AllScenes, State.AllNPCs, timePeriod);
    }

    /// <summary>对外暴露的场景 NPC 刷新接口(剧情解锁/消失后由 UI 层调用)</summary>
    public void UpdateSceneNPCsExternal() => UpdateSceneNPCs();

    private void ProcessNPCInteractions()
    {
        var scene = GetCurrentScene();
        if (scene == null) return;

        var ai = new CharacterAI();
        foreach (var npc in scene.PresentNPCs)
        {
            if (!npc.IsAlive) continue;
            var decision = ai.DecideAction(npc, State.Player);
            switch (decision.Action)
            {
                case NPCActionType.Talk:
                    Log(decision.DialogueContent ?? npc.GetGreeting(State.Player));
                    break;
                case NPCActionType.Challenge:
                    Log($"{npc.Name}向你发起了切磋邀请！(点击切磋按钮接受)");
                    break;
                case NPCActionType.Attack:
                    Log($"{npc.Name}对你怒目而视，似乎想要动手！(点击挑战按钮应战)");
                    break;
            }
        }
    }

    // ── 存档系统 ──

    public string GetSaveDir()
    {
        var dir = AppPaths.SavesDir;
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetSavePath(int slot)
    {
        return Path.Combine(GetSaveDir(), $"save_{slot}.json");
    }

    public SaveSlotInfo? GetSaveInfo(int slot)
    {
        var path = GetSavePath(slot);
        if (!File.Exists(path)) return null;

        try
        {
            var state = GameState.Load(path);
            if (state == null) return null;
            return new SaveSlotInfo
            {
                Slot = slot,
                PlayerName = state.Player.Name,
                Day = state.GameTime.Day,
                SceneName = state.AllScenes.TryGetValue(state.CurrentSceneId, out var s) ? s.Name : "未知",
                SaveTime = File.GetLastWriteTime(path)
            };
        }
        catch { return null; }
    }

    public void SaveGame(int slot)
    {
        // 同步门派任务池快照到State(含月度生成的收集任务),随存档序列化
        State.FactionQuestPool = FactionQuests.ExportPool();
        State.Save(GetSavePath(slot));
        Log($"游戏已保存到存档位 {slot}。");
    }

    public bool LoadGame(int slot)
    {
        var path = GetSavePath(slot);
        var state = GameState.Load(path);
        if (state == null)
        {
            Log("读取存档失败。");
            return false;
        }

        // 恢复 SectLeader 类型（序列化后会变成普通 NPC）
        RestoreNPCTypes(state);

        State = state;
        // 根据存档版本号迁移旧档(补入新NPC/场景/门派等),使旧档兼容新版本内容
        MigrateSave(state);
        PostCombat = null;
        CurrentCombat = null;
        // 重建对话系统以引用新State(对话历史随State序列化,自动恢复)
        DialogueSystem = new DialogueSystem(AI, State, Config);
        // 从存档恢复门派任务池(含月度生成的收集任务);旧存档无快照则fallback从配置加载
        FactionQuests = new Quests.FactionQuestManager(Config);
        if (State.FactionQuestPool != null && State.FactionQuestPool.Count > 0)
            FactionQuests.RestorePool(State.FactionQuestPool);
        else
            FactionQuests.LoadFromConfig();
        // 重建月度更新系统以引用新的任务管理器
        MonthlyUpdateSystem = new MonthlyUpdateSystem(AI, AIConfig.Load(), Config, FactionQuests);
        AnnualUpdateSystem = new AnnualUpdateSystem(AI, AIConfig.Load(), Config);
        UpdateSceneNPCs();
        Log($"存档 {slot} 已读取。{State.GameTime.Display}");
        return true;
    }

    /// <summary>
    /// 恢复 NPC 的具体类型（如 SectLeader）
    /// </summary>
    private void RestoreNPCTypes(GameState state)
    {
        var toReplace = new List<(string id, SectLeader leader)>();
        foreach (var (id, npc) in state.AllNPCs)
        {
            if (npc.IsSectLeader && npc is not SectLeader)
            {
                var leader = new SectLeader
                {
                    Id = npc.Id,
                    Name = npc.Name,
                    Description = npc.Description,
                    Personality = npc.Personality,
                    MaxHP = npc.MaxHP,
                    CurrentHP = npc.CurrentHP,
                    MaxMP = npc.MaxMP,
                    CurrentMP = npc.CurrentMP,
                    BaseAttack = npc.BaseAttack,
                    BaseDefense = npc.BaseDefense,
                    Speed = npc.Speed,
                    Mood = npc.Mood,
                    Karma = npc.Karma,
                    Stamina = npc.Stamina,
                    MaxStamina = npc.MaxStamina,
                    FactionId = npc.FactionId,
                    CurrentSceneId = npc.CurrentSceneId,
                    ActiveInternalArt = npc.ActiveInternalArt,
                    ActiveExternalArt = npc.ActiveExternalArt,
                    LearnedArts = npc.LearnedArts,
                    Relations = npc.Relations,
                    History = npc.History,
                    Inventory = npc.Inventory,
                    Gold = npc.Gold,
                    Schedule = npc.Schedule,
                    DefaultSceneId = npc.DefaultSceneId,
                    IsHiddenPower = npc.IsHiddenPower,
                    IsAlive = npc.IsAlive,
                    IsInCombat = npc.IsInCombat,
                    LifeEvents = npc.LifeEvents,
                    BuddhistValue = npc.BuddhistValue,
                    GiftPreference = npc.GiftPreference,
                    IsSectLeader = true
                };
                toReplace.Add((id, leader));
            }
        }
        foreach (var (id, leader) in toReplace)
            state.AllNPCs[id] = leader;
    }

    /// <summary>
    /// 根据存档版本号逐步迁移旧档到当前版本。
    /// 每次有破坏性改动(新增NPC/场景/门派、改数据结构)时:
    ///   1. GameState.CurrentSaveVersion +1
    ///   2. 在此加 if (state.SaveVersion &lt; N) { ...迁移逻辑... } 块
    /// 旧档无 SaveVersion 字段,反序列化为 0,会从 v0 逐步迁到 CurrentSaveVersion。
    /// </summary>
    private void MigrateSave(GameState state)
    {
        // ── 幂等补全:每次加载都补入配置新增的 NPC/场景/门派 ──
        // 旧档 AllNPCs/AllScenes/AllFactions 是快照,缺后续新增内容;只补缺失、不覆盖已有(保留玩家进度)。
        // 加新 NPC/城镇/门派时无需 bump 版本,旧档加载自动兼容。
        bool added = false;
        foreach (var (id, _) in Config.Characters)
        {
            if (id == "player_default" || state.AllNPCs.ContainsKey(id)) continue;
            try
            {
                var npc = Config.CreateNPC(id);
                if (npc == null) continue;
                state.AllNPCs[id] = npc;
                if (ShopSystem.IsMerchant(npc.NpcRole))
                    ShopSystem.InitShopItems(npc, Config);
                added = true;
            }
            catch { /* 单个NPC加载失败不阻塞 */ }
        }
        foreach (var (id, sceneConfig) in Config.Scenes)
        {
            if (!state.AllScenes.ContainsKey(id))
            {
                state.AllScenes[id] = Scene.FromConfig(sceneConfig);
                added = true;
            }
        }
        foreach (var (id, _) in Config.Factions)
        {
            if (state.AllFactions.ContainsKey(id)) continue;
            var faction = Config.CreateFaction(id);
            if (faction != null) { state.AllFactions[id] = faction; added = true; }
        }
        if (added) Log("存档补全:补入新增 NPC/场景/门派(含各城杂货铺)");

        // ── 版本化迁移(破坏性数据结构改动,按版本号逐步执行) ──
        // 每次破坏性改动:GameState.CurrentSaveVersion +1,在此加 if (state.SaveVersion < N) { ... } 块。
        // if (state.SaveVersion < 1)
        // {
        //     ... 一次性数据结构转换 ...
        //     Log("存档迁移 v1: ...");
        // }

        state.SaveVersion = GameState.CurrentSaveVersion;
    }

    // ── NPC经历初始化 ──

    private void InitNPCLifeEvents()
    {
        foreach (var (_, npc) in State.AllNPCs)
        {
            if (npc.LifeEvents.Count > 0) continue; // 存档已加载

            // 基础背景经历
            npc.AddLifeEvent(0, LifeEventType.Background, $"{npc.Name}在江湖中已有一定声望。");
            if (npc.FactionId != null)
            {
                var factionName = State.AllFactions.TryGetValue(npc.FactionId, out var f) ? f.Name : npc.FactionId;
                npc.AddLifeEvent(0, LifeEventType.Background, $"{npc.Name}是{factionName}的成员。");
            }
            if (npc.ActiveExternalArt != null)
                npc.AddLifeEvent(0, LifeEventType.Background, $"修炼{npc.ActiveExternalArt.Name}已有{npc.ActiveExternalArt.Level}层功力。");
        }
    }

    // ── 月度更新 ──

    public bool NeedsMonthlyUpdate()
    {
        return MonthlyUpdateSystem.ShouldTriggerMonthly(State);
    }

    public async Task<string> TriggerMonthlyUpdate()
    {
        Log("═══════════════════════════════");
        Log($"    【月度变化】{State.GameTime.MonthDisplay}");
        Log("═══════════════════════════════");
        Log("江湖风云变幻，各路人马各有际遇...");

        string summary;
        try
        {
            summary = await MonthlyUpdateSystem.ExecuteMonthlyUpdate(State);
        }
        catch (Exception ex)
        {
            GameLogger.AI($"月度Agent异常: {ex.Message}，使用默认更新");
            summary = MonthlyUpdateSystem.GenerateDefaultUpdate(State);
        }

        Log(summary);

        // 刷新商贩商品
        foreach (var npc in State.AllNPCs.Values)
        {
            if (npc.IsAlive && ShopSystem.IsMerchant(npc.NpcRole))
            {
                ShopSystem.RefreshShopItems(npc, Config, forceRefresh: true);
            }
        }

        State.LastMonthlyUpdateDay = State.GameTime.Day;
        UpdateSceneNPCs();
        Log("");
        return summary;
    }

    // ── 年度更新(大事件生成) ──

    public bool NeedsAnnualUpdate()
    {
        return AnnualUpdateSystem.ShouldTriggerAnnual(State);
    }

    public async Task<string> TriggerAnnualUpdate()
    {
        Log("════════════════════════════════");
        Log($"    【年度风云】{State.GameTime.YearDisplay}");
        Log("════════════════════════════════");
        Log("岁末年初，江湖暗流涌动，似有大事将起...");

        string summary;
        try
        {
            summary = await AnnualUpdateSystem.ExecuteAnnualUpdate(State);
        }
        catch (Exception ex)
        {
            GameLogger.AI($"年度Agent异常: {ex.Message}");
            summary = "这一年江湖风平浪静，并无大事发生。";
        }

        Log(summary);
        State.LastAnnualUpdateDay = State.GameTime.Day;
        Log("");
        return summary;
    }

    // ── 经历记录辅助方法 ──

    public void RecordCombatLifeEvent(NPC npc, bool wasSpar, CombatOutcome outcome)
    {
        var day = State.GameTime.Day;
        string desc;
        if (wasSpar)
        {
            desc = outcome == CombatOutcome.PlayerWin
                ? $"与{State.Player.Name}切磋，未能取胜。"
                : outcome == CombatOutcome.NPCWin
                    ? $"与{State.Player.Name}切磋，取得胜利。"
                    : $"与{State.Player.Name}切磋，对方认输。";
        }
        else
        {
            desc = outcome == CombatOutcome.PlayerWin
                ? $"与{State.Player.Name}生死战败，受到重创。"
                : outcome == CombatOutcome.NPCWin
                    ? $"与{State.Player.Name}生死战斗，取得胜利。"
                    : $"与{State.Player.Name}交手，对方逃脱。";
        }
        npc.AddLifeEvent(day, LifeEventType.Combat, desc);
    }

    public void RecordDialogueLifeEvent(NPC npc, bool playerInitiated)
    {
        var day = State.GameTime.Day;
        // 避免重复记录（同一天只记一次）
        var recentDialogue = npc.LifeEvents
            .Where(e => e.Day == day && e.Type == LifeEventType.Social)
            .Any();
        if (!recentDialogue)
        {
            npc.AddLifeEvent(day, LifeEventType.Social,
                playerInitiated ? $"{State.Player.Name}前来拜访，与{npc.Name}交谈了一番。" : $"与{State.Player.Name}有了交谈。");
        }
    }

    public void RecordFactionJoinLifeEvent(NPC sectLeader, string factionName)
    {
        var day = State.GameTime.Day;
        sectLeader.AddLifeEvent(day, LifeEventType.Major,
            $"收了{State.Player.Name}为新弟子，加入{factionName}。");
    }
}

public class SaveSlotInfo
{
    public int Slot { get; set; }
    public string PlayerName { get; set; } = "";
    public int Day { get; set; }
    public string SceneName { get; set; } = "";
    public DateTime SaveTime { get; set; }

    public override string ToString()
    {
        return $"存档{Slot}: {PlayerName} 第{Day}天 [{SceneName}] ({SaveTime:MM-dd HH:mm})";
    }
}

public class NPCAIDecision
{
    public string? Action { get; set; }
    public string? Reason { get; set; }
    public string? Dialogue { get; set; }
}
