namespace Devil.Server.Controller;

public sealed class DevilServerConfig
{
    public double SpawnChanceDevil { get; set; } = 100;
    public double DevilHarbingerBonusChance { get; set; } = 0;
    public double SpawnChanceCultGang { get; set; } = 100;

    public bool Oni { get; set; } = true;
    public bool Prizrak { get; set; } = true;

    public Dictionary<string, double> CultGangEscortSpawnChance { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["woods"] = 100,
        ["bigmap"] = 100,
        ["shoreline"] = 100
    };

    public Dictionary<string, double> DevilEscortSpawnChance { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["woods"] = 70,
        ["bigmap"] = 50,
        ["shoreline"] = 100,
        ["rezervbase"] = 80,
        ["tarkovstreets"] = 100,
        ["lighthouse"] = 100
    };
}

public sealed class DevilClientSettings
{
    public double SpawnChanceDevil { get; init; }
    public double DevilHarbingerBonusChance { get; init; }
    public double SpawnChanceCultGang { get; init; }
}