using Flare.Extensions;
using Flare.Theme.VisualStudio;
using Lattice;
using Lattice.SampleIde;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFlare(options => options.DefaultTheme = new VisualStudioTheme());
builder.Services.AddFlareTheme(new VisualStudioTheme());
builder.Services.AddLocalization();
builder.Services.AddLattice(options => options.AddPlugin<LatticeHostPlugin>().AddPlugin<NotesPlugin>());
builder.Services.AddLatticeComponents();
builder.Services.AddLatticeBrowserStateStorage("sample-ide");

var app = builder.Build();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
