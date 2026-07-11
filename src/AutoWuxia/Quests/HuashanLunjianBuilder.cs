using System;
using System.Collections.Generic;
using System.Linq;
using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Config.Models;

namespace AutoWuxia.Quests;

/// <summary>
/// 华山论剑 10 人车轮战对手选取器。
/// 按玩家善恶分流(善打恶人、恶打正派),优先选与玩家有关系者,阅历升序排列(弱→强,末位最终Boss),
/// 同阅历档轻度随机,并按倍率放大对手属性以提升终章难度。
/// </summary>
public static class HuashanLunjianBuilder
{
    /// <summary>绝顶高手阅历门槛</summary>
    private const int MinTopLevel = 40;
    /// <summary>玩家 karma ≥ 此值视为"善",打恶人;否则打正派</summary>
    private const int GoodKarmaThreshold = 50;
    /// <summary>恶人判定:karma 低于此值算恶(供善玩家作对手)</summary>
    private const int EvilKarmaThreshold = 30;
    /// <summary>车轮战人数</summary>
    public const int GauntletSize = 10;

    /// <summary>
    /// 构建华山论剑对手列表。
    /// </summary>
    /// <param name="player">玩家(读取 Karma 与 Relations)</param>
    /// <param name="config">配置管理器(读取全部角色配置并实例化 NPC)</param>
    /// <param name="statMultiplier">对手属性放大倍率(1.0=不放大)</param>
    public static List<NPC> Build(Player player, ConfigManager config, double statMultiplier = 1.0)
    {
        var playerIsGood = player.Karma >= GoodKarmaThreshold;

        // 候选池:type=npc、非隐藏、阅历达标、善恶阵营对立
        var candidates = config.Characters.Values
            .Where(c => c.Type == "npc" && !c.IsHidden && c.JianghuLevel >= MinTopLevel)
            .Where(c => playerIsGood ? c.Karma < EvilKarmaThreshold : c.Karma >= GoodKarmaThreshold)
            .ToList();

        // 关系优先:玩家关系网中非"素不相识"的候选,按|好感|降序(爱恨皆算"有瓜葛")
        var related = candidates
            .Where(c => player.Relations.TryGetValue(c.Id, out var rel) && rel.Type != RelationType.Stranger)
            .OrderByDescending(c => Math.Abs(player.Relations[c.Id].Favorability))
            .ToList();

        var chosen = new List<CharacterConfig>();
        var used = new HashSet<string>();
        foreach (var c in related)
        {
            chosen.Add(c);
            used.Add(c.Id);
            if (chosen.Count >= GauntletSize) break;
        }

        // 不足则从剩余候选按阅历降序补足(取最强者补位)
        if (chosen.Count < GauntletSize)
        {
            var fillers = candidates
                .Where(c => !used.Contains(c.Id))
                .OrderByDescending(c => c.JianghuLevel)
                .ToList();
            foreach (var c in fillers)
            {
                chosen.Add(c);
                used.Add(c.Id);
                if (chosen.Count >= GauntletSize) break;
            }
        }

        // 排序:阅历升序(弱→强,末位最终Boss),同阅历档随机洗牌
        var ordered = chosen
            .GroupBy(c => c.JianghuLevel)
            .SelectMany(g => g.OrderBy(_ => Random.Shared.Next()))
            .OrderBy(c => c.JianghuLevel)
            .ToList();

        // 实例化 + 属性放大 + 标记隐藏(临时副本对手,不污染场景 NPC 列表)
        var result = new List<NPC>();
        foreach (var cfg in ordered)
        {
            NPC npc;
            try { npc = config.CreateNPC(cfg.Id); }
            catch { continue; }   // 跳过实例化失败的配置
            npc.IsHidden = true;
            if (statMultiplier > 1.0) ApplyMultiplier(npc, statMultiplier);
            result.Add(npc);
        }
        return result;
    }

    /// <summary>按倍率放大对手 HP/MP/攻防(满血满蓝起手)。</summary>
    private static void ApplyMultiplier(NPC npc, double m)
    {
        npc.MaxHP = (int)(npc.MaxHP * m);
        npc.CurrentHP = npc.MaxHP;
        npc.MaxMP = (int)(npc.MaxMP * m);
        npc.CurrentMP = npc.MaxMP;
        npc.BaseAttack = (int)(npc.BaseAttack * m);
        npc.BaseDefense = (int)(npc.BaseDefense * m);
    }
}
