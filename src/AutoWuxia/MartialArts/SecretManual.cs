using AutoWuxia.Config.Models;

namespace AutoWuxia.MartialArts;

public class SecretManual
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> ContainedArtIds { get; set; } = new();
    public Dictionary<string, int> LearnRequirements { get; set; } = new();

    public static SecretManual FromConfig(SecretManualConfig config)
    {
        return new SecretManual
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            ContainedArtIds = config.ContainedArtIds,
            LearnRequirements = config.LearnRequirements
        };
    }
}
