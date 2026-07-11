using AutoWuxia.Config.Models;

namespace AutoWuxia.World;

public class Location
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public List<string> SceneIds { get; set; } = new();
    public int MapX { get; set; }
    public int MapY { get; set; }

    public static Location FromConfig(LocationConfig config)
    {
        return new Location
        {
            Id = config.Id,
            Name = config.Name,
            Region = config.Region,
            SceneIds = config.SceneIds,
            MapX = config.MapX,
            MapY = config.MapY
        };
    }
}

public class Route
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public double Distance { get; set; }
    public double TravelTime { get; set; }

    public static Route FromConfig(RouteConfig config)
    {
        return new Route
        {
            From = config.From,
            To = config.To,
            Distance = config.Distance,
            TravelTime = config.TravelTime
        };
    }
}
