namespace WorkshopRoom.Data;

// Small bits of room configuration resolved at startup.
public sealed class RoomConfig
{
    // Where the board scans for workshops, and where a "new workshop" repo is
    // created + cloned. Settable so the operator can point it at the folder
    // their repos live in from the UI (persisted via WorkshopsDirStore) instead
    // of relying on the drive-root fallback.
    public string WorkshopsBaseDir { get; set; } = "";

    // True when WorkshopsBaseDir was fixed by the WORKSHOP_DIR environment
    // variable. The env var wins over the UI, so the picker is shown read-only.
    public bool WorkshopsDirLocked { get; init; }

    // Per-agent launch defaults (MCPs, plugin, model, agent, resume shape),
    // resolved from appsettings + environment at startup. Keyed by agent key
    // (e.g. "agency"). Copilot needs none, so it's absent here.
    public IReadOnlyDictionary<string, AgentLaunchSettings> AgentDefaults { get; init; }
        = new Dictionary<string, AgentLaunchSettings>();

    // The launch settings for an agent, or null when it has none configured.
    public AgentLaunchSettings? SettingsFor(string? agentKey)
        => agentKey is not null && AgentDefaults.TryGetValue(agentKey, out var s) ? s : null;
}
