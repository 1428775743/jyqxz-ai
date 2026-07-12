using System.Text;
using System.Text.Json;

namespace AutoWuxia.Systems;

/// <summary>从打包的数据中整理主线任务的起始 NPC，用于“全主线”成就详情。</summary>
public static class MainStoryGuide
{
    public static string BuildStartNpcGuide()
    {
        try
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var characterNames = new Dictionary<string, string>();
            var characterDir = Path.Combine(dataDir, "characters");
            foreach (var file in Directory.EnumerateFiles(characterDir, "*.json"))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var id) && root.TryGetProperty("name", out var name))
                    characterNames[id.GetString() ?? ""] = name.GetString() ?? "";
            }

            var rows = new List<(string name, string npc)>();
            var mainDir = Path.Combine(dataDir, "quests", "main");
            foreach (var file in Directory.EnumerateFiles(mainDir, "*.json"))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (!root.TryGetProperty("name", out var nameEl) || !root.TryGetProperty("triggerNpcId", out var npcEl))
                    continue;
                var npcId = npcEl.GetString() ?? "";
                if (string.IsNullOrEmpty(npcId)) continue;
                rows.Add((nameEl.GetString() ?? Path.GetFileNameWithoutExtension(file),
                    characterNames.TryGetValue(npcId, out var npcName) ? npcName : npcId));
            }

            var sb = new StringBuilder("【主线起始人物】\n");
            foreach (var row in rows.OrderBy(r => r.name, StringComparer.CurrentCulture))
                sb.AppendLine($"· {row.name}：找 {row.npc}");
            return sb.ToString();
        }
        catch
        {
            return "主线指引暂时无法读取；请在各城镇与任务人物对话，或查看任务列表。";
        }
    }
}
