using AutoWuxia.Characters;

namespace AutoWuxia.World;

public class NPCLocationManager
{
    private Dictionary<string, Dictionary<string, string>> _dailySchedule = new();

    public void ScheduleNPCsForDay(Dictionary<string, NPC> allNPCs, string timePeriod)
    {
        _dailySchedule.Clear();
        foreach (var (npcId, npc) in allNPCs)
        {
            if (!npc.IsAlive) continue;
            var sceneId = npc.GetCurrentSceneByTime(timePeriod);
            if (!_dailySchedule.ContainsKey(sceneId))
                _dailySchedule[sceneId] = new Dictionary<string, string>();
            _dailySchedule[sceneId][npcId] = timePeriod;
        }
    }

    public List<NPC> GetNPCsInScene(string sceneId, Dictionary<string, NPC> allNPCs, string timePeriod)
    {
        var result = new List<NPC>();
        foreach (var npc in allNPCs.Values)
        {
            if (!npc.IsAlive) continue;
            if (npc.IsHidden) continue;
            var targetScene = npc.GetCurrentSceneByTime(timePeriod);
            if (targetScene == sceneId)
                result.Add(npc);
        }
        return result;
    }

    public void UpdateAllSceneNPCs(Dictionary<string, Scene> scenes, Dictionary<string, NPC> allNPCs, string timePeriod)
    {
        foreach (var scene in scenes.Values)
        {
            scene.PresentNPCs = GetNPCsInScene(scene.Id, allNPCs, timePeriod);
        }
    }
}
