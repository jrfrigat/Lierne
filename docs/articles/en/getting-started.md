# Set up a Blazor host

[Русский](../ru/getting-started.md)

Lierne runs in a .NET 11 Blazor application. The host registers Flare, chooses a theme, and
lists the plugins that make up the workspace. The current source projects and examples use the
`Lattice` namespace.

## Try the sample

Run this command from the repository root with the .NET 11 SDK.

```sh
dotnet run --project samples/Lattice.SampleIde
```

Open the address printed in the terminal. Choose **New note**, type a few lines, and reload the
page. The sample restores the note from browser storage. In **Tools → Options**, you can change
the shortcut for **New note** or the default note title.

## Connect your application

For a Blazor Web App, add a project reference to
`src/Lattice.Components/Lattice.Components.csproj` from this checkout. The public NuGet package
will be named `Lierne`. Reference `Flare.Blazor` and a Flare theme package compatible with your
build. The sample uses `Flare.Theme.VisualStudio`.

For an interactive server host, register the services before `builder.Build()`. Replace
`NotesPlugin` with your own plugin when you move beyond the sample.

```csharp
using Flare.Extensions;
using Flare.Theme.VisualStudio;
using Lattice;
using Lattice.SampleIde;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddLocalization();

builder.Services.AddFlare(options => options.DefaultTheme = new VisualStudioTheme());
builder.Services.AddFlareTheme(new VisualStudioTheme());

builder.Services.AddLattice(options => options
    .AddPlugin<LatticeHostPlugin>()
    .AddPlugin<NotesPlugin>());
builder.Services.AddLatticeComponents();
builder.Services.AddLatticeBrowserStateStorage("my-ide");

var app = builder.Build();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
```

Keep the Flare bootstrap script and stylesheets in the app's HTML shell. The
[Lattice Sample IDE](#try-the-sample) uses these references for the Visual Studio theme.

```html
<script src="_content/Flare.Components/js/flare-bootstrap.js" data-default-theme="visualstudio"></script>
<link rel="stylesheet" href="_content/Flare.Components/css/flare-components.css" />
<link rel="stylesheet" href="_content/Flare.Components.IDE/css/flare-ide.css" />
<link rel="stylesheet" href="_content/Lattice.Components/Lattice.Components.bundle.scp.css" />
```

Place the shell on an interactive page, inside the Flare providers.

```razor
@page "/"
@using Flare.Components
@using Lattice
@using Microsoft.AspNetCore.Components.Web
@rendermode @(new InteractiveServerRenderMode(prerender: false))

<FlareLocalizationProvider>
    <FlareThemeProvider>
        <LatticeShell />
    </FlareThemeProvider>
</FlareLocalizationProvider>
```

`LatticeHostPlugin` supplies the built-in options document. Browser storage is optional.
See [plugin composition](plugins.md) for your first contribution and
[workspace configuration](workspace.md) for storage and layout options.
