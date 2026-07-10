namespace WorkshopRoom.Data;

// Small bits of room configuration resolved at startup.
public sealed class RoomConfig
{
    // Where a "new workshop" repo is created + cloned. Defaults to the drive
    // root so workshops land beside each other, not inside the product repo.
    public string WorkshopsBaseDir { get; init; } = "";

    // Per-agent launch defaults (MCPs, plugin, model, agent, resume shape),
    // resolved from appsettings + environment at startup. Keyed by agent key
    // (e.g. "agency"). Copilot needs none, so it's absent here.
    public IReadOnlyDictionary<string, AgentLaunchSettings> AgentDefaults { get; init; }
        = new Dictionary<string, AgentLaunchSettings>();

    // The launch settings for an agent, or null when it has none configured.
    public AgentLaunchSettings? SettingsFor(string? agentKey)
        => agentKey is not null && AgentDefaults.TryGetValue(agentKey, out var s) ? s : null;
}
