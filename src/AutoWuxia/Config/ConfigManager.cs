using System.Text.Json;
using AutoWuxia.Characters;
using AutoWuxia.Config.Models;
using AutoWuxia.Items;
using AutoWuxia.MartialArts;
using AutoWuxia.Quests;
using AutoWuxia.World;

namespace AutoWuxia.Config;

public class ConfigManager
{
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public Dictionary<string, CharacterConfig> Characters { get; } = new();
    public Dictionary<string, SceneConfig> Scenes { get; } = new();
    public Dictionary<string, MartialArtConfig> MartialArts { get; } = new();
    public Dictionary<string, FactionConfig> Factions { get; } = new();
    public Dictionary<string, ItemConfig> Items { get; } = new();
    public Dictionary<string, QuestConfig> Quests { get; } = new();
    public Dictionary<string, DungeonConfig> Dungeons { get; } = new();
    public Dictionary<string, MedicineRecipeConfig> MedicineRecipes { get; } = new();
    public Dictionary<string, FoodRecipeConfig> FoodRecipes { get; } = new();
    public Dictionary<string, ForgeRecipeConfig> ForgeRecipes { get; } = new();
    public WorldMapConfig? WorldMap { get; private set; }

    public ConfigManager(string? dataPath = null)
    {
        _dataPath = dataPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
    }

    public void LoadAll()
    {
        LoadCharacters();
        LoadScenes();
        LoadMartialArts();
        LoadFactions();
        LoadItems();
        LoadQuests();
        LoadDungeons();
        LoadMedicineRecipes();
        LoadFoodRecipes();
        LoadForgeRecipes();
        LoadWorldMap();
    }

    private void LoadCharacters()
    {
        var dir = Path.Combine(_dataPath, "characters");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<CharacterConfig>(file);
            if (config != null) Characters[config.Id] = config;
        }
    }

    private void LoadScenes()
    {
        var dir = Path.Combine(_dataPath, "scenes");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<SceneConfig>(file);
            if (config != null) Scenes[config.Id] = config;
        }
    }

    private void LoadMartialArts()
    {
        foreach (var subDir in new[] { "internal", "external", "light" })
        {
            var dir = Path.Combine(_dataPath, "martial_arts", subDir);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var config = LoadJson<MartialArtConfig>(file);
                if (config != null) MartialArts[config.Id] = config;
            }
        }
    }

    private void LoadFactions()
    {
        var dir = Path.Combine(_dataPath, "factions");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<FactionConfig>(file);
            if (config != null) Factions[config.Id] = config;
        }
    }

    private void LoadItems()
    {
        var dir = Path.Combine(_dataPath, "items");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<ItemConfig>(file);
            if (config != null) Items[config.Id] = config;
        }
    }

    private void LoadQuests()
    {
        foreach (var subDir in new[] { "main", "side", "faction", "dungeon_quest" })
        {
            var dir = Path.Combine(_dataPath, "quests", subDir);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var config = LoadJson<QuestConfig>(file);
                if (config != null) Quests[config.Id] = config;
            }
        }
    }

    private void LoadDungeons()
    {
        var dir = Path.Combine(_dataPath, "dungeons");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<DungeonConfig>(file);
            if (config != null) Dungeons[config.Id] = config;
        }
    }

    /// <summary>加载药物配方。支持 data/medicine_recipes.json(数组)或 data/medicine_recipes/ 目录。</summary>
    private void LoadMedicineRecipes()
    {
        var singleFile = Path.Combine(_dataPath, "medicine_recipes.json");
        if (File.Exists(singleFile))
        {
            var list = LoadJson<List<MedicineRecipeConfig>>(singleFile);
            if (list != null)
                foreach (var r in list)
                    if (!string.IsNullOrEmpty(r.Id)) MedicineRecipes[r.Id] = r;
            return;
        }
        var dir = Path.Combine(_dataPath, "medicine_recipes");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<MedicineRecipeConfig>(file);
            if (config != null && !string.IsNullOrEmpty(config.Id))
                MedicineRecipes[config.Id] = config;
        }
    }

    /// <summary>加载菜肴配方。支持 data/food_recipes.json(数组)或 data/food_recipes/ 目录。</summary>
    private void LoadFoodRecipes()
    {
        var singleFile = Path.Combine(_dataPath, "food_recipes.json");
        if (File.Exists(singleFile))
        {
            var list = LoadJson<List<FoodRecipeConfig>>(singleFile);
            if (list != null)
                foreach (var r in list)
                    if (!string.IsNullOrEmpty(r.Id)) FoodRecipes[r.Id] = r;
            return;
        }
        var dir = Path.Combine(_dataPath, "food_recipes");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<FoodRecipeConfig>(file);
            if (config != null && !string.IsNullOrEmpty(config.Id))
                FoodRecipes[config.Id] = config;
        }
    }

    /// <summary>加载锻造配方。支持 data/forge_recipes.json(数组)或 data/forge_recipes/ 目录。</summary>
    private void LoadForgeRecipes()
    {
        var singleFile = Path.Combine(_dataPath, "forge_recipes.json");
        if (File.Exists(singleFile))
        {
            var list = LoadJson<List<ForgeRecipeConfig>>(singleFile);
            if (list != null)
                foreach (var r in list)
                    if (!string.IsNullOrEmpty(r.Id)) ForgeRecipes[r.Id] = r;
            return;
        }
        var dir = Path.Combine(_dataPath, "forge_recipes");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var config = LoadJson<ForgeRecipeConfig>(file);
            if (config != null && !string.IsNullOrEmpty(config.Id))
                ForgeRecipes[config.Id] = config;
        }
    }

    private void LoadWorldMap()
    {
        var file = Path.Combine(_dataPath, "world_map.json");
        if (File.Exists(file))
            WorldMap = LoadJson<WorldMapConfig>(file);
    }

    private T? LoadJson<T>(string filePath) where T : class
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败 {filePath}: {ex.Message}");
            return null;
        }
    }

    public NPC CreateNPC(string characterId)
    {
        if (!Characters.TryGetValue(characterId, out var config))
            throw new KeyNotFoundException($"角色配置未找到: {characterId}");

        var npc = config.IsSectLeader
            ? new SectLeader { FactionId = config.FactionId ?? "" }
            : new NPC();

        npc.Id = config.Id;
        npc.Name = config.Name;
        npc.Description = config.Description;
        npc.Personality = config.Personality;
        // 全体 NPC 基础气血统一提高 10%；旧存档由 GameEngine 的版本迁移处理。
        npc.MaxHP = (int)Math.Round(config.MaxHP * 1.10, MidpointRounding.AwayFromZero);
        npc.CurrentHP = npc.MaxHP;
        npc.MaxMP = config.MaxMP;
        npc.CurrentMP = config.MaxMP;
        npc.BaseAttack = config.Attack;
        npc.BaseDefense = config.Defense;
        npc.Speed = config.Speed;
        npc.Karma = config.Karma;
        npc.FactionId = config.FactionId;
        npc.IsHiddenPower = config.HiddenPower;
        npc.IsHidden = config.IsHidden;
        npc.IsSectLeader = config.IsSectLeader;  // 设置掌门标识
        npc.IsTrainer = config.IsTrainer;        // 传武护法标识
        npc.DefaultSceneId = config.DefaultScene;
        npc.CurrentSceneId = config.DefaultScene;
        npc.Schedule = config.Schedule;

        // 装载已学武功(支持多套内功/外功)
        LoadLearnedArts(npc, config);

        // 设置江湖阅历等级
        npc.JianghuLevel = Math.Clamp(config.JianghuLevel, 1, 100);
        npc.NpcRole = config.NpcRole;
        npc.PortraitPath = config.PortraitPath;
        npc.Gender = config.Gender;
        // 装备初始武器/防具(直接挂槽位,不入背包;GetTotalAttack/Defense 自动计入加成)
        if (!string.IsNullOrEmpty(config.EquippedWeaponId) && Items.TryGetValue(config.EquippedWeaponId, out var wCfg))
            npc.EquippedWeapon = ItemFromConfig(wCfg);
        if (!string.IsNullOrEmpty(config.EquippedArmorId) && Items.TryGetValue(config.EquippedArmorId, out var aCfg))
            npc.EquippedArmor = ItemFromConfig(aCfg);
        npc.ShopFixedItems = config.ShopFixedItems ?? new();
        npc.ShopRandomItems = config.ShopRandomItems ?? new();

        foreach (var (targetId, relType) in config.Relations)
        {
            var rel = npc.GetRelation(targetId);
            if (Enum.TryParse<RelationType>(relType, out var rt))
                rel.Type = rt;
        }

        // 初始化技艺属性
        foreach (var (skillId, level) in config.CraftSkills)
            npc.SetCraftSkill(skillId, level);

        // 初始化NPC背包
        GiveNPCItems(npc, config);

        // 武功必须先装载完再回满；内功会提高总HP/MP上限。
        // 旧逻辑在装载武功前按基础上限赋值，导致所有会内功的NPC初始就是残血残蓝。
        npc.RestoreVitalsToFull();

        return npc;
    }

    public Player CreatePlayer()
    {
        var player = new Player();
        if (Characters.TryGetValue("player_default", out var config))
        {
            player.Name = config.Name;
            player.Description = config.Description;
            player.PortraitPath = string.IsNullOrWhiteSpace(config.PortraitPath)
                ? player.PortraitPath
                : config.PortraitPath;
            player.MaxHP = config.MaxHP;
            player.CurrentHP = config.MaxHP;
            player.MaxMP = config.MaxMP;
            player.CurrentMP = config.MaxMP;
            player.BaseAttack = config.Attack;
            player.BaseDefense = config.Defense;
            player.Speed = config.Speed;
            player.Karma = config.Karma;
            player.Talent = Math.Clamp(config.Talent, 1, 10);

            // 设置江湖阅历等级
            player.JianghuLevel = Math.Clamp(config.JianghuLevel, 1, 100);

            // 装载已学武功(支持多套内功/外功)
            LoadLearnedArts(player, config);

            // 初始化玩家技艺
            foreach (var (skillId, level) in config.CraftSkills)
                player.SetCraftSkill(skillId, level);
        }

        // 初始化玩家背包（给一些基础物品）
        GiveDefaultItems(player.Inventory);
        return player;
    }

    /// <summary>
    /// 按配置加载角色掌握的所有武功(内+外),并按 ActiveInternalArtId / ActiveExternalArtId
    /// 决定主动武功。未显式指定 active 时,取列表中第一本对应类型的武功。
    /// </summary>
    private void LoadLearnedArts(Characters.CharacterBase character, CharacterConfig config)
    {
        if (config.LearnedArts == null || config.LearnedArts.Count == 0)
            return;

        InternalArt? firstInternal = null;
        ExternalArt? firstExternal = null;
        LightArt? firstLight = null;
        InternalArt? activeInternal = null;
        ExternalArt? activeExternal = null;
        LightArt? activeLight = null;

        foreach (var entry in config.LearnedArts)
        {
            if (string.IsNullOrEmpty(entry.ArtId)) continue;
            if (!MartialArts.TryGetValue(entry.ArtId, out var artConfig))
            {
                System.Diagnostics.Debug.WriteLine($"角色 {config.Id}: 武功配置 {entry.ArtId} 未找到,跳过");
                continue;
            }

            int level = Math.Max(1, entry.ProficiencyLevel);
            MartialArtBase art = artConfig.Type switch
            {
                "internal" => InternalArt.FromConfig(artConfig, level),
                "light" => LightArt.FromConfig(artConfig, level),
                _ => ExternalArt.FromConfig(artConfig, level)
            };

            // 直接塞进 LearnedArts 列表,避免 LearnArt 自动设 active 抢占顺序
            if (!character.LearnedArts.Any(a => a.Id == art.Id))
                character.LearnedArts.Add(art);

            if (art is InternalArt ia)
            {
                firstInternal ??= ia;
                if (config.ActiveInternalArtId == ia.Id) activeInternal = ia;
            }
            else if (art is LightArt la)
            {
                firstLight ??= la;
                if (config.ActiveLightArtId == la.Id) activeLight = la;
            }
            else if (art is ExternalArt ea)
            {
                firstExternal ??= ea;
                if (config.ActiveExternalArtId == ea.Id) activeExternal = ea;
            }
        }

        character.ActiveInternalArt = activeInternal ?? firstInternal;
        character.ActiveLightArt = activeLight ?? firstLight;

        // 辅助内功:按 config.AuxiliaryInternalArtIds 装载(最多2本,跳过主内功)
        character.AuxiliaryInternalArts.Clear();
        if (config.AuxiliaryInternalArtIds != null)
        {
            foreach (var auxId in config.AuxiliaryInternalArtIds)
            {
                if (character.AuxiliaryInternalArts.Count >= CharacterBase.MaxAuxiliaryInternalArts) break;
                if (string.IsNullOrEmpty(auxId)) continue;
                var aux = character.LearnedArts.OfType<InternalArt>().FirstOrDefault(a => a.Id == auxId);
                if (aux == null) continue;
                if (character.ActiveInternalArt != null && aux.Id == character.ActiveInternalArt.Id) continue;
                if (character.AuxiliaryInternalArts.Any(a => a.Id == aux.Id)) continue;
                character.AuxiliaryInternalArts.Add(aux);
            }
        }

        // 外功:先放 active 那本,再按学习顺序补齐到上限
        character.ActiveExternalArts.Clear();
        var primary = activeExternal ?? firstExternal;
        if (primary != null)
            character.ActiveExternalArts.Add(primary);
        foreach (var ea in character.LearnedArts.OfType<ExternalArt>())
        {
            if (character.ActiveExternalArts.Count >= CharacterBase.MaxActiveExternalArts) break;
            if (character.ActiveExternalArts.Any(a => a.Id == ea.Id)) continue;
            character.ActiveExternalArts.Add(ea);
        }
    }

    public Item? CreateItem(string itemId)
    {
        if (!Items.TryGetValue(itemId, out var config)) return null;
        return ItemFromConfig(config);
    }

    public static Item ItemFromConfig(ItemConfig config)
    {
        var item = new Item
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            Type = Enum.TryParse<ItemType>(config.Type, true, out var t) ? t : ItemType.Consumable,
            Value = config.Value,
            Quantity = config.Quantity,
            Stackable = config.Stackable,
            ContainedArtIds = config.ContainedArtIds ?? new(),
            HPRecovery = config.HPRecovery,
            MPRecovery = config.MPRecovery,
            KarmaChange = config.KarmaChange,
            BuddhistChange = config.BuddhistChange,
            BuffType = config.BuffType,
            BuffValue = config.BuffValue,
            BuffDurationDays = config.BuffDurationDays,
            CombatUsable = config.CombatUsable,
            IsFood = config.IsFood,
            GiftFavorBonus = config.GiftFavorBonus,
            GiftPreference = config.GiftPreference,
            Rarity = string.IsNullOrEmpty(config.Rarity) ? "common" : config.Rarity,
            Slot = Enum.TryParse<EquipSlot>(config.EquipSlot, true, out var es) ? es : null,
            AttackBonus = config.AttackBonus,
            DefenseBonus = config.DefenseBonus,
            WeaponType = config.WeaponType
        };

        if (config.Prerequisite != null)
        {
            item.Prerequisite = new LearnPrerequisite
            {
                RequiredFaction = config.Prerequisite.RequiredFaction,
                RequiredKarma = config.Prerequisite.RequiredKarma,
                RequiredBuddhist = config.Prerequisite.RequiredBuddhist,
                RequiredAttack = config.Prerequisite.RequiredAttack,
                RequiredDefense = config.Prerequisite.RequiredDefense,
                RequiredArtId = config.Prerequisite.RequiredArtId,
                RequiredArtLevel = config.Prerequisite.RequiredArtLevel
            };
        }

        return item;
    }

    private void GiveDefaultItems(Inventory inv)
    {
        // 给玩家一些初始物品
        if (Items.TryGetValue("healing_pill_small", out var hpCfg))
            inv.AddItem(ItemFromConfig(hpCfg));
        if (Items.TryGetValue("mp_pill", out var mpCfg))
            inv.AddItem(ItemFromConfig(mpCfg));
        if (Items.TryGetValue("good_wine", out var wineCfg))
            inv.AddItem(ItemFromConfig(wineCfg));
    }

    public MartialArtBase? CreateMartialArt(string artId, int level = 1)
    {
        if (!MartialArts.TryGetValue(artId, out var config)) return null;
        return config.Type switch
        {
            "internal" => InternalArt.FromConfig(config, level),
            "light" => LightArt.FromConfig(config, level),
            _ => ExternalArt.FromConfig(config, level)
        };
    }

    private void GiveNPCItems(NPC npc, CharacterConfig config)
    {
        // 根据角色身份给不同物品
        if (config.IsSectLeader)
        {
            // 掌门有秘籍
            if (config.FactionId == "shaolin" && Items.TryGetValue("yijinjing_manual", out var shaolinManual))
                npc.Inventory.AddItem(ItemFromConfig(shaolinManual));
            if (config.FactionId == "wudang" && Items.TryGetValue("healing_pill_large", out var wudangPill))
                npc.Inventory.AddItem(ItemFromConfig(wudangPill));
        }

        // 少林NPC有佛法值
        if (config.FactionId == "shaolin")
        {
            npc.BuddhistValue = 30 + Random.Shared.Next(0, 30);
            if (Items.TryGetValue("sutra_diamond", out var sutra))
                npc.Inventory.AddItem(ItemFromConfig(sutra));
        }

        // 通用物品
        if (config.Attack > 150 && Items.TryGetValue("healing_pill_small", out var hp))
            npc.Inventory.AddItem(ItemFromConfig(hp));
    }

    public Faction? CreateFaction(string factionId)
    {
        if (!Factions.TryGetValue(factionId, out var config)) return null;
        return Faction.FromConfig(config);
    }
}
