using AutoWuxia.Characters;
using AutoWuxia.MartialArts;

namespace AutoWuxia.Combat;

public class DamageResult
{
    public int RawDamage { get; set; }
    public int ActualDamage { get; set; }
    public int MPCost { get; set; }
    public bool IsCrit { get; set; }
    public double ProficiencyMultiplier { get; set; } = 1.0;
    public List<string> TriggeredEffects { get; set; } = new();

    /// <summary>反弹给攻击者的伤害</summary>
    public int ReflectedDamage { get; set; }

    /// <summary>是否触发了反击</summary>
    public bool CounterAttackTriggered { get; set; }

    /// <summary>是否触发了追击</summary>
    public bool ExtraAttackTriggered { get; set; }
}

public static class DamageCalculator
{
    public static DamageResult Calculate(CharacterBase attacker, CharacterBase defender, MartialArtBase? usedArt = null)
    {
        const double DefConstant = 800.0;    // 防御收益常数K:防=K时减伤50%
        const double MaxReduction = 0.70;    // 减伤buff叠加上限70%

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

        // ── 主伤害:百分比减伤 damage = Raw × K / (effDef + K) ──
        // 无视防御降低有效防御值;防=K减伤50%,防=2K减伤67%,低防几乎不减
        int defense = defender.GetTotalDefense();
        int effDef = (int)(defense * (1 - Math.Min(ignoreDef, 1.0)));
        double damage = result.RawDamage * DefConstant / (effDef + DefConstant);

        // ── 真实伤害:绕过百分比减伤,按 Raw 的比例全额附加 ──
        damage += result.RawDamage * Math.Min(trueDamageRate, 1.0);

        // ── 固定减伤(百分比后扣减,如坐忘功Lv8 -10) ──
        var defInternalArt = defender.ActiveInternalArt;
        if (defInternalArt != null)
        {
            foreach (var effect in defInternalArt.Effects.Where(e => e.Type == EffectType.FlatDamageReduction))
            {
                if (effect.IsUnlocked(defInternalArt.Level))
                {
                    int flatReduce = (int)effect.Value;
                    damage -= flatReduce;
                    result.TriggeredEffects.Add($"固定减伤 (-{flatReduce})");
                }
            }
        }

        // ── 减伤buff(TimedBuff + 内功被动):百分比叠加,上限70% ──
        double reductionPct = 0;
        if (defender.HasTimedBuff("damage_reduction"))
        {
            int r = defender.GetTimedBuffValue("damage_reduction");
            reductionPct += r / 100.0;
            result.TriggeredEffects.Add($"减伤 {r}%");
        }
        if (defInternalArt != null)
        {
            foreach (var effect in defInternalArt.Effects.Where(e => e.Type == EffectType.DamageReduction))
            {
                if (effect.TryActivate(defInternalArt.Level))
                {
                    reductionPct += effect.Value;
                    result.TriggeredEffects.Add($"护体减伤 {(int)(effect.Value * 100)}%");
                }
            }
        }
        reductionPct = Math.Min(reductionPct, MaxReduction);
        damage *= (1 - reductionPct);

        result.ActualDamage = Math.Max(1, (int)damage);

        // 反弹伤害 TimedBuff
        if (defender.HasTimedBuff("reflect_damage") && result.ActualDamage > 0)
        {
            int reflectPercent = defender.GetTimedBuffValue("reflect_damage");
            result.ReflectedDamage = (int)(result.ActualDamage * reflectPercent / 100.0);
            if (result.ReflectedDamage > 0)
                result.TriggeredEffects.Add($"反弹 {reflectPercent}% ({result.ReflectedDamage})");
        }

        // 反弹被动效果（内功等级解锁）
        if (defInternalArt != null && result.ActualDamage > 0)
        {
            foreach (var effect in defInternalArt.Effects.Where(e => e.Type == EffectType.ReflectDamage))
            {
                if (effect.TryActivate(defInternalArt.Level))
                {
                    int reflected = (int)(result.ActualDamage * effect.Value);
                    result.ReflectedDamage += reflected;
                    result.TriggeredEffects.Add($"反伤 {(int)(effect.Value * 100)}% (+{reflected})");
                }
            }
        }

        // 防御方MPResist(抗吸内,如易筋经)
        double mpResist = 0;
        if (defender.ActiveInternalArt != null)
            mpResist = defender.ActiveInternalArt.Effects
                .Where(e => e.Type == EffectType.MPResist && e.IsUnlocked(defender.ActiveInternalArt.Level))
                .Sum(e => e.Value);

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
                    int drainAmount = (int)(defender.CurrentMP * effect.Value * (1 - mpResist));
                    if (drainAmount > 0)
                    {
                        defender.CurrentMP = Math.Max(0, defender.CurrentMP - drainAmount);
                        result.TriggeredEffects.Add($"消耗对方 {drainAmount} 内力(内功)" + (mpResist > 0 ? $"(被抗{mpResist:P0})" : ""));
                    }
                }
            }
        }

        // 防御方反击检测（外功被动：被攻击时概率反击）
        var defExternalArt = defender.ActiveExternalArt;
        if (defExternalArt != null)
        {
            foreach (var effect in defExternalArt.Effects.Where(e => e.Type == EffectType.CounterAttack))
            {
                if (effect.TryActivate(defExternalArt.Level))
                {
                    result.CounterAttackTriggered = true;
                    result.TriggeredEffects.Add("触发反击！");
                    break; // 只触发一次
                }
            }
        }
        // 内功反击(如蛤蟆功/玉女心经)
        if (!result.CounterAttackTriggered && defender.ActiveInternalArt != null)
        {
            foreach (var effect in defender.ActiveInternalArt.Effects.Where(e => e.Type == EffectType.CounterAttack))
            {
                if (effect.TryActivate(defender.ActiveInternalArt.Level))
                {
                    result.CounterAttackTriggered = true;
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
                    result.TriggeredEffects.Add("触发追击！");
                    break; // 只触发一次
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 次要伤害(连击/反击/追击)的百分比减伤计算,与主伤害共用 dota 式减伤曲线。
    /// 次要伤害只过防御力减伤,不再叠减伤buff/固定减伤,保持简洁。
    /// </summary>
    /// <param name="rawBase">基础伤害(已含武功系数与暴击,不含倍率)</param>
    /// <param name="defense">防御方总防御</param>
    /// <param name="damageMultiplier">伤害倍率(连击0.85/反击0.6/追击0.6)</param>
    /// <param name="defenseFactor">有效防御系数(默认1.0;反击0.5=只算一半防御)</param>
    public static int ComputeSecondaryDamage(int rawBase, int defense, double damageMultiplier = 1.0, double defenseFactor = 1.0)
    {
        const double DefConstant = 800.0;
        int effDef = Math.Max(0, (int)(defense * defenseFactor));
        double dmg = rawBase * damageMultiplier * DefConstant / (effDef + DefConstant);
        return Math.Max(1, (int)dmg);
    }
}
