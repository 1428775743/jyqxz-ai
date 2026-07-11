namespace AutoWuxia.Config;

/// <summary>
/// 音频配置:背景音乐开关/音量、音效音量(预留)。
/// 持久化到 %AppData%\AutoWuxia\audio_config.json,仿 <see cref="AI.AIConfig"/> 模式。
/// </summary>
public class AudioConfig
{
    /// <summary>是否启用背景音乐</summary>
    public bool MusicEnabled { get; set; } = true;

    /// <summary>背景音乐音量 0~100</summary>
    public int MusicVolume { get; set; } = 50;

    /// <summary>音效音量 0~100(预留,当前无音效资源)</summary>
    public int SoundVolume { get; set; } = 70;

    private static readonly string ConfigPath = Path.Combine(
        Core.AppPaths.UserDataDir, "audio_config.json");

    public void Save()
    {
        Directory.CreateDirectory(Core.AppPaths.UserDataDir);
        var json = System.Text.Json.JsonSerializer.Serialize(this,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static AudioConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            return System.Text.Json.JsonSerializer.Deserialize<AudioConfig>(json) ?? new AudioConfig();
        }
        return new AudioConfig();
    }
}
