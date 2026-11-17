# Lattice

Lattice — фреймворк композиции IDE-подобных приложений на
[Flare](https://github.com/jrfrigat/Flare). Приложение собирается из плагинов — компонентов
системы, компилируемых в host, а не динамически загружаемых пакетов. Каждый плагин объявляет
свои документы, tool windows, команды, пункты риббона и настройки.

Фреймворк выделяется из [Quas](https://github.com/jrfrigat/Quas) — database IDE, который стал его
первым потребителем. Quas остаётся полигоном: первый реальный плагин — подключение к БД
(обозреватель баз данных и тулбокс подключения).

## Состояние

Архитектура строится поэтапно. Сейчас есть:

- `Lattice.Abstractions` — типизированные id, контракт плагина, описатели вкладов, контракты
  времени выполнения;
- `Lattice.Core` — каталог плагинов с сортировкой и валидацией, исполнение команд с наблюдателями
  и shortcut'ами, документы и tool windows, настройки по схеме, наблюдаемый контекст, каталоги
  локализованных строк;
- `Lattice.Core.Tests` — тесты сортировки, диагностик, исполнения, состояния и регистрации.

Дальше — UI-хост на Flare (`Lattice.Components`), персистентное состояние и первый реальный
плагин (подключение к БД в Quas): плагины живут в приложении-потребителе, а не в Lattice.
См. [docs/architecture.md](docs/architecture.md).

## Быстрый старт

```csharp
services.AddLattice(options => options
    .AddPlugin<CorePlugin>()
    .AddPlugin<ConnectionPlugin>());

public sealed class ConnectionPlugin : ILatticePlugin
{
    public PluginId Id => new("app.connection");

    public StringKey DisplayName => new("app.connection", "displayName");

    public IReadOnlyList<PluginId> DependsOn => [new PluginId("app.core")];

    public void Configure(ILatticePluginBuilder builder)
    {
        builder.AddDocument(new DocumentDescriptor(
            new DocumentTypeId("app.connection.explorer"),
            new StringKey("app.connection", "explorer"),
            "database",
            typeof(DatabaseExplorerDocument)));
    }
}
```

Композиция проверяется до запуска хоста: дубликаты id, отсутствующие зависимости, циклы и пункты
риббона, ссылающиеся на неизвестные команды, падают с диагностикой, где названы оба плагина.

## Сборка и тесты

```sh
dotnet build Lattice.slnx -c Release
dotnet test Lattice.slnx -c Release --no-build
```

## Лицензия

MIT. См. `PackageLicenseExpression` в `Directory.Build.props`.
