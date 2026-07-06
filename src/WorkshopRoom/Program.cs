using WorkshopRoom.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var sessionRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "session-state");
var usageCache = Path.Combine(builder.Environment.ContentRootPath, "usage-cache.json");
var deskNames = Path.Combine(builder.Environment.ContentRootPath, "desk-names.json");
var resolvedPath = Path.Combine(builder.Environment.ContentRootPath, "handsup-resolved.json");
builder.Services.AddSingleton(new WorkshopRoom.Data.SessionStoreReader(sessionRoot, usageCache, deskNames, resolvedPath));

var classroomDir = Directory.GetParent(builder.Environment.ContentRootPath)?.Parent?.FullName ?? builder.Environment.ContentRootPath;
builder.Services.AddSingleton(new WorkshopRoom.Data.RoomConfig { DesksBaseDir = classroomDir });

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
