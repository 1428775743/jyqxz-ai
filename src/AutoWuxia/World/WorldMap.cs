using AutoWuxia.Config.Models;

namespace AutoWuxia.World;

public class WorldMap
{
    public Dictionary<string, Location> Locations { get; set; } = new();
    public Dictionary<string, Scene> Scenes { get; set; } = new();
    public List<Route> Routes { get; set; } = new();

    /// <summary>
    /// 获取两个场景之间的路线（保留兼容，用于本地场景导航）
    /// </summary>
    public Route? GetRoute(string from, string to)
    {
        return Routes.FirstOrDefault(r =>
            (r.From == from && r.To == to) || (r.From == to && r.To == from));
    }

    /// <summary>
    /// 获取两个城镇之间的路线
    /// </summary>
    public Route? GetRouteBetweenLocations(string fromLocId, string toLocId)
    {
        return Routes.FirstOrDefault(r =>
            (r.From == fromLocId && r.To == toLocId) || (r.From == toLocId && r.To == fromLocId));
    }

    /// <summary>
    /// 获取当前场景的本地连接场景列表
    /// </summary>
    public List<Scene> GetConnectedScenes(string sceneId)
    {
        if (!Scenes.TryGetValue(sceneId, out var scene)) return new List<Scene>();
        return scene.ConnectedSceneIds
            .Where(id => Scenes.ContainsKey(id))
            .Select(id => Scenes[id])
            .ToList();
    }

    /// <summary>
    /// 根据场景ID获取所属城镇
    /// </summary>
    public Location? GetLocationByScene(string sceneId)
    {
        return Locations.Values.FirstOrDefault(l => l.SceneIds.Contains(sceneId));
    }

    /// <summary>
    /// 计算城镇间旅行费用（银两）= distance * 0.5
    /// </summary>
    public int GetTravelCost(string fromLocationId, string toLocationId)
    {
        var route = GetRouteBetweenLocations(fromLocationId, toLocationId);
        if (route == null) return 0;
        return (int)(route.Distance * 0.5);
    }

    /// <summary>
    /// 获取城镇间旅行时间（时辰）
    /// </summary>
    public double GetTravelTime(string fromLocationId, string toLocationId)
    {
        var route = GetRouteBetweenLocations(fromLocationId, toLocationId);
        return route?.TravelTime ?? 0;
    }

    /// <summary>
    /// 获取与某城镇直接相连的所有城镇列表
    /// </summary>
    public List<(Location Loc, Route Route)> GetConnectedLocations(string locationId)
    {
        var result = new List<(Location, Route)>();
        foreach (var route in Routes)
        {
            string? targetId = null;
            if (route.From == locationId) targetId = route.To;
            else if (route.To == locationId) targetId = route.From;
            if (targetId != null && Locations.TryGetValue(targetId, out var loc))
                result.Add((loc, route));
        }
        return result;
    }

    /// <summary>
    /// Dijkstra最短路径算法，返回路径信息（总距离、总时间、途经城镇列表）
    /// </summary>
    public (double TotalDistance, double TotalTime, List<string> Path)? FindShortestPath(string fromLocId, string toLocId)
    {
        if (!Locations.ContainsKey(fromLocId) || !Locations.ContainsKey(toLocId))
            return null;
        if (fromLocId == toLocId)
            return (0, 0, new List<string> { fromLocId });

        var dist = new Dictionary<string, double>();
        var prev = new Dictionary<string, string?>();
        var visited = new HashSet<string>();
        var pq = new SortedSet<(double Dist, string Id)>();

        foreach (var locId in Locations.Keys)
        {
            dist[locId] = double.MaxValue;
            prev[locId] = null;
        }
        dist[fromLocId] = 0;
        pq.Add((0, fromLocId));

        while (pq.Count > 0)
        {
            var (curDist, curId) = pq.Min;
            pq.Remove(pq.Min);

            if (visited.Contains(curId)) continue;
            visited.Add(curId);

            if (curId == toLocId) break;

            foreach (var (neighbor, route) in GetConnectedLocations(curId))
            {
                if (visited.Contains(neighbor.Id)) continue;
                double newDist = curDist + route.Distance;
                if (newDist < dist[neighbor.Id])
                {
                    pq.Remove((dist[neighbor.Id], neighbor.Id));
                    dist[neighbor.Id] = newDist;
                    prev[neighbor.Id] = curId;
                    pq.Add((newDist, neighbor.Id));
                }
            }
        }

        if (dist[toLocId] == double.MaxValue)
            return null; // 不可达

        // 重建路径
        var path = new List<string>();
        string? current = toLocId;
        while (current != null)
        {
            path.Insert(0, current);
            prev.TryGetValue(current, out current);
        }

        // 计算总时间（每段路线时间累加）
        double totalTime = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var segRoute = GetRouteBetweenLocations(path[i], path[i + 1]);
            if (segRoute != null) totalTime += segRoute.TravelTime;
        }

        return (dist[toLocId], totalTime, path);
    }

    public static WorldMap FromConfig(WorldMapConfig config, Dictionary<string, Scene> scenes)
    {
        var map = new WorldMap { Scenes = scenes };
        foreach (var loc in config.Locations)
            map.Locations[loc.Id] = Location.FromConfig(loc);
        foreach (var route in config.Routes)
            map.Routes.Add(Route.FromConfig(route));
        return map;
    }
}
