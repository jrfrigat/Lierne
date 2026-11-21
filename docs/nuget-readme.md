# Lierne

Lierne is a .NET 11 framework for composing IDE-style Blazor applications on
[Flare](https://github.com/jrfrigat/Flare). Plugins provide documents, tool windows, commands and
settings. The host chooses its plugins and creates a workspace from their contributions.

Install the package and a Flare theme:

```sh
dotnet add package Lierne
dotnet add package Flare.Blazor
dotnet add package Flare.Theme.VisualStudio
```

For a Blazor host with Flare and a theme already configured, register the plugin catalog and
the shell services. This example enables the built-in options document.

```csharp
using Lierne;

builder.Services.AddLierne(options => options.AddPlugin<LierneHostPlugin>());
builder.Services.AddLierneComponents();
```

Add your plugins to the registration and place `<LierneShell />` on an interactive page inside
the Flare theme and localization providers. Register `AddLierneBrowserStateStorage("my-ide")`
to restore preferences and open documents in a browser. The package includes guides under
`docs/articles/en` and `docs/articles/ru`.

## Русский

Lierne - фреймворк для IDE-подобных Blazor-приложений на .NET 11 и Flare. Плагины объявляют
документы, окна инструментов, команды и настройки. Хост выбирает плагины и собирает из их
вкладов рабочую область.

Настройте Flare и тему в Blazor-хосте, затем зарегистрируйте каталог плагинов и сервисы
оболочки, как показано выше. В примере подключён встроенный документ параметров.

Добавьте свои плагины и разместите `<LierneShell />` на интерактивной странице внутри провайдеров
темы и локализации Flare. Для восстановления настроек и документов в браузере зарегистрируйте
`AddLierneBrowserStateStorage("my-ide")`. В пакет входят руководства в `docs/articles/ru` и
`docs/articles/en`.

## Links

- [Repository and guides](https://github.com/jrfrigat/Lierne#documentation)
- [Architecture notes](https://github.com/jrfrigat/Lierne/blob/main/docs/architecture.md)

## License

MIT
