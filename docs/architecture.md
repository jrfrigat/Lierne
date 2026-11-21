# Архитектура Lierne

Lierne - workspace-каркас для доменных IDE. Он собирает рабочее пространство из плагинов,
команд, доменных документов и панелей и не содержит редактор кода. Flare - общая визуальная
библиотека Blazor-компонентов, которая не знает о Lierne. Quas - первая IDE на Lierne:
документы подключений, схем, запросов и результатов с таблицами, графиками и формами.

Документ описывает концепцию, границы слоёв и целевую модель, а затем текущее устройство
репозитория. Целевые контракты помечены явно; текущий API сохраняется до миграции.

## 1. Концепция: workspace-каркас

Lierne отвечает не на вопрос "что такое IDE", а на вопрос "из чего состоит рабочее
пространство". Модель состоит из четырёх существительных и одного контейнера.

| Понятие | Что это | Чего Lierne о нём не знает |
| --- | --- | --- |
| Plugin | доменный модуль: документы, views, команды, панели, сервисы, настройки | что именно делает домен |
| Command | универсальное намерение пользователя | какая поверхность его вызвала |
| Document | доменный объект и набор views | что внутри: таблица, график, форма, SQL |
| Panel | инструментальная поверхность с контентом | какие данные в ней живут |
| Workspace | версионированное состояние: раскладка, документы, панели, view state | как устроен конкретный продукт |

Формула: Lierne - это workspace-ядро, в котором плагины описывают доменные документы, команды
и панели, а Flare рисует общую визуальную грамматику.

Следствия:

- Нет редактора как центра. SQL-редактор в Quas - один из views, а не ядро.
- Нет PSI, rope и write actions. Состояние документа определяется кодеком плагина.
- Нет фиксированной оболочки. Lierne - конструктор: можно собрать полный `LierneShell` или
  отдельные поверхности.
- Нет XML-грамматики дескрипторов. Всё код-first: типизированные дескрипторы и точки
  расширения.
- Нет classloader-машинерии. Плагины изолируются DI-scope'ами и disposal; выгрузка плагина
  требует перезапуска.
- Flare не знает Lierne. Это жёсткая граница, а не API/impl split внутри одной платформы.

## 2. Границы Flare, Lierne и Quas

```
Flare
  Flare                  базовые визуальные компоненты, темы, иконки, inputs, menus, tabs, DataGrid
  Flare.Docking          опциональная generic-поверхность докинга, когда она появится
  Flare.Components.IDE   опциональный IDE-пресет и визуальная композиция
        ^
        | адаптер: модель, RenderFragment-контент, intents
Lierne
  Lierne.Abstractions    контракты и описатели, без Flare и Blazor
  Lierne.Core            каталог, команды, документы, панели, workspace, state, readiness, events
  Lierne                 UI-хост: LierneShell, Flare-адаптер, command surfaces, document/panel hosts
        ^
        | описатели плагинов, views, команд и панелей
Quas
  Quas.Plugins.*         доменные документы, views, команды, панели
  Quas.Hosting           composition root приложения
```

Правила зависимостей:

| Слой | Может зависеть от | Не может зависеть от |
| --- | --- | --- |
| Flare | общих .NET/Blazor-зависимостей | Lierne и его типов |
| Lierne.Abstractions | DI-абстракций | Flare и `Microsoft.AspNetCore.Components` |
| Lierne.Core | Lierne.Abstractions | Flare и `Microsoft.AspNetCore.Components` |
| Lierne | Lierne.Abstractions, Lierne.Core, Flare | доменных типов Quas |
| Quas | Lierne, Flare и собственных плагинов | внутренних типов Lierne.Core |

`ArchitectureBoundaryTests` проверяет, что сборки `Lierne.Abstractions` и `Lierne.Core` не
ссылаются на Flare или `Microsoft.AspNetCore.Components`. Нарушение этого теста - дефект
архитектуры, а не повод ослабить проверку.

### Flare

Flare отвечает за повторно используемые визуальные компоненты, темы, иконки, generic docking,
DnD, splitter, tabs и content hosting. Flare не знает о документах, панелях, командах,
плагинах, workspace, персистентности и политике размещения. Flare может выбрать собственную
реализацию и предложить другой механизм, если он закрывает потребности приложения; Lierne
адаптируется через свой adapter.

### Lierne

Lierne отвечает за семантику: доменные документы и views, команды и их презентацию, панели и
раскладку, плагины и точки расширения, состояние и миграции, readiness, progress, события и
disposal. Lierne не содержит редактор кода и не зависит от конкретной визуальной библиотеки в
своём ядре.

### Quas

Quas - первая IDE и первый потребитель. Его плагины определяют документы подключений, схем,
запросов и результатов, views в виде таблиц, графиков, форм и SQL-редактора, команды и панели.
Quas не добавляет доменные типы в Lierne.

## 3. Целевая модель

### 3.1 Плагины

Плагин проходит три стадии: raw descriptors -> resolved catalog -> runtime plugin scopes.

- Raw: плагин объявляет зависимости и вклады, код ещё не создан.
- Resolved: каталог проверяет дубликаты, циклы, отсутствующие зависимости, версии и
  essential closure; каждая диагностика несёт причину и плагин.
- Runtime: плагин получает DI-scope, lazy factories и disposal.

Целевые возможности:

- типизированные точки расширения `IExtensionPoint<T>` для необязательных возможностей;
- essential plugins и их транзитивное замыкание;
- состояния disabled / installed / loaded;
- выгрузка через restart-required, без динамических classloader'ов;
- ленивое создание handler'ов и компонентов.

### 3.2 Команды

Команда - единственная единица намерения. Меню, риббон, тулбар, статус-бар, контекстное меню и
палитра - проекции одной модели.

- `CommandDescriptor`: id, заголовок, группа, иконка, shortcut, threading, RequiresReady.
- `ICommandHandler`: выполнение.
- `ICommandUpdateHandler`: презентация и доступность.
- `CommandPresentation`: заголовок, описание, иконка, enabled/visible/checked, shortcut text.
- `CommandContext`: типизированные ключи и неизменяемый снимок контекста.
- `CommandPlacement`: поверхность, группа, порядок, anchor, переопределения.
- `ICommandService`: единый pipeline с pre-check, reentrancy guard, структурированными ошибками
  и observers.
- `ICommandUpdateService`: версия, инвалидация и батчевое обновление поверхностей.
- `IKeymapService`: кандидаты по жесту, пользовательские переопределения и chords.
- Встроенная command palette.

### 3.3 Документы и views

Документ - доменный объект, а не текст и не редактор.

- `DocumentTypeDescriptor`: id, заголовок, иконка, factory, ReusePolicy, Persistent, views.
- `DocumentInstance`: id, resource key, type, title, icon, state, dirty, version.
- `DocumentViewDescriptor`: id, заголовок, иконка, компонент, applicability, priority,
  default/exclusive, per-view state codec.
- `DocumentHost`: вкладка, выбранный view, preview/pin, close.
- `IDocumentService`: open/reuse/close/activate/move/save и события.
- `IDocumentViewService`: создание views и per-view state.
- `IDocumentSavePipeline`: veto, conflict policy и сохранение.

Документ может быть таблицей, графиком, формой, SQL-редактором или чем угодно ещё. Lierne
предоставляет хост и жизненный цикл, а не содержимое.

### 3.4 Панели и workspace

- `PanelDescriptor`: id, заголовок, иконка, регион по умолчанию, порядок, factory.
- `PanelContentDescriptor`: вкладки внутри панели.
- `PanelPlacement`: region, slot, mode, visible, pinned, order, size, slot size.
- `IPanelService`: show/hide/activate/add/remove/move/pin.
- `IPanelLayoutService`: политика размещения, availability и hide-on-empty,
  activation/displacement stacks.
- `WorkspaceState`: версия схемы, panel placements, document hosts, layout tree, active ids,
  recent.
- `IWorkspaceService`: load/save/restore, best-effort restore, миграции.

До появления generic docking во Flare Lierne использует текущую IDE-композицию как первый
adapter. Семантическая модель панелей и workspace от этого не зависит.

### 3.5 Foundations

- DI scopes: host -> workspace -> plugin -> document.
- State channels: UserSettings, Workspace, Cache.
- `IReadinessService`: IsReady, WhenReadyAsync, RequiresReady с причинами.
- `IProgressService`: background/modal, cancellation, progress nodes.
- `IEventBus`: типизированные topics, scoped subscriptions, broadcast policy.
- Disposable tree и `IDisposableScope`.
- Diagnostics с plugin id и цепочкой причин.
- Опциональный `IUndoService` на уровне документа или команды.

### 3.6 Shell kit

`LierneShell` - композиция, а не монолит:

- `LierneWorkspace` - docks и document/panel hosts;
- `LierneCommandBar` - menu/ribbon/toolbar;
- `LierneStatusBar`;
- `LierneCommandPalette`;
- `LierneSettingsHost`;
- `LierneNotificationHost`.

Приложение может собрать полный shell или отдельные поверхности и заменить chrome.

## 4. Текущее устройство

Этот раздел описывает API, которое существует сегодня. Целевая модель выше вводится
постепенно, без ломки текущих контрактов.

### 4.1 Контракт плагина

```csharp
public interface ILiernePlugin
{
    PluginId Id { get; }
    StringKey DisplayName { get; }
    IReadOnlyList<PluginId> DependsOn { get; }
    void Configure(ILiernePluginBuilder builder);
}
```

`Configure` вызывается один раз, в порядке зависимостей. Плагин регистрирует сервисы в
`builder.Services` и добавляет описатели вкладов. Метод не резолвит сервисы и не строит UI:
к моменту вызова каталог ещё не собран.

`ILiernePluginBuilder` - типизированный сборщик:

| Метод | Вклад |
| --- | --- |
| `AddDocument` | тип документа в основной области |
| `AddDocumentView` | вкладка (view) в тип документа, в том числе объявленный другим плагином |
| `AddToolWindow` | панель инструментов (Explorer, Properties, Toolbox) |
| `AddCommand` | команда с локализованным заголовком, иконкой, shortcut и типом аргумента |
| `AddRibbonGroup` / `AddRibbonItem` | группа и пункт риббона, пункт ссылается на команду |
| `AddMenuGroup` / `AddMenuItem` | группы и пункты меню |
| `AddSettingsPage` / `AddSetting` | страница настроек и одна настройка по схеме |
| `AddToolbar` | компонент полосы команд, который рисует сам плагин |
| `AddStatusBarItem` | элемент статус-бара с командой или компонентом |
| `AddShellContent` | компонент, заменяющий одну поверхность оболочки |
| `AddSettingsTransfer` | секция импорта/экспорта настроек |
| `AddStrings` | каталог локализованных строк |

Зависимости плагинов задают порядок композиции. Порядок вкладов - сначала порядок плагинов,
затем порядок объявления внутри плагина; это поведение фиксируется тестами.

`ILiernePlugin` может объявить `Version`, `OptionalDependsOn` и `Essential`. Optional-зависимости
упорядочивают присутствующие плагины, но их отсутствие не является ошибкой. `IExtensionPoint<T>`
даёт типизированную точку расширения: плагин объявляет её через `AddExtensionPoint<T>`, другой
плагин добавляет расширение через `GetExtensionPoint<T>`. Дубликат id даёт диагностику, первое
объявление остаётся активным; подписка `Add` снимает расширение при dispose.

### 4.2 Вклады

#### Документы и tool windows

Это разные вклады. Документ живёт в основной области (`LierneDockLocation.Main/Left/Right`) и
может быть persistent; tool window живёт в боковом или нижнем регионе
(`LierneToolRegion.Left/Right/Bottom`) и имеет собственный toolbar. Обозреватель БД - tool
window, а не документ.

`DocumentDescriptor` и `ToolWindowDescriptor` содержат `Type Component` (Blazor-компонент) и
строковый `IconKey`. Строковый ключ выбран осознанно: `Lierne.Abstractions` не зависит от
Flare, а UI-слой разрешает ключ в `FlareIcon`. Плагин, который хочет Flare-иконку напрямую,
может сделать это в своём компоненте.

`DocumentViewDescriptor` добавляет вкладку (view) к типу документа и адресуется только id:
тип документа может быть объявлен другим плагином, поэтому ссылки между сборками плагинов не
нужны. Описатель задаёт тип документа, id view, локализованный заголовок, иконку, компонент и
порядок. Каталог проверяет, что тип объявлен хотя бы одним плагином, а id view уникален в
пределах типа. `LiernePluginCatalog.DocumentViews` отдаёт все views, а `ViewsFor(typeId)` -
views одного типа по `Order`, затем по `DocumentViewId.Value`. Оболочка не рисует views сама:
хост решает, как расположить вкладки и какой контекст передать компоненту.

`DocumentDescriptor.ReusePolicy` и `DocumentKey` позволяют переиспользовать открытый документ по
стабильному ключу. `LierneDocumentInstance` хранит `Dirty`, `Version`, выбранный view, preview и
pin, а также per-view state. `SetPayload` увеличивает версию и ставит dirty; `MarkSaved` снимает
dirty только для записанной версии. Состояние view сериализуется через
`LierneDocumentViewStateCodec`.

#### Команды

`CommandDescriptor` описывает метаданные (id, заголовок, иконка, shortcut, тип аргумента), а
`AddCommand(descriptor, handler, canExecute)` добавляет к ним обработчик и предикат
`CanExecute` над общим состоянием. `ILierneCommandService` валидирует аргумент по типу,
проверяет предикат и уведомляет наблюдателей `ILierneCommandObserver` до и после обработчика.
Исключение обработчика превращается в `LierneCommandResult` с кодом `lattice.command.failed`.
Сбой наблюдателя записывается в трассировку, а остальные наблюдатели и команда продолжают
работу. Встроенные пункты меню, риббона и статус-бара принимают команды без обязательного
аргумента.

`ILierneShortcutService` собирает shortcut'ы из описателей, применяет пользовательские
переопределения (пустой жест снимает привязку), разрешает жест без учёта регистра и сообщает
конфликты (`LierneShortcutConflict`). `Resolve` возвращает первый кандидат, `ResolveAll` -
все кандидаты в порядке регистрации. Нормализация жестов остаётся за клавиатурным роутером
хоста.

`ICommandPresentationService` (реализуется `LierneCommandService`) отдаёт
`CommandPresentation` для поверхности и запускает `ICommandUpdateHandler`; `Search` ищет
команды по id, заголовку, описанию и keywords. `ICommandPlacementService` отдаёт
`CommandPlacementDescriptor` по поверхности. Меню и риббон пока продолжают использовать
описатели `MenuItemDescriptor`/`RibbonItemDescriptor`; переход поверхностей на placements
выполняется в задаче shell kit.

#### Риббоны и меню

Описатели групп и пунктов позволяют нескольким плагинам добавлять пункты в одну группу, а
валидатор проверяет, что пункт ссылается на существующую команду и группу.

Меню описаны отдельно: `MenuGroupDescriptor` - верхний уровень (`File`, `View`, `Tools` или
собственный корень плагина), `MenuItemDescriptor` - пункт с командой, порядком, разделителем и
вложенными `Children`. Хост рисует меню через `FlareMenuBar`/`FlareMenu`/`FlareSubMenu` и
рекурсивный `LierneMenuEntry`: локализованный заголовок, эффективный shortcut, `CanExecute` и
выполнение команды. Встроенный `LierneHostPlugin` объявляет группу `lattice.tools`; приложение
добавляет свои пункты в неё, а не создаёт вторую группу с тем же id. Фрагмент `MenuBar` хоста
заменяет меню каталога, если приложению нужна своя полоса.

#### Настройки

`SettingDescriptor` задаёт схему: ключ, локализованную подпись, вид редактора, значение по
умолчанию, допустимый диапазон, варианты выбора и страницу (`SettingsPageId? Page`).
`ISettingsStore` отдаёт значения по схеме, проверяет вид, диапазон и допустимые варианты и
сообщает об изменении ключа. Валидатор каталога проверяет значение по умолчанию и ссылку на
страницу настройки.

Встроенный документ параметров (`LierneOptionsDocument` из `LierneHostPlugin`) показывает
страницы слева и генерирует форму справа: `FlareSelect` для выбора, `FlareNumericField` для
целых, `FlareCheckbox` с внешней подписью для Boolean, `FlareTextField` для текста. Значения
читаются и пишутся через `ISettingsStore`, ошибка валидации показывается в странице.
`SettingsPageDescriptor.Component` остаётся escape hatch для сложных страниц (например, редактор
горячих клавиш). Scoped-хранилище предоставляет синхронный доступ к уже загруженным значениям.
При наличии `ILierneStateStorage` оболочка автоматически загружает и сохраняет переопределения.

#### Состояние, документы и tool windows

`ILierneContext` - наблюдаемая типизированная сумка ключей, которые определяют плагины;
`CanExecute`, видимость пунктов и хост читают её. `ILierneDocumentService` и
`ILierneToolWindowService` - scoped-сервисы: документы открываются/активируются/закрываются,
tool windows показываются/скрываются/переключаются. Неизвестный тип или экземпляр возвращает
стабильный код ошибки (`lattice.document.*`, `lattice.toolwindow.*`), а не исключение.

#### Локализация

Плагин добавляет каталог строк через `AddStrings(new LierneStringCatalog(namespace,
resourceManager))`. Строки живут в embedded `.resx`: нейтральный файл содержит английский текст,
`*.ru.resx` - перевод в сателлитной сборке. `ILierneLocalizer` находит каталог по
`StringKey.Namespace` и читает `StringKey.Name` из его `ResourceManager`; fallback на
нейтральные ресурсы делает сам `ResourceManager`, а неизвестный ключ возвращается полным
текстом `StringKey`. `Changed` поднимается при смене культуры. Словари в коде не используются.

### 4.3 Композиция и диагностика

`LiernePluginCatalog.Build`:

1. проверяет дубликаты `PluginId` (первый экземпляр побеждает, второй даёт диагностику);
2. строит порядок обходом в глубину по `DependsOn` с обнаружением циклов;
3. вызывает `Configure` каждого плагина в этом порядке;
4. собирает вклады и проверяет дубликаты id по каждому виду вклада;
5. проверяет, что view документа ссылается на объявленный тип документа, а его id уникален в
   пределах типа;
6. проверяет, что пункт риббона ссылается на существующие команду и группу;
7. проверяет пункты меню (группа, команда, лист без команды и детей) и настройки (страница);
8. возвращает замороженный каталог и список диагностик.

`AddLierne` бросает `LierneCompositionException`, если среди диагностик есть ошибка. Текст
исключения содержит код и сообщение каждой диагностики, а сообщение называет оба плагина.

Коды диагностик:

| Код | Значение |
| --- | --- |
| `lattice.plugin.duplicate` | один `PluginId` зарегистрирован дважды |
| `lattice.plugin.missing_dependency` | `DependsOn` ссылается на незарегистрированный плагин |
| `lattice.plugin.cycle` | цикл в зависимостях |
| `lattice.contribution.duplicate` | один id вклада объявлен двумя плагинами |
| `lattice.documentview.unknown_type` | view документа ссылается на неизвестный тип документа |
| `lattice.ribbon.unknown_command` | пункт риббона ссылается на неизвестную команду |
| `lattice.ribbon.unknown_group` | пункт риббона ссылается на неизвестную группу |
| `lattice.menu.unknown_group` | пункт меню ссылается на неизвестную группу |
| `lattice.menu.unknown_command` | пункт меню вызывает неизвестную команду |
| `lattice.menu.invalid_item` | у листового пункта нет ни команды, ни детей |
| `lattice.settings.unknown_page` | настройка ссылается на незарегистрированную страницу |
| `lattice.strings.duplicate_namespace` | один namespace строк объявлен двумя плагинами |
| `lattice.strings.missing_manifest` | у каталога строк нет нейтрального ресурса |
| `lattice.extensionpoint.duplicate` | один id точки расширения объявлен двумя плагинами |

### 4.4 Сервисы времени выполнения

`AddLierne` регистрирует:

| Сервис | Интерфейс | Время жизни |
| --- | --- | --- |
| Каталог плагинов | `LiernePluginCatalog` | singleton |
| Состояние | `ILierneContext` | scoped |
| Команды | `ILierneCommandService` | scoped |
| Презентации команд | `ICommandPresentationService` | scoped |
| Размещения команд | `ICommandPlacementService` | scoped |
| Документы | `ILierneDocumentService` | scoped |
| Tool windows | `ILierneToolWindowService` | scoped |
| Настройки | `ISettingsStore` | scoped |
| Строки | `ILierneLocalizer` | scoped |
| Shortcut'ы | `ILierneShortcutService` | scoped |

Повторный `AddLierne` не пересобирает каталог: наличие `LiernePluginCatalog` в коллекции
останавливает регистрацию. Плагины регистрируют свои сервисы в `ILiernePluginBuilder.Services`
и сами выбирают их время жизни.

### 4.5 Хост на Flare

`Lierne` собирает `FlareIdeLayout` из каталога:

- `MenuBar` - меню каталога (`FlareMenuBar`/`FlareMenu`/`FlareSubMenu` и рекурсивный
  `LierneMenuEntry`) или фрагмент хоста, если приложению нужна своя полоса;
- `Toolbars` - компоненты, добавленные через `AddToolbar`; хост рисует их в слоте `MenuBar`
  после меню и в этом случае не создаёт `FlareRibbon`. Если вкладов нет, хост оставляет
  `Ribbon` с `FlareRibbon` (заголовок параметром, по умолчанию `Home`) и группами/пунктами
  каталога;
- `Documents`/`ActiveDocuments`/`DocumentContent` - документы Lierne как документы Flare;
  полоса центральных вкладок, выбор отдельно по областям, закрытие и перемещение;
- `LeftToolPanel`/`RightToolPanel` - видимые tool windows через `FlareToolPanelGroup` и
  `FlareToolPanel`, с заголовком и иконкой из описателя; вкладки снизу, без встроенной
  кнопки сворачивания, с собственными кнопками закрепить/закрыть. Закрепление задаёт
  `IdePanelMode.Docked`/`AutoHide`, закрытие скрывает видимые tool windows региона;
- `StatusBar` - `FlareStatusBar` с элементами, добавленными через `AddStatusBarItem`;
  фрагмент `StatusBar`, переданный хостом, имеет приоритет;
- `BottomPanel` включает видимые нижние tool windows и переданный хостом нижний контент.
  `StartContent` задаёт стартовый контент.

Контракт компонентов плагина: `LierneDocumentComponent`, `LierneToolWindowComponent` и
`LierneToolbarComponent` с параметром `Context`
(`LierneDocumentContext`/`LierneToolWindowContext`/`LierneToolbarContext`), который отдаёт
экземпляр, команды, сервис-владелец, локализатор и scoped `IServiceProvider`.

Иконки описателей - строковые ключи; `ILierneIconResolver` превращает ключ в `FlareIcon`.
По умолчанию резолвер возвращает `null`, приложение-хост регистрирует свой (например, Material
Design 3). `AddLierneComponents()` регистрирует резолвер. Это единственная точка, где хост
знает о Flare-иконках; сами описатели остаются свободными от Flare.

Текущая IDE-композиция Flare - первый adapter Lierne. Когда Flare предоставит generic docking,
Lierne заменит adapter, не меняя семантическую модель документов, панелей и workspace.

### 4.6 Сохранение состояния

`LierneStatePersistence` хранит настройки и рабочую область в независимых JSON-документах.
Хост регистрирует `ILierneStateStorage`. Для веб-хоста `AddLierneBrowserStateStorage` использует
localStorage, для Hybrid `LierneFileStateStorage` атомарно заменяет файлы в каталоге приложения.
`LierneShell` загружает сохранённые данные перед первым интерактивным рендером содержимого.

- Настройки и сочетания сохраняются по стабильным ключам. Для секции старого формата хост может
  зарегистрировать `LierneStateMigration`. Версии, которые приложение не умеет читать, сохраняются
  без перезаписи.
- Снимок рабочего пространства содержит видимость окон инструментов, размеры и состояния панелей,
  активное окно инструментов, выбранные документы и активный документ.
- Документы восстанавливаются по id типа и экземпляра. `Persistent: false` исключает документ из
  снимка. Для документа с payload плагин задаёт `StateCodec` и обрабатывает старые версии
  формата при чтении. При редактировании payload компонент вызывает `SetPayload`, чтобы оболочка
  сохранила новое значение. Типы документов, которых больше нет в каталоге, пропускаются.
- Ключ документа, выбранный view, preview/pin и per-view state сохраняются вместе с документом.
  После успешной записи workspace dirty снимается только для записанной версии, поэтому
  параллельное редактирование не теряет новый dirty.
- Quas продолжает восстанавливать свои SQL-документы через собственную модель рабочего
  пространства. Lierne сохраняет настройки, сочетания и панели оболочки Quas.

Целевое состояние разделяет каналы UserSettings, Workspace и Cache. Текущие `Preferences` и
`Workspace` - первый шаг к этой модели. `LierneStateSection.Cache` уже добавлен для
регенерируемых данных, которые можно потерять без потери работы пользователя.

### 4.7 Foundations

`AddLierne` регистрирует scoped-сервисы:

| Сервис | Интерфейс | Назначение |
| --- | --- | --- |
| Readiness | `ILierneReadinessService` | области готовности, `WhenReadyAsync`, `BeginPending` |
| Progress | `ILierneProgressService` | handles с `Report`, `Cancel`, `DisposeAsync` |
| Events | `ILierneEventBus` | типизированные подписки и публикация в пределах scope |
| Disposal | `LierneDisposableBag` | освобождение ресурсов в обратном порядке |

Readiness поддерживает вложенные pending-токены и причину ожидания; неизвестная область
считается готовой. Progress публикует Started, Reported, Completed и Canceled и отменяет
handle через `CancellationToken`. Event bus вызывает подписчиков синхронно на потоке
издателя; ошибка одного подписчика логируется и не мешает остальным. Эти сервисы не зависят
от Flare и Blazor.

## 5. Структура репозитория

```
src/Lierne.Abstractions   контракты и описатели, без Flare и Blazor
src/Lierne.Core           каталог, валидация, команды, документы, tool windows, настройки,
                          контекст, локализация, shortcut'ы
src/Lierne                Flare-хост: LierneShell, документы, tool windows, риббон, иконки
tests/Lierne.Core.Tests   тесты каталога, сервисов и границ архитектуры
tests/Lierne.Components.Tests  рендер LierneShell с тестовым плагином
samples/Lierne.SampleIde  интерактивный пример хоста и плагина заметок
```

`Lierne.Abstractions` зависит только от DI-абстракций. `Lierne.Core` - от Abstractions.
`Lierne` зависит от Flare.Components.IDE и Lierne.Core; Core о Flare не знает.

## 6. Принятые решения

- **Flare не знает Lierne.** Общие визуальные компоненты и generic docking остаются в Flare,
  семантика - в Lierne. Flare может выбирать реализацию; Lierne адаптируется.
- **Ядро без визуальной библиотеки.** `Lierne.Abstractions` и `Lierne.Core` не ссылаются на
  Flare или Blazor. Это позволяет позже добавить другой визуальный adapter.
- **Документ - доменный объект.** Lierne не предполагает текст, PSI или редактор. View может
  быть таблицей, графиком, формой или SQL-редактором.
- **Command-first.** Меню, риббон, тулбар, статус-бар и палитра - проекции команд, а не
  отдельные реестры.
- **Workspace - first-class состояние.** Раскладка, открытые документы и view state
  версионируются и мигрируются как данные.
- **Scopes вместо classloader'ов.** Плагины изолируются DI-scope'ами и disposal; выгрузка
  требует перезапуска.
- **Один каталог, много реестров.** Каталог замораживает вклады; реестры модулей читают его.
- **Диагностика вместо исключений в Build.** `Build` возвращает каталог с диагностиками, чтобы
  тесты и инструменты могли показать все проблемы сразу.
- **Прикладные плагины принадлежат хосту.** Lierne не содержит ни одного прикладного плагина.
  Первый плагин (управление подключениями к БД) реализуется в Quas.

## 7. Направление развития

1. Границы и контракты: этот документ и тест архитектурных зависимостей.
2. Foundations: DI scopes, state channels, readiness, progress, events, disposal.
3. Команды: презентация, placements, keymap, палитра.
4. Документы: domain document model, views, hosts, dirty/save.
5. Плагины: extension points, lazy factories, состояния и plugin scopes.
6. Панели и workspace: семантическая модель, persistence и adapter к текущей IDE-композиции Flare.
7. Shell kit: `LierneWorkspace`, command surfaces, status bar, palette, settings.
8. Quas: доменные документы, views и команды на новой модели.

Целевые контракты вводятся постепенно. Текущий API и Quas остаются работоспособными на каждом
шаге; ломающие изменения допускаются только вместе с миграцией и тестами.
