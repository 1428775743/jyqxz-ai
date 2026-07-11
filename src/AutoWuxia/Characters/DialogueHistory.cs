namespace AutoWuxia.Characters;

public class DialogueRecord
{
    public string SpeakerId { get; set; } = "";
    public string SpeakerName { get; set; } = "";
    public string Content { get; set; } = "";
    public string GameTimeDisplay { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class DialogueHistory
{
    public string NPCId { get; set; } = "";
    public List<DialogueRecord> Records { get; set; } = new();

    public void AddRecord(string speakerId, string speakerName, string content, string gameTimeDisplay)
    {
        Records.Add(new DialogueRecord
        {
            SpeakerId = speakerId,
            SpeakerName = speakerName,
            Content = content,
            GameTimeDisplay = gameTimeDisplay,
            Timestamp = DateTime.Now
        });
    }

    public string GetRecentContext(int count = 10)
    {
        var recent = Records.TakeLast(count);
        return string.Join("\n", recent.Select(r => $"[{r.GameTimeDisplay}] {r.SpeakerName}: {r.Content}"));
    }

    public int TotalMessages => Records.Count;
}

public class DialogueResponse
{
    public bool WillingToTalk { get; set; } = true;
    public string OpeningLine { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Thinking { get; set; } = "";
    public bool WantsToEnd { get; set; }
    public string? EndReason { get; set; }
}
