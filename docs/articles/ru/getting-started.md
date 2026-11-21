# Подключение Blazor-хоста

[English](../en/getting-started.md)

Lierne работает в приложении Blazor на .NET 11. Хост регистрирует Flare, выбирает тему и
перечисляет плагины рабочей области.

## Установка

```sh
dotnet add package Lierne
dotnet add package Flare.Blazor
dotnet add package Flare.Theme.VisualStudio
```

Пакет `Lierne` подключает компоненты IDE Flare. Добавьте `Flare.Blazor` и пакет темы, если хост
ещё их не использует.

## Запустите пример

Выполните команду из корня репозитория с установленным .NET 11 SDK.

```sh
dotnet run --project samples/Lierne.SampleIde
```

Откройте адрес из терминала. Выберите **New note**, введите несколько строк и перезагрузите
страницу. Пример восстановит заметку из браузерного хранилища. В **Tools → Options** можно
изменить сочетание клавиш для **New note** или заголовок новых заметок по умолчанию.

## Подключите своё приложение

В Blazor Web App добавьте ссылку на проект `src/Lierne/Lierne.csproj` из исходников
или установите пакет `Lierne`. Подключите `Flare.Blazor` и совместимый пакет темы Flare.
В примере используется `Flare.Theme.VisualStudio`.

Для интерактивного серверного хоста зарегистрируйте сервисы до `builder.Build()`. В своём
приложении замените `NotesPlugin` на собственный плагин.

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

Подключите скрипт и стили Flare в HTML-оболочке приложения. Для темы Visual Studio
[пример IDE](#запустите-пример) использует следующие ссылки.

```html
<script src="_content/Flare.Components/js/flare-bootstrap.js" data-default-theme="visualstudio"></script>
<link rel="stylesheet" href="_content/Flare.Components/css/flare-components.css" />
<link rel="stylesheet" href="_content/Flare.Components.IDE/css/flare-ide.css" />
<link rel="stylesheet" href="_content/Lierne/Lierne.bundle.scp.css" />
```

Разместите оболочку на интерактивной странице внутри провайдеров Flare.

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

`LierneHostPlugin` добавляет встроенный документ параметров. Браузерное хранилище
подключается по желанию. [Руководство по плагинам](plugins.md) описывает первый вклад,
а [руководство по рабочей области](workspace.md) - хранение и компоновку.

## Обновите хост, который использовал Lattice

Подключите `src/Lierne/Lierne.csproj` или пакет `Lierne`. Если хост напрямую ссылается на ядро
или контракты, обновите эти ссылки до `Lierne.Core` и `Lierne.Abstractions`. Замените импорты
на `using Lierne;`, а префикс `Lattice` в именах API - на `Lierne`, как в `AddLierne`,
`ILiernePlugin` и `LierneShell`. Укажите путь к таблице стилей из примера выше.

Сохраните ключ приложения для браузерного хранилища или каталог для файлового. Lierne читает
существующие рабочие области и экспортированные настройки с их исходными идентификаторами.
