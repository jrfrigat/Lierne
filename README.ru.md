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
- `Lattice.Components` — Flare-хост: `LatticeShell`, риббон, меню, документы, tool windows и
  резолвер иконок; компоненты плагина наследуют `LatticeDocumentComponent` или
  `LatticeToolWindowComponent`. `LatticeHostPlugin` добавляет встроенный документ параметров
  (генерируется из схемы настроек) и группу меню «Инструменты»;
- `Lattice.Core.Tests` и `Lattice.Components.Tests` — композиция, runtime и рендер оболочки.

Дальше — персистентное состояние и первый реальный плагин (подключение к БД в Quas): плагины
живут в приложении-потребителе, а не в Lattice. См. [docs/architecture.md](docs/architecture.md).

## Быстрый старт

```csharp
services.AddFlare();                 // тему выбирает приложение
services.AddLattice(options => options
    .AddPlugin<LatticeHostPlugin>()     // документ параметров и группа «Инструменты»
    .AddPlugin<CorePlugin>()
    .AddPlugin<ConnectionPlugin>());
services.AddLatticeComponents();     // резолвер иконок и сервисы хоста

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

По умолчанию сборка использует Flare 0.59.0 из NuGet. Для работы с соседним checkout Flare скопируйте
`Directory.Build.local.props.example` в
git-ignored `Directory.Build.local.props`; `UseLocalFlare=true` заменит package references на
project references.

## Лицензия

MIT. См. `PackageLicenseExpression` в `Directory.Build.props`.
