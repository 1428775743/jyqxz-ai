namespace AutoWuxia.Forms;

/// <summary>
/// 加载项目图片且不长期锁定源文件。相对路径以程序输出目录为基准。
/// </summary>
internal static class ImageAssetLoader
{
    public static Image? Load(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        foreach (var path in GetCandidates(relativePath))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                using var stream = File.OpenRead(path);
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
            catch (Exception ex)
            {
                Core.GameLogger.Warn($"图片加载失败: {path} ({ex.Message})");
            }
        }

        Core.GameLogger.Warn($"找不到图片资源: {relativePath}");
        return null;
    }

    private static IEnumerable<string> GetCandidates(string path)
    {
        if (Path.IsPathRooted(path))
        {
            yield return path;
            yield break;
        }

        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        yield return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, normalized));
        yield return Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", normalized));
    }
}
