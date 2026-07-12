using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.MartialArts;

namespace AutoWuxia.Combat;

public class CombatEngine
{
    public CharacterBase Player { get; }
    public CharacterBase Opponent { get; }
    public CombatResult Result { get; } = new();
    public int Round { get; private set; }
    public bool IsSpar { get; }
    public bool OpponentHiddenPower { get; private set; }

    private int _opponentRealAttack;
    private int _opponentRealDefense;

    // 身法行动队列: 每个元素是 true=玩家行动, false=对手行动 (仅用于 UI 预测显示)
    private List<bool> _actionQueue = new();

    // ── 读条战斗:每帧 charge += Speed*0.1,先到 1000 者出手 ──
    // 辟邪剑法/葵花宝典效果:重置后从 400 开始(即下次只需读到 1000,涨 600)
    private const double ChargeFull = 1000.0;
    private const double HalfResetValue = 400.0;
    private const int ActionsPerRound = 4;   // 每个大回合产生 4 次出手(原 7-slot 节奏)
    private double _playerCharge;
    private double _opponentCharge;
    /// <summary>玩家上一次用辟邪类武功(ExtraActionNextRound),下次出手从 400 开始</summary>
    private bool _playerHalfReset;
    private bool _opponentHalfReset;
    private readonly Dictionary<CharacterBase, int> _mpSiphonedThisRound = new();

    /// <summary>对外暴露读条进度(0..1000),UI 用</summary>
    public double PlayerCharge => _playerCharge;
    public double OpponentCharge => _opponentCharge;

    public CombatEngine(CharacterBase player, CharacterBase opponent, bool isSpar = false)
    {
        Player = player;
        Opponent = opponent;
        IsSpar = isSpar;
        Result.IsSpar = isSpar;

        if (isSpar && opponent is NPC { IsHiddenPower: true })
        {
            OpponentHiddenPower = true;
            _opponentRealAttack = opponent.BaseAttack;
            _opponentRealDefense = opponent.BaseDefense;
            opponent.BaseAttack = (int)(opponent.BaseAttack * 0.4);
            opponent.BaseDefense = (int)(opponent.BaseDefense * 0.4);
        }

        BuildActionQueue();
    }

    /// <summary>
    /// 预测接下来 7 次出手的顺序(基于当前 charge 与 speed),仅用于战斗开始时
    /// 的"行动顺序"提示,与实际推进完全独立。
    /// </summary>
    private void BuildActionQueue()
    {
        _actionQueue.Clear();
        double p = _playerCharge, o = _opponentCharge;
        double pStep = Math.Max(1, Player.GetTotalSpeed()) * 0.1;
        double oStep = Math.Max(1, Opponent.GetTotalSpeed()) * 0.1;
        bool pHalf = HasPermanentHalfReset(Player);
        bool oHalf = HasPermanentHalfReset(Opponent);

        for (int i = 0; i < 7; i++)
        {
            // 推进到一方满
            while (p < ChargeFull && o < ChargeFull)
            {
                p += pStep;
                o += oStep;
            }
            if (p >= o)
            {
                _actionQueue.Add(true);
                p = pHalf ? HalfResetValue : 0;
            }
            else
            {
                _actionQueue.Add(false);
                o = oHalf ? HalfResetValue : 0;
            }
        }
    }

    /// <summary>
    /// 内功是否有 PermanentExtraAction 被动(出手只归 50,如葵花宝典)
    /// </summary>
    private static bool HasPermanentHalfReset(CharacterBase character)
    {
        var intArt = character.ActiveInternalArt;
        if (intArt == null) return false;
        foreach (var effect in intArt.Effects)
        {
            if (effect.Type == EffectType.PermanentExtraAction && effect.IsUnlocked(intArt.Level))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 推进读条直到某一方满 100,返回 true=玩家先满,false=对手先满
    /// </summary>
    private bool AdvanceUntilSomeoneReady()
    {
        double pStep = Math.Max(1, Player.GetTotalSpeed()) * 0.1;
        double oStep = Math.Max(1, Opponent.GetTotalSpeed()) * 0.1;
        int safety = 100000;  // 防御性上限
        while (_playerCharge < ChargeFull && _opponentCharge < ChargeFull && safety-- > 0)
        {
            _playerCharge += pStep;
            _opponentCharge += oStep;
        }
        return _playerCharge >= _opponentCharge;
    }

    /// <summary>
    /// 出手后归零(辟邪一次性 / 葵花永久 时归 HalfResetValue)
    /// </summary>
    private void ResetChargeAfterAction(bool isPlayer)
    {
        bool halfReset;
        if (isPlayer)
        {
            halfReset = _playerHalfReset || HasPermanentHalfReset(Player);
            _playerHalfReset = false;  // 消费一次性
            _playerCharge = halfReset ? HalfResetValue : 0.0;
        }
        else
        {
            halfReset = _opponentHalfReset || HasPermanentHalfReset(Opponent);
            _opponentHalfReset = false;
            _opponentCharge = halfReset ? HalfResetValue : 0.0;
        }
    }

    public List<SkillOption> GetPlayerSkills()
    {
        var skills = new List<SkillOption>();

        // 普通攻击（无CD无消耗）
        skills.Add(new SkillOption
        {
            Name = "普通攻击",
            IsAvailable = true,
            MPCost = 0,
            CooldownLeft = 0,
            Art = null
        });

        // 外功技能(玩家可装备最多 3 本,每本都生成一个独立按钮)
        foreach (var art in Player.ActiveExternalArts)
        {
            skills.Add(new SkillOption
            {
                Name = art.Name,
                IsAvailable = art.IsReady && Player.CurrentMP >= art.MPCost,
                MPCost = art.MPCost,
                CooldownLeft = art.CurrentCooldown,
                Art = art
            });
        }

        // 内功技能（加防御buff等）
        if (Player.ActiveInternalArt != null)
        {
            var art = Player.ActiveInternalArt;
            int mpCost = art.GetMPCost(Player.GetTotalMaxMP());
            skills.Add(new SkillOption
            {
                Name = art.Name,
                IsAvailable = art.IsReady && Player.CurrentMP >= mpCost,
                MPCost = mpCost,
                CooldownLeft = art.CurrentCooldown,
                Art = art,
                IsInternal = true
            });
        }

        // 认输/逃跑
        skills.Add(new SkillOption
        {
            Name = IsSpar ? "认输" : "逃跑",
            IsAvailable = true,
            IsFlee = true
        });

        // 战斗嗑药:背包中 CombatUsable 消耗品(回血/回内),每件一个按钮,使用消耗1次出手
        foreach (var item in Player.Inventory.Items)
        {
            if (item.Type != Items.ItemType.Consumable) continue;
            if (!item.CombatUsable || item.Quantity <= 0) continue;
            string desc = "";
            if (item.HPRecovery > 0) desc += $" 回{item.HPRecovery}血";
            if (item.MPRecovery > 0) desc += $" 回{item.MPRecovery}内";
            skills.Add(new SkillOption
            {
                Name = $"{item.Name}×{item.Quantity}{desc}",
                IsAvailable = true,
                IsItem = true,
                ItemId = item.Id
            });
        }

        return skills;
    }

    /// <summary>
    /// <summary>
    /// 执行一个大回合: 推进读条至 ActionsPerRound 次出手(或战斗结束)
    /// </summary>
    public List<string> ExecuteRound(int playerSkillIndex)
    {
        var logs = new List<string>();
        var skills = GetPlayerSkills();
        Round++;
        _mpSiphonedThisRound.Clear();

        // 记录回合开始
        logs.Add($"═══ 第{Round}回合 ═══");

        // 递减所有技能CD
        TickAllCooldowns(Player);
        TickAllCooldowns(Opponent);

        // 递减所有TimedBuff
        // 回合开始:处理流血DoT(在Buff递减前结算,确保本回合仍生效)
        ProcessBleed(Player, logs);
        ProcessBleed(Opponent, logs);
        CheckCombatEnd();

        // 递减所有TimedBuff
        Player.TickTimedBuffs();
        Opponent.TickTimedBuffs();

        // 回合开始：处理双方内功的被动效果（MP/HP回复等，每回合各1次，对称）
        ProcessInternalArtEffects(Player);
        ProcessInternalArtEffects(Opponent);

        int actionsTaken = 0;
        while (actionsTaken < ActionsPerRound && Result.Outcome == CombatOutcome.InProgress)
        {
            bool isPlayerTurn = AdvanceUntilSomeoneReady();
            var actor = isPlayerTurn ? Player : Opponent;

            // 点穴定身:跳过本次出手(消费stun标记;rounds=2以抵御回合开始递减)
            if (actor.HasTimedBuff("stun"))
            {
                actor.TimedBuffs.Remove("stun");
                logs.Add($"  ↳ {actor.Name}被封住穴道,这一击无法出手!");
                ResetChargeAfterAction(isPlayerTurn);
            }
            else if (isPlayerTurn)
            {
                // 玩家行动
                if (playerSkillIndex < 0 || playerSkillIndex >= skills.Count)
                    playerSkillIndex = 0;

                var chosen = skills[playerSkillIndex];
                if (chosen.IsFlee)
                {
                    string log = HandleFlee();
                    logs.Add(log);
                    ResetChargeAfterAction(true);
                    if (Result.Outcome != CombatOutcome.InProgress) break;
                }
                else if (chosen.IsItem && chosen.ItemId != null)
                {
                    // 战斗嗑药:服下消耗品回血/回内,消耗本次出手(不造成伤害)
                    var item = Player.Inventory.GetItem(chosen.ItemId);
                    if (item == null || item.Quantity <= 0)
                    {
                        logs.Add($"{Player.Name}欲取药,背包中却已没有了。");
                    }
                    else
                    {
                        int oldHP = Player.CurrentHP, oldMP = Player.CurrentMP;
                        bool used = item.Use(Player);
                        if (used)
                        {
                            var msg = $"{Player.Name}服下【{item.Name}】";
                            if (Player.CurrentHP > oldHP) msg += $",回血{Player.CurrentHP - oldHP}";
                            if (Player.CurrentMP > oldMP) msg += $",回内{Player.CurrentMP - oldMP}";
                            logs.Add(msg + "。(消耗本次出手)");
                            if (item.Quantity <= 0) Player.Inventory.RemoveItem(item.Id, 1);
                        }
                        else
                        {
                            logs.Add($"{Player.Name}未能服下{item.Name}。");
                        }
                    }
                    ResetChargeAfterAction(true);
                }
                else if (chosen.IsInternal && chosen.Art is InternalArt intArt)
                {
                    int mpCost = intArt.GetMPCost(Player.GetTotalMaxMP());
                    Player.CurrentMP -= mpCost;
                    intArt.UseSkill();
                    int defBoost = (int)(intArt.GetDefenseBonus() * intArt.GetProficiencyDamageMultiplier());
                    Player.AddTempBuff("defense_boost", defBoost);
                    var intLogs = new List<string> { $"{Player.Name}运起【{intArt.Name}】，防御临时提升{defBoost}！(消耗{mpCost}内力)" };
                    ApplyInternalSkillBuffs(Player, intArt, intLogs);
                    logs.AddRange(intLogs);
                    GainArtProficiencyForPlayer(intArt, 3);
                    ResetChargeAfterAction(true);
                }
                else
                {
                    string log = ExecuteAttack(Player, Opponent, chosen.Art as ExternalArt);
                    logs.Add(log);
                    if (chosen.Art != null)
                        GainArtProficiencyForPlayer(chosen.Art, 3);
                    ResetChargeAfterAction(true);
                }
            }
            else
            {
                // NPC行动 - AI自动选择
                string log = ExecuteNPCAction();
                logs.Add(log);
                ResetChargeAfterAction(false);
            }

            actionsTaken++;
            CheckCombatEnd();
        }

        Result.TotalRounds = Round;
        foreach (var log in logs)
            Result.AddLog(log);

        return logs;
    }

    private string HandleFlee()
    {
        if (IsSpar)
        {
            Result.Outcome = CombatOutcome.Surrendered;
            return $"{Player.Name}拱手认输。";
        }
        else
        {
            if (Random.Shared.NextDouble() < 0.3)
            {
                Result.Outcome = CombatOutcome.Fled;
                return $"{Player.Name}成功逃走了！";
            }
            return $"{Player.Name}尝试逃跑，但失败了！";
        }
    }

    private string ExecuteAttack(CharacterBase attacker, CharacterBase defender, ExternalArt? usedArt)
    {
        string attackName = usedArt?.Name ?? "普通拳脚";

        // 消耗MP和设置CD
        if (usedArt != null)
        {
            attacker.CurrentMP -= usedArt.MPCost;
            usedArt.UseSkill();
        }

        // ── 闪避被动:轻功 + 内功 + 外功的 Evasion ──
        double evasion = 0;
        string? evasionArtName = null;
        if (defender.ActiveLightArt != null)
        {
            double ev = defender.ActiveLightArt.GetEvasionChance();
            if (ev > 0) { evasion += ev; evasionArtName = defender.ActiveLightArt.Name; }
        }
        if (defender.ActiveInternalArt != null)
            foreach (var e in defender.ActiveInternalArt.Effects)
                if (e.Type == EffectType.Evasion && e.IsUnlocked(defender.ActiveInternalArt.Level)) { evasion += e.Value; if (evasionArtName == null) evasionArtName = defender.ActiveInternalArt.Name; }
        foreach (var ea in defender.ActiveExternalArts)
            foreach (var e in ea.Effects)
                if (e.Type == EffectType.Evasion && e.IsUnlocked(ea.Level)) { evasion += e.Value; if (evasionArtName == null) evasionArtName = ea.Name; }
        evasion = Math.Min(0.95, evasion);
        if (evasion > 0 && Random.Shared.NextDouble() < evasion)
        {
            if (usedArt != null && attacker == Player) GainArtProficiencyForPlayer(usedArt, 2);
            // 防御方触发闪避: 给防御方轻功加熟练度
            if (defender == Player && defender.ActiveLightArt != null) GainArtProficiencyForPlayer(defender.ActiveLightArt, 3);
            return $"{attacker.Name}使出【{attackName}】,但{defender.Name}身形飘忽,凭借【{evasionArtName}】闪避了攻击!";
        }

        var damageResult = DamageCalculator.Calculate(attacker, defender, usedArt);

        // ── 轻功被动减伤(永久生效,例如一苇渡江-5%) ──
        if (defender.ActiveLightArt != null)
        {
            double reduction = defender.ActiveLightArt.GetPassiveDamageReduction();
            if (reduction > 0)
            {
                int reducibleDamage = Math.Max(0, damageResult.ActualDamage - damageResult.TrueDamage);
                damageResult.ActualDamage = Math.Max(1,
                    (int)(reducibleDamage * (1 - reduction)) + damageResult.TrueDamage);
            }
        }

        ApplyMPShield(defender, damageResult);

        int actualDmg = DealCombatDamage(defender, damageResult.ActualDamage);

        // 北冥类吸功只在攻击真正命中后结算。攻击吸功要求造成伤害；受击吸功即使护盾全挡也能触发。
        if (actualDmg > 0)
            ApplyMPSiphon(attacker, defender, EffectType.SiphonMPOnHit, 0.02, damageResult);
        ApplyMPSiphon(defender, attacker, EffectType.SiphonMPOnHurt, 0.01, damageResult);

        if (attacker == Player)
            Result.PlayerDamageDealt += actualDmg;
        else
            Result.PlayerDamageReceived += actualDmg;

        var log = $"{attacker.Name}使出【{attackName}】攻击{defender.Name}，" +
                  $"造成 {actualDmg} 点伤害";

        if (usedArt != null)
            log += $" (Lv.{usedArt.Level} {usedArt.GetLevelDamageMultiplier():P0})";
        if (damageResult.IsCrit) log += "（暴击！）";
        if (damageResult.TriggeredEffects.Count > 0)
            log += $" [{string.Join("，", damageResult.TriggeredEffects)}]";

        log += $" | {defender.Name} HP:{defender.CurrentHP}/{defender.GetTotalMaxHP()}";

        if (IsSpar && defender.CurrentHP <= 1)
            log += " （切磋点到为止）";

        // 使用外功后设置TimedBuff效果
        if (usedArt != null)
        {
            int artLv = usedArt.Level;
            foreach (var effect in usedArt.Effects)
            {
                if (!effect.IsUnlocked(artLv)) continue;

                switch (effect.Type)
                {
                    case EffectType.DamageReduction:
                        attacker.AddTimedBuff("damage_reduction", (int)(effect.Value * 100), 2);
                        log += $"\n  ↳ {attacker.Name}获得减伤{(int)(effect.Value * 100)}%(2回合)";
                        break;
                    case EffectType.ReflectDamage:
                        attacker.AddTimedBuff("reflect_damage", (int)(effect.Value * 100), 2);
                        log += $"\n  ↳ {attacker.Name}获得反弹{(int)(effect.Value * 100)}%(2回合)";
                        break;
                    case EffectType.NextAttackBoost:
                        attacker.AddTimedBuff("next_attack_boost", (int)(effect.Value * 100), 2);
                        log += $"\n  ↳ {attacker.Name}蓄力增伤{(int)(effect.Value * 100)}%(下次攻击)";
                        break;
                    case EffectType.ExtraActionNextRound:
                        // 读条加速:下一次出手时读条从 HalfResetValue 开始(一次性)
                        if (attacker == Player) _playerHalfReset = true;
                        else _opponentHalfReset = true;
                        log += $"\n  ↳ {attacker.Name}招式诡异,下一次出手读条加速!";
                        break;
                    case EffectType.Stun:
                    {
                        if (!effect.TryActivate(artLv)) break;
                        defender.AddTimedBuff("stun", 1, 2);
                        log += $"\n  ↳ {defender.Name}被点穴,下次出手将无法行动!";
                        break;
                    }
                    case EffectType.Bleed:
                    {
                        if (!effect.TryActivate(artLv)) break;
                        int bleedDmg = Math.Max(5, (int)(attacker.GetTotalAttack() * effect.Value * 0.25));
                        defender.AddTimedBuff("bleed", bleedDmg, 3);
                        log += $"\n  ↳ {defender.Name}中招流血,每回合-{bleedDmg}HP(3回合)";
                        break;
                    }
                    case EffectType.MPRecover:
                    {
                        if (!effect.TryActivate(artLv)) break;
                        int r = (int)(attacker.GetTotalMaxMP() * effect.Value);
                        if (r > 0) { attacker.RecoverMP(r); log += $"\n  ↳ {attacker.Name}借招回内{r}"; }
                        break;
                    }
                    case EffectType.HPRecover:
                    {
                        if (!effect.TryActivate(artLv)) break;
                        int r = (int)(attacker.GetTotalMaxHP() * effect.Value);
                        if (r > 0) { attacker.Heal(r); log += $"\n  ↳ {attacker.Name}借招回血{r}"; }
                        break;
                    }
                    case EffectType.Knockback:
                    {
                        if (!effect.TryActivate(artLv)) break;
                        int delay = (int)(ChargeFull * effect.Value);
                        if (defender == Player) _playerCharge = Math.Max(0, _playerCharge - delay);
                        else _opponentCharge = Math.Max(0, _opponentCharge - delay);
                        log += $"\n  ↳ {defender.Name}被击退,出手延缓!";
                        break;
                    }
                    case EffectType.DoubleStrike:
                    {
                        if (!effect.TryActivate(artLv)) break;
                        if (defender.IsAlive && attacker.IsAlive)
                        {
                            int baseDmg2 = GetScaledAttackBase(attacker, usedArt);
                            int actualDouble = DamageCalculator.ComputeSecondaryDamage(baseDmg2, defender.GetTotalDefense(), 0.85);
                            int dealt = DealCombatDamage(defender, actualDouble);
                            log += $"\n  ↳ {attacker.Name}连击!对{defender.Name}造成 {dealt} 点伤害 | {defender.Name} HP:{defender.CurrentHP}/{defender.GetTotalMaxHP()}";
                            if (attacker == Player) Result.PlayerDamageDealt += dealt;
                            else Result.PlayerDamageReceived += dealt;
                        }
                        break;
                    }
                }
            }
        }

        // 内功连击(如葵花宝典):usedArt是外功/普攻时也检查内功的DoubleStrike
        if (attacker.ActiveInternalArt != null && defender.IsAlive && attacker.IsAlive)
        {
            foreach (var effect in attacker.ActiveInternalArt.Effects)
            {
                if (effect.Type == EffectType.DoubleStrike && effect.TryActivate(attacker.ActiveInternalArt.Level))
                {
                    int baseDmg2 = GetScaledAttackBase(attacker, usedArt);
                    int actualDouble = DamageCalculator.ComputeSecondaryDamage(baseDmg2, defender.GetTotalDefense(), 0.85);
                    int dealt = DealCombatDamage(defender, actualDouble);
                    log += $"\n  ↳ {attacker.Name}连击(内功)!对{defender.Name}造成 {dealt} 点伤害 | {defender.Name} HP:{defender.CurrentHP}/{defender.GetTotalMaxHP()}";
                    if (attacker == Player) Result.PlayerDamageDealt += dealt;
                    else Result.PlayerDamageReceived += dealt;
                    break;
                }
            }
        }

        // 消费蓄力增伤Buff（攻击后移除）
        if (attacker.HasTimedBuff("next_attack_boost"))
            attacker.TimedBuffs.Remove("next_attack_boost");

        // 反弹伤害
        if (damageResult.ReflectedDamage > 0 && attacker.IsAlive)
        {
            int reflected = DealCombatDamage(attacker, damageResult.ReflectedDamage);
            log += $"\n  ↳ 反弹伤害！{attacker.Name}受到 {reflected} 点反伤 | HP:{attacker.CurrentHP}/{attacker.GetTotalMaxHP()}";
            if (attacker == Player)
                Result.PlayerDamageReceived += reflected;
            else
                Result.PlayerDamageDealt += reflected;
        }

        // 反击（防御方自动攻击攻击者）
        if (damageResult.CounterAttackTriggered && defender.IsAlive && attacker.IsAlive)
        {
            var counterArt = defender.ActiveExternalArt;
            int counterBase = GetScaledAttackBase(defender, counterArt);
            int actualCounter = DamageCalculator.ComputeSecondaryDamage(counterBase, attacker.GetTotalDefense(), 0.6, 0.5);
            int dealt = DealCombatDamage(attacker, actualCounter);
            log += $"\n  ↳ {defender.Name}反击！对{attacker.Name}造成 {dealt} 点伤害 | {attacker.Name} HP:{attacker.CurrentHP}/{attacker.GetTotalMaxHP()}";
            if (defender == Player)
                Result.PlayerDamageDealt += dealt;
            else
                Result.PlayerDamageReceived += dealt;
        }

        // 追击（攻击方额外攻击一次，伤害60%）
        if (damageResult.ExtraAttackTriggered && attacker.IsAlive && defender.IsAlive)
        {
            var extraArt = usedArt ?? attacker.ActiveExternalArt;
            int extraBase = GetScaledAttackBase(attacker, extraArt);
            int actualExtra = DamageCalculator.ComputeSecondaryDamage(extraBase, defender.GetTotalDefense(), 0.6);
            int dealt = DealCombatDamage(defender, actualExtra);
            log += $"\n  ↳ {attacker.Name}追击！对{defender.Name}造成 {dealt} 点伤害 | {defender.Name} HP:{defender.CurrentHP}/{defender.GetTotalMaxHP()}";
            if (attacker == Player)
                Result.PlayerDamageDealt += dealt;
            else
                Result.PlayerDamageReceived += dealt;
        }

        return log;
    }

    /// <summary>
    /// 战斗结束清理：移除临时Buff
    /// </summary>
    public void CleanupCombat()
    {
        Player.ClearTempBuffs();
        Opponent.ClearTempBuffs();
    }

    /// <summary>
    /// 获取战斗结束后获得的阅历经验（用于UI展示）
    /// </summary>
    public int PlayerExpGained { get; private set; }

    /// <summary>
    /// 获取战斗结束后获得的阅历经验（用于UI展示）
    /// </summary>
    public int OpponentExpGained { get; private set; }

    private string ExecuteNPCAction()
    {
        var npc = Opponent;

        // NPC AI：低血量时运功防御；拥有内力护盾/吸功强化的内功会更积极地主动运功。
        bool hasTacticalInternal = npc.ActiveInternalArt?.Effects.Any(e =>
            e.IsUnlocked(npc.ActiveInternalArt.Level) &&
            e.Type is EffectType.MPShield or EffectType.SiphonMPOnHit or EffectType.SiphonMPOnHurt) == true;
        bool shouldUseInternal = npc.CurrentHP < npc.GetTotalMaxHP() * 0.4
            || (hasTacticalInternal
                && !npc.HasTimedBuff("mp_shield_boost")
                && !npc.HasTimedBuff("mp_siphon_boost")
                && Random.Shared.NextDouble() < 0.35);
        if (npc.ActiveInternalArt != null && npc.ActiveInternalArt.IsReady
            && npc.CurrentMP >= npc.ActiveInternalArt.GetMPCost(npc.GetTotalMaxMP())
            && shouldUseInternal
            && Random.Shared.NextDouble() < 0.5)
        {
            var intArt = npc.ActiveInternalArt;
            int mpCost = intArt.GetMPCost(npc.GetTotalMaxMP());
            npc.CurrentMP -= mpCost;
            intArt.UseSkill();
            int defBoost = (int)(intArt.GetDefenseBonus() * intArt.GetProficiencyDamageMultiplier());
            npc.AddTempBuff("defense_boost", defBoost);
            intArt.GainProficiency(5);
            var npcLogs = new List<string> { $"{npc.Name}运起【{intArt.Name}】，防御临时提升{defBoost}！(消耗{mpCost}内力)" };
            ApplyInternalSkillBuffs(npc, intArt, npcLogs);
            return string.Join("\n  ↳ ", npcLogs);
        }

        // NPC: 从已装备的外功中随机选一本可用的(CD好+MP够)
        var availableExt = npc.ActiveExternalArts
            .Where(a => a.IsReady && npc.CurrentMP >= a.MPCost)
            .ToList();
        if (availableExt.Count > 0)
        {
            var chosen = availableExt[Random.Shared.Next(availableExt.Count)];
            return ExecuteAttack(npc, Player, chosen);
        }

        return ExecuteAttack(npc, Player, null);
    }

    /// <summary>
    /// 处理角色内功的被动效果（每回合触发）
    /// </summary>
    private void ProcessInternalArtEffects(CharacterBase character)
    {
        // buff regen:每回合恢复 Value 比例最大HP(药buff/食buff均可,玩家专属,叠加)
        if (character is Player p)
        {
            double regenRate = 0;
            if (p.MedicineBuff != null && p.MedicineBuff.Matches("regen")) regenRate += p.MedicineBuff.Value;
            if (p.FoodBuff != null && p.FoodBuff.Matches("regen")) regenRate += p.FoodBuff.Value;
            if (regenRate > 0)
            {
                int recoverAmount = (int)(character.GetTotalMaxHP() * regenRate);
                if (recoverAmount > 0) character.Heal(recoverAmount);
            }
        }

        if (character.ActiveInternalArt == null) return;
        var intArt = character.ActiveInternalArt;
        int artLv = intArt.Level;

        foreach (var effect in intArt.Effects)
        {
            if (effect.Type == EffectType.MPRecover && effect.TryActivate(artLv))
            {
                int recoverAmount = (int)(character.GetTotalMaxMP() * effect.Value);
                if (recoverAmount > 0)
                {
                    character.RecoverMP(recoverAmount);
                }
            }

            // 被动HP恢复：每回合恢复一定比例最大HP
            if (effect.Type == EffectType.HPRecover && effect.TryActivate(artLv))
            {
                int recoverAmount = (int)(character.GetTotalMaxHP() * effect.Value);
                if (recoverAmount > 0)
                {
                    character.Heal(recoverAmount);
                }
            }
        }
    }

    /// <summary>
    /// 内功技能使用时设置TimedBuff效果（减伤、反弹等）
    /// </summary>
    private void ApplyInternalSkillBuffs(CharacterBase character, InternalArt intArt, List<string> logs)
    {
        int artLv = intArt.Level;
        foreach (var effect in intArt.Effects)
        {
            if (!effect.IsUnlocked(artLv)) continue;

            switch (effect.Type)
            {
                case EffectType.DamageReduction:
                    character.AddTimedBuff("damage_reduction", (int)(effect.Value * 100), 2);
                    logs.Add($"{character.Name}获得减伤{(int)(effect.Value * 100)}%(2回合)");
                    break;
                case EffectType.ReflectDamage:
                    character.AddTimedBuff("reflect_damage", (int)(effect.Value * 100), 2);
                    logs.Add($"{character.Name}获得反弹{(int)(effect.Value * 100)}%(2回合)");
                    break;
                case EffectType.MPShield:
                    character.AddTimedBuff("mp_shield_boost", 15, 2);
                    logs.Add($"{character.Name}气海大开，内力护盾额外抵消15%伤害(2回合)");
                    break;
                case EffectType.SiphonMPOnHit:
                case EffectType.SiphonMPOnHurt:
                    if (!character.HasTimedBuff("mp_siphon_boost"))
                    {
                        character.AddTimedBuff("mp_siphon_boost", 50, 2);
                        logs.Add($"{character.Name}运转海纳百川，吸取内力效率提高50%(2回合)");
                    }
                    break;
            }
        }
    }

    private static void ApplyMPShield(CharacterBase defender, DamageResult result)
    {
        var art = defender.ActiveInternalArt;
        if (art == null || defender.CurrentMP <= 0) return;

        double shieldRate = art.Effects
            .Where(e => e.Type == EffectType.MPShield && e.IsUnlocked(art.Level))
            .Sum(e => e.Value);
        if (shieldRate <= 0) return;

        shieldRate += defender.GetTimedBuffValue("mp_shield_boost") / 100.0;
        shieldRate = Math.Min(0.90, shieldRate);
        int reducibleDamage = Math.Max(0, result.ActualDamage - result.TrueDamage);
        int mpSpent = Math.Min(defender.CurrentMP, (int)(reducibleDamage * shieldRate));
        if (mpSpent <= 0) return;

        defender.CurrentMP -= mpSpent;
        result.ActualDamage = Math.Max(0, result.ActualDamage - mpSpent);
        result.TriggeredEffects.Add($"{art.Name}耗内{mpSpent}抵消{mpSpent}伤害");
    }

    private void ApplyMPSiphon(CharacterBase siphoner, CharacterBase target, EffectType trigger,
        double singleCapRate, DamageResult result)
    {
        var art = siphoner.ActiveInternalArt;
        if (art == null || target.CurrentMP <= 0) return;

        double rate = art.Effects
            .Where(e => e.Type == trigger && e.IsUnlocked(art.Level))
            .Sum(e => e.Value);
        if (rate <= 0) return;

        rate *= 1 + siphoner.GetTimedBuffValue("mp_siphon_boost") / 100.0;
        double mpResist = target.ActiveInternalArt?.Effects
            .Where(e => e.Type == EffectType.MPResist && e.IsUnlocked(target.ActiveInternalArt.Level))
            .Sum(e => e.Value) ?? 0;
        mpResist = Math.Clamp(mpResist, 0, 1);

        int maxMP = siphoner.GetTotalMaxMP();
        int singleCap = Math.Max(1, (int)(maxMP * singleCapRate));
        int roundCap = Math.Max(1, (int)(maxMP * 0.08));
        int alreadySiphoned = _mpSiphonedThisRound.GetValueOrDefault(siphoner, 0);
        int storage = Math.Max(0, maxMP - siphoner.CurrentMP);
        int amount = (int)(target.CurrentMP * rate * (1 - mpResist));
        amount = Math.Min(amount, Math.Min(singleCap, Math.Min(roundCap - alreadySiphoned, storage)));
        if (amount <= 0) return;

        target.CurrentMP -= amount;
        siphoner.RecoverMP(amount);
        _mpSiphonedThisRound[siphoner] = alreadySiphoned + amount;
        string source = trigger == EffectType.SiphonMPOnHit ? "攻敌" : "受击";
        result.TriggeredEffects.Add($"{art.Name}{source}吸取{amount}内力" + (mpResist > 0 ? $"(被抵抗{mpResist:P0})" : ""));
    }

    /// <summary>
    /// 处理流血DoT:回合开始时对带有"bleed"标记的角色造成固定伤害。
    /// 在 TickTimedBuffs 之前结算,确保本回合流血仍生效。
    /// </summary>
    private void ProcessBleed(CharacterBase character, List<string> logs)
    {
        if (!character.IsAlive) return;
        if (!character.HasTimedBuff("bleed")) return;
        int dmg = character.GetTimedBuffValue("bleed");
        if (dmg <= 0) return;
        int dealt = DealCombatDamage(character, dmg);
        logs.Add($"  ↳ {character.Name}流血不止,损失 {dealt} HP | HP:{character.CurrentHP}/{character.GetTotalMaxHP()}");
        if (character == Player) Result.PlayerDamageReceived += dealt;
        else Result.PlayerDamageDealt += dealt;
    }

    /// <summary>外功基础伤害同时计入武功等级倍率，供连击/反击/追击等次要伤害使用。</summary>
    private static int GetScaledAttackBase(CharacterBase attacker, ExternalArt? art)
    {
        if (art == null) return attacker.GetTotalAttack();
        return (int)(art.CalculateBaseDamage(attacker.GetTotalAttack()) * art.GetProficiencyDamageMultiplier());
    }

    /// <summary>统一结算战斗伤害；切磋时任何伤害来源都只能将气血降至1。</summary>
    private int DealCombatDamage(CharacterBase target, int damage)
    {
        int capped = Math.Max(0, damage);
        if (IsSpar)
            capped = Math.Min(capped, Math.Max(0, target.CurrentHP - 1));
        return target.TakeDamage(capped);
    }

    private void TickAllCooldowns(CharacterBase character)
    {
        // 所有已装备的外功 CD 递减(玩家可有多本)
        foreach (var ea in character.ActiveExternalArts)
            ea.TickCooldown();
        character.ActiveInternalArt?.TickCooldown();
    }

    /// <summary>
    /// 给玩家武功累加熟练度,自动应用玩家悟性倍率(TalentMultiplier)。
    /// 用 baseAmount 表示基础点数,悟性5时实际等于 baseAmount。
    /// </summary>
    private void GainArtProficiencyForPlayer(MartialArtBase art, int baseAmount)
    {
        if (art == null) return;
        double mult = Player is Player p ? p.EffectiveTrainingMultiplier : 1.0;
        int actual = Math.Max(1, (int)Math.Round(baseAmount * mult));
        art.GainProficiency(actual);
    }

    /// <summary>
    /// 计算战斗阅历经验（在EndCombat前调用）
    /// </summary>
    public void CalculateExpRewards()
    {
        // 玩家经验:随对手江湖等级二次方曲线成长(与升级曲线 50+level²×2 匹配)。
        // 打5级对手≈22, 35级≈622, 60级≈1810, 80级≈3210。
        // 越级挑战高风险高回报;低级对手几乎不给经验。
        int oppLevel = Math.Max(1, Opponent.JianghuLevel);
        int baseExp = 10 + oppLevel * oppLevel / 2;

        if (Result.Outcome == CombatOutcome.PlayerWin)
        {
            // 实战胜利给全额;切磋给1/4(切磋可反复刷且无死亡风险,需控制)
            PlayerExpGained = IsSpar ? Math.Max(3, baseExp / 4) : baseExp;
        }
        else if (Result.Outcome == CombatOutcome.NPCWin)
        {
            PlayerExpGained = IsSpar ? 2 : Math.Max(3, baseExp / 8); // 输了也学到东西
        }
        else
        {
            PlayerExpGained = IsSpar ? 1 : Math.Max(2, baseExp / 10);
        }

        // NPC获得的经验
        int playerPower = Player.BaseAttack + Player.BaseDefense;
        int npcBaseExp = 5 + playerPower / 30;
        if (Result.Outcome == CombatOutcome.NPCWin)
            OpponentExpGained = Math.Clamp(npcBaseExp, 5, 20);
        else
            OpponentExpGained = Math.Clamp(npcBaseExp / 2, 2, 10);
    }

    /// <summary>
    /// 战后给玩家装备的内/外/轻功一次性结算熟练度。
    /// 基础10点,按战斗结果(胜负)、对手强度、回合数与玩家悟性进行调节。
    /// 返回每件武功获得的熟练度列表用于日志展示。
    /// </summary>
    public List<(MartialArtBase art, int gained)> SettleCombatProficiency()
    {
        var results = new List<(MartialArtBase, int)>();
        if (Player is not Player p) return results;

        // 基础点数:对手越强、回合越长给得越多
        int opponentPower = Opponent.BaseAttack + Opponent.BaseDefense;
        double basePoints = 10 + opponentPower / 100.0 + Round * 1.5;

        // 胜负倍率
        double outcomeMult = Result.Outcome switch
        {
            CombatOutcome.PlayerWin => IsSpar ? 0.8 : 1.0,
            CombatOutcome.NPCWin    => 0.4,  // 输了也学到东西
            CombatOutcome.Surrendered or CombatOutcome.Fled => 0.3,
            _ => 0.5
        };

        double talentMult = p.EffectiveTrainingMultiplier;
        int finalAmount = Math.Max(1, (int)Math.Round(basePoints * outcomeMult * talentMult));

        // 给玩家所有装备中的武功结算(外功是 List,可有多本)
        if (p.ActiveInternalArt != null)
        {
            p.ActiveInternalArt.GainProficiency(finalAmount);
            results.Add((p.ActiveInternalArt, finalAmount));
        }
        foreach (var ea in p.ActiveExternalArts)
        {
            ea.GainProficiency(finalAmount);
            results.Add((ea, finalAmount));
        }
        if (p.ActiveLightArt != null)
        {
            p.ActiveLightArt.GainProficiency(finalAmount);
            results.Add((p.ActiveLightArt, finalAmount));
        }
        return results;
    }

    private void CheckCombatEnd()
    {
        if (Result.Outcome != CombatOutcome.InProgress) return;

        if (IsSpar)
        {
            if (Player.CurrentHP <= 1)
                Result.Outcome = CombatOutcome.NPCWin;
            else if (Opponent.CurrentHP <= 1)
                Result.Outcome = CombatOutcome.PlayerWin;
        }
        else
        {
            if (!Player.IsAlive)
                Result.Outcome = CombatOutcome.NPCWin;
            else if (!Opponent.IsAlive)
                Result.Outcome = CombatOutcome.PlayerWin;
        }
    }

    public void RestoreOpponentPower()
    {
        if (OpponentHiddenPower)
        {
            Opponent.BaseAttack = _opponentRealAttack;
            Opponent.BaseDefense = _opponentRealDefense;
            OpponentHiddenPower = false;
        }
    }

    public bool IsCombatOver => Result.Outcome != CombatOutcome.InProgress;

    /// <summary>
    /// 获取当前回合的行动顺序展示
    /// </summary>
    public string GetActionOrderDisplay()
    {
        var parts = new List<string>();
        foreach (bool isPlayer in _actionQueue)
        {
            parts.Add(isPlayer ? Player.Name : Opponent.Name);
        }
        return string.Join(" → ", parts);
    }
}

public class SkillOption
{
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set; }
    public int MPCost { get; set; }
    public int CooldownLeft { get; set; }
    public MartialArtBase? Art { get; set; }
    public bool IsFlee { get; set; }
    public bool IsInternal { get; set; }

    /// <summary>是否为"使用物品"(消耗品)。true 时 ItemId 指向背包物品。</summary>
    public bool IsItem { get; set; }
    public string? ItemId { get; set; }

    public string DisplayText
    {
        get
        {
            if (IsFlee) return Name;
            if (IsItem) return Name;
            if (!IsAvailable)
            {
                if (CooldownLeft > 0) return $"{Name} (CD:{CooldownLeft}回合)";
                return $"{Name} (内力不足)";
            }
            var cost = MPCost > 0 ? $" MP:{MPCost}" : "";
            return $"{Name}{cost}";
        }
    }
}
