using Flare.Extensions;
using Flare.Theme.VisualStudio;
using Lierne;
using Lierne.SampleIde;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFlare(options => options.DefaultTheme = new VisualStudioTheme());
builder.Services.AddFlareTheme(new VisualStudioTheme());
builder.Services.AddLocalization();
builder.Services.AddLierne(options => options.AddPlugin<LierneHostPlugin>().AddPlugin<NotesPlugin>());
builder.Services.AddLierneComponents();
builder.Services.AddLierneBrowserStateStorage("sample-ide");

var app = builder.Build();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
