using WorkshopRoom.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The Copilot session-state root and the workshops base dir default to the
// real locations, but both can be pointed elsewhere via environment variables
// (WORKSHOP_SESSION_ROOT / WORKSHOP_DIR) — used to run an isolated demo
// instance with throwaway, fake-named data (e.g. for docs screenshots).
var sessionRoot = Environment.GetEnvironmentVariable("WORKSHOP_SESSION_ROOT") is { Length: > 0 } envRoot
    ? envRoot
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "session-state");
var usageCache = Path.Combine(builder.Environment.ContentRootPath, "usage-cache.json");
var deskNames = Path.Combine(builder.Environment.ContentRootPath, "desk-names.json");
var resolvedPath = Path.Combine(builder.Environment.ContentRootPath, "handsup-resolved.json");
var closedPath = Path.Combine(builder.Environment.ContentRootPath, "closed-desks.json");
var alertsPath = Path.Combine(builder.Environment.ContentRootPath, "alerts-dismissed.json");
var deskAgentsPath = Path.Combine(builder.Environment.ContentRootPath, "desk-agents.json");
var deskAgents = new WorkshopRoom.Data.DeskAgentStore(deskAgentsPath);
builder.Services.AddSingleton(deskAgents);
builder.Services.AddSingleton(new WorkshopRoom.Data.SessionStoreReader(sessionRoot, usageCache, deskNames, resolvedPath, closedPath, agents: deskAgents, alertsPath: alertsPath));

var workshopsDir = Environment.GetEnvironmentVariable("WORKSHOP_DIR") is { Length: > 0 } envWsDir
    ? envWsDir
    : (Path.GetPathRoot(builder.Environment.ContentRootPath) ?? builder.Environment.ContentRootPath);

// Per-agent launch defaults. Agency is the only configurable agent today: it
// launches (and resumes) wrapped with these MCPs/plugin/model/agent instead of
// bare (issue #2). Resolution order: environment override > appsettings > the
// pr-inbox default. Env: WORKSHOP_AGENCY_{MCPS,PLUGIN,MODEL,AGENT,RESUME}.
var cfg = builder.Configuration;
string AgencyOpt(string env, string key, string fallback)
{
    var e = Environment.GetEnvironmentVariable(env);
    if (!string.IsNullOrWhiteSpace(e)) return e.Trim();
    var c = cfg[$"AgentDefaults:agency:{key}"];
    return string.IsNullOrWhiteSpace(c) ? fallback : c.Trim();
}
var agentDefaults = new Dictionary<string, WorkshopRoom.Data.AgentLaunchSettings>
{
    ["agency"] = new WorkshopRoom.Data.AgentLaunchSettings(
        Mcps: AgencyOpt("WORKSHOP_AGENCY_MCPS", "Mcps", "workiq,teams"),
        Plugin: AgencyOpt("WORKSHOP_AGENCY_PLUGIN", "Plugin", ""),
        Model: AgencyOpt("WORKSHOP_AGENCY_MODEL", "Model", ""),
        Agent: AgencyOpt("WORKSHOP_AGENCY_AGENT", "Agent", ""),
        ResumeMode: AgencyOpt("WORKSHOP_AGENCY_RESUME", "ResumeMode", "wrapper")),
};
builder.Services.AddSingleton(new WorkshopRoom.Data.RoomConfig { WorkshopsBaseDir = workshopsDir, AgentDefaults = agentDefaults });

var archivedPath = Path.Combine(builder.Environment.ContentRootPath, "archived-workshops.json");
builder.Services.AddSingleton(new WorkshopRoom.Data.WorkshopArchive(archivedPath));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
