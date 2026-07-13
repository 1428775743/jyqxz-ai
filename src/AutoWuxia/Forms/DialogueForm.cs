using AutoWuxia.Characters;
using AutoWuxia.Core;
using AutoWuxia.Systems;

namespace AutoWuxia.Forms;

/// <summary>
/// NPC行为回调参数
/// </summary>
public class NPCActionEventArgs : EventArgs
{
    public string Action { get; set; } = "none";
    public string? ActionTarget { get; set; }
    /// <summary>乐师演奏收取的赏钱(玩家付给NPC),仅 play_music 时有效。</summary>
    public int MusicFee { get; set; }
    /// <summary>药师炼药收取的工费(玩家付给NPC),仅 craft_medicine 时有效。</summary>
    public int CraftFee { get; set; }
}

/// <summary>
/// NPC相关链式任务摘要(供 DialogueForm 在聊天流中呈现)。
/// BlockedReason 为空时可接取；否则展示明确的解锁线索。
/// </summary>
public record ChainQuestOffer(string Id, string Name, string Description, string? BlockedReason = null)
{
    public bool CanAccept => string.IsNullOrEmpty(BlockedReason);
}

public class DialogueForm : Form
{
    private readonly NPC _npc;
    private readonly Player _player;
    private readonly DialogueSystem _dialogueSystem;
    private readonly GameTime _gameTime;

    private RichTextBox _chatBox = null!;
    private TextBox _inputBox = null!;
    private Button _sendButton = null!;
    private Button _endButton = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;

    private Label _thinkingIndicator = null!;

    private bool _dialogueActive;

    /// <summary>
    /// NPC触发行为时的回调（spar/attack/teach_art等）
    /// </summary>
    public event EventHandler<NPCActionEventArgs>? NPCActionTriggered;

    /// <summary>
    /// 查询该 NPC 当前相关的链式任务(MainForm 注入)，包含可接任务与解锁线索。
    /// 返回空列表表示没有相关任务。
    /// </summary>
    public Func<List<ChainQuestOffer>>? QueryOfferableQuests { get; set; }

    /// <summary>玩家确认接取任务时回调(MainForm 注入),参数为 questId。</summary>
    public Action<string>? AcceptQuest { get; set; }

    public DialogueForm(NPC npc, Player player, DialogueSystem dialogueSystem, GameTime gameTime, DialogueHistory history)
    {
        _npc = npc;
        _player = player;
        _dialogueSystem = dialogueSystem;
        _gameTime = gameTime;

        InitializeComponent();
        LoadHistory(history);
    }

    private void InitializeComponent()
    {
        Text = $"与 {_npc.Name} 对话";
        Size = new Size(650, 550);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(25, 25, 35);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // NPC头像
        var portraitBox = new PictureBox
        {
            Location = new Point(10, 8),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.StretchImage
        };
        portraitBox.Image = PortraitHelper.GetPortraitOrDefault(_npc.PortraitPath, _npc.Name, 48);

        // 标题栏
        _titleLabel = new Label
        {
            Text = $"【{_npc.Name}】{GetRelationText()}",
            Location = new Point(65, 8),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(12f, FontStyle.Bold)
        };

        _statusLabel = new Label
        {
            Text = $"体力:{_player.Stamina:F0}/{_player.MaxStamina:F0} | 每条消息消耗体力{DialogueSystem.DialogueStaminaCost:F0} + 时间{DialogueSystem.DialogueTimeCost:F1}时辰",
            Location = new Point(65, 35),
            AutoSize = true,
            ForeColor = Color.FromArgb(150, 150, 170),
            Font = WuxiaTheme.UiFont(8.5f)
        };

        // 聊天记录
        _chatBox = new RichTextBox
        {
            Location = new Point(10, 60),
            Size = new Size(615, 340),
            ReadOnly = true,
            BackColor = Color.FromArgb(20, 20, 30),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(10f),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        // 输入框
        _inputBox = new TextBox
        {
            Location = new Point(10, 412),
            Size = new Size(470, 28),
            MaxLength = 500,
            BackColor = Color.FromArgb(30, 30, 45),
            ForeColor = Color.FromArgb(220, 220, 200),
            Font = WuxiaTheme.UiFont(10f),
            BorderStyle = BorderStyle.FixedSingle
        };
        _inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = SendAsync();
            }
        };

        // 发送按钮
        _sendButton = MakeButton("发送", new Point(490, 410), new Size(65, 30),
            Color.FromArgb(60, 100, 60));
        _sendButton.Click += async (s, e) => await SendAsync();

        // 思考指示器（默认隐藏，居中显示在聊天区）
        _thinkingIndicator = new Label
        {
            Text = "",
            Location = new Point(10 + (615 - 200) / 2, 60 + (340 - 40) / 2),
            Size = new Size(200, 40),
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.FromArgb(255, 220, 150),
            Font = WuxiaTheme.UiFont(11f),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        _thinkingIndicator.BringToFront();

        // 结束对话按钮
        _endButton = MakeButton("告辞", new Point(560, 410), new Size(65, 30),
            Color.FromArgb(100, 60, 60));
        _endButton.Click += (s, e) =>
        {
            AppendSystem("你拱手告辞。");
            _dialogueActive = false;
            Close();
        };

        // 字数提示
        var charCountLabel = new Label
        {
            Text = "0/500",
            Location = new Point(10, 445),
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 120),
            Font = WuxiaTheme.UiFont(8f)
        };
        _inputBox.TextChanged += (s, e) =>
            charCountLabel.Text = $"{_inputBox.Text.Length}/500";

        Controls.AddRange(new Control[]
        {
            portraitBox, _titleLabel, _statusLabel, _chatBox, _thinkingIndicator, _inputBox, _sendButton, _endButton, charCountLabel
        });

        WuxiaTheme.ApplyScaling(this);  // 应用界面缩放
    }

    private void LoadHistory(DialogueHistory history)
    {
        if (history.TotalMessages > 0)
        {
            AppendSystem($"─── 历史对话（共{history.TotalMessages}条）───");
            foreach (var record in history.Records.TakeLast(20))
            {
                AppendMessage(record.GameTimeDisplay, record.SpeakerName,
                    record.Content, record.SpeakerId == _player.Id);
            }
            AppendSystem("───────────────");
        }
    }

    public void ShowOpeningLine(DialogueResponse response)
    {
        _dialogueActive = response.WillingToTalk;

        if (response.WillingToTalk)
        {
            // 显示NPC思考
            if (!string.IsNullOrEmpty(response.Thinking))
                AppendThinking(response.Thinking);

            AppendNPCMessage(response.OpeningLine);
            var history = _dialogueSystem.GetHistory(_npc.Id);
            history.AddRecord(_npc.Id, _npc.Name, response.OpeningLine, _gameTime.Display);

            // 开场白后:若该 NPC 有可委派的链式任务,在聊天流中呈现 + 武侠风确认框接取
            TryOfferQuestInDialogue();
        }
        else
        {
            // 显示NPC思考
            if (!string.IsNullOrEmpty(response.Thinking))
                AppendThinking(response.Thinking);

            AppendSystem(response.OpeningLine);
            AppendSystem($"（{_npc.Name}不愿与你对话）");
            _sendButton.Enabled = false;
            _inputBox.Enabled = false;
        }
    }

    /// <summary>
    /// 在聊天流中呈现该 NPC 可委派的链式任务(若有),并以武侠风确认框接取。
    /// 取代原"对话关闭后 MainForm 弹系统框接取"的流程。
    /// </summary>
    private void TryOfferQuestInDialogue()
    {
        if (QueryOfferableQuests == null) return;
        var offers = QueryOfferableQuests();
        if (offers.Count == 0) return;

        foreach (var blocked in offers.Where(o => !o.CanAccept))
            AppendSystem($"【任务线索·{blocked.Name}】{blocked.BlockedReason}");

        var offer = offers.FirstOrDefault(o => o.CanAccept);
        if (offer == null) return;
        AppendSystem($"【{_npc.Name}似乎有事相托】{offer.Description}");

        if (WuxiaConfirmBox.Show(this, "任务委托",
            $"【{offer.Name}】\n\n{offer.Description}\n\n是否接受该任务?",
            "接受", "婉拒", WuxiaConfirmStyle.Neutral))
        {
            AcceptQuest?.Invoke(offer.Id);
            AppendSystem($"你接受了任务：【{offer.Name}】");
        }
        else
        {
            AppendSystem("你婉言谢绝了委托。");
        }
    }

    private async Task SendAsync()
    {
        var message = _inputBox.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;
        if (!_dialogueActive) return;

        // 检查体力
        if (!_dialogueSystem.CanContinueDialogue(_player))
        {
            AppendWarning("体力不足，无法继续对话。");
            return;
        }

        _sendButton.Enabled = false;
        _inputBox.Enabled = false;
        _inputBox.Clear();

        // 消耗体力和时间
        _dialogueSystem.ConsumeDialogueCost(_player, _gameTime);
        UpdateStatus();

        // 显示玩家消息
        AppendPlayerMessage(message);

        // 显示思考中指示器
        ShowThinkingIndicator($"{_npc.Name}正在思考...");

        DialogueReply reply;
        try
        {
            reply = await _dialogueSystem.SendMessage(_npc, _player, message, _gameTime);
        }
        finally
        {
            HideThinkingIndicator();
        }

        // 显示NPC思考内容（灰色小字）
        if (!string.IsNullOrEmpty(reply.Thinking))
            AppendThinking(reply.Thinking);

        // 显示NPC对话
        AppendNPCMessage(reply.Reply);

        // 显示好感度变化
        if (reply.FavorChange != 0)
        {
            var favorText = reply.FavorChange > 0 ? $"+{reply.FavorChange}" : $"{reply.FavorChange}";
            AppendSystem($"（好感度{favorText}）");
            // 同步刷新标题上的关系/好感度（之前一直显示0是因为标题文本只在构造时设置过）
            _titleLabel.Text = $"【{_npc.Name}】{GetRelationText()}";
        }

        // 处理NPC行为
        if (reply.Action != "none")
        {
            HandleNPCAction(reply);
        }

        // NPC想结束对话
        if (reply.WantsToEnd || reply.Action == "end_dialogue")
        {
            _dialogueActive = false;
            AppendSystem($"（{_npc.Name}{reply.EndReason ?? "不想继续对话了"}）");
            _sendButton.Enabled = false;
            _inputBox.Enabled = false;
        }
        else
        {
            _sendButton.Enabled = true;
            _inputBox.Enabled = true;
            _inputBox.Focus();
        }
    }

    /// <summary>
    /// 处理NPC触发的行为
    /// </summary>
    private void HandleNPCAction(DialogueReply reply)
    {
        switch (reply.Action)
        {
            case "spar":
                AppendSystem($"（{_npc.Name}提出切磋！）");
                if (WuxiaConfirmBox.Show(this, "切磋邀约",
                    $"{_npc.Name}想与你切磋武艺,是否接受?",
                    "接受", "婉拒", WuxiaConfirmStyle.Neutral))
                {
                    AppendSystem("你点头应战。");
                    NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                    {
                        Action = "spar",
                        ActionTarget = reply.ActionTarget
                    });
                }
                else
                {
                    AppendSystem("你婉言谢绝了切磋。");
                }
                break;

            case "attack":
                WuxiaConfirmBox.Alert(this, "战斗", $"{_npc.Name}怒而出手！", WuxiaConfirmStyle.Danger);
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "attack",
                    ActionTarget = reply.ActionTarget
                });
                break;

            case "swear_brotherhood":
                if (WuxiaConfirmBox.Show(this, "结拜", $"{_npc.Name}有意与你义结金兰,是否应允?", "应允", "婉拒", WuxiaConfirmStyle.Neutral))
                {
                    AppendSystem($"你与{_npc.Name}义结金兰!");
                    NPCActionTriggered?.Invoke(this, new NPCActionEventArgs { Action = "swear_brotherhood" });
                }
                else AppendSystem($"你婉拒了{_npc.Name}的结拜之意。");
                break;

            case "marry":
                if (!RelationshipSystem.CanBecomeSpouses(_player, _npc, out var marryReason))
                {
                    AppendWarning($"（暂时无法与{_npc.Name}结为配偶：{marryReason}。）");
                    break;
                }
                if (WuxiaConfirmBox.Show(this, "求亲", $"{_npc.Name}向你吐露心意,愿结连理,是否接受?", "接受", "婉拒", WuxiaConfirmStyle.Success))
                {
                    AppendSystem($"你与{_npc.Name}喜结连理!");
                    NPCActionTriggered?.Invoke(this, new NPCActionEventArgs { Action = "marry" });
                }
                else AppendSystem($"你婉拒了{_npc.Name}的心意。");
                break;

            case "take_disciple":
                if (WuxiaConfirmBox.Show(this, "收徒", $"{_npc.Name}有意收你为徒,是否拜师?", "拜师", "婉拒", WuxiaConfirmStyle.Success))
                {
                    AppendSystem($"你正式拜{_npc.Name}为师!");
                    NPCActionTriggered?.Invoke(this, new NPCActionEventArgs { Action = "take_disciple" });
                }
                else AppendSystem($"你婉拒了拜师之意。");
                break;


            case "teach_art":
                AppendSystem($"（{_npc.Name}似乎想传授你武功...）");
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "teach_art",
                    ActionTarget = reply.ActionTarget
                });
                break;

            case "play_music":
            {
                // 乐师演奏:免费直接演奏;收费需玩家同意(不同意则不演奏、不扣款)
                if (string.IsNullOrEmpty(reply.ActionTarget)) break;
                int fee = reply.MusicFee;
                if (fee > 0)
                {
                    if (!WuxiaConfirmBox.Show(this, "乐师赏钱",
                            $"{_npc.Name}索要{fee}两赏钱方肯抚琴，是否支付？",
                            "支付", "婉拒", WuxiaConfirmStyle.Neutral))
                    {
                        AppendSystem($"（你婉拒了赏钱，{_npc.Name}收琴未奏）");
                        break;
                    }
                    AppendSystem($"（你支付了{fee}两赏钱）");
                }
                // 免费 或 已同意付费:触发事件由 MainForm 实际播放并扣款
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "play_music",
                    ActionTarget = reply.ActionTarget,
                    MusicFee = fee
                });
                break;
            }

            case "craft_medicine":
            {
                // 药师炼药:玩家需同意支付工费(不同意则不炼、不扣款)。材料校验由 MainForm 执行。
                if (string.IsNullOrEmpty(reply.ActionTarget)) break;
                int fee = reply.CraftFee;
                if (fee > 0)
                {
                    if (!WuxiaConfirmBox.Show(this, "药师工费",
                            $"{_npc.Name}愿为你炼药，索要{fee}两工费，是否支付？",
                            "支付", "婉拒", WuxiaConfirmStyle.Neutral))
                    {
                        AppendSystem($"（你婉拒了工费，{_npc.Name}收起药炉）");
                        break;
                    }
                    AppendSystem($"（你支付了{fee}两工费）");
                }
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "craft_medicine",
                    ActionTarget = reply.ActionTarget,
                    CraftFee = fee
                });
                break;
            }

            case "craft_food":
            {
                // 厨师做菜:玩家需同意支付工费(不同意则不做、不扣款)。食材校验由 MainForm 执行。
                if (string.IsNullOrEmpty(reply.ActionTarget)) break;
                int fee = reply.CraftFee;
                if (fee > 0)
                {
                    if (!WuxiaConfirmBox.Show(this, "厨师工费",
                            $"{_npc.Name}愿为你下厨，索要{fee}两工费，是否支付？",
                            "支付", "婉拒", WuxiaConfirmStyle.Neutral))
                    {
                        AppendSystem($"（你婉拒了工费，{_npc.Name}收起炊具）");
                        break;
                    }
                    AppendSystem($"（你支付了{fee}两工费）");
                }
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "craft_food",
                    ActionTarget = reply.ActionTarget,
                    CraftFee = fee
                });
                break;
            }

            case "craft_forge":
            {
                // 铁匠打造:玩家需同意支付工费(不同意则不打、不扣款)。材料校验由 MainForm 执行。
                if (string.IsNullOrEmpty(reply.ActionTarget)) break;
                int fee = reply.CraftFee;
                if (fee > 0)
                {
                    if (!WuxiaConfirmBox.Show(this, "铁匠工费",
                            $"{_npc.Name}愿为你打造，索要{fee}两工费，是否支付？",
                            "支付", "婉拒", WuxiaConfirmStyle.Neutral))
                    {
                        AppendSystem($"（你婉拒了工费，{_npc.Name}收起铁锤）");
                        break;
                    }
                    AppendSystem($"（你支付了{fee}两工费）");
                }
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "craft_forge",
                    ActionTarget = reply.ActionTarget,
                    CraftFee = fee
                });
                break;
            }

            case "give_item":
                // 赠予消息已由 DialogueSystem 拼入 reply.Reply(含正确中文名)。
                // 仅当玩家实际未收到物品(转移失败)时,补一条用配置名解析的提示;
                // 若物品ID无效(幻觉),ResolveItemName 返回 null,不显示原始ID。
                if (!string.IsNullOrEmpty(reply.ActionTarget)
                    && _player.Inventory.GetItem(reply.ActionTarget) == null)
                {
                    var itemName = _dialogueSystem.ResolveItemName(reply.ActionTarget);
                    if (!string.IsNullOrEmpty(itemName))
                        AppendSystem($"（{_npc.Name}赠予了你: {itemName}）");
                }
                break;

            case "ask_item":
                AppendSystem($"（{_npc.Name}向你索要物品。）");
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "ask_item",
                    ActionTarget = reply.ActionTarget
                });
                break;

            case "heal":
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "heal",
                    ActionTarget = reply.ActionTarget
                });
                break;

            case "castrate":
                AppendSystem($"（{_npc.Name}取出净身器具，示意你三思。）");
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "castrate"
                });
                break;

            case "query_location":
                NPCActionTriggered?.Invoke(this, new NPCActionEventArgs
                {
                    Action = "query_location",
                    ActionTarget = reply.ActionTarget
                });
                break;
        }
    }

    private void AppendMessage(string time, string speaker, string content, bool isPlayer)
    {
        var color = isPlayer ? Color.FromArgb(100, 200, 100) : Color.FromArgb(100, 180, 255);
        AppendColoredText($"[{time}] ", Color.FromArgb(120, 120, 140));
        AppendColoredText($"{speaker}: ", color);
        AppendColoredText($"{content}\n", Color.FromArgb(220, 220, 200));
    }

    private void AppendPlayerMessage(string message)
    {
        AppendMessage(_gameTime.Display, _player.Name, message, true);
    }

    private void AppendNPCMessage(string message)
    {
        AppendMessage(_gameTime.Display, _npc.Name, message, false);
    }

    /// <summary>
    /// 显示NPC的内心独白（暗色斜体小字）
    /// </summary>
    private void AppendThinking(string thinking)
    {
        if (string.IsNullOrEmpty(thinking)) return;
        _chatBox.SelectionStart = _chatBox.TextLength;
        _chatBox.SelectionLength = 0;
        _chatBox.SelectionColor = Color.FromArgb(130, 120, 100);
        _chatBox.SelectionFont = WuxiaTheme.UiFont(8.5f, FontStyle.Italic);
        _chatBox.AppendText($"（心想：{thinking}）\n");
        _chatBox.ScrollToCaret();
    }

    private void AppendSystem(string text)
    {
        AppendColoredText($"{text}\n", Color.FromArgb(180, 180, 100));
    }

    private void AppendWarning(string text)
    {
        AppendColoredText($"{text}\n", Color.FromArgb(255, 200, 100));
    }

    private void AppendColoredText(string text, Color color)
    {
        _chatBox.SelectionStart = _chatBox.TextLength;
        _chatBox.SelectionLength = 0;
        _chatBox.SelectionColor = color;
        _chatBox.AppendText(text);
        _chatBox.ScrollToCaret();
    }

    private void ShowThinkingIndicator(string text)
    {
        _thinkingIndicator.Text = text;
        _thinkingIndicator.Visible = true;
        _thinkingIndicator.Refresh();
    }

    private void HideThinkingIndicator()
    {
        _thinkingIndicator.Visible = false;
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = $"体力:{_player.Stamina:F0}/{_player.MaxStamina:F0} | 每条消息消耗体力{DialogueSystem.DialogueStaminaCost:F0} + 时间{DialogueSystem.DialogueTimeCost:F1}时辰";
    }

    private string GetRelationText()
    {
        var rel = _npc.GetRelation(_player.Id);
        return $"[{rel.GetRelationDescription()} 好感度{rel.Favorability}]";
    }

    private Button MakeButton(string text, Point loc, Size size, Color bgColor)
    {
        return new Button
        {
            Text = text, Location = loc, Size = size,
            BackColor = bgColor,
            ForeColor = Color.FromArgb(220, 220, 200),
            FlatStyle = FlatStyle.Flat,
            Font = WuxiaTheme.UiFont(9f),
            Cursor = Cursors.Hand
        };
    }
}
