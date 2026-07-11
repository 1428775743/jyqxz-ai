namespace AutoWuxia.Items;

/// <summary>
/// 物品类型
/// </summary>
public enum ItemType
{
    Manual,       // 武功秘籍
    Consumable,   // 消耗品（药、食物）
    QuestItem,    // 任务道具
    Gift,         // 礼物（可赠送）
    Material,     // 材料
    Equipment     // 装备
}

/// <summary>
/// 装备槽位
/// </summary>
public enum EquipSlot
{
    Weapon,   // 武器
    Armor     // 防具
}

/// <summary>
/// 修炼前置条件
/// </summary>
public class LearnPrerequisite
{
    public string? RequiredFaction { get; set; }     // 需要门派
    public int RequiredKarma { get; set; }            // 最低善恶值
    public int RequiredBuddhist { get; set; }         // 最低佛法值
    public int RequiredAttack { get; set; }           // 最低攻击
    public int RequiredDefense { get; set; }          // 最低防御
    public string? RequiredArtId { get; set; }        // 需要已学会的武功
    public int RequiredArtLevel { get; set; }         // 该武功需要的等级

    /// <summary>
    /// 检查角色是否满足前置条件
    /// </summary>
    public PrerequisiteCheckResult Check(Characters.CharacterBase character)
    {
        var failures = new List<string>();

        if (RequiredFaction != null && character.FactionId != RequiredFaction)
            failures.Add($"需要加入{RequiredFaction}门派");
        if (character.Karma < RequiredKarma)
            failures.Add($"善恶值不足(需要{RequiredKarma},当前{character.Karma})");
        if (RequiredBuddhist > 0)
        {
            int buddhist = 0;
            if (character is Characters.Player p) buddhist = p.BuddhistValue;
            else if (character is Characters.NPC n) buddhist = n.BuddhistValue;
            if (buddhist < RequiredBuddhist)
                failures.Add($"佛法值不足(需要{RequiredBuddhist},当前{buddhist})");
        }
        if (character.BaseAttack < RequiredAttack)
            failures.Add($"攻击不足(需要{RequiredAttack},当前{character.BaseAttack})");
        if (character.BaseDefense < RequiredDefense)
            failures.Add($"防御不足(需要{RequiredDefense},当前{character.BaseDefense})");
        if (RequiredArtId != null)
        {
            var art = character.LearnedArts.FirstOrDefault(a => a.Id == RequiredArtId);
            if (art == null)
                failures.Add($"需要学会武功【{RequiredArtId}】");
            else if (art.Level < RequiredArtLevel)
                failures.Add($"【{RequiredArtId}】需要{RequiredArtLevel}级(当前{art.Level}级)");
        }

        return new PrerequisiteCheckResult
        {
            Passed = failures.Count == 0,
            FailureReasons = failures
        };
    }
}

public class PrerequisiteCheckResult
{
    public bool Passed { get; set; }
    public List<string> FailureReasons { get; set; } = new();
    public string FailureSummary => string.Join("; ", FailureReasons);
}

/// <summary>
/// 游戏物品
/// </summary>
public class Item
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemType Type { get; set; }
    public int Value { get; set; }          // 价值（银两）
    public int Quantity { get; set; } = 1;  // 数量
    public bool Stackable { get; set; }     // 可堆叠

    // 秘籍专用
    public List<string> ContainedArtIds { get; set; } = new();
    public LearnPrerequisite? Prerequisite { get; set; }

    // 消耗品效果
    public int HPRecovery { get; set; }
    public int MPRecovery { get; set; }
    public int KarmaChange { get; set; }
    public int BuddhistChange { get; set; }

    // 药物增益(消耗品专属):吃完挂一个1天的药buff(单槽覆盖)
    /// <summary>buff类型:atk/def/speed/maxhp/maxmp/regen。空=无buff。</summary>
    public string? BuffType { get; set; }
    /// <summary>buff数值(比例):0.3=+30%</summary>
    public double BuffValue { get; set; }
    /// <summary>buff持续天数(默认1)</summary>
    public int BuffDurationDays { get; set; } = 1;
    /// <summary>是否可在战斗中使用(消耗1次出手)。回血/回内丹药=true,buff药=false(仅外用)。</summary>
    public bool CombatUsable { get; set; }
    /// <summary>是否为食物(挂食buff,与药buff共存)。食物=true,药物=false。</summary>
    public bool IsFood { get; set; }

    // 礼物效果
    public int GiftFavorBonus { get; set; } = 5;   // 赠送时好感度加成
    public string? GiftPreference { get; set; }     // 送礼偏好标签（如“文人”、“武者”）
    
    // 稀有度
    public string Rarity { get; set; } = "common";  // common/uncommon/rare/epic/legendary/mythic

    // 装备专用（Type == Equipment 时有效）
    public EquipSlot? Slot { get; set; }              // 装备槽：Weapon/Armor
    public int AttackBonus { get; set; }              // 武器攻击加成
    public int DefenseBonus { get; set; }             // 防具防御加成
    public string? WeaponType { get; set; }           // 武器类型标签：fist/sword/blade/spear/staff/special（仅描述）
    
    /// <summary>
    /// 稀有度对应的显示颜色
    /// </summary>
    public System.Drawing.Color RarityColor => GetRarityColor(Rarity);
    
    /// <summary>
    /// 稀有度对应的中文名称
    /// </summary>
    public string RarityName => GetRarityName(Rarity);

    /// <summary>
    /// 检查是否是秘籍
    /// </summary>
    public bool IsManual => Type == ItemType.Manual;

    /// <summary>
    /// 检查是否可赠送
    /// </summary>
    public bool IsGiftable => Type == ItemType.Gift || Type == ItemType.Consumable || Type == ItemType.Material;

    /// <summary>
    /// 检查是否是可装备物品
    /// </summary>
    public bool IsEquipment => Type == ItemType.Equipment && Slot != null;

    /// <summary>
    /// 使用消耗品
    /// </summary>
    public bool Use(Characters.CharacterBase character)
    {
        if (Type != ItemType.Consumable) return false;
        if (Quantity <= 0) return false;

        if (HPRecovery > 0) character.Heal(HPRecovery);
        if (MPRecovery > 0) character.RecoverMP(MPRecovery);
        if (KarmaChange != 0) character.Karma = Math.Clamp(character.Karma + KarmaChange, -100, 100);
        if (BuddhistChange != 0)
        {
            if (character is Characters.Player p) p.BuddhistValue += BuddhistChange;
            else if (character is Characters.NPC n) n.BuddhistValue += BuddhistChange;
        }

        // buff:有 BuffType 时挂到玩家身上(药buff/食buff各自单槽覆盖,互不冲突)
        if (!string.IsNullOrEmpty(BuffType) && character is Characters.Player player)
        {
            int days = Math.Max(1, BuffDurationDays);
            if (IsFood)
            {
                player.FoodBuff = new Characters.FoodBuff
                {
                    BuffId = Id, Name = Name, BuffType = BuffType!,
                    Value = BuffValue, RemainingDays = days
                };
            }
            else
            {
                player.MedicineBuff = new Characters.MedicineBuff
                {
                    BuffId = Id, Name = Name, BuffType = BuffType!,
                    Value = BuffValue, RemainingDays = days
                };
            }
        }

        Quantity--;
        return true;
    }

    /// <summary>
    /// 检查角色能否修炼此秘籍
    /// </summary>
    public PrerequisiteCheckResult CanLearn(Characters.CharacterBase character)
    {
        if (!IsManual || Prerequisite == null)
            return new PrerequisiteCheckResult { Passed = true };
        return Prerequisite.Check(character);
    }

    public Item Clone()
    {
        return new Item
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Type = Type,
            Value = Value,
            Quantity = Quantity,
            Stackable = Stackable,
            ContainedArtIds = new List<string>(ContainedArtIds),
            Prerequisite = Prerequisite,
            HPRecovery = HPRecovery,
            MPRecovery = MPRecovery,
            KarmaChange = KarmaChange,
            BuddhistChange = BuddhistChange,
            BuffType = BuffType,
            BuffValue = BuffValue,
            BuffDurationDays = BuffDurationDays,
            CombatUsable = CombatUsable,
            IsFood = IsFood,
            GiftFavorBonus = GiftFavorBonus,
            GiftPreference = GiftPreference,
            Rarity = Rarity,
            Slot = Slot,
            AttackBonus = AttackBonus,
            DefenseBonus = DefenseBonus,
            WeaponType = WeaponType
        };
    }

    public string GetDisplayText()
    {
        var typeLabel = Type switch
        {
            ItemType.Manual => "📜秘籍",
            ItemType.Consumable => "🧪消耗品",
            ItemType.QuestItem => "📦任务",
            ItemType.Gift => "🎁礼物",
            ItemType.Material => "⚒材料",
            ItemType.Equipment => "⚔装备",
            _ => "物品"
        };
        var qty = Quantity > 1 ? $" x{Quantity}" : "";
        var bonus = "";
        if (Type == ItemType.Equipment)
        {
            var parts = new List<string>();
            if (AttackBonus > 0) parts.Add($"攻+{AttackBonus}");
            if (DefenseBonus > 0) parts.Add($"防+{DefenseBonus}");
            if (parts.Count > 0) bonus = $" [{string.Join(" ", parts)}]";
        }
        return $"{typeLabel} {Name}{qty}{bonus}";
    }

    /// <summary>
    /// 稀有度字符串转颜色
    /// </summary>
    public static System.Drawing.Color GetRarityColor(string rarity) => rarity switch
    {
        "common" => System.Drawing.Color.FromArgb(157, 157, 157),      // 灰色
        "uncommon" => System.Drawing.Color.FromArgb(30, 255, 0),       // 绿色
        "rare" => System.Drawing.Color.FromArgb(0, 112, 221),          // 蓝色
        "epic" => System.Drawing.Color.FromArgb(163, 53, 238),         // 紫色
        "legendary" => System.Drawing.Color.FromArgb(255, 128, 0),     // 橙色
        "mythic" => System.Drawing.Color.FromArgb(255, 0, 0),          // 红色
        _ => System.Drawing.Color.FromArgb(157, 157, 157)
    };

    /// <summary>
    /// 稀有度转中文名称
    /// </summary>
    public static string GetRarityName(string rarity) => rarity switch
    {
        "common" => "普通",
        "uncommon" => "精良",
        "rare" => "稀有",
        "epic" => "史诗",
        "legendary" => "传说",
        "mythic" => "神话",
        _ => "普通"
    };
}

/// <summary>
/// 背包/仓库
/// </summary>
public class Inventory
{
    public List<Item> Items { get; set; } = new();

    public int Count => Items.Sum(i => i.Quantity);
    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// 添加物品
    /// </summary>
    public void AddItem(Item item)
    {
        if (item.Stackable)
        {
            var existing = Items.FirstOrDefault(i => i.Id == item.Id);
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
                return;
            }
        }
        Items.Add(item.Clone());
    }

    /// <summary>
    /// 移除物品（返回实际移除数量）
    /// </summary>
    public int RemoveItem(string itemId, int quantity = 1)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return 0;

        int removed = Math.Min(item.Quantity, quantity);
        item.Quantity -= removed;
        if (item.Quantity <= 0)
            Items.Remove(item);
        return removed;
    }

    /// <summary>
    /// 检查是否有某物品
    /// </summary>
    public bool HasItem(string itemId, int quantity = 1)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        return item != null && item.Quantity >= quantity;
    }

    /// <summary>
    /// 获取物品
    /// </summary>
    public Item? GetItem(string itemId)
    {
        return Items.FirstOrDefault(i => i.Id == itemId);
    }

    /// <summary>
    /// 按类型筛选
    /// </summary>
    public List<Item> GetItemsByType(ItemType type)
    {
        return Items.Where(i => i.Type == type).ToList();
    }

    /// <summary>
    /// 获取所有可赠送的物品
    /// </summary>
    public List<Item> GetGiftableItems()
    {
        return Items.Where(i => i.IsGiftable).ToList();
    }

    /// <summary>
    /// 获取所有秘籍
    /// </summary>
    public List<Item> GetManuals()
    {
        return Items.Where(i => i.IsManual).ToList();
    }

    /// <summary>
    /// 背包摘要
    /// </summary>
    public string GetSummary()
    {
        if (Items.Count == 0) return "空";
        return string.Join(", ", Items.Select(i => i.GetDisplayText()));
    }

    /// <summary>
    /// 转移物品到另一个背包
    /// </summary>
    public bool TransferTo(Inventory target, string itemId, int quantity = 1)
    {
        var item = GetItem(itemId);
        if (item == null || item.Quantity < quantity) return false;

        var transferred = item.Clone();
        transferred.Quantity = quantity;
        target.AddItem(transferred);
        RemoveItem(itemId, quantity);
        return true;
    }
}
