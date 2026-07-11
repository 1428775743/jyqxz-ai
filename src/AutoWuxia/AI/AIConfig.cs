namespace AutoWuxia.AI;

public class AIConfig
{
    public string ApiEndpoint { get; set; } = "https://api.deepseek.com";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "deepseek-v4-flash";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// 月度 Agent 使用的模型（需支持 function calling）
    /// </summary>
    public string MonthlyModel { get; set; } = "deepseek-chat";

    /// <summary>
    /// Agent 最大循环次数
    /// </summary>
    public int MonthlyMaxIterations { get; set; } = 100;

    private static readonly string ConfigPath = Path.Combine(
        Core.AppPaths.UserDataDir, "ai_config.json");

    public void Save()
    {
        Directory.CreateDirectory(Core.AppPaths.UserDataDir);
        var json = System.Text.Json.JsonSerializer.Serialize(this,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static AIConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            return System.Text.Json.JsonSerializer.Deserialize<AIConfig>(json) ?? new AIConfig();
        }
        return new AIConfig();
    }
}
