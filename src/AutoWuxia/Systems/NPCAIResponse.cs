namespace AutoWuxia.Systems;

/// <summary>
/// NPC AI 统一响应格式
/// 所有 NPC 决策（对话、赠送、主动行为）都使用此格式
/// </summary>
public class NPCAIResponse
{
    /// <summary>NPC的内心独白/思考过程</summary>
    public string Thinking { get; set; } = "";

    /// <summary>NPC说的话（对外表达）</summary>
    public string Dialogue { get; set; } = "";

    /// <summary>
    /// NPC行为类型:
    /// none - 仅对话，无额外行为
    /// spar - NPC主动提出切磋
    /// attack - NPC主动攻击（仇敌/被冒犯）
    /// give_item - NPC赠送物品给玩家（actionTarget为物品ID）
    /// ask_item - NPC向玩家索要物品（actionTarget为物品ID）
    /// marry - 与玩家结为配偶
    /// swear_brotherhood - 与玩家义结金兰
    /// take_disciple - 收玩家为徒
    /// teach_art - NPC传授武功给玩家（actionTarget为武功ID）
    /// end_dialogue - NPC想结束对话
    /// </summary>
    public string Action { get; set; } = "none";

    /// <summary>行为目标（物品ID或武功ID）</summary>
    public string? ActionTarget { get; set; }

    /// <summary>好感度变化 -10~+10</summary>
    public int FavorChange { get; set; }

    /// <summary>NPC花费的银两</summary>
    public int GoldSpent { get; set; }

    /// <summary>乐师演奏收取的赏钱(玩家付给NPC,0=免费)。仅 action=play_music 时由AI决定。</summary>
    public int MusicFee { get; set; }

    /// <summary>药师炼药收取的工费(玩家付给NPC)。仅 action=craft_medicine 时由AI决定。</summary>
    public int CraftFee { get; set; }

    /// <summary>是否想结束对话</summary>
    public bool WantsToEnd { get; set; }

    /// <summary>结束原因</summary>
    public string? EndReason { get; set; }
}

/// <summary>
/// NPC AI 开场白响应（用于 StartDialogue）
/// </summary>
public class NPCAIOpeningResponse
{
    /// <summary>NPC的内心独白/思考</summary>
    public string Thinking { get; set; } = "";

    /// <summary>是否愿意对话</summary>
    public bool WillingToTalk { get; set; } = true;

    /// <summary>开场白/拒绝语</summary>
    public string Dialogue { get; set; } = "";

    /// <summary>原因</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// NPC AI 赠送决策响应
/// </summary>
public class NPCAIGiftResponse
{
    /// <summary>NPC的内心独白</summary>
    public string Thinking { get; set; } = "";

    /// <summary>是否接受礼物</summary>
    public bool Accepted { get; set; }

    /// <summary>NPC的回应话语</summary>
    public string Dialogue { get; set; } = "";

    /// <summary>好感度变化</summary>
    public int FavorChange { get; set; }

    /// <summary>
    /// 接受后的额外行为:
    /// none - 无
    /// give_item - 回赠
    /// teach_art - 传授武功作为感谢
    /// </summary>
    public string ReturnAction { get; set; } = "none";

    /// <summary>额外行为目标（物品ID或武功ID）</summary>
    public string? ReturnActionTarget { get; set; }
}
