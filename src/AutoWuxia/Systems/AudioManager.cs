using AutoWuxia.Config;
using NAudio.Wave;

namespace AutoWuxia.Systems;

/// <summary>
/// 音频管理单例:基于 NAudio 播放/停止 mp3,管理背景音乐与试听。
/// 背景音乐可循环,试听单曲一次。两者共用一个输出设备,试听时暂停背景音乐。
/// </summary>
public sealed class AudioManager : IDisposable
{
    private static readonly Lazy<AudioManager> _instance = new(() => new AudioManager());
    public static AudioManager Instance => _instance.Value;

    private AudioConfig _config = new();
    private WaveOutEvent? _waveOut;
    private Mp3FileReader? _reader;
    private string? _currentFile;
    private bool _loop;
    private bool _manualStop;
    private bool _disposed;

    private AudioManager() { }

    /// <summary>初始化:加载配置并应用音量。在程序启动时调用一次。</summary>
    public void Init(AudioConfig? config = null)
    {
        _config = config ?? AudioConfig.Load();
        EnsureDevice();
        ApplyVolume();
    }

    /// <summary>当前音频配置(运行时可改)。</summary>
    public AudioConfig Config => _config;

    private void EnsureDevice()
    {
        if (_waveOut == null)
        {
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed || _manualStop) return;

        // 自然播放结束:循环则重头继续,否则清理
        if (_loop && _reader != null && _currentFile != null && File.Exists(_currentFile))
        {
            try
            {
                _reader.Position = 0;
                _waveOut?.Play();
            }
            catch { /* 忽略重播失败 */ }
        }
    }

    /// <summary>播放背景音乐(默认循环)。切歌时自动停旧播新。</summary>
    public void PlayMusic(string filePath, bool loop = true)
    {
        if (!_config.MusicEnabled) return;
        if (!File.Exists(filePath)) return;

        EnsureDevice();
        StopInternal();
        _currentFile = filePath;
        _loop = loop;
        _reader = new Mp3FileReader(filePath);
        _waveOut!.Init(_reader);
        ApplyVolume();
        _waveOut.Play();
    }

    /// <summary>停止背景音乐。</summary>
    public void StopMusic() => StopInternal();

    /// <summary>试听指定曲目(单曲,不循环)。会暂停当前背景音乐。</summary>
    public void PlayPreview(string filePath)
    {
        if (!File.Exists(filePath)) return;

        EnsureDevice();
        StopInternal();
        _currentFile = filePath;
        _loop = false;
        _reader = new Mp3FileReader(filePath);
        _waveOut!.Init(_reader);
        ApplyVolume();
        _waveOut.Play();
    }

    /// <summary>停止试听。</summary>
    public void StopPreview() => StopInternal();

    private void StopInternal()
    {
        _manualStop = true;
        try { _waveOut?.Stop(); } catch { }
        _reader?.Dispose();
        _reader = null;
        _manualStop = false;
    }

    /// <summary>设置背景音乐音量(0~100),即时生效。</summary>
    public void SetMusicVolume(int volume0to100)
    {
        _config.MusicVolume = Math.Clamp(volume0to100, 0, 100);
        ApplyVolume();
    }

    /// <summary>开关背景音乐:关闭时停止当前播放。</summary>
    public void SetMusicEnabled(bool enabled)
    {
        _config.MusicEnabled = enabled;
        if (!enabled) StopInternal();
    }

    private void ApplyVolume()
    {
        if (_waveOut != null)
            _waveOut.Volume = (_config.MusicEnabled ? _config.MusicVolume : 0) / 100f;
    }

    /// <summary>是否正在播放。</summary>
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>列出 assets/music 目录下所有 mp3 全路径(目录不存在返回空)。</summary>
    public static List<string> ListMusicFiles()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music");
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.EnumerateFiles(dir, "*.mp3", SearchOption.TopDirectoryOnly)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    public void Dispose()
    {
        _disposed = true;
        StopInternal();
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }
    }
}
