namespace AutoWuxia.Characters;

/// <summary>
/// 玩家身上的食物增益(食buff)。同时只能挂1个,吃新食物覆盖旧的。
/// 与药buff(MedicineBuff)独立共存、互不冲突。持续以"天"为单位(每日TickDay递减,0自动清除)。
/// 战斗中通过 GetTotal* 生效(与药buff并列相乘)。
/// BuffType: atk/def/speed/maxhp/maxmp/regen (regen=战斗中每回合回血 Value 比例最大HP)
/// </summary>
public class FoodBuff
{
    /// <summary>来源食物ID(防重复/展示)</summary>
    public string BuffId { get; set; } = "";

    /// <summary>显示名(烤肉串/佛跳墙…)</summary>
    public string Name { get; set; } = "";

    /// <summary>buff类型:atk/def/speed/maxhp/maxmp/regen</summary>
    public string BuffType { get; set; } = "";

    /// <summary>数值。比例类(atk/def/speed/maxhp/maxmp/regen):0.3=+30%</summary>
    public double Value { get; set; }

    /// <summary>剩余天数(0=过期)</summary>
    public int RemainingDays { get; set; } = 1;

    public bool IsExpired => RemainingDays <= 0;

    /// <summary>推进一天</summary>
    public void TickDay()
    {
        if (RemainingDays > 0) RemainingDays--;
    }

    /// <summary>该buff是否作用于指定属性键</summary>
    public bool Matches(string buffType) => BuffType == buffType;

    /// <summary>属性加成倍率(1+Value),不匹配返回1</summary>
    public double MultiplierFor(string buffType) => Matches(buffType) ? (1.0 + Value) : 1.0;
}
