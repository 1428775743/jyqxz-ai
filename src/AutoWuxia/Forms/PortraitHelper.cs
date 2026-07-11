using System.Drawing.Drawing2D;

namespace AutoWuxia.Forms;

/// <summary>
/// NPC头像加载与渲染工具
/// </summary>
public static class PortraitHelper
{
    private static readonly Dictionary<string, Image?> _cache = new();
    private static readonly string _portraitsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "portraits");

    /// <summary>头像尺寸</summary>
    public const int PortraitSize = 64;

    /// <summary>
    /// 加载NPC头像（带缓存），返回null表示无头像
    /// </summary>
    public static Image? LoadPortrait(string? portraitPath)
    {
        if (string.IsNullOrEmpty(portraitPath)) return null;

        if (_cache.TryGetValue(portraitPath, out var cached))
            return cached;

        var fullPath = Path.IsPathRooted(portraitPath)
            ? portraitPath
            : Path.Combine(_portraitsDir, portraitPath);

        Image? img = null;
        if (File.Exists(fullPath))
        {
            try
            {
                img = Image.FromFile(fullPath);
            }
            catch
            {
                img = null;
            }
        }

        _cache[portraitPath] = img;
        return img;
    }

    /// <summary>
    /// 获取NPC头像（如果有则加载原图，否则生成默认头像）
    /// </summary>
    public static Image GetPortraitOrDefault(string? portraitPath, string npcName, int size = PortraitSize)
    {
        var img = LoadPortrait(portraitPath);
        if (img != null)
            return ResizeImage(img, size, size);
        return GenerateDefaultPortrait(npcName, size);
    }

    /// <summary>
    /// 生成默认头像（圆形背景 + NPC名字首字）
    /// </summary>
    public static Image GenerateDefaultPortrait(string name, int size = PortraitSize)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(40, 40, 55));

        // 圆形背景
        var rect = new Rectangle(2, 2, size - 4, size - 4);
        using var brush = new SolidBrush(Color.FromArgb(70, 70, 95));
        g.FillEllipse(brush, rect);

        // 名字首字
        var initial = string.IsNullOrEmpty(name) ? "?" : name[..1];
        var fontSize = size * 0.45f;
        using var font = new Font("Microsoft YaHei", fontSize, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(255, 220, 150));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(initial, font, textBrush, size / 2f, size / 2f, sf);

        return bmp;
    }

    /// <summary>
    /// 缩放图片
    /// </summary>
    public static Image ResizeImage(Image img, int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage(img, 0, 0, width, height);
        return bmp;
    }

    /// <summary>
    /// 检查NPC是否有自定义头像文件
    /// </summary>
    public static bool HasPortraitFile(string? portraitPath)
    {
        if (string.IsNullOrEmpty(portraitPath)) return false;
        var fullPath = Path.IsPathRooted(portraitPath)
            ? portraitPath
            : Path.Combine(_portraitsDir, portraitPath);
        return File.Exists(fullPath);
    }
}
