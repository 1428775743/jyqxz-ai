namespace AutoWuxia.Characters;

/// <summary>
/// 玩家身上的长期标签/Buff（如重伤、中毒、阉人、称号等）
/// </summary>
public class PlayerTag
{
    /// <summary>标签唯一ID (heavy_injury / poison / severe_poison / eunuch / alliance_leader 等)</summary>
    public string TagId { get; set; } = "";

    /// <summary>显示名称（重伤/中毒/中剧毒/阉人/武林盟主）</summary>
    public string Name { get; set; } = "";

    /// <summary>描述文本</summary>
    public string Description { get; set; } = "";

    /// <summary>剩余天数 (-1=永久不过期)</summary>
    public int RemainingDays { get; set; } = -1;

    /// <summary>每天扣健康度下限（0=不扣）</summary>
    public int DailyHealthLoss { get; set; } = 0;

    /// <summary>每天扣健康度上限（=DailyHealthLoss时为固定值）</summary>
    public int DailyHealthLossMax { get; set; } = 0;

    /// <summary>是否永久标签（称号类、阉人等）</summary>
    public bool IsPermanent { get; set; } = false;

    /// <summary>获得标签时的游戏天数</summary>
    public int AcquiredDay { get; set; } = 0;

    /// <summary>标签是否过期（非永久且剩余天数=0）</summary>
    public bool IsExpired => !IsPermanent && RemainingDays == 0;

    /// <summary>获取每日健康度损耗（随机范围）</summary>
    public int GetDailyLoss()
    {
        if (DailyHealthLoss <= 0) return 0;
        if (DailyHealthLossMax <= DailyHealthLoss) return DailyHealthLoss;
        return Random.Shared.Next(DailyHealthLoss, DailyHealthLossMax + 1);
    }

    /// <summary>推进一天（减少剩余天数）</summary>
    public void TickDay()
    {
        if (!IsPermanent && RemainingDays > 0)
            RemainingDays--;
    }

    /// <summary>显示摘要</summary>
    public string GetSummary()
    {
        var parts = new List<string> { Name };
        if (!IsPermanent)
        {
            if (RemainingDays > 0) parts.Add($"剩余{RemainingDays}天");
            else if (RemainingDays < 0) parts.Add("无限期");
            else parts.Add("已过期");
        }
        else
        {
            parts.Add("永久");
        }
        if (DailyHealthLoss > 0)
        {
            if (DailyHealthLossMax > DailyHealthLoss)
                parts.Add($"每日健康-{DailyHealthLoss}~{DailyHealthLossMax}");
            else
                parts.Add($"每日健康-{DailyHealthLoss}");
        }
        return $"[{string.Join("/", parts)}]";
    }

    // ── 预定义标签工厂 ──

    public static PlayerTag CreateHeavyInjury(int acquiredDay, int days = 30)
    {
        return new PlayerTag
        {
            TagId = "heavy_injury",
            Name = "重伤",
            Description = "身受重伤，每日健康度-1，持续1个月",
            RemainingDays = days,
            DailyHealthLoss = 1,
            DailyHealthLossMax = 1,
            IsPermanent = false,
            AcquiredDay = acquiredDay
        };
    }

    public static PlayerTag CreatePoison(int acquiredDay, int days = 15)
    {
        return new PlayerTag
        {
            TagId = "poison",
            Name = "中毒",
            Description = "身中寻常之毒，每日健康度-2~5",
            RemainingDays = days,
            DailyHealthLoss = 2,
            DailyHealthLossMax = 5,
            IsPermanent = false,
            AcquiredDay = acquiredDay
        };
    }

    public static PlayerTag CreateSeverePoison(int acquiredDay, int days = 10)
    {
        return new PlayerTag
        {
            TagId = "severe_poison",
            Name = "中剧毒",
            Description = "身中剧毒，每日健康度-5~10，极为凶险",
            RemainingDays = days,
            DailyHealthLoss = 5,
            DailyHealthLossMax = 10,
            IsPermanent = false,
            AcquiredDay = acquiredDay
        };
    }

    public static PlayerTag CreateEunuch(int acquiredDay)
    {
        return new PlayerTag
        {
            TagId = "eunuch",
            Name = "阉人",
            Description = "已行自宫之术，可修炼辟邪剑法、葵花宝典等阴柔武学",
            RemainingDays = -1,
            IsPermanent = true,
            AcquiredDay = acquiredDay
        };
    }

    public static PlayerTag CreateTitle(string tagId, string name, string description, int acquiredDay)
    {
        return new PlayerTag
        {
            TagId = tagId,
            Name = name,
            Description = description,
            RemainingDays = -1,
            IsPermanent = true,
            AcquiredDay = acquiredDay
        };
    }

    /// <summary>
    /// 战败受辱:战斗中所有属性(攻/防/速/HP/MP上限)降低20%,持续若干天。
    /// 由 NPC 胜利后羞辱玩家时施加。
    /// </summary>
    public static PlayerTag CreateBeatenDown(int acquiredDay, int days = 7)
    {
        return new PlayerTag
        {
            TagId = "beaten_down",
            Name = "战败受辱",
            Description = "战败受辱,内伤未愈,战斗中所有属性降低20%",
            RemainingDays = days,
            IsPermanent = false,
            AcquiredDay = acquiredDay
        };
    }
}
