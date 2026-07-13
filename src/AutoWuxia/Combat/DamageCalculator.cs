using AutoWuxia.Characters;
using AutoWuxia.MartialArts;

namespace AutoWuxia.Combat;

public class DamageResult
{
    public int RawDamage { get; set; }
    public int ActualDamage { get; set; }
    /// <summary>实际伤害中不受百分比减伤影响的真实伤害部分</summary>
    public int TrueDamage { get; set; }
    public int MPCost { get; set; }
    public bool IsCrit { get; set; }
    public double ProficiencyMultiplier { get; set; } = 1.0;
    public List<string> TriggeredEffects { get; set; } = new();

    /// <summary>反弹给攻击者的伤害</summary>
    public int ReflectedDamage { get; set; }

    /// <summary>是否触发了反击</summary>
    public bool CounterAttackTriggered { get; set; }

    /// <summary>实际触发反击的外功；内功反击时为空。</summary>
    public ExternalArt? CounterAttackArt { get; set; }

    /// <summary>反击伤害倍率，来自触发词条的 Value。</summary>
    public double CounterAttackMultiplier { get; set; } = 0.6;

    /// <summary>是否触发了追击</summary>
    public bool ExtraAttackTriggered { get; set; }

    /// <summary>追击伤害倍率，来自触发词条的 Value。</summary>
    public double ExtraAttackMultiplier { get; set; } = 0.6;
}

public static class DamageCalculator
{
    // K 越低，防御转化为减伤的效率越高。防御=K 时减伤 50%。
    private const double DefenseConstant = 700.0;
    private const double MaxReduction = 0.70;

    public static DamageResult Calculate(CharacterBase attacker, CharacterBase defender, MartialArtBase? usedArt = null)
    {
        var result = new DamageResult();
        var externalArt = usedArt as ExternalArt;
        int attack = attacker.GetTotalAttack();

        result.RawDamage = externalArt != null
            ? externalArt.CalculateBaseDamage(attack)
            : attack;

        // 应用熟练度系数(等级倍率)
        if (externalArt != null)
        {
            result.ProficiencyMultiplier = externalArt.GetProficiencyDamageMultiplier();
            result.RawDamage = (int)(result.RawDamage * result.ProficiencyMultiplier);
        }

        // 蓄力增伤 TimedBuff
        if (attacker.HasTimedBuff("next_attack_boost"))
        {
            int boostPercent = attacker.GetTimedBuffValue("next_attack_boost");
            result.RawDamage = (int)(result.RawDamage * (1 + boostPercent / 100.0));
            result.TriggeredEffects.Add($"蓄力增伤 +{boostPercent}%");
        }

        // 散功压制：吸星大法将目标内力吸至低位后，暂时削弱其后续伤害。
        if (attacker.HasTimedBuff("damage_dealt_reduction"))
        {
            int reduction = attacker.GetTimedBuffValue("damage_dealt_reduction");
            result.RawDamage = Math.Max(1, (int)(result.RawDamage * (1 - reduction / 100.0)));
            result.TriggeredEffects.Add($"散功压制 -{reduction}%伤害");
        }

        // 暴击(×1.5伤害)
        if (externalArt != null && externalArt.CritChance > 0 && Random.Shared.NextDouble() <= externalArt.CritChance)
        {
            result.IsCrit = true;
            result.RawDamage = (int)(result.RawDamage * 1.5);
            result.TriggeredEffects.Add("暴击！");
        }

        // ── 分离:无视防御(降低有效防御) vs 真实伤害(绕过百分比减伤) ──
        double ignoreDef = 0;
        double trueDamageRate = 0;
        int artLevel = externalArt?.Level ?? 99;

        if (externalArt != null)
        {
            foreach (var effect in externalArt.Effects.Where(e => e.Type == EffectType.IgnoreDefense))
            {
                if (effect.TryActivate(artLevel))
                {
                    ignoreDef += effect.Value;
                    result.TriggeredEffects.Add($"无视防御 {effect.Value:P0}");
                }
            }

            foreach (var effect in externalArt.Effects.Where(e => e.Type == EffectType.TrueDamage))
            {
                if (effect.TryActivate(artLevel))
                {
                    trueDamageRate += effect.Value;
                    result.TriggeredEffects.Add($"真实伤害 {effect.Value:P0}");
                }
            }
        }

        // 内功真实伤害(如九阳神功Lv8)
        var atkIntArt = attacker.ActiveInternalArt;
        if (atkIntArt != null)
        {
            foreach (var effect in atkIntArt.Effects.Where(e => e.Type == EffectType.TrueDamage))
            {
                if (effect.TryActivate(atkIntArt.Level))
                {
                    trueDamageRate += effect.Value;
                    result.TriggeredEffects.Add($"真实伤害(内功) {effect.Value:P0}");
                }
            }
        }

        ApplyDefenderMitigation(result, defender, ignoreDef, trueDamageRate);

        // 防御方MPResist(抗吸内,如易筋经)
        double mpResist = 0;
        if (defender.ActiveInternalArt != null)
            mpResist = defender.ActiveInternalArt.Effects
                .Where(e => e.Type == EffectType.MPResist && e.IsUnlocked(defender.ActiveInternalArt.Level))
                .Sum(e => e.Value);
        mpResist = Math.Clamp(mpResist, 0, 1);

        // 吸内力:外功(化骨大法) + 内功(吸星大法)
        if (externalArt != null)
        {
            foreach (var effect in externalArt.Effects.Where(e => e.Type == EffectType.DrainMP))
            {
                if (effect.TryActivate(artLevel))
                {
                    int drainAmount = (int)(defender.CurrentMP * effect.Value * (1 - mpResist));
                    if (drainAmount > 0)
                    {
                        defender.CurrentMP = Math.Max(0, defender.CurrentMP - drainAmount);
                        result.TriggeredEffects.Add($"消耗对方 {drainAmount} 内力" + (mpResist > 0 ? $"(被抗{mpResist:P0})" : ""));
                    }
                }
            }
        }
        if (attacker.ActiveInternalArt != null)
        {
            foreach (var effect in attacker.ActiveInternalArt.Effects.Where(e => e.Type == EffectType.DrainMP))
            {
                if (effect.TryActivate(attacker.ActiveInternalArt.Level))
                {
                    double drainBoost = 1 + attacker.GetTimedBuffValue("drain_mp_boost") / 100.0;
                    double drainRate = Math.Min(1.0, effect.Value * drainBoost);
                    int drainAmount = (int)(defender.CurrentMP * drainRate * (1 - mpResist));
                    if (drainAmount > 0)
                    {
                        defender.CurrentMP = Math.Max(0, defender.CurrentMP - drainAmount);
                        int beforeRecover = attacker.CurrentMP;
                        attacker.RecoverMP(drainAmount);
                        int recovered = attacker.CurrentMP - beforeRecover;
                        result.TriggeredEffects.Add(
                            $"{attacker.ActiveInternalArt.Name}吸取对方 {drainAmount} 内力" +
                            (recovered > 0 ? $"(自身恢复{recovered})" : "") +
                            (mpResist > 0 ? $"(被抗{mpResist:P0})" : ""));

                        double drainedDamageRate = attacker.ActiveInternalArt.Effects
                            .Where(e => e.Type == EffectType.DrainedMPDamage
                                && e.IsUnlocked(attacker.ActiveInternalArt.Level))
                            .Sum(e => e.Value);
                        int drainedDamage = (int)(drainAmount * drainedDamageRate);
                        if (drainedDamage > 0)
                        {
                            result.TrueDamage += drainedDamage;
                            result.ActualDamage += drainedDamage;
                            result.TriggeredEffects.Add($"异种真气反噬 +{drainedDamage}真实伤害");
                        }

                        foreach (var exhaustion in attacker.ActiveInternalArt.Effects
                                     .Where(e => e.Type == EffectType.MPExhaustion))
                        {
                            if (!exhaustion.TryActivate(attacker.ActiveInternalArt.Level)) continue;
                            if (defender.CurrentMP > defender.GetTotalMaxMP() * exhaustion.Value) continue;

                            int weakenPercent = Math.Max(1, (int)(exhaustion.Value * 100));
                            defender.AddTimedBuff("damage_dealt_reduction", weakenPercent, 2);
                            result.TriggeredEffects.Add($"散功压制 -{weakenPercent}%伤害(2回合)");
                            break;
                        }
                    }
                }
            }
        }

        // 防御方反击检测（外功被动：被攻击时概率反击）。
        // 多外功装备后必须记录真正触发反击的武功，否则会错误使用列表第一本的系数。
        foreach (var defExternalArt in defender.ActiveExternalArts)
        {
            foreach (var effect in defExternalArt.Effects.Where(e => e.Type == EffectType.CounterAttack))
            {
                if (effect.TryActivate(defExternalArt.Level))
                {
                    result.CounterAttackTriggered = true;
                    result.CounterAttackArt = defExternalArt;
                    result.CounterAttackMultiplier = effect.Value;
                    result.TriggeredEffects.Add("触发反击！");
                    break; // 只触发一次
                }
            }
            if (result.CounterAttackTriggered) break;
        }
        // 内功反击(如蛤蟆功/玉女心经)
        if (!result.CounterAttackTriggered && defender.ActiveInternalArt != null)
        {
            foreach (var effect in defender.ActiveInternalArt.Effects.Where(e => e.Type == EffectType.CounterAttack))
            {
                if (effect.TryActivate(defender.ActiveInternalArt.Level))
                {
                    result.CounterAttackTriggered = true;
                    result.CounterAttackMultiplier = effect.Value;
                    result.TriggeredEffects.Add("触发反击(内功)！");
                    break;
                }
            }
        }

        // 攻击方追击检测（外功被动：攻击后概率追加攻击）
        if (externalArt != null)
        {
            foreach (var effect in externalArt.Effects.Where(e => e.Type == EffectType.ExtraAttack))
            {
                if (effect.TryActivate(artLevel))
                {
                    result.ExtraAttackTriggered = true;
                    result.ExtraAttackMultiplier = effect.Value;
                    result.TriggeredEffects.Add("触发追击！");
                    break; // 只触发一次
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 次要伤害（连击/反击/追击）完整结算防御层，但不再次触发连击、反击、追击、吸内等攻击效果。
    /// </summary>
    /// <param name="attacker">次要攻击的发起者</param>
    /// <param name="defender">次要攻击的承受者</param>
    /// <param name="sourceArt">本次攻击沿用的外功，用于无视防御与真实伤害</param>
    /// <param name="rawBase">基础伤害(已含武功系数与等级倍率)</param>
    /// <param name="damageMultiplier">对应连击/反击/追击词条的伤害倍率</param>
    /// <param name="defenseFactor">有效防御系数(默认1.0;反击0.5=只算一半防御)</param>
    public static DamageResult CalculateSecondary(CharacterBase attacker, CharacterBase defender,
        ExternalArt? sourceArt, int rawBase, double damageMultiplier = 1.0, double defenseFactor = 1.0)
    {
        var result = new DamageResult
        {
            RawDamage = Math.Max(1, (int)(rawBase * Math.Max(0, damageMultiplier)))
        };

        if (attacker.HasTimedBuff("damage_dealt_reduction"))
        {
            int reduction = attacker.GetTimedBuffValue("damage_dealt_reduction");
            result.RawDamage = Math.Max(1, (int)(result.RawDamage * (1 - reduction / 100.0)));
            result.TriggeredEffects.Add($"散功压制 -{reduction}%伤害");
        }

        double ignoreDef = 0;
        double trueDamageRate = 0;
        if (sourceArt != null)
        {
            foreach (var effect in sourceArt.Effects.Where(e => e.Type == EffectType.IgnoreDefense))
            {
                if (!effect.TryActivate(sourceArt.Level)) continue;
                ignoreDef += effect.Value;
                result.TriggeredEffects.Add($"无视防御 {effect.Value:P0}");
            }
            foreach (var effect in sourceArt.Effects.Where(e => e.Type == EffectType.TrueDamage))
            {
                if (!effect.TryActivate(sourceArt.Level)) continue;
                trueDamageRate += effect.Value;
                result.TriggeredEffects.Add($"真实伤害 {effect.Value:P0}");
            }
        }

        var internalArt = attacker.ActiveInternalArt;
        if (internalArt != null)
        {
            foreach (var effect in internalArt.Effects.Where(e => e.Type == EffectType.TrueDamage))
            {
                if (!effect.TryActivate(internalArt.Level)) continue;
                trueDamageRate += effect.Value;
                result.TriggeredEffects.Add($"真实伤害(内功) {effect.Value:P0}");
            }
        }

        ApplyDefenderMitigation(result, defender, ignoreDef, trueDamageRate, defenseFactor);
        return result;
    }

    private static void ApplyDefenderMitigation(DamageResult result, CharacterBase defender,
        double ignoreDef, double trueDamageRate, double defenseFactor = 1.0)
    {
        int defense = defender.GetTotalDefense();
        int effDef = Math.Max(0, (int)(defense * Math.Max(0, defenseFactor)
            * (1 - Math.Min(ignoreDef, 1.0))));
        double normalDamage = result.RawDamage * DefenseConstant / (effDef + DefenseConstant);
        double trueDamage = result.RawDamage * Math.Min(trueDamageRate, 1.0);

        var defInternalArt = defender.ActiveInternalArt;
        if (defInternalArt != null)
        {
            foreach (var effect in defInternalArt.Effects.Where(e => e.Type == EffectType.FlatDamageReduction))
            {
                if (!effect.IsUnlocked(defInternalArt.Level)) continue;
                int flatReduce = (int)effect.Value;
                normalDamage -= flatReduce;
                result.TriggeredEffects.Add($"固定减伤 (-{flatReduce})");
            }
        }

        double reductionPct = 0;
        if (defender.HasTimedBuff("damage_reduction"))
        {
            int reduction = defender.GetTimedBuffValue("damage_reduction");
            reductionPct += reduction / 100.0;
            result.TriggeredEffects.Add($"减伤 {reduction}%");
        }
        if (defender.HasTimedBuff("adaptive_defense"))
        {
            int reduction = defender.GetTimedBuffValue("adaptive_defense");
            reductionPct += reduction / 100.0;
            result.TriggeredEffects.Add($"易筋护体 {reduction}%");
        }
        if (defInternalArt != null)
        {
            foreach (var effect in defInternalArt.Effects.Where(e => e.Type == EffectType.DamageReduction))
            {
                if (!effect.TryActivate(defInternalArt.Level)) continue;
                reductionPct += effect.Value;
                result.TriggeredEffects.Add($"护体减伤 {(int)(effect.Value * 100)}%");
            }
        }
        reductionPct = Math.Min(reductionPct, MaxReduction);
        normalDamage = Math.Max(0, normalDamage) * (1 - reductionPct);
        result.TrueDamage = Math.Max(0, (int)trueDamage);
        result.ActualDamage = Math.Max(1, (int)normalDamage + result.TrueDamage);

        if (defender.ActiveLightArt != null)
        {
            double lightReduction = defender.ActiveLightArt.GetPassiveDamageReduction();
            if (lightReduction > 0)
            {
                int reducibleDamage = Math.Max(0, result.ActualDamage - result.TrueDamage);
                result.ActualDamage = Math.Max(1,
                    (int)(reducibleDamage * (1 - lightReduction)) + result.TrueDamage);
                result.TriggeredEffects.Add($"轻功减伤 {(int)(lightReduction * 100)}%");
            }
        }
    }

    /// <summary>在轻功与内力护盾结算完成后，按最终实际伤害计算反弹。</summary>
    public static void ApplyReflection(CharacterBase defender, DamageResult result)
    {
        var defInternalArt = defender.ActiveInternalArt;
        if (defender.HasTimedBuff("reflect_damage") && result.ActualDamage > 0)
        {
            int reflectPercent = defender.GetTimedBuffValue("reflect_damage");
            result.ReflectedDamage = (int)(result.ActualDamage * reflectPercent / 100.0);
            if (result.ReflectedDamage > 0)
                result.TriggeredEffects.Add($"反弹 {reflectPercent}% ({result.ReflectedDamage})");
        }

        if (defInternalArt == null || result.ActualDamage <= 0) return;
        foreach (var effect in defInternalArt.Effects.Where(e => e.Type == EffectType.ReflectDamage))
        {
            if (!effect.TryActivate(defInternalArt.Level)) continue;
            int reflected = (int)(result.ActualDamage * effect.Value);
            result.ReflectedDamage += reflected;
            result.TriggeredEffects.Add($"反伤 {(int)(effect.Value * 100)}% (+{reflected})");
        }
    }
}
