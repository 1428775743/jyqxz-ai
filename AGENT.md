# 金庸群侠传 AutoWuxia - 项目架构文档

## 技术栈
- **语言**: C# / .NET 8
- **框架**: WinForms
- **数据格式**: JSON (配置驱动)
- **AI接口**: OpenAI 协议兼容 (支持自定义 endpoint + apikey)

## 项目结构

```
AutoWuxia/
├── AutoWuxia.sln              # 解决方案
├── src/AutoWuxia/             # 主项目
│   ├── Core/                  # 核心引擎层 (GameEngine/GameTime/EventSystem/GameState)
│   ├── Characters/            # 人物系统 (CharacterBase/Player/NPC/SectLeader/PlayerTag)
│   ├── MartialArts/           # 武功系统 (InternalArt/ExternalArt/LightArt/ArtEffect)
│   ├── Combat/                # 战斗系统 (CombatEngine/DamageCalculator/CombatAction)
│   ├── World/                 # 世界/地图/门派
│   ├── Quests/                # 任务系统 (QuestSystem/FactionQuestManager/ChainQuest)
│   ├── Systems/               # 辅助系统 (心情/善恶/体力/关系/音频/月度/年度演化)
│   ├── AI/                    # AI服务 (AIService/AIPromptBuilder/MonthlyAgentTools/AnnualAgentTools)
│   ├── Config/                # 配置管理 (ConfigManager/Models)
│   ├── Items/                 # 物品/背包 (Item/Inventory/EquipSlot)
│   └── Forms/                 # UI层
├── data/                      # JSON配置文件
│   ├── characters/            # 角色配置 (155+)
│   ├── scenes/                # 场景配置
│   ├── martial_arts/          # 武功配置 (internal/external/light)
│   ├── factions/              # 门派配置
│   ├── items/                 # 物品配置 (消耗品/秘籍/礼物/装备)
│   ├── dungeons/              # 副本配置
│   ├── quests/                # 任务配置 (main主线/faction门派)
│   └── world_map.json         # 世界地图 (locations + routes)
└── assets/
    ├── portraits/             # 人物头像
    ├── music/                 # 背景音乐 (mp3)
    ├── icons/                 # 图标
    └── ui/                    # UI素材
```

## 核心架构

### 继承体系

**人物**: `CharacterBase` → `Player` / `NPC` → `SectLeader`
- CharacterBase: HP/MP/攻防/体力/心情/善恶/武功/关系/履历
- Player: 背包/任务/位置
- NPC: 性格/日程/AI决策/隐藏实力
- SectLeader: 门派/收徒

**武功**: `MartialArtBase` → `InternalArt` / `ExternalArt`
- InternalArt: 护体(百分比+内力系数)/属性加成/词条
- ExternalArt: 伤害系数/暴击率/词条(无视防御/连击/吸内力等)

### 伤害计算
```
攻击 * 伤害系数 = 基础伤害 (暴击x1.5)
基础伤害 - 防御 * (1-无视防御%) = 实际伤害
护体: 抵消 = 实际伤害 * 护体%, 内力消耗 = 抵消 * 系数
最终扣血 = 实际伤害 - 护体吸收
```

### 时间系统
- 12时辰 (子丑寅卯辰巳午未申酉戌亥)
- 对话=0.5时辰, 切磋=1时辰, 战斗=2时辰, 赶路=按距离

### AI策略
- **配置文件驱动**: 主线/固定支线/核心判定
- **AI驱动**: NPC位置调度/自由对话/动态支线/NPC主动交互
- Prompt模板将NPC信息打包为JSON，返回结构化决策

## 关键文件

| 文件 | 说明 |
|------|------|
| `Core/GameEngine.cs` | 游戏主引擎，协调所有系统 |
| `Core/GameTime.cs` | 古代时辰系统 (12时辰/年/月) |
| `Core/EventSystem.cs` | 事件总线 |
| `Core/GameState.cs` | 游戏状态 (含存档/RuntimeQuests) |
| `Characters/CharacterBase.cs` | 人物基类 (装备位/GetTotal攻防/EquipItem) |
| `Combat/CombatEngine.cs` | 回合制战斗 |
| `Combat/DamageCalculator.cs` | 伤害计算(含护体/装备) |
| `Items/Item.cs` | 物品/背包/装备槽 (ItemType/EquipSlot/Inventory/药buff字段) |
| `Systems/HerbGatheringSystem.cs` | 采药系统 (planting技艺, 仿挖矿) |
| `Characters/MedicineBuff.cs` | 药buff单槽 (1天/覆盖/GetTotal*读取) |
| `AI/AIService.cs` | OpenAI协议客户端 |
| `AI/AIPromptBuilder.cs` | Prompt模板构建 (含aiHint/音乐家专属技能) |
| `AI/MonthlyAgentTools.cs` / `AnnualAgentTools.cs` | 月度/年度AI工具集 |
| `Quests/HuashanLunjianBuilder.cs` | 华山论剑10人对手选取 (善恶分流/关系优先/随机/阅历升序) |
| `Quests/DungeonRunner.cs` | 副本调度 (含华山扁平10人模式 FlatOpponents) |
| `Systems/AudioManager.cs` | NAudio音频单例 |
| `Systems/MonthlyUpdateSystem.cs` / `AnnualUpdateSystem.cs` | 月度/年度演化 |
| `Config/ConfigManager.cs` | JSON配置加载器 (CreateNPC/CreateItem) |
| `Forms/MainForm.cs` | 主UI窗体 |
| `Forms/CharacterCreateForm.cs` | 角色创建 (roll属性/技艺/天赋/头像) |
| `Forms/CombatForm.cs` | 战斗UI (血条+头像) |
| `Forms/StoryDialogueForm.cs` | RPG剧情对话窗 |
| `Forms/DungeonForm.cs` | 副本多轮战斗UI |
| `Forms/EndingForm.cs` | 华山论剑结束画面 (流式AI作传+感谢游玩+回首页) |

## 构建运行
```bash
# 需要 .NET 8 SDK
dotnet restore
dotnet build
dotnet run --project src/AutoWuxia
```

## 项目进度

### 第一批：基础框架 ✅
- [x] .NET 8 WinForms 项目结构
- [x] GameEngine / GameTime / EventSystem
- [x] CharacterBase / Player / NPC / SectLeader
- [x] ConfigManager JSON配置加载
- [x] MainForm 基础UI
- [x] 基础场景切换和NPC交互

### 第二批：武功与战斗 ✅
- [x] InternalArt / ExternalArt 武功体系
- [x] 护体内功机制
- [x] 武功词条系统 (ArtEffect)
- [x] CombatEngine 回合制战斗
- [x] DamageCalculator 伤害计算

### 第三批：关系与门派 ✅
- [x] RelationshipSystem (好感度/拜师/结拜)
- [x] FactionSystem (门派加入/学武)
- [x] MoodSystem / KarmaSystem / StaminaSystem

### 第四批：地图与NPC调度 ✅
- [x] WorldMap / Location / Scene
- [x] NPCLocationManager (AI批量位置调度)
- [x] 赶路系统

### 第五批：AI系统与任务 ✅
- [x] AIService (OpenAI协议)
- [x] AIPromptBuilder
- [x] SettingsForm (API配置)
- [x] 任务系统 (MainQuest/SideQuest/DynamicQuest)

### 第六批：战斗后果与完善 ✅
- [x] 战后处理系统 (杀死/羞辱/放过，NPC胜利后可杀死/羞辱玩家)
- [x] 切磋系统 (非致命，NPC隐藏实力时只显示40%属性)
- [x] 多存档位系统 (5个存档位，含存档信息预览)
- [x] UI全面优化 (战斗面板、战后选择按钮、地图查看、拜入门派)
- [x] 丰富配置数据 (10个角色、11个武功、3个门派、9个场景)
- [x] 被杀惩罚 (攻击力-2、位置重置、NPC关系变化)
- [x] 杀死NPC引发连锁关系反应 (亲友怀恨)

### 第七批：任务系统 & 副本系统 ✅
- [x] Player 声望(Reputation) + 门派贡献(FactionContributions) 字段
- [x] 任务三态状态机 (InProgress / Completed / Rewarded / Failed)
- [x] 任务奖励扩展 (金钱、声望、门派贡献、物品)
- [x] 收集任务: 提交物品流向委托 NPC 背包
- [x] 门派任务管理器 `FactionQuestManager` (添加/删除/接受)
- [x] 山贼讨伐任务三难度 (easy/medium/hard 副本)
- [x] 副本系统 `Dungeon` + `DungeonRunner` (多轮战斗 + 战后 AI 对话)
- [x] 副本失败处罚配置驱动 (deductGold / deductHP / gameOver)
- [x] 华山论剑触发器 (声望 ≥ 10000 时点击地图弹"飞鸽传书")
- [x] 任务列表 UI (`QuestListForm` 三页签 Modal 窗口)
- [x] 副本战斗 UI (`DungeonForm` 多轮 + 战后对话)
- [x] 6 个山贼角色 + 4 个副本配置 + 5 个门派任务样例

### 第八批：金庸三部曲剧情线 ✅
- [x] 射雕英雄传主线(8步): 襄阳遇黄蓉→拜洪七公学降龙十八掌/打狗棒法→桃花岛战欧阳克→困岛遇周伯通习九阴真经/左右互搏/空明拳→铁掌峰战裘千仞→皇宫取武穆遗书+岳家枪法
- [x] 神雕侠侣4链: 全真风雨(被逐下山解锁古墓)→古墓情缘→神雕剑冢(习玄铁剑法)→神雕大侠(守襄阳/学黯然销魂掌)
- [x] 倚天屠龙记1主线3支线: 武当学九阳→光明顶战成昆→任教主→灵蛇岛得屠龙刀→战赵敏得倚天剑→刀剑互斫取遗书+乾坤大挪移
- [x] 全真教+古墓派两门派, 25角色, 24武功, 17物品, 13场景
- [x] AIPromptBuilder.BuildQuestContext 通用化aiHint注入(剧情NPC自动获态度提示,按知情度分层防剧透)
- [x] QuestReward.GetSummary 显示武功/物品中文名而非拼音ID

### 第九批：背景音乐/角色创建/天赋/战败处置重构 ✅
- [x] AudioManager(NAudio) + AudioConfig, 设置窗改 TabControl(AI/音效/音乐试听)
- [x] 首页播放大地图BGM, assets/music 入库6首
- [x] 角色创建 CharacterCreateForm: roll 血/攻/防(仅全部重投)、技艺roll+自由分配(总值上限75)、天赋3选1(小虾米来也/轻功大师/勤学苦练)
- [x] Player.TrainingSpeedBonus/EffectiveTrainingMultiplier 修炼速度, CombatEngine应用
- [x] 头像放大查看(人物信息窗点击头像预览)
- [x] 生死战确认框仅在玩家主动挑战时弹出
- [x] 战败处置重构: NPC胜利由AI决策杀/赎金/羞辱, 移除攻击力-2惩罚与强制传送, 改为战败受辱debuff(战斗属性-20%持续7天), 修复KillPlayer未触发GameOver
- [x] 返回首页功能(MainForm顶部按钮 + Program循环)

### 第十批：BGM场景映射 + 音乐家NPC巡游演奏 ✅
- [x] BGM场景映射: 首页/角色创建/主界面/华山副本 各自BGM,进出副本自动切换
- [x] 4位音乐家NPC(npcRole=musician): 琴仙子/笛翁/箫逸人/鼓乐客,分驻不同区域巡游
- [x] 月度AI高频巡游乐师(schedule各时段分散,context标注【巡游乐师】)
- [x] play_music专属技能(AIPromptBuilder对musician注入角色能力块,曲目列表动态取自AudioManager.ListMusicFiles)
- [x] 乐师演奏收赏钱(musicFee,NPC自定默认10~30两,收费需玩家确认)

### 第十一批：年度AI剧情生成器 + 日志玩家视角 ✅
- [x] AnnualAgentTools剧情编辑器7工具(create_story_quest/add_quest_step/remove/update/finalize/query_world_elements/list_draft_quests),全流程ID防幻觉校验
- [x] AnnualUpdateSystem: 每12个月触发,注入人物经历/关系+玩家状态+元素ID清单+模板,生成2-3个大事件剧情任务
- [x] AnnualProgressForm年度进度窗, GameTime加Year/IsNewYear, GameState.RuntimeQuests运行时任务池(随存档序列化)
- [x] 运行时任务池: GetOfferableQuests+AcceptChainQuest同时扫描配置文件+RuntimeQuests
- [x] 月度/年度日志改玩家视角(江湖传闻文案,去技术日志/时间前缀,CleanSummary去套话)

### 第十二批：RPG剧情对话系统 + AI稳定性 ✅
- [x] StoryDialogueForm: RPG剧情对话窗(接任务/步骤完成/领奖励时弹,左头像右台词,点击或空格推进)
- [x] 年度AI新增set_quest_dialogue工具生成剧情对话
- [x] 修复deepseek-v4-flash把回复放reasoning_content而content为空(content空时回退读reasoning_content,强化JSON提取)
- [x] 赠礼窗口加高加宽 + give_item/ask_item防拼音幻觉(背包注入物品ID,无效ID降级none)

### 第十三批：天龙八部剧情线 + 战斗效果补全 ✅
- [x] 天龙四任务线: 段誉/乔峰/虚竹三线汇聚少室山大战(前置三线完成方可触发)
- [x] 14位人物, 12本武功(六脉神剑/北冥神功/一阳指/天山折梅手/斗转星移/生死符等), 7场景, 2门派(大理段氏/逍遥派), 9物品
- [x] QuestConfig加PrerequisiteQuestIds字段, GetOfferableQuests加前置任务完成检查(支持多线汇聚)
- [x] CombatEngine实现Stun/Bleed/Knockback/DoubleStrike四个原本未生效的战斗效果(复用TimedBuff)

### 第十四批：玩家头像选择系统 ✅
- [x] Player新增PortraitPath字段, ConfigManager读取player_default.json
- [x] CharacterCreateForm增加头像选择区(6款:布衣少侠/青衫剑客/黑衣浪客/红衣女侠/白衣侠女/劲装游侠)
- [x] MainForm/StoryDialogueForm适配玩家头像, 补充NPC头像资源

### 第十五批：连城诀 + 碧血剑剧情线 ✅
- [x] 连城诀: 5武功(神照经mythic)/6物品/13角色/4场景/3任务(丁典线/血刀线/汇聚线)
- [x] 碧血剑: 6武功(金蛇剑法mythic)+复用神行百变/五毒掌/6物品/13角色/3场景/3任务

### 第十六批：侠客行 + 鹿鼎记剧情线 ✅
- [x] 侠客行: 5武功(太玄经mythic,全属性极强+回血+减伤+反震)+1复用/4物品/11角色/5场景/3任务
- [x] 鹿鼎记: 6武功(化骨绵掌legendary,化骨蚀肉流血)+1复用/6物品/11角色/5场景/3任务(武功层次偏低符合原著)

### 第十七批：装备系统 ✅
- [x] 武器/防具装备系统: EquipSlot(Weapon/Armor) + AttackBonus/DefenseBonus/WeaponType
- [x] Item/ItemConfig加装备字段, Item.Clone同步, 显示带[攻+X 防+Y]
- [x] CharacterBase加EquippedWeapon/Armor装备位, GetTotalAttack/Defense计入加成(战斗与UI全自动生效), EquipItem/UnequipItem方法
- [x] CharacterConfig加EquippedWeaponId/ArmorId, CreateNPC装载初始装备
- [x] UI: 玩家/NPC信息面板攻防显示「基础(总值)」+装备行, 背包弹窗装备按钮, 战斗画面血条旁双方头像
- [x] 20件装备(14新+6迁移): 拳套3/剑4/刀2/枪1/棍2/暗器2 + 防具6
- [x] 15个NPC配装备(郭靖金丝拳套+软猬甲/令狐冲玄铁剑/血刀老祖血刀/韦小宝含沙射影+金丝宝衣/东方不败葵花银针等)
- [x] 5个任务加装备奖励(碧血剑金蛇剑/连城诀血刀/侠客行乌蚕衣/射雕软猬甲/天龙金丝拳套)
- [x] 倚天剑保留秘籍(藏九阴真经), 屠龙刀转装备

### 第十八批：华山论剑终章 ✅
- [x] 华山论剑改为终章 10 人车轮战:按玩家善恶分流(善≥50打恶人 karma<30 / 恶打正派 karma≥50),关系优先+同档随机+阅历升序选 10 位绝顶高手(Lv≥40),末位为最终Boss
- [x] 进入论剑一次性满血满蓝(决战公平起手),10 场之间不回血(纯车轮战,未来制药系统补战斗嗑药);败北=Game Over
- [x] 对手属性按 `opponentStatMultiplier`(默认1.2)放大 HP/MP/攻防;`HuashanLunjianBuilder` 选人,`DungeonRunner` 加 `FlatOpponents/IsHuashanLunjian/DefeatedOpponents` 扁平模式
- [x] `AIService.ChatStreamAsync` SSE 流式接口(逐块回调 onChunk,reasoning_content 回退,失败静默)
- [x] `EndingForm` 结束画面:玩家头像+属性摘要,AI 流式作传(打字机效果),收尾"感谢游玩·江湖路远,后会有期",回首页按钮;AI 未配置/失败走降级模板
- [x] 接线:QuestListForm 检测 huashan_lunjian → 确认框 → CreateHuashanRunner(满血)→ DungeonForm(进度 X/10,胜利不弹框)→ EndingForm → 透传 ReturnToStart 回首页;败北透传 Abort→Application.Exit
- [x] `GameState.HuashanCompleted` 标记;BGM 沧海一声笑(结束曲)

### 第十九批：药物系统 ✅
- [x] 采集(采药):`HerbGardenConfig`+`HerbGatheringSystem`(用 gathering(原planting,batch21改名)技艺,仿挖矿);5个backhill场景加药园节点;新增4草药(黄芩/当归/灵芝/雪莲);MainForm"采药"按钮+UI
- [x] 药buff:`Item`加 BuffType/BuffValue/BuffDurationDays/CombatUsable 字段;`Player.MedicineBuff` 单槽(吃新药覆盖,按天tick);`GetTotalAttack/Defense/Speed/MaxHP/MaxMP` 读取;regen类每回合回血
- [x] 药商:5个 medicine_merchant 补全 shopItems(含5种buff药:聚力/铁骨/凝神/活血/强身丹);现有小还丹/大还丹/回气丸标 combatUsable
- [x] 战斗嗑药:`CombatEngine.GetPlayerSkills` 把 CombatUsable 消耗品加入技能面板;`ExecuteRound` IsItem 分支(服下回血/回内,消耗1次出手);华山10连战由此可玩
- [x] 药师AI制作(仿琴师):`AIPromptBuilder` 对 wandering_doctor/imperial_doctor 注入 craft_medicine 能力块+配方清单(按医术过滤);AI 返回 action=craft_medicine/actionTarget=配方ID/craftFee;`DialogueForm` 工费确认框;`MainForm.HandleCraftMedicine` 校验配方/医术/好感/材料/银两→扣料产出
- [x] 配方分层:`data/medicine_recipes.json` 6配方4品阶(普通清心丹/聚力散→稀有九转小还丹→珍贵黑玉断续丹→传说九转还魂丹/太乙神丹);15新物品(4草药+5药商buff药+6药师丹药)

### 第二十批：1主2辅内功 ✅
- [x] `CharacterBase.AuxiliaryInternalArts`(最多2本)+`MaxAuxiliaryInternalArts`;`GetTotalAttack/Defense/MaxHP/MaxMP` 加辅助内功50%属性贡献(`AuxiliaryBonus`);被动/主动仍只读主内功 `ActiveInternalArt`
- [x] `CharacterConfig.AuxiliaryInternalArtIds` 字段;`ConfigManager.LoadLearnedArts` 按ID装载辅助内功(跳过主内功)
- [x] `MartialEquipForm` 加辅助内功 CheckedListBox(最多2,不可与主重复,UI显示含辅助50%的合计加成)
- [x] 存档序列化自动兼容(旧存档辅助槽为空)

### 第二十一批：打猎+厨师+食物buff ✅
- [x] 技艺调整:`planting→gathering`(采药,中文名"采集");玩家 `medicine` 技艺(无用,全代码仅NPC读取)替换为 `hunting`(打猎)。`CharacterBase.GetCraftSkillName` 加 gathering/hunting/cooking 映射;`Player`默认+`CharacterCreateForm.CraftIds` 更新为 art/forging/mining/gathering/hunting;`HerbGatheringSystem` 用 gathering;`HuntingSystem` 由 mining 改 hunting;67个角色JSON+`wudang_town` CraftLesson 批量 planting→gathering(NPC药师 medicine 保留作炼药门槛,cooking 为厨师NPC专属)
- [x] 打猎食材:新增 `fresh_meat`(鲜肉)物品,加入6个backhill场景 hunt loot;打猎产出=皮毛卖钱+食材(肉/鱼/菇/蜜)+药材(熊胆/鹿茸/参)
- [x] 食buff:`FoodBuff.cs`(镜像 MedicineBuff,单槽覆盖);`Player.FoodBuff`;`CharacterBase.BuffMultiplier`(药buff+食buff独立叠加,替换原 MedicineMultiplier,作用于GetTotalAttack/Defense/Speed/MaxHP/MaxMP);`GameEngine.ProcessDailyTags` 食buff tick;`CombatEngine` regen 兼容药/食叠加;`Item.IsFood` 字段+`Use()`路由(食物挂食buff,药挂药buff,互不冲突)+Clone同步
- [x] 菜谱配置:`FoodRecipeConfig`(RequiredCookingSkill/AllowedChefs,镜像 MedicineRecipeConfig,复用 RecipeMaterialEntry);`ConfigManager.FoodRecipes`+`LoadFoodRecipes()`(读 data/food_recipes.json);`ItemFromConfig` 映射 IsFood
- [x] 厨师AI制作(仿药师):`AIPromptBuilder` 对 npcRole=chef 注入 craft_food 能力块+菜谱清单(按厨艺+AllowedChefs过滤);`DialogueForm` craft_food case(工费确认);`DialogueSystem` craftFee clamp 加 craft_food;`MainForm.HandleCraftFood` 校验菜谱/厨艺/好感/食材/银两→扣料产出
- [x] 数据:6厨师NPC(临安陆厨子/嵩山孙厨娘/襄阳赵军厨/大理段师傅/武当清风道厨/临安皇城御厨王鼎,厨艺40→100,御厨独占传说菜);`data/food_recipes.json` 9菜4档;9菜肴物品(烤肉串/蘑菇鲜肉汤/腊肉煲饭/红烧野味/蜜汁烤鱼/山珍烩饭/鹿茸炖鸡/佛跳墙/满汉全席)

### 第二十二批：铁匠打造装备 ✅
- [x] 配置镜像:`ForgeRecipeConfig`(RequiredForgingSkill/AllowedBlacksmiths,复用 RecipeMaterialEntry);`ConfigManager.ForgeRecipes`+`LoadForgeRecipes()`(读 data/forge_recipes.json)
- [x] 铁匠AI打造(仿药师/厨师):`AIPromptBuilder` 对 npcRole=blacksmith/weapon_merchant 注入 craft_forge 能力块+配方清单(按锻造+AllowedBlacksmiths过滤,明示可造上至稀有、传说神兵不可造);`DialogueForm` craft_forge case(工费确认);`DialogueSystem` craftFee clamp 加 craft_forge;`MainForm.HandleCraftForge` 校验配方/锻造/好感/材料/银两→扣料产出装备
- [x] 数据:3铁匠NPC已存在(刘铁匠刘家村forging60/孙铁匠武当60/王铁匠襄阳80);`data/forge_recipes.json` 18配方(10 T1普通/稀有 + 8稀有进阶);16装备物品(T1:铜刀/铁枪/铁禅杖/铁手套/银剑/铁甲/铜甲/银丝甲+复用铁剑皮甲;稀有:精钢剑/寒铁剑/烈焰刀/陨星剑/精钢甲/寒铁甲/烈焰甲/陨铁甲)
- [x] 平衡:锻造上限 rare(陨星剑攻+80/陨铁甲防+80,需天外陨铁/寒铁/火矿等黑木崖高危矿所产特矿);传说/神话神兵(玄铁剑120/打狗棒100/屠龙刀180/天蚕衣110)不可造,剧情至宝仍是最强。王铁匠(forging80)独占烈焰/陨星/陨铁顶级配方

### 第二十三批：善恶系统强化 + 恶人线剧情 ✅
- [x] 默认善恶 50→**20**(Player.cs + player_default.json;20=普通人,卡档)。KarmaBonus 任务奖励已生效(QuestSystem.GrantReward);MonthlyAgentTools manage_faction_quests 补 karmaBonus(schema+parse);AnnualAgentTools 已有 karmaBonus。月度/年度 AI prompt 加"按任务性质设 karmaBonus(正+/邪−)、可创建恶人线"指令;create_story_quest 补 prerequisiteQuestIds/exclusiveWithQuestIds 字段
- [x] 正/恶线互斥:`QuestConfig.ExclusiveWithQuestIds`(玩家任务日志有任一则不可接);`MainForm.TryOffer` 加校验
- [x] 门派阵营:`FactionConfig.Alignment`(正派/中立/邪派);9门派标注(正派5:少林/武当/华山/全真/大理;中立3:古墓/逍遥/明教;邪派1:日月神教);`MonthlyAgentTools` 门派任务未显式设 karmaBonus 时按阵营默认(正派+5/邪派−5/中立0);月度AI prompt 按门派阵营设 karmaBonus → 不同门派任务善恶奖励不同
- [x] 恶人线数据(6条,正/恶互斥):天龙`tianlong_shaoshi_evil`(助慕容复/丁春秋/鸠摩智,打三兄弟,reward 小无相功,−20);笑傲`xiaojiao_evil`(助任我行,打令狐冲/岳不群,reward 葵花宝典,−15);倚天`yitian_evil`(助成昆/赵敏,打张无忌/谢逊,reward 玄冥神掌,−15);连城诀`liancheng_evil`(助凌退思/万震山夺宝,打狄云/丁典,reward 血刀大法+连城宝藏,−15);鹿鼎记`luding_evil`(助鳌拜篡权,打康熙/海大富,reward 化骨大法,−15);神雕`shendiao_evil`(助蒙古攻宋,打杨过/小龙女,reward 迦沙伏魔,−15)。各正线反向加 exclusiveWith;射雕跳过

### 第二十四批：九阳加强+反派补内功+效果审计 ✅
- [x] 九阳神功加强:加 HPRecover 10%(九阳护体每回合回血)+ DamageReduction 20%@Lv5(九阳护体减伤),贴合原著"刀枪不入、生生不息"
- [x] 反派补内功:9位无内功恶人配上弱内功(血刀老祖→少林罗汉Lv5,温方达/温方山→武当基础Lv5,吴三桂/万震山→基础气功Lv5,宝象/戚长发/万圭/凌退思→基础气功Lv4),略微提升战力
- [x] 效果审计(检测未实装效果,详见下"未实装效果清单")

### 未实装效果清单(第二十四批审计,已于第二十五批全修复)
- ~~`MPResist`(易筋经)、`levelBonuses`(全武学)完全死配置~~ → 已修复
- ~~内功 DoubleStrike/TrueDamage/CounterAttack/DrainMP 被外功处理器漏掉~~ → 已修复
- ~~外功 MPRecover 被内功处理器漏掉、Evasion 仅轻功生效~~ → 已修复

### 第二十五批：未实装效果全修复 ✅
审计发现 8 类未实装效果,全部修复(复测"全部实装",0 死配置):
- [x] `levelBonuses` 实装:`ExternalArt.GetEffectiveDamageCoefficient()` 按等级应用最高已解锁阈值的 damageCoefficient 覆盖(太极拳Lv5→2.2/Lv10→2.8);118 条 levelBonuses 全部生效
- [x] `MPResist` 实装:`DamageCalculator` 吸内计算扣减 `(1-MPResist)`(易筋经抗吸内生效)
- [x] 内功 `DoubleStrike`(葵花宝典)/`TrueDamage`(九阳)/`CounterAttack`(蛤蟆功,玉女心经)/`DrainMP`(吸星大法):处理器扩展同时查 `ActiveInternalArt`(原仅查外功)
- [x] 外功 `MPRecover`(太极拳)/`HPRecover`:`CombatEngine.ExecuteAttack` 加 on-use case(出招时回内/回血,原仅内功被动路径)
- [x] `Evasion` 扩展:`CombatEngine` 闪避判定查轻功+内功+外功三源(原仅轻功),天罡北斗/打狗棒法/太极剑的闪避生效
- [x] 影响:一批高手配置真正生效——张三丰太极拳系数 1.8→2.8、欧阳锋反击、令狐冲吸内、东方不败葵花连击、九阳真伤等;之前按死配置算的战力/单挑分析需重估

### 第二十六批：标题/UI优化/存档/打造/MiMo/暴击（2026-07-09）
- [x] 游戏窗口/首页标题改"金庸群侠传-AI"
- [x] 休息6时辰(恢复≤1天一半)/休息3天(回满)按钮；玩家信息+NPC经历查看(LifeEvent系统提升到CharacterBase)；时间显示"第N年 第M天 时辰"(代码层仍Day+ShiChenIndex)；"皇图霸业谈笑中"按钮弹游戏介绍窗
- [x] 存档遗漏修复：对话历史(`GameState.DialogueHistories`)、门派任务池(`GameState.FactionQuestPool`)纳入存档序列化
- [x] 门派任务限制(非本门弟子不可接)；打造好感度移除NPC方法内硬限制改提示词判断；矿石数量提示词区分玩家持有量vs配方需求量
- [x] 小米MiMo API适配：`AIService.GetChatEndpoint`(兼容endpoint带不带/v1) + `SerializeRequest`注入`thinking.type=disabled`关思考链
- [x] 暴击伤害修复：`ExternalArt.CalculateBaseDamage`移除内部暴击(避免双重随机不同步)，`DamageCalculator`统一判断×1.5；月度/年度agent改用主Model(`modelOverride:null`，原MonthlyModel默认deepseek-chat导致400)

### 第二十七批：UI缩放+战斗+拖动+采集（2026-07-09）
- [x] 界面缩放倍率(4K支持)：设置->显示选100/125/150/200%；`WuxiaTheme.Scale`(启动从`DisplayConfig`加载)+`ApplyScaling(form)`(form.Scale缩控件+窗体Size,防二次Tag="scaled")+字体统一`UiFont`乘系数；动态控件Size用`S(w,h)`/`V(v)`；首页改缩放自动刷新(StartForm关闭重建,无需重启进程)，主界面改缩放需重启
- [x] 战斗技能框自适应高度(`LayoutSkillArea`按技能数算行数,日志区让出空间,缩放感知)；车轮战`DungeonForm`按切磋`CombatForm`深色主题重做(双方头像/读条/攻防速面板,修对手HP Label未加入Controls的预存bug,技能按钮改DisplayText)
- [x] 弹窗拖动(`Forms/FormDragHelper`)：MouseDown+ReleaseCapture+SendMessage(WM_NCLBUTTONDOWN,HTCAPTION)；无边框窗体(StoryDialogueForm阈值拖动不破坏点击推进剧情/WuxiaConfirmBox标题栏/CreateLoadingForm)
- [x] 副业采集：挖矿/采药/打猎单次体力消耗统一10点(原15/12/12)；三个采集窗体加"一键挖矿/采药/打猎"按钮耗尽体力连续采集(times=Stamina/10,推进时间,产出自动堆叠,汇总日志)，`MainForm.RunBatchGather`统一循环

### 第二十八批：武功10层+CD+月度门派+任务触发+场景路线+任务提示（2026-07-09）
- [x] 武功最高10层：原22个武功maxLevel为5/6/8统一改10(门派武功需8层学下一层,上限需>8)，`data/martial_arts/`全102个均10层
- [x] 请教武功CD 30天->5天(`MainForm.HandleTeachArt`+`AIPromptBuilder`)；加入门派被AI拒绝后10天不可再申请该门派(`Player.FactionJoinRejections`记录被拒Day,`GameEngine.TryJoinFactionAsync`检查)
- [x] 月度agent门派任务工具描述从"少林/武当/明教"扩展到全部9个可加入门派(少林/武当/华山/明教/全真/古墓/逍遥/大理段氏/日月)，补充各派掌门作委托NPC
- [x] 进场景任务触发器：`QuestConfig`新增`TriggerSceneId`字段,进入该场景自动弹委托框接取(`MainForm.OnSceneEntered`/`OfferSceneTriggeredQuests`)；跨城镇旅行(`TravelToLocation`)补go步骤推进(原只有本地步行TravelToScene推进)；围攻光明顶专项(接取yitian_main时成昆从少林移至光明顶供交手,`HandleQuestAcceptSideEffects`改DefaultSceneId+全时辰Schedule)
- [x] 场景路线修正：光明顶移除直连武当镇(走world_map)；万安寺改连京师(同北京地区,原直连临安不合理)+world_map京师location加wanansi
- [x] 任务步骤推荐条件提示：任务列表详情当前步骤(▶)显示"提示"行(`QuestListForm.BuildStepHint`)，根据ActionType+Target生成目标(前往某场景/与某NPC对话交手)+Conditions(需学N门武功/需门派/善恶区间)，防止玩家瞎猜

### 第二十九批：月度并行+关系系统+性别+杂货铺+存档版本号（2026-07-10）
- [x] 月度演化并行化：拆4子任务(位置调度/武功经验/经历际遇/门派任务)`Task.WhenAll`并行,各独立AI调用+工具子集(`MonthlyAgentTools.GetToolDefinitions`支持子集),实时打印(子任务完成触发`OnToolResult`,`MonthlyProgressForm`已Invoke安全);主agent汇总各子任务结果生成月度总结
- [x] 月度总结改「江湖月报」风格(`GenerateSummary` prompt)：小标题分段(门派风云/江湖际遇/高手动向)、不写任务细节、挑主要NPC经历总结、夸张修辞、300-500字
- [x] 技艺分配修复：roll随机基础值锁定不能减(`CharacterCreateForm`加`_craftBase`跟踪,`AdjustCraft`的减检查`cur>_craftBase[id]`),只能加,加的才能减回
- [x] 关系系统(AI驱动)：移除按钮,改NPC对话时AI按条件主动提(结拜/结婚/收徒)。`Gender`字段(`CharacterBase`+`NPCConfig`+`ConfigManager`)+玩家创建选性别+批量配161个NPC性别(男134女33,动物跳过)。`AIPromptBuilder`加3个action说明+context注入NPC性别/玩家性别/婚姻师徒状态;结婚仅异性、收徒仅宗师(legendary/mythic内功)、结拜不限性别。`DialogueForm`弹确认+`MainForm`调`RelationshipSystem`
- [x] 仇敌月度寻仇：`PostProcessNPCs`把Enemy关系NPC调度到玩家当前场景(改DefaultSceneId+Schedule各时辰),月度日志显示
- [x] 师徒请教CD更短：`HandleTeachArt`检查师徒关系(`RelationType.Disciple`),师徒CD 2天/普通5天
- [x] 城镇杂货铺：9城各加杂货铺NPC(临安/襄阳/扬州/武当镇/福建/嘉兴/京师/大理/荆州)+10地方特产(龙井茶/桂花酒/普洱茶/铁观音/烧刀子/马奶酒/山水画卷/景德瓷器/蜀锦/燕窝),按地方特色配固定+随机商品,零C#改动复用`ShopSystem`
- [x] 存档版本号+迁移：`GameState`加`SaveVersion`+`CurrentSaveVersion`常量;`MigrateSave`幂等补全(每次加载补入配置新增NPC/场景/门派,加新内容旧档自动兼容无需bump)+版本化迁移(破坏性结构改动按版本号);`.gitignore`补`dist/`
- [x] 百晓阁查询位置bug修复：`HandleQueryLocation`改用`GetCurrentSceneByTime`(当前时辰)按Schedule算实际位置(原用`CurrentSceneId`旧值,成昆被围攻光明顶改Schedule后查询仍返回少林)


### 待完成
- [ ] 装备系统扩展(饰品槽/套装效果/强化/商店售卖装备)
- [ ] 更多武器防具与NPC装备配置
- [ ] 更多动态支线/年度AI剧情打磨
- [ ] 更丰富的AI对话场景与NPC交互

## 配置说明

所有游戏数据在 `data/` 目录下，修改JSON即可：
- 添加角色：`data/characters/新角色.json`
- 添加场景：`data/scenes/新场景.json`
- 添加武功：`data/martial_arts/internal|external/新武功.json`
- 添加门派：`data/factions/新门派.json`
- 修改路线：`data/world_map.json`

## AI API配置

在设置界面中配置：
- Endpoint: API地址 (默认 https://api.deepseek.com)
- API Key: 密钥
- Model: 模型名

支持任何兼容 OpenAI `/v1/chat/completions` 协议的API。

## 门派任务管理 (Agent 接口)

`Quests/FactionQuestManager.cs` 提供门派任务的程序化管理接口,
通过 `_engine.FactionQuests` 访问。

### 接口签名

```csharp
// 收集类任务 (NPC 委托, 物品提交后流入 issuer NPC 的背包)
FactionQuest AddCollectionQuest(
    string factionId, string issuerNpcId,
    string itemId, int quantity,
    QuestReward? reward = null,
    string? name = null, string? description = null);

// 山贼讨伐任务 (绑定 bandit_easy / bandit_medium / bandit_hard 副本)
FactionQuest AddBanditQuest(string factionId, string difficulty);

// 移除任务
bool RemoveQuest(string questId);

// 查询
List<FactionQuest> GetAvailableQuests(string factionId);
List<FactionQuest> GetIssuedByNpc(string npcId);
List<FactionQuest> GetAll();

// 玩家接受任务 (从可领取池移入 player.QuestLog)
bool AcceptQuest(Player player, string questId);
```

### 收集任务的 NPC 委托工作流

1. Agent 决策某 NPC 想要某物 → 调 `AddCollectionQuest("wudang", "wudang_town_elder", "herb_baizhu", 5, reward)`
2. 任务进入 `_byFaction["wudang"]` 可领取池, 玩家在与 NPC 对话或门派事务中可见
3. 玩家 `AcceptQuest` → 从池中移除, 加入玩家 QuestLog (Status=InProgress)
4. 玩家凑齐物品后在「任务列表」窗口点「提交物品」
5. `quest.TrySubmitItems(player, issuerNpc, out msg)` 把物品 `Inventory.TransferTo` 到 NPC 背包, 状态转为 Completed
6. 玩家点「领取奖励」 → 状态转为 Rewarded, 奖励发放

### 玩家如何在游戏内领取门派任务

1. 进入门派大殿（武当: `wudang_hall` / 少林: `shaolin_hall` / 明教: `xiangyang_cheng`）
2. 找到带「门派执事」身份的 NPC：
   - 武当：清风道长 (`wudang_disciple_qing`)
   - 少林：慧明小师弟 (`shaolin_disciple_hui`)
   - 明教：明教护法韦一笑 (`mingjiao_warrior`)
3. 点击 NPC，操作面板出现「接取任务」按钮
4. 点开任务榜（`FactionQuestBoardForm`）→ 浏览任务详情/奖励 → 点「接取任务」
5. 任务进入「任务列表」窗口的「进行中」页签
6. 收集任务的委托人（如 `wudang_town_elder`）NPC 操作面板会出现「查看委托」按钮，可看到该 NPC 当前发布的所有委托

设计上：
- 「接取任务」按钮按 `npc.NpcRole == "quest_giver"` 出现，需配合该 NPC 有 `factionId`
- 「查看委托」按钮按 `FactionQuests.GetIssuedByNpc(id).Count > 0` 出现，与角色标签解耦，新增委托人不需要改 UI

### 山贼讨伐任务三难度

| 难度 | 副本 | 对手数 | 体力 | 时间 | 默认奖励 (金/声望/贡献) |
|---|---|---|---|---|---|
| easy   | bandit_easy   | 2 轮 × 2 | 20 | 2 时辰 | 50 / 30 / 20 |
| medium | bandit_medium | 3 轮 × 2 | 30 | 3 时辰 | 100 / 50 / 35 |
| hard   | bandit_hard   | 3 轮多人 | 40 | 4 时辰 | 200 / 80 / 60 |

调用 `AddBanditQuest("shaolin", "easy")` 即可生成对应任务。
任务详情页提供「处理(自动)」按钮,点击后打开 `DungeonForm` 多轮战斗 UI。

## 副本配置 JSON Schema

`data/dungeons/*.json`:

```json
{
  "id": "bandit_easy",
  "name": "山道剿匪",
  "description": "...",
  "type": "bandit",                  // bandit / huashan_lunjian / story
  "rounds": [
    {
      "opponentCharacterId": "...",  // 固定对手 (与 opponentPool 互斥)
      "opponentPool": ["id1","id2"], // 随机池
      "count": 2,                    // 本轮对手数 (顺序战斗)
      "triggerDialogue": true        // 战胜后是否触发 AI 对话
    }
  ],
  "reward": {
    "gold": 80,
    "reputation": 50,
    "factionContribution": 0,
    "factionId": null
  },
  "onFail": {
    "type": "deductGold",            // deductGold / deductHP / gameOver
    "amount": 30
  },
  "staminaCost": 20,
  "timeCostHours": 2
}
```

副本中的对手通过 `ConfigManager.CreateNPC` 现场实例化, 临时角色加 `IsHidden=true`
不污染场景 NPC 列表 (`data/characters/bandit_*.json` 已配 `"isHidden": true`)。

## 华山论剑触发

- 触发条件: `player.Reputation >= 10000` 且 `state.HuashanInvited == false`
- 触发时机: 玩家点击地图前往任意场景前
- 行为: 弹出"飞鸽传书"邀请, 自动添加 `DungeonQuest("main_huashan_lunjian")` 到任务日志
- 副本配置: `data/dungeons/huashan_lunjian.json` (`type: huashan_lunjian`, `rounds: []` 运行时覆盖, `onFail: gameOver`, `opponentStatMultiplier: 1.2`)

### 终章 10 人车轮战(第十八批)

玩家在任务列表点"进入副本" → `QuestListForm.OnEnterDungeon` 检测 `huashan_lunjian` → 确认框(败北即 Game Over)→ `GameEngine.CreateHuashanRunner()`:

1. **一次性满血满蓝**(`Player.CurrentHP/MP = GetTotalMaxHP/MP`),决战公平起手。
2. **`HuashanLunjianBuilder.Build`** 选 10 位绝顶高手:
   - 候选池:`type=npc` 且 `!isHidden` 且 `JianghuLevel≥40`。
   - 善恶分流:`player.Karma≥50` → 取 `karma<30` 的恶人;否则取 `karma≥50` 的正派。
   - 关系优先:玩家 `Relations` 中非 Stranger 者(师/结拜/夫妻/至交/仇敌/对手)按 `|好感|` 降序先选;不足从剩余候选按阅历降序补足。
   - 排序:阅历升序(弱→强,末位最终Boss),同阅历档随机洗牌。
   - 难度:`CreateNPC` 后按 `opponentStatMultiplier` 放大 HP/MP/攻防。
3. **`DungeonRunner` 扁平模式**:`FlatOpponents` 注入(单轮 10 人),`IsHuashanLunjian=true`,每场胜后记入 `DefeatedOpponents`。**10 场之间不回血**(沿用同 Player 对象 HP 持续);未来制药系统接入战斗嗑药前,纯靠起手满血+护体内力。
4. **胜负**:全胜 → `DungeonForm` 胜利不弹框 → `EndingForm` 流式作传 + 感谢游玩 + 回首页(透传 `ReturnToStart` → `MainForm` → `Program` 循环回 StartForm);任一败北 → `onFail=gameOver` → `DialogResult.Abort` → `Application.Exit`。

### 结束画面(EndingForm)

- `AIService.ChatStreamAsync(system, user, Action<string> onChunk)`:SSE `stream:true` + `ResponseHeadersRead` 逐行读 `data:`,取 `choices[0].delta.content` 喂 `onChunk`;`[DONE]` 结束;content 全空回退 `reasoning_content`;失败静默。
- EndingForm 喂给 AI 的玩家数据:姓名/身世/阅历/善恶描述/声望/门派/天赋/金钱/武学(中文名)/印记标签/江湖天数/重要关系(按类型聚合)/已了结任务数/华山连胜 10 人姓名(末位为最终对手)。
- AI 未配置/失败 → `BuildFallbackSummary` 固定文案(同数据),仍显示感谢游玩。
- BGM:沧海一声笑(结束曲)。

## 月度 Agent 自动管理任务

月度 Agent (`MonthlyUpdateSystem`) 在每月演化时自动管理门派任务池。

### 工具: `manage_faction_quests`

Agent 每月的第 4 步会调用此工具，为各门派添加新任务：

```
actions: [
  { action: "list", factionId: "shaolin" },           // 查看当前任务池
  { action: "add", factionId: "wudang", subType: "collect",
    name: "采购药材", description: "...", itemId: "herb_baizhu",
    quantity: 3, issuerNpcId: "wudang_town_elder" },  // 添加收集任务
  { action: "add", factionId: "mingjiao", subType: "bandit",
    name: "扫荡山贼", dungeonId: "bandit_medium",      // 添加剿匪任务
    difficulty: "medium" },
  { action: "remove", questId: "fq_xxx" }              // 移除旧任务
]
```

### 可用物品/委托人

| 物品 ID | 名称 | 常用委托人 |
|---|---|---|
| healing_pill_small | 小还丹 | shaolin_disciple_hui |
| healing_pill_large | 大还丹 | wudang_disciple_qing |
| herb_baizhu | 白术 | wudang_town_elder |
| fire_ore | 火石矿 | mingjiao_warrior |
| copper_ore / iron_ore / gold_ore | 铜/铁/金矿 | chen_medicine |
| sutra_diamond | 金刚经 | shaolin_disciple_hui |
| good_wine | 好酒 | - |
| mp_pill | 回气丹 | - |

### 初始任务配置

`data/quests/faction/*.json` 中预置了 12 个门派任务（每门派 4 个）：
- 少林：收集小还丹x3、收集金刚经 x2、收集大还丹x3、剿匪(easy/medium)
- 武当：收集白术x5、收集大还丹x2、剿匪(easy/medium)
- 明教：收集火石矿x5、剿匪(medium/hard)

月度 Agent 每月会自动为各门派添加 1-2 个新任务，保持任务池新鲜。

## 装备系统

武器加攻击、防具加防御，战斗中自动生效。

### 数据配置 (`data/items/*.json`)
```json
{
  "id": "xuantie_jian",
  "name": "玄铁剑",
  "type": "Equipment",
  "equipSlot": "weapon",        // weapon / armor
  "weaponType": "sword",         // fist/sword/blade/spear/staff/special (仅描述用)
  "attackBonus": 120,
  "defenseBonus": 0,
  "value": 5000,
  "stackable": false,
  "rarity": "legendary"
}
```

### 装备生效链路
- `CharacterBase.EquippedWeapon/Armor` 装备位；`GetTotalAttack/Defense` 计入装备加成（战斗与所有 UI 都走 GetTotal，零额外改动）
- `EquipItem(Item)` / `UnequipItem(EquipSlot)`：装备从背包取出装槽，旧装备回背包
- NPC 初始装备：`CharacterConfig.equippedWeaponId/armorId`，`CreateNPC` 装载
- 玩家装备：背包弹窗选中装备物品→「装备」按钮
- UI：信息面板攻防显示「基础(总值)」+ 装备行；战斗画面血条旁双方 80×80 头像
- 注：倚天剑保留 Manual 秘籍（藏九阴真经），屠龙刀转装备

## 剧情线概览

7 条金庸剧情线，全部配置驱动（`triggerNpcId` + `prerequisiteQuestIds` 前置门控 + `minFavorabilityToOffer` 好感度门控，`QuestAutoAdvance` 按 talk/fight/go/spar/meditate/mine 推进）：

| 剧情线 | 顶级武功 | 汇聚方式 |
|---|---|---|
| 天龙八部 | 六脉神剑/北冥神功 (mythic) | 段誉/乔峰/虚竹三线汇聚少室山 |
| 射雕英雄传 | 降龙十八掌/九阴真经 | 单主线8步 |
| 神雕侠侣 | 玄铁剑法/黯然销魂掌 | 4链(全真→古墓→剑冢→神雕) |
| 倚天屠龙记 | 九阳神功/乾坤大挪移 | 1主线3支线 |
| 连城诀 | 神照经 (mythic) | 丁典/血刀/汇聚 |
| 碧血剑 | 金蛇剑法 (mythic) | 华山/金蛇/汇聚 |
| 侠客行 | 太玄经 (mythic,全属性+回血+减伤+反震) | 摩天崖/雪山/汇聚 |
| 鹿鼎记 | 化骨绵掌 (legendary,化骨蚀肉流血) | 宫廷/天地会/汇聚(武功层次偏低符合原著) |

## 音频系统

- `AudioManager` (NAudio 单例)：PlayMusic(loop)/StopMusic/PlaySfx/ListMusicFiles
- BGM 场景映射：首页大地图 / 角色创建 / 主界面 / 华山副本 各自 BGM，进出副本自动切换
- 音乐家 NPC (`npcRole=musician`)：琴仙子/笛翁/箫逸人/鼓乐客巡游演奏，`play_music` 专属技能（曲目列表动态取自 ListMusicFiles），演奏可收赏钱（musicFee，收费需玩家确认）
- 设置窗 TabControl：AI / 音效 / 音乐试听 三页签，音量调节

## 角色创建与天赋

`CharacterCreateForm`（新游戏时插入）：
- roll 血/攻/防（仅全部重投，不可单项重投）
- 技艺 roll + 自由分配（总值上限 75，单项无上限）
- 天赋 3 选 1：小虾米来也 / 轻功大师 / 勤学苦练
- 头像 6 选 1
- `Player.TrainingSpeedBonus` / `EffectiveTrainingMultiplier` 影响武功熟练度获取速度

## 年度 AI 剧情生成

- 每 12 个月触发（`AnnualUpdateSystem`），生成 2-3 个大事件剧情任务
- `AnnualAgentTools` 7 工具（剧情编辑器）：create_story_quest / add_quest_step / remove / update / finalize / query_world_elements / list_draft_quests，全流程 NPC/场景/武功/物品 ID 防幻觉校验
- 生成的任务存入 `GameState.RuntimeQuests`（随存档序列化），玩家找对应 NPC 接取
- `StoryDialogueForm`：接任务/步骤完成/领奖励时弹 RPG 对话窗（左头像右台词，点击或空格推进）
- 月度/年度日志改玩家视角（江湖传闻文案，非技术日志）

