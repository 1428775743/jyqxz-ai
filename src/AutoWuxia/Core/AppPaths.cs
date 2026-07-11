namespace AutoWuxia.Core;

/// <summary>
/// 用户数据目录统一管理。
/// 所有运行时生成的用户数据(AI/音频配置、存档)统一存放于 %AppData%\AutoWuxia,
/// 与 exe 所在目录解耦——避免安装到 Program Files 等只读目录时写入失败,
/// 也方便整体备份/迁移用户数据。
/// 路径属性仅为字符串计算,不创建目录;写操作处自行 Directory.CreateDirectory。
/// </summary>
public static class AppPaths
{
    /// <summary>用户数据根目录: %AppData%\AutoWuxia</summary>
    public static string UserDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoWuxia");

    /// <summary>存档目录: %AppData%\AutoWuxia\saves</summary>
    public static string SavesDir => Path.Combine(UserDataDir, "saves");

    /// <summary>日志目录: %AppData%\AutoWuxia\logs</summary>
    public static string LogsDir => Path.Combine(UserDataDir, "logs");
}
