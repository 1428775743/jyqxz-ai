namespace AutoWuxia.Config;

/// <summary>
/// 界面显示配置(缩放倍率),持久化到 %AppData%/AutoWuxia/display_config.json。
/// </summary>
public class DisplayConfig
{
    /// <summary>界面缩放倍率(1.0=100%)。范围 0.75~3.0。</summary>
    public double ScaleFactor { get; set; } = 1.0;

    private static readonly string ConfigPath = Path.Combine(
        Core.AppPaths.UserDataDir, "display_config.json");

    public void Save()
    {
        Directory.CreateDirectory(Core.AppPaths.UserDataDir);
        var json = System.Text.Json.JsonSerializer.Serialize(this,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static DisplayConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = System.Text.Json.JsonSerializer.Deserialize<DisplayConfig>(json);
                if (cfg != null && cfg.ScaleFactor is >= 0.75 and <= 3.0)
                    return cfg;
            }
            catch { }
        }
        return new DisplayConfig();
    }
}
