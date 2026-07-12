namespace AutoWuxia.Config.Models;

public class CharacterConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "npc";
    public string Personality { get; set; } = "";
    public string Description { get; set; } = "";
    public int MaxHP { get; set; } = 1000;
    public int MaxMP { get; set; } = 500;
    public int Attack { get; set; } = 100;
    public int Defense { get; set; } = 50;
    public int Speed { get; set; } = 50;

    /// <summary>
    /// 角色掌握的所有武功(内功/外功皆可)。每项形如 { "artId": "...", "proficiencyLevel": 5 }。
    /// proficiencyLevel 会通过 GetProficiencyForLevel 换算为累计熟练度。
    /// </summary>
    public List<LearnedArtEntry> LearnedArts { get; set; } = new();

    /// <summary>当前激活的内功 ID(必须出现在 LearnedArts 中);未指定则取 LearnedArts 中第一本内功。</summary>
    public string? ActiveInternalArtId { get; set; }

    /// <summary>辅助内功ID列表(最多2本,仅+50%属性,被动不生效)。NPC初始配置用。</summary>
    public List<string> AuxiliaryInternalArtIds { get; set; } = new();

    /// <summary>当前激活的外功 ID(必须出现在 LearnedArts 中);未指定则取 LearnedArts 中第一本外功。</summary>
    public string? ActiveExternalArtId { get; set; }

    /// <summary>当前激活的轻功 ID(必须出现在 LearnedArts 中);未指定则取 LearnedArts 中第一本轻功。</summary>
    public string? ActiveLightArtId { get; set; }

    public Dictionary<string, string> Relations { get; set; } = new();
    public string? FactionId { get; set; }
    public int Karma { get; set; } = 50;
    public string? Gender { get; set; }   // 性别"男"/"女",结婚关系需异性
    public bool HiddenPower { get; set; }
    public bool IsSectLeader { get; set; }
    /// <summary>是否为本门派传武护法（非掌门也能教武功）</summary>
    public bool IsTrainer { get; set; }
    public Dictionary<string, string> Schedule { get; set; } = new();
    public string DefaultScene { get; set; } = "";

    /// <summary>
    /// 初始江湖阅历等级
    /// </summary>
    public int JianghuLevel { get; set; } = 1;

    /// <summary>
    /// NPC角色类型：wine_merchant/weapon_merchant/medicine_merchant/martial_instructor/craft_teacher
    /// </summary>
    public string? NpcRole { get; set; }

    /// <summary>
    /// 商贩固定商品ID列表
    /// </summary>
    public List<string> ShopFixedItems { get; set; } = new();

    /// <summary>
    /// 商贩随机商品池ID列表
    /// </summary>
    public List<string> ShopRandomItems { get; set; } = new();

    /// <summary>
    /// 技艺初始值：art, forging, mining, planting, medicine
    /// </summary>
    public Dictionary<string, int> CraftSkills { get; set; } = new();

    /// <summary>
    /// 是否为隐藏角色 (山贼/副本敌人，不出现在场景 NPC 列表)
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>玩家悟性(1~10):仅 player 类型使用,影响武功熟练度获取速度</summary>
    public int Talent { get; set; } = 5;

    /// <summary>
    /// 头像图片路径（相对于 assets/portraits/）
    /// </summary>
    public string? PortraitPath { get; set; }

    /// <summary>初始装备的武器物品 ID（需在 items 中存在，可空表示无武器）。</summary>
    public string? EquippedWeaponId { get; set; }
    /// <summary>初始装备的防具物品 ID（可空表示无防具）。</summary>
    public string? EquippedArmorId { get; set; }
}

/// <summary>
/// 角色配置中的"已学武功"条目。proficiencyLevel 是 1~MaxLevel 的目标等级,
/// 加载时通过 GetProficiencyForLevel 换算成累计熟练度。
/// </summary>
public class LearnedArtEntry
{
    public string ArtId { get; set; } = "";
    public int ProficiencyLevel { get; set; } = 1;
}

public class SceneConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Region { get; set; } = "";
    /// <summary>场景背景图路径（相对于程序目录）。</summary>
    public string? BackgroundImagePath { get; set; }
    public List<string> ConnectedScenes { get; set; } = new();
    public List<string> DefaultNPCs { get; set; } = new();
    public string? FactionId { get; set; }
    public bool IsSpecial { get; set; }

    /// <summary>
    /// 场景可学习的技艺
    /// </summary>
    public List<SceneCraftLesson> CraftLessons { get; set; } = new();

    /// <summary>
    /// 场景武术馆可学习的武功
    /// </summary>
    public List<MartialLesson> MartialLessons { get; set; } = new();

    /// <summary>
    /// 矿场配置（null表示该场景不可挖矿）
    /// </summary>
    public MineConfig? Mine { get; set; }

    /// <summary>
    /// 打猎配置（null表示该场景不可打猎）
    /// </summary>
    public HuntConfig? Hunt { get; set; }

    /// <summary>
    /// 药园配置（null表示该场景不可采药）。采药用 planting 技艺。
    /// </summary>
    public HerbGardenConfig? HerbGarden { get; set; }
}

public class MineConfig
{
    /// <summary>
    /// 矿场等级：normal/medium/high
    /// </summary>
    public string Tier { get; set; } = "normal";

    /// <summary>
    /// 可出产的矿石列表
    /// </summary>
    public List<MineOreEntry> Ores { get; set; } = new();

    /// <summary>
    /// 每次挖矿耗时(时辰)
    /// </summary>
    public double TimeCostPerMine { get; set; } = 1.0;

    /// <summary>
    /// 每次挖矿消耗体力
    /// </summary>
    public double StaminaCost { get; set; } = 15;
}

public class MineOreEntry
{
    /// <summary>
    /// 矿石物品ID
    /// </summary>
    public string ItemId { get; set; } = "";

    /// <summary>
    /// 稀有度：common/uncommon/rare/epic/legendary
    /// </summary>
    public string Rarity { get; set; } = "common";
}

/// <summary>
/// 药园配置(采药节点),结构与 MineConfig 平行,用 planting 技艺。
/// </summary>
public class HerbGardenConfig
{
    /// <summary>药园等级:normal/medium/high</summary>
    public string Tier { get; set; } = "normal";

    /// <summary>可出产的草药列表</summary>
    public List<HerbEntry> Herbs { get; set; } = new();

    /// <summary>每次采药耗时(时辰)</summary>
    public double TimeCostPerGather { get; set; } = 1.0;

    /// <summary>每次采药消耗体力</summary>
    public double StaminaCost { get; set; } = 12;
}

public class HerbEntry
{
    /// <summary>草药物品ID</summary>
    public string ItemId { get; set; } = "";

    /// <summary>稀有度:common/uncommon/rare/epic/legendary</summary>
    public string Rarity { get; set; } = "common";
}

public class HuntConfig
{
    /// <summary>
    /// 猎场等级：normal/medium/high
    /// </summary>
    public string Tier { get; set; } = "normal";

    /// <summary>
    /// 可获得的猎物列表
    /// </summary>
    public List<HuntLootEntry> Loots { get; set; } = new();

    /// <summary>
    /// 每次打猎耗时(时辰)
    /// </summary>
    public double TimeCostPerHunt { get; set; } = 1.0;

    /// <summary>
    /// 每次打猎消耗体力
    /// </summary>
    public double StaminaCost { get; set; } = 12;
}

public class HuntLootEntry
{
    /// <summary>
    /// 猎物物品ID
    /// </summary>
    public string ItemId { get; set; } = "";

    /// <summary>
    /// 稀有度：common/uncommon/rare
    /// </summary>
    public string Rarity { get; set; } = "common";
}

public class SceneCraftLesson
{
    public string SkillId { get; set; } = "";    // 技艺ID
    public int Cost { get; set; } = 50;          // 学费银两
    public int Gain { get; set; } = 5;           // 每次获得的技艺经验
    public int MaxLevel { get; set; } = 50;      // 此处能学到的上限
}

public class MartialLesson
{
    public string ArtId { get; set; } = "";      // 武功ID
    public int Cost { get; set; } = 200;         // 学费银两
}

public class MartialArtConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "external";
    public string Description { get; set; } = "";
    public int MaxLevel { get; set; } = 10;
    /// <summary>武功品质：common/uncommon/rare/epic/legendary/mythic，决定升级所需熟练度</summary>
    public string Rarity { get; set; } = "common";
    public double DamageCoefficient { get; set; } = 1.0;
    public double CritChance { get; set; }
    public int Cooldown { get; set; }
    public int MPCost { get; set; }
    public double MPCostPercent { get; set; }
    public int HPBonusPerLevel { get; set; }
    public int MPBonusPerLevel { get; set; }
    public int AttackBonusPerLevel { get; set; }
    public int DefenseBonusPerLevel { get; set; }
    /// <summary>轻功的速度加成(每级递增)</summary>
    public int SpeedBonusPerLevel { get; set; }
    public List<ArtEffectConfig> Effects { get; set; } = new();
    public Dictionary<string, LevelBonusConfig> LevelBonuses { get; set; } = new();
    public string? RequiredArtId { get; set; }
    public int RequiredArtLevel { get; set; }
}

public class ArtEffectConfig
{
    public string Type { get; set; } = "";
    public double Value { get; set; }
    public double Chance { get; set; } = 1.0;
    public string? Description { get; set; }

    /// <summary>效果激活所需的武功等级（0=无限制）</summary>
    public int RequiredLevel { get; set; }
}

public class LevelBonusConfig
{
    public double? DamageCoefficient { get; set; }
    public string? NewEffect { get; set; }
    public List<ArtEffectConfig>? AdditionalEffects { get; set; }
}

public class FactionConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LeaderId { get; set; } = "";
    public string SceneId { get; set; } = "";
    public List<string> AvailableArts { get; set; } = new();
    public int JoinKarmaMin { get; set; } = -100;
    public int JoinKarmaMax { get; set; } = 100;
    public Dictionary<string, string> AlliedFactions { get; set; } = new();
    public Dictionary<string, string> EnemyFactions { get; set; } = new();

    /// <summary>门派阵营:正派/中立/邪派。影响门派任务默认善恶奖励(正派+/邪派−/中立0)。</summary>
    public string Alignment { get; set; } = "正派";
}

public class QuestConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "main";  // main/side/faction/dungeon/chain
    public string Description { get; set; } = "";
    public List<QuestStepConfig> Steps { get; set; } = new();
    public QuestRewardConfig? Reward { get; set; }

    /// <summary>接取任务时播放的剧情对话(可选)</summary>
    public DialogueScriptConfig? IntroDialogue { get; set; }

    /// <summary>领取最终奖励(任务完成)时播放的剧情对话(可选,可衔接下一任务)</summary>
    public DialogueScriptConfig? CompleteDialogue { get; set; }

    /// <summary>委托发布任务的NPC (收集任务必填)</summary>
    public string? IssuerNpcId { get; set; }

    /// <summary>触发任务的NPC（链式任务发起者，用于在场景找到该NPC时自动接取任务）</summary>
    public string? TriggerNpcId { get; set; }

    /// <summary>触发任务的场景（进入该场景时自动接取，与TriggerNpcId二选一）。</summary>
    public string? TriggerSceneId { get; set; }

    /// <summary>所属门派 (门派任务必填)</summary>
    public string? FactionId { get; set; }

    /// <summary>关联副本ID (讨伐山贼/副本任务必填)</summary>
    public string? DungeonId { get; set; }

    /// <summary>难度 easy/normal/hard</summary>
    public string Difficulty { get; set; } = "normal";

    /// <summary>需要提交的物品列表</summary>
    public List<QuestItemRequirementConfig> RequiredItems { get; set; } = new();

    /// <summary>接取该任务所需的最低好感度（默认0=无要求，链式任务发布前需与触发NPC好感度≥该值）</summary>
    public int MinFavorabilityToOffer { get; set; }

    /// <summary>接取该任务所需玩家任一武功最低等级（默认0=无要求，用于"武功学到中段才可触发剧情"）</summary>
    public int MinAnyArtLevel { get; set; }

    /// <summary>仅供剧情自动接取(不出现在NPC可委托列表)。例如失败后的惩罚任务"思过崖面壁"</summary>
    public bool AutoAcceptOnly { get; set; }

    /// <summary>是否要求玩家与发布者同门派(用于门派内部委托,如华山师门考验)</summary>
    public bool RequireSameFaction { get; set; }

    /// <summary>限制特定门派玩家不可接取；用于向其他门派开放的独立剧情线。</summary>
    public string? ExcludeFactionId { get; set; }

    /// <summary>关联人NPC ID列表（这些NPC对话时知道"发布者委托玩家处理本任务"，但不知详情）</summary>
    public List<string> RelatedNpcIds { get; set; } = new();

    /// <summary>
    /// 前置任务ID列表：玩家须全部完成(Completed/Rewarded)才可接取本任务。
    /// 用于"多线汇聚"剧情(如天龙八部少室山大战需段誉/乔峰/虚竹三线完成方可触发)。
    /// </summary>
    public List<string> PrerequisiteQuestIds { get; set; } = new();

    /// <summary>
    /// 互斥任务ID列表:玩家任务日志中若有其中任一(任何状态),则本任务不可接取。
    /// 用于正/恶线互斥(接了正线则恶线不可接,反之亦然)。
    /// </summary>
    public List<string> ExclusiveWithQuestIds { get; set; } = new();
}

public class QuestItemRequirementConfig
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public class QuestStepConfig
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public string? TargetScene { get; set; }
    public string? TargetNPC { get; set; }
    public string ActionType { get; set; } = "talk";
    public Dictionary<string, string> Conditions { get; set; } = new();

    /// <summary>给发布者AI的剧情上下文提示（当NPC作为该任务发布者对话时注入prompt,引导AI在不同步骤的态度/行为）</summary>
    public string? AiHint { get; set; }

    /// <summary>该步骤的节点奖励（链式任务中每步可有独立奖励）</summary>
    public QuestRewardConfig? Reward { get; set; }

    /// <summary>该步骤完成时播放的剧情对话(可选)</summary>
    public DialogueScriptConfig? Dialogue { get; set; }

    public List<QuestItemRequirementConfig> RequiredItems { get; set; } = new();
}

/// <summary>一句剧情对话:一个说话人的连续台词(逐句点击推进)</summary>
public class DialogueLineConfig
{
    /// <summary>说话人: NPC ID, 或 "旁白"/"玩家"</summary>
    public string Speaker { get; set; } = "";
    /// <summary>该说话人的连续台词列表</summary>
    public List<string> Lines { get; set; } = new();
}

/// <summary>一段剧情对话:多个说话人轮流发言。结构 [{人物,[对话数组]}]</summary>
public class DialogueScriptConfig
{
    public List<DialogueLineConfig> Lines { get; set; } = new();
}

public class QuestRewardConfig
{
    public int HPBonus { get; set; }
    public int MPBonus { get; set; }
    public int AttackBonus { get; set; }
    public int DefenseBonus { get; set; }
    public string? MartialArtId { get; set; }
    public int KarmaBonus { get; set; }
    public int JianghuExp { get; set; }

    /// <summary>金钱奖励</summary>
    public int GoldBonus { get; set; }

    /// <summary>声望奖励</summary>
    public int ReputationBonus { get; set; }

    /// <summary>门派贡献奖励</summary>
    public int FactionContributionBonus { get; set; }

    /// <summary>贡献度归属的门派ID (默认使用任务的 FactionId)</summary>
    public string? FactionIdForContribution { get; set; }

    /// <summary>物品奖励</summary>
    public List<RewardItemConfig> Items { get; set; } = new();
}

public class RewardItemConfig
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

// ── 副本配置 ──

public class DungeonConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "bandit";  // bandit/huashan_lunjian/story
    public List<DungeonRoundConfig> Rounds { get; set; } = new();
    public DungeonRewardConfig Reward { get; set; } = new();
    public DungeonFailPenaltyConfig OnFail { get; set; } = new();
    public int StaminaCost { get; set; } = 20;
    public double TimeCostHours { get; set; } = 2;

    /// <summary>
    /// 对手属性放大倍率(仅华山论剑等终章副本用)。1.0=不放大。
    /// 作用于 MaxHP/CurrentHP/MaxMP/CurrentMP/BaseAttack/BaseDefense。
    /// </summary>
    public double OpponentStatMultiplier { get; set; } = 1.0;
}

public class DungeonRoundConfig
{
    /// <summary>固定对手角色ID</summary>
    public string? OpponentCharacterId { get; set; }

    /// <summary>对手随机池 (与 OpponentCharacterId 互斥)</summary>
    public List<string> OpponentPool { get; set; } = new();

    /// <summary>本轮对手数量</summary>
    public int Count { get; set; } = 1;

    /// <summary>战胜后是否触发 AI 对话</summary>
    public bool TriggerDialogue { get; set; } = true;
}

public class DungeonRewardConfig
{
    public int Gold { get; set; }
    public int Reputation { get; set; }
    public int FactionContribution { get; set; }
    public string? FactionId { get; set; }
}

public class DungeonFailPenaltyConfig
{
    public string Type { get; set; } = "deductGold";  // deductGold/deductHP/gameOver
    public int Amount { get; set; }
}

public class WorldMapConfig
{
    public List<LocationConfig> Locations { get; set; } = new();
    public List<RouteConfig> Routes { get; set; } = new();
}

public class LocationConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public List<string> SceneIds { get; set; } = new();
    public int MapX { get; set; }
    public int MapY { get; set; }
}

public class RouteConfig
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public double Distance { get; set; }
    public double TravelTime { get; set; }
}

public class SecretManualConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> ContainedArtIds { get; set; } = new();
    public Dictionary<string, int> LearnRequirements { get; set; } = new();
}

public class ItemConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "Consumable";
    public int Value { get; set; }
    public int Quantity { get; set; } = 1;
    public bool Stackable { get; set; }
    public List<string> ContainedArtIds { get; set; } = new();
    public LearnPrerequisiteConfig? Prerequisite { get; set; }
    public int HPRecovery { get; set; }
    public int MPRecovery { get; set; }
    public int KarmaChange { get; set; }
    public int BuddhistChange { get; set; }

    // 药物增益(消耗品专属)
    /// <summary>buff类型:atk/def/speed/maxhp/maxmp/regen。空=无buff。</summary>
    public string? BuffType { get; set; }
    /// <summary>buff数值(比例):0.3=+30%</summary>
    public double BuffValue { get; set; }
    /// <summary>buff持续天数(默认1)</summary>
    public int BuffDurationDays { get; set; } = 1;
    /// <summary>是否可在战斗中使用(消耗1次出手)。</summary>
    public bool CombatUsable { get; set; }
    /// <summary>是否为食物(挂食buff,与药buff共存)。食物=true。</summary>
    public bool IsFood { get; set; }

    public int GiftFavorBonus { get; set; } = 5;
    public string? GiftPreference { get; set; }

    /// <summary>
    /// 稀有度：common/uncommon/rare/epic/legendary/mythic
    /// </summary>
    public string Rarity { get; set; } = "common";

    /// <summary>装备槽：weapon/armor；为空表示非装备物品。</summary>
    public string? EquipSlot { get; set; }
    /// <summary>武器攻击加成。</summary>
    public int AttackBonus { get; set; }
    /// <summary>防具防御加成。</summary>
    public int DefenseBonus { get; set; }
    /// <summary>武器类型标签：fist/sword/blade/spear/staff/special（仅描述用）。</summary>
    public string? WeaponType { get; set; }
}

// ── 药物配方(药师制作) ──

/// <summary>
/// 药物配方:玩家携材料找药师制作。配方清单会注入药师AI prompt。
/// </summary>
public class MedicineRecipeConfig
{
    /// <summary>配方ID(=产出丹药ID,便于药师AI引用)</summary>
    public string Id { get; set; } = "";

    /// <summary>丹药名称</summary>
    public string Name { get; set; } = "";

    /// <summary>品阶:普通/稀有/珍贵/传说</summary>
    public string Tier { get; set; } = "普通";

    /// <summary>产出丹药物品ID</summary>
    public string ResultItemId { get; set; } = "";

    /// <summary>产出数量</summary>
    public int ResultQuantity { get; set; } = 1;

    /// <summary>所需材料</summary>
    public List<RecipeMaterialEntry> Materials { get; set; } = new();

    /// <summary>药师所需 medicine 技艺等级</summary>
    public int RequiredMedicineSkill { get; set; } = 50;

    /// <summary>最低好感度要求(AI据好感决定是否接单,此为硬下限)</summary>
    public int MinFavorability { get; set; } = 0;

    /// <summary>费用区间[下限,上限]银两,AI在此范围自定</summary>
    public List<int> FeeRange { get; set; } = new() { 50, 100 };

    /// <summary>配方可由哪些药师制作(空=任意药师)。NPC id列表。</summary>
    public List<string>? AllowedPharmacists { get; set; }
}

public class RecipeMaterialEntry
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// 菜肴配方:玩家携材料找厨师制作。配方清单会注入厨师AI prompt。
/// </summary>
public class FoodRecipeConfig
{
    /// <summary>配方ID(=产出菜肴ID,便于厨师AI引用)</summary>
    public string Id { get; set; } = "";

    /// <summary>菜肴名称</summary>
    public string Name { get; set; } = "";

    /// <summary>品阶:普通/稀有/珍贵/传说</summary>
    public string Tier { get; set; } = "普通";

    /// <summary>产出菜肴物品ID</summary>
    public string ResultItemId { get; set; } = "";

    /// <summary>产出数量</summary>
    public int ResultQuantity { get; set; } = 1;

    /// <summary>所需食材</summary>
    public List<RecipeMaterialEntry> Materials { get; set; } = new();

    /// <summary>厨师所需 cooking 技艺等级</summary>
    public int RequiredCookingSkill { get; set; } = 50;

    /// <summary>最低好感度要求(AI据好感决定是否接单,此为硬下限)</summary>
    public int MinFavorability { get; set; } = 0;

    /// <summary>费用区间[下限,上限]银两,AI在此范围自定</summary>
    public List<int> FeeRange { get; set; } = new() { 50, 100 };

    /// <summary>配方可由哪些厨师制作(空=任意厨师)。NPC id列表。</summary>
    public List<string>? AllowedChefs { get; set; }
}

/// <summary>
/// 锻造配方:玩家携矿石找铁匠打造装备。配方清单会注入铁匠AI prompt。
/// 仅T1(普通/稀有)装备,剧情神兵(珍贵/传说)不可打造。
/// </summary>
public class ForgeRecipeConfig
{
    /// <summary>配方ID(=产出装备ID,便于铁匠AI引用)</summary>
    public string Id { get; set; } = "";

    /// <summary>装备名称</summary>
    public string Name { get; set; } = "";

    /// <summary>品阶:普通/稀有可造;珍贵(epic)/传说(legendary)神兵不可打造</summary>
    public string Tier { get; set; } = "普通";

    /// <summary>产出装备物品ID</summary>
    public string ResultItemId { get; set; } = "";

    /// <summary>产出数量</summary>
    public int ResultQuantity { get; set; } = 1;

    /// <summary>所需矿石/材料</summary>
    public List<RecipeMaterialEntry> Materials { get; set; } = new();

    /// <summary>铁匠所需 forging 技艺等级</summary>
    public int RequiredForgingSkill { get; set; } = 50;

    /// <summary>最低好感度要求(AI据好感决定是否接单,此为硬下限)</summary>
    public int MinFavorability { get; set; } = 0;

    /// <summary>费用区间[下限,上限]银两,AI在此范围自定</summary>
    public List<int> FeeRange { get; set; } = new() { 50, 100 };

    /// <summary>配方可由哪些铁匠打造(空=任意铁匠)。NPC id列表。</summary>
    public List<string>? AllowedBlacksmiths { get; set; }
}

public class LearnPrerequisiteConfig
{
    public string? RequiredFaction { get; set; }
    public int RequiredKarma { get; set; }
    public int RequiredBuddhist { get; set; }
    public int RequiredAttack { get; set; }
    public int RequiredDefense { get; set; }
    public string? RequiredArtId { get; set; }
    public int RequiredArtLevel { get; set; }
}
