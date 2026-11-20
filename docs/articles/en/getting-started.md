# Set up a Blazor host

[Русский](../ru/getting-started.md)

Lierne runs in a .NET 11 Blazor application. The host registers Flare, chooses a theme, and
lists the plugins that make up the workspace.

## Try the sample

Run this command from the repository root with the .NET 11 SDK.

```sh
dotnet run --project samples/Lierne.SampleIde
```

Open the address printed in the terminal. Choose **New note**, type a few lines, and reload the
page. The sample restores the note from browser storage. In **Tools → Options**, you can change
the shortcut for **New note** or the default note title.

## Connect your application

For a Blazor Web App, reference `src/Lierne/Lierne.csproj` from this checkout or use
the `Lierne` package from your NuGet feed. Reference `Flare.Blazor` and a compatible
Flare theme package. The sample uses `Flare.Theme.VisualStudio`.

For an interactive server host, register the services before `builder.Build()`. Replace
`NotesPlugin` with your own plugin when you move beyond the sample.

```csharp
using Flare.Extensions;
using Flare.Theme.VisualStudio;
using Lierne;
using Lierne.SampleIde;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddLocalization();

builder.Services.AddFlare(options => options.DefaultTheme = new VisualStudioTheme());
builder.Services.AddFlareTheme(new VisualStudioTheme());

builder.Services.AddLierne(options => options
    .AddPlugin<LierneHostPlugin>()
    .AddPlugin<NotesPlugin>());
builder.Services.AddLierneComponents();
builder.Services.AddLierneBrowserStateStorage("my-ide");

var app = builder.Build();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
```

Keep the Flare bootstrap script and stylesheets in the app's HTML shell. The
[Lierne Sample IDE](#try-the-sample) uses these references for the Visual Studio theme.

```html
<script src="_content/Flare.Components/js/flare-bootstrap.js" data-default-theme="visualstudio"></script>
<link rel="stylesheet" href="_content/Flare.Components/css/flare-components.css" />
<link rel="stylesheet" href="_content/Flare.Components.IDE/css/flare-ide.css" />
<link rel="stylesheet" href="_content/Lierne/Lierne.bundle.scp.css" />
```

Place the shell on an interactive page, inside the Flare providers.

```razor
@page "/"
@using Flare.Components
@using Lierne
@using Microsoft.AspNetCore.Components.Web
@rendermode @(new InteractiveServerRenderMode(prerender: false))

<FlareLocalizationProvider>
    <FlareThemeProvider>
        <LierneShell />
    </FlareThemeProvider>
</FlareLocalizationProvider>
```

`LierneHostPlugin` supplies the built-in options document. Browser storage is optional.
See [plugin composition](plugins.md) for your first contribution and
[workspace configuration](workspace.md) for storage and layout options.

## Update a host that used Lattice

Point the host at `src/Lierne/Lierne.csproj` or the `Lierne` package, and update any direct
references to `Lierne.Core` and `Lierne.Abstractions`. Change imports to `using Lierne;`
and replace the `Lattice` prefix in API names with `Lierne`, as in `AddLierne`,
`ILiernePlugin` and `LierneShell`. Update the stylesheet URL to the one above.

Keep your application key for browser storage or directory for file storage. Lierne reads
existing workspaces and settings exports with their original identifiers.
