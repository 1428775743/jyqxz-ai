namespace AutoWuxia.Core;

public class GameTime
{
    private static readonly string[] ShiChenNames =
        { "子时", "丑时", "寅时", "卯时", "辰时", "巳时", "午时", "未时", "申时", "酉时", "戌时", "亥时" };

    public int Day { get; private set; }
    public int ShiChenIndex { get; private set; }
    public double Fraction { get; private set; }

    public string ShiChenName => ShiChenNames[ShiChenIndex];
    public string Display => $"第{Day}天 {ShiChenName}";

    /// <summary>
    /// 总时辰数（用于冷却计算）
    /// </summary>
    public int TotalShiChen => Day * 12 + ShiChenIndex;

    public GameTime(int day = 1, int shiChenIndex = 4)
    {
        Day = day;
        ShiChenIndex = shiChenIndex;
        Fraction = 0;
    }

    public void Advance(double shiChens)
    {
        Fraction += shiChens;
        while (Fraction >= 1.0)
        {
            Fraction -= 1.0;
            ShiChenIndex++;
            if (ShiChenIndex >= 12)
            {
                ShiChenIndex = 0;
                Day++;
            }
        }
    }

    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;

    public int Month => (Day - 1) / DaysPerMonth + 1;
    public int DayInMonth => (Day - 1) % DaysPerMonth + 1;
    public int Year => (Month - 1) / MonthsPerYear + 1;
    public string MonthDisplay => $"第{Month}月 第{DayInMonth}天";
    public string YearDisplay => $"第{Year}年 第{Month}月";

    /// <summary>年内第几天（1~360，用于"n年n天"显示）。</summary>
    public int DayInYear => (Day - 1) % (DaysPerMonth * MonthsPerYear) + 1;
    /// <summary>"第N年 第N天"格式（经历查看用，不带时辰）。</summary>
    public string YearDayDisplay => $"第{Year}年 第{DayInYear}天";
    /// <summary>"第N年 第N天 时辰"格式（玩家界面用，带时辰）。</summary>
    public string YearDayShiChenDisplay => $"第{Year}年 第{DayInYear}天 {ShiChenName}";

    /// <summary>将任意游戏天数格式化为"第N年 第N天"（用于经历记录展示）。</summary>
    public static string FormatYearDay(int day)
    {
        int daysPerYear = DaysPerMonth * MonthsPerYear;
        int y = (day - 1) / daysPerYear + 1;
        int d = (day - 1) % daysPerYear + 1;
        return $"第{y}年 第{d}天";
    }

    public bool IsNewMonth(int previousDay)
    {
        int prevMonth = (previousDay - 1) / DaysPerMonth + 1;
        return Month > prevMonth;
    }

    /// <summary>是否跨年(每12个月为一年)。previousDay为上次年度更新的天数。</summary>
    public bool IsNewYear(int previousDay)
    {
        int prevYear = ((previousDay - 1) / DaysPerMonth + 1 - 1) / MonthsPerYear + 1;
        return Year > prevYear;
    }

    public bool IsNewDay(int previousDay) => Day > previousDay;

    public void SleepUntilNextDay()
    {
        Day++;
        ShiChenIndex = 4;
        Fraction = 0;
    }

    public string GetTimePeriod()
    {
        return ShiChenIndex switch
        {
            <= 2 or 11 => "夜晚",
            >= 3 and <= 6 => "清晨",
            >= 7 and <= 10 => "白天",
            _ => "黄昏"
        };
    }
}
