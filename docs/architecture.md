# Архитектура Lattice

Документ фиксирует устройство фреймворка и состояние его реализации. Он дополняет README и
обновляется вместе с кодом.

## 1. Место в стеке

```
Flare              UI-компоненты, IDE-оболочка (FlareIdeLayout, FlareRibbon, FlareMenuBar,
                   FlareDocumentTabs, FlareToolPanel), темы, иконки, локализация
  ↑
Lattice            композиция приложения из плагинов: контракты, каталог, валидация,
                   встроенные модули (команды, документы, риббоны, настройки, состояние)
  ↑
Host-приложение    Quas и другие IDE: перечисляет плагины и регистрирует прикладные сервисы
```

- Flare не знает о Lattice. Lattice не рисует собственные IDE-контролы: его UI-слой собирает
  компоненты Flare.
- Lattice не знает о конкретной предметной области. SQL, подключения и Monaco живут в плагинах.
- Плагин — compile-time компонент: host перечисляет его в `AddLattice`. Динамической загрузки,
  выгрузки и marketplace нет.

## 2. Принципы

1. **Один composition root.** `AddLattice` собирает плагины, валидирует вклады и замораживает
   каталог. Никаких статических синглтонов и «auto-create services».
2. **Две фазы.** Определения и сервисы регистрируются один раз на старте; UI-состояние и
   привязанные к circuit сервисы создаются на область. Это снимает ловушку Quas, где модуль
   регистрировал портативную команду per circuit в singleton-каталог.
3. **Стабильные типизированные id.** `PluginId`, `CommandId`, `DocumentTypeId`, `ToolWindowId`,
   `RibbonGroupId`, `RibbonItemId`, `SettingsPageId`, `SettingsKey` — обёртки над непустой
   строкой. CLR-имена типов не попадают в персистентность.
4. **Валидация до запуска.** Дубликаты, отсутствующие зависимости, циклы и ссылки на неизвестные
   команды — это ошибки композиции, а не тихая деградация UI.
5. **Вклады — данные.** Плагин объявляет описатели; хост решает, как их отрисовать. Описатели не
   содержат `Func<Task>`; пункт меню или риббона несёт `CommandId` и типизированный аргумент.
6. **Строить от реального плагина.** Тип вклада входит в Lattice только тогда, когда его
   использует плагин «Подключение к БД» (или другой реальный плагин).

## 3. Контракт плагина

```csharp
public interface ILatticePlugin
{
    PluginId Id { get; }
    StringKey DisplayName { get; }
    IReadOnlyList<PluginId> DependsOn { get; }
    void Configure(ILatticePluginBuilder builder);
}
```

`Configure` вызывается один раз, в порядке зависимостей. Плагин регистрирует сервисы в
`builder.Services` и добавляет описатели вкладов. Метод не резолвит сервисы и не строит UI:
к моменту вызова каталог ещё не собран.

`ILatticePluginBuilder` — типизированный сборщик:

| Метод | Вклад |
| --- | --- |
| `AddDocument` | тип документа в основной области |
| `AddToolWindow` | панель инструментов (Explorer, Properties, Toolbox) |
| `AddCommand` | команда с локализованным заголовком, иконкой, shortcut и типом аргумента |
| `AddRibbonGroup` / `AddRibbonItem` | группа и пункт риббона, пункт ссылается на команду |
| `AddSettingsPage` / `AddSetting` | страница настроек и одна настройка по схеме |

Зависимости плагинов задают порядок композиции. Порядок вкладов — сначала порядок плагинов, затем
порядок объявления внутри плагина; это поведение фиксируется тестами.

## 4. Вклады

### Документы и tool windows

Это разные вклады. Документ живёт в основной области (`LatticeDockLocation.Main/Left/Right`) и
может быть persistent; tool window живёт в боковом или нижнем регионе
(`LatticeToolRegion.Left/Right/Bottom`) и имеет собственный toolbar. Обозреватель БД — tool
window, а не документ.

`DocumentDescriptor` и `ToolWindowDescriptor` содержат `Type Component` (Blazor-компонент) и
строковый `IconKey`. Строковый ключ выбран осознанно: `Lattice.Abstractions` не зависит от Flare,
а UI-слой разрешает ключ в `FlareIcon`. Плагин, который хочет Flare-иконку напрямую, может
сделать это в своём компоненте.

### Команды

`CommandDescriptor` описывает метаданные (id, заголовок, иконка, shortcut, тип аргумента), а
`AddCommand(descriptor, handler, canExecute)` добавляет к ним обработчик и предикат
`CanExecute` над общим состоянием. `ILatticeCommandService` валидирует аргумент по типу,
проверяет предикат, уведомляет наблюдателей `ILatticeCommandObserver` до и после обработчика и
превращает исключения в `LatticeCommandResult` с кодом `lattice.command.failed`. Наблюдатели
заменяют ветви `Home.OnShellCommandExecuted` в Quas.

`ILatticeShortcutService` собирает shortcut'ы из описателей, применяет пользовательские
переопределения (пустой жест снимает привязку), разрешает жест без учёта регистра и сообщает
конфликты (`LatticeShortcutConflict`). Нормализация жестов остаётся за клавиатурным роутером
хоста.

### Риббоны и меню

Описатели групп и пунктов позволяют нескольким плагинам добавлять пункты в одну группу, а
валидатор проверяет, что пункт ссылается на существующую команду и группу.

Меню описаны отдельно: `MenuGroupDescriptor` — верхний уровень (`File`, `View`, `Tools` или
собственный корень плагина), `MenuItemDescriptor` — пункт с командой, порядком, разделителем и
вложенными `Children`. Хост рисует меню через `FlareMenuBar`/`FlareMenu`/`FlareSubMenu` и
рекурсивный `LatticeMenuEntry`: локализованный заголовок, эффективный shortcut, `CanExecute` и
выполнение команды. Встроенный `LatticeHostPlugin` объявляет группу `lattice.tools`; приложение
добавляет свои пункты в неё, а не создаёт вторую группу с тем же id. Фрагмент `MenuBar` хоста
заменяет меню каталога, если приложению нужна своя полоса.

### Настройки

`SettingDescriptor` задаёт схему: ключ, локализованную подпись, вид редактора, значение по
умолчанию, допустимый диапазон, варианты выбора и страницу (`SettingsPageId? Page`). `ISettingsStore`
отдаёт значения по схеме, проверяет вид, диапазон и допустимые варианты и сообщает об изменении
ключа. Валидатор каталога сообщает настройку с незарегистрированной страницей.

Встроенный документ параметров (`LatticeOptionsDocument` из `LatticeHostPlugin`) показывает
страницы слева и генерирует форму справа: `FlareSelect` для выбора, `FlareNumericField` для
целых, `FlareCheckbox` с внешней подписью для Boolean, `FlareTextField` для текста. Значения
читаются и пишутся через `ISettingsStore`, ошибка валидации показывается в странице.
`SettingsPageDescriptor.Component` остаётся escape hatch для сложных страниц (например, редактор
горячих клавиш). Хранилище пока в памяти; порт `ISettingsStorage` (localStorage/файл) — следующий
инкремент.

### Состояние, документы и tool windows

`ILatticeContext` — наблюдаемая типизированная сумка ключей, которые определяют плагины;
`CanExecute`, видимость пунктов и хост читают её. `ILatticeDocumentService` и
`ILatticeToolWindowService` — scoped-сервисы: документы открываются/активируются/закрываются,
tool windows показываются/скрываются/переключаются. Неизвестный тип или экземпляр возвращает
стабильный код ошибки (`lattice.document.*`, `lattice.toolwindow.*`), а не исключение.

### Локализация

Плагин добавляет каталог строк через `AddStrings(new LatticeStringCatalog(namespace, resourceManager))`.
Строки живут в embedded `.resx`: нейтральный файл содержит английский текст, `*.ru.resx` —
перевод в сателлитной сборке. `ILatticeLocalizer` находит каталог по `StringKey.Namespace` и
читает `StringKey.Name` из его `ResourceManager`; fallback на нейтральные ресурсы делает сам
`ResourceManager`, а неизвестный ключ возвращается полным текстом `StringKey`. `Changed`
поднимается при смене культуры. Словари в коде не используются.

## 5. Композиция и диагностика

`LatticePluginCatalog.Build`:

1. проверяет дубликаты `PluginId` (первый экземпляр побеждает, второй даёт диагностику);
2. строит порядок обходом в глубину по `DependsOn` с обнаружением циклов;
3. вызывает `Configure` каждого плагина в этом порядке;
4. собирает вклады и проверяет дубликаты id по каждому виду вклада;
5. проверяет, что пункт риббона ссылается на существующие команду и группу;
6. проверяет пункты меню (группа, команда, лист без команды и детей) и настройки (страница);
7. возвращает замороженный каталог и список диагностик.

`AddLattice` бросает `LatticeCompositionException`, если среди диагностик есть ошибка. Текст
исключения содержит код и сообщение каждой диагностики, а сообщение называет оба плагина.

Коды диагностик:

| Код | Значение |
| --- | --- |
| `lattice.plugin.duplicate` | один `PluginId` зарегистрирован дважды |
| `lattice.plugin.missing_dependency` | `DependsOn` ссылается на незарегистрированный плагин |
| `lattice.plugin.cycle` | цикл в зависимостях |
| `lattice.contribution.duplicate` | один id вклада объявлен двумя плагинами |
| `lattice.ribbon.unknown_command` | пункт риббона ссылается на неизвестную команду |
| `lattice.ribbon.unknown_group` | пункт риббона ссылается на неизвестную группу |
| `lattice.menu.unknown_group` | пункт меню ссылается на неизвестную группу |
| `lattice.menu.unknown_command` | пункт меню вызывает неизвестную команду |
| `lattice.menu.invalid_item` | у листового пункта нет ни команды, ни детей |
| `lattice.settings.unknown_page` | настройка ссылается на незарегистрированную страницу |
| `lattice.strings.duplicate_namespace` | один namespace строк объявлен двумя плагинами |
| `lattice.strings.missing_manifest` | у каталога строк нет нейтрального ресурса |

### 5.1 Сервисы времени выполнения

`AddLattice` регистрирует:

| Сервис | Интерфейс | Время жизни |
| --- | --- | --- |
| Каталог плагинов | `LatticePluginCatalog` | singleton |
| Состояние | `ILatticeContext` | scoped |
| Команды | `ILatticeCommandService` | scoped |
| Документы | `ILatticeDocumentService` | scoped |
| Tool windows | `ILatticeToolWindowService` | scoped |
| Настройки | `ISettingsStore` | scoped |
| Строки | `ILatticeLocalizer` | scoped |
| Shortcut'ы | `ILatticeShortcutService` | scoped |

Повторный `AddLattice` не пересобирает каталог: наличие `LatticePluginCatalog` в коллекции
останавливает регистрацию. Плагины регистрируют свои сервисы в `ILatticePluginBuilder.Services`
и сами выбирают их время жизни.

## 6. Хост на Flare (сделано)

`Lattice.Components` собирает `FlareIdeLayout` из каталога:

- `MenuBar` — меню каталога (`FlareMenuBar`/`FlareMenu`/`FlareSubMenu` и рекурсивный
  `LatticeMenuEntry`) или фрагмент хоста, если приложению нужна своя полоса;
- `Toolbars` — компоненты, добавленные через `AddToolbar`; хост рисует их в слоте `MenuBar`
  после меню и в этом случае не создаёт `FlareRibbon`. Компонент владеет своей полосой
  (например, рендерит `FlareToolbar`), поэтому приложение может повторить собственную
  командную панель. Если вкладов нет, хост оставляет прежний `Ribbon` с `FlareRibbon`
  (заголовок параметром, по умолчанию `Home`) и группами/пунктами каталога;
- `Documents`/`ActiveDocuments`/`DocumentContent` — документы Lattice как документы Flare;
  встроенная полоса Center-докинга, активный документ, закрытие и перемещение;
- `LeftToolPanel`/`RightToolPanel` — видимые tool windows через `FlareToolPanelGroup` и
  `FlareToolPanel`, с заголовком и иконкой из описателя; вкладки снизу, без встроенной
  кнопки сворачивания, с собственными кнопками закрепить/закрыть. Закрепление задаёт
  `IdePanelMode.Docked`/`AutoHide`, закрытие скрывает видимые tool windows региона;
- `StatusBar` — `FlareStatusBar` с элементами, добавленными через `AddStatusBarItem`
  (выравнивание, порядок, иконка, команда или компонент с `LatticeStatusBarContext`);
  фрагмент `StatusBar`, переданный хостом, имеет приоритет;
- `BottomPanel`, `ChildContent` (стартовый контент) — параметры хоста;
- документ параметров — `LatticeHostPlugin` и `LatticeOptionsDocument`: дерево настроек с узлом
  «Основное» (встроенные страницы «Команды» и «Темы») и узлом каждого плагина; страница с
  `Component` рендерит свой компонент, иначе контролы генерируются из схемы `SettingDescriptor`;
  приложение обязано добавить плагин явно.

Контракт компонентов плагина: `LatticeDocumentComponent`, `LatticeToolWindowComponent` и
`LatticeToolbarComponent` с параметром `Context`
(`LatticeDocumentContext`/`LatticeToolWindowContext`/`LatticeToolbarContext`), который отдаёт
экземпляр, команды, сервис-владелец, локализатор и scoped `IServiceProvider`.

Иконки описателей — строковые ключи; `ILatticeIconResolver` превращает ключ в `FlareIcon`.
По умолчанию резолвер возвращает `null`, приложение-хост регистрирует свой (например, Material
Design 3). `AddLatticeComponents()` регистрирует резолвер. Это единственная точка, где хост
знает о Flare-иконках; сами описатели остаются свободными от Flare.

## 6.1 Сборка с локальным Flare

Lattice, как и Quas, может собираться против соседнего checkout Flare: скопировать
`Directory.Build.local.props.example` в git-ignored `Directory.Build.local.props` с
`UseLocalFlare=true` (по умолчанию `..\Flare`). Локальная разработка идёт на исходниках 0.58.
Без этого файла `Lattice.Components` и его тесты используют NuGet-пакеты Flare 0.58.0
(`Directory.Packages.props`): эта версия должна быть опубликована до сборки из пакетов; перед
пушем пользователь переключает локальное подключение на NuGet.

## 7. Персистентность (план)

- Документы: `DocumentTypeId` + стабильный id экземпляра + версия payload.
- Tool windows: id, регион, видимость, выбранная вкладка.
- Раскладка: размеры регионов и состояние доков — из FlareIdeLayout.
- Настройки: namespace плагина, версия и миграции на scope.
- Хранилище: порт `ISettingsStorage`/`IStateStore`; web-реализация — localStorage, hybrid —
  файл. Атомарная запись, как в `WorkspaceStateStore` Quas.

## 8. Структура репозитория

```
src/Lattice.Abstractions   контракты и описатели, без Flare и Blazor
src/Lattice.Core           каталог, валидация, команды, документы, tool windows, настройки,
                           контекст, локализация, shortcut'ы
src/Lattice.Components     Flare-хост: LatticeShell, документы, tool windows, риббон, иконки
src/Lattice.Localization   (план; сейчас строки живут в Core)
tests/Lattice.Core.Tests   тесты каталога и сервисов времени выполнения
tests/Lattice.Components.Tests  рендер LatticeShell с тестовым плагином
samples/Lattice.SampleIde  (план) витрина и reference host
```

`Lattice.Abstractions` зависит только от DI-абстракций. `Lattice.Core` — от Abstractions.
`Lattice.Components` зависит от Flare.Components.IDE и Lattice.Core; Core о Flare не знает.

## 9. Дорожная карта

1. **Ядро композиции** (сделано): контракт плагина, вклады, каталог, порядок, валидация, тесты.
2. **Сервисы времени выполнения** (сделано): команды с обработчиками, `CanExecute`, наблюдатели,
   аргументы и shortcut'ы; контекст; документы и tool windows; настройки по схеме; локализация.
3. **UI-хост** (сделано): `Lattice.Components` на FlareIdeLayout; риббон, меню, документы,
   tool windows, документ параметров, иконки.
4. **Персистентность**: `ISettingsStorage`/`IStateStore` (localStorage/файл), миграции состояния,
   восстановление раскладки.
5. **Первый реальный плагин**: `Quas.Plugins.Connection` в sample host, затем в Quas через
   адаптер и постепенное удаление прошитой обвязки из `Home`. Плагины живут в Quas; Lattice
   остаётся механизмом.

## 10. Принятые решения

- **Без Flare в Abstractions.** Иконки — строковые ключи, компоненты — `Type`. Это держит
  контракт плагина независимым от версии Flare и позволяет плагину использовать Flare только
  там, где он рисует UI.
- **Без Quas-типов в контракте.** `LatticeDockLocation`/`LatticeToolRegion` — собственные
  перечисления Lattice, а не `WorkspaceTabLocation`/`WorkspacePanelRegion` Quas.
- **Один каталог, много реестров.** Каталог замораживает вклады; реестры модулей (команды,
  документы, настройки) читают его. Так порядок и валидация остаются в одном месте.
- **Диагностика вместо исключений в Build.** `Build` возвращает каталог с диагностиками, чтобы
  тесты и инструменты могли показать все проблемы сразу; `AddLattice` превращает ошибки в
  исключение на границе приложения.
- **Lattice — механизм, плагины — в приложении.** Lattice не содержит ни одного прикладного
  плагина. Первый плагин (управление подключениями к БД) реализуется в Quas и подключается к
  Lattice-хосту; это проверяет, что контракт достаточно общий.
