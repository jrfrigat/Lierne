# Подключение Blazor-хоста

[English](../en/getting-started.md)

Lierne работает в приложении Blazor на .NET 11. Хост регистрирует Flare, выбирает тему и
перечисляет плагины рабочей области. В текущих исходниках и примерах используется пространство
имён `Lattice`.

## Запустите пример

Выполните команду из корня репозитория с установленным .NET 11 SDK.

```sh
dotnet run --project samples/Lattice.SampleIde
```

Откройте адрес из терминала. Выберите **New note**, введите несколько строк и перезагрузите
страницу. Пример восстановит заметку из браузерного хранилища. В **Tools → Options** можно
изменить сочетание клавиш для **New note** или заголовок новых заметок по умолчанию.

## Подключите своё приложение

В Blazor Web App добавьте ссылку на проект
`src/Lattice.Components/Lattice.Components.csproj` из текущих исходников. Публичный пакет NuGet
будет называться `Lierne`. Подключите `Flare.Blazor` и совместимый пакет темы Flare. В примере
используется `Flare.Theme.VisualStudio`.

Для интерактивного серверного хоста зарегистрируйте сервисы до `builder.Build()`. В своём
приложении замените `NotesPlugin` на собственный плагин.

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

Подключите скрипт и стили Flare в HTML-оболочке приложения. Для темы Visual Studio
[пример IDE](#запустите-пример) использует следующие ссылки.

```html
<script src="_content/Flare.Components/js/flare-bootstrap.js" data-default-theme="visualstudio"></script>
<link rel="stylesheet" href="_content/Flare.Components/css/flare-components.css" />
<link rel="stylesheet" href="_content/Flare.Components.IDE/css/flare-ide.css" />
<link rel="stylesheet" href="_content/Lattice.Components/Lattice.Components.bundle.scp.css" />
```

Разместите оболочку на интерактивной странице внутри провайдеров Flare.

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

`LatticeHostPlugin` добавляет встроенный документ параметров. Браузерное хранилище
подключается по желанию. [Руководство по плагинам](plugins.md) описывает первый вклад,
а [руководство по рабочей области](workspace.md) — хранение и компоновку.
