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
var closedPath = Path.Combine(builder.Environment.ContentRootPath, "closed-desks.json");
builder.Services.AddSingleton(new WorkshopRoom.Data.SessionStoreReader(sessionRoot, usageCache, deskNames, resolvedPath, closedPath));

var workshopsDir = Path.GetPathRoot(builder.Environment.ContentRootPath) ?? builder.Environment.ContentRootPath;
builder.Services.AddSingleton(new WorkshopRoom.Data.RoomConfig { WorkshopsBaseDir = workshopsDir });

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
