# Настройка и сохранение рабочей области

[English](../en/workspace.md)

`LierneShell` собирает IDE из каталога плагинов: панель команд, докинг рабочей области, строку
состояния, палитру команд и хост уведомлений. Для собственного содержимого приложения
используйте стартовую страницу или стандартные слоты оболочки. Плагин может добавить панель
команд, элемент строки состояния или компонент для одного из слотов.

Рабочая область сопоставляет документы и окна инструментов с `FlareDockWorkspace`. Пользователь
может переносить вкладки, делить группы, менять порядок и размеры; Lierne принимает изменение
и обновляет сервисы документов и панелей. Подключённый пакет Flare должен содержать
`FlareDockWorkspace`.

## Соберите оболочку

`LierneShellOptions` включает и выключает отдельные поверхности. Параметры `CommandBarContent`,
`WorkspaceContent`, `CommandPaletteContent` и `NotificationsContent` заменяют поверхность
разметкой приложения. Хост может оставить рабочую область Lierne и передать собственное
оформление или использовать `LierneCommandBar`, `LierneWorkspace`, `LierneStatusBar`,
`LierneCommandPalette` и `LierneNotificationHost` по отдельности.

```razor
<LierneShell Options="@(new LierneShellOptions { ShowRibbon = false })">
    <StatusBar>Своя строка состояния</StatusBar>
</LierneShell>
```

## Ограничьте зоны переноса

Задайте `DropZones` у `LierneShell` или `LierneWorkspace` - это флаги `DockDropZones`, которые
хост разрешает для докинга. Например, центр и горизонтальные края оставляют перенос и
горизонтальные split, но убирают вертикальные цели.

```razor
<LierneShell DropZones="@(DockDropZones.Into | DockDropZones.Start | DockDropZones.End)" />
```

## Настройте вкладки документов

Задайте один шаблон `DocumentHeaderContent` для центра, левой и правой областей. Шаблон
получает текущий документ и локализованный заголовок. Рядом с заголовком можно показать
состояние или категорию из приложения. Вкладка сохраняет доступное имя и элементы управления.

```razor
@using Flare.Components
@using Flare.Icons
@using Lierne

<LierneShell>
    <DocumentHeaderContent Context="tab">
        <FlareIconView Value="@FlareIcons.PushPin" />
        @tab.Title
    </DocumentHeaderContent>
</LierneShell>
```

Lierne использует один и тот же шаблон в заголовке вкладки для центра, левого и правого доков.
Вкладка сохраняет доступное имя и кнопку закрытия.

## Выберите хранилище

Подключите хранилище для восстановления настроек, сочетаний клавиш, расположения панелей и
открытых документов. `LierneWorkspace` загружает сохранённое состояние при переходе в
интерактивный режим и применяет сохранённые `PanelPlacement`.

Для веб-хоста задайте стабильный ключ приложения. Данные хранятся в local storage текущего
браузера для данного источника страницы.

```csharp
services.AddLierneBrowserStateStorage("my-ide");
```

Для настольного приложения или Blazor Hybrid подключите файловое хранилище в каталоге
данных приложения.

```csharp
using Lierne;
using Microsoft.Extensions.DependencyInjection;

var stateDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MyIde");

services.AddScoped<ILierneStateStorage>(_ => new LierneFileStateStorage(stateDirectory));
```

Настройки и рабочая область сохраняются как отдельные версионированные секции JSON. Настройки
содержат пользовательские значения и сочетания клавиш. Рабочая область содержит видимость и
расположение окон инструментов, позиции и выбор документов, выбранный view каждой группы
раскладки документа и долю каждого split'а. `LierneFileStateStorage` заменяет каждый файл
атомарно. Для другого хранилища реализуйте `ILierneStateStorage`.

## Восстанавливайте содержимое документов

Тип документа по умолчанию участвует в восстановлении. Укажите `Persistent: false` в описателе,
чтобы исключить его. Документ с непустым `Payload` требует `StateCodec` в описателе. Используйте
стабильный идентификатор типа и поддерживайте чтение ранее сохранённых версий данных.

Для текстовых данных присвойте `DocumentDescriptor.StateCodec` кодек по примеру ниже.

```csharp
new LierneDocumentStateCodec(
    1,
    payload => JsonSerializer.SerializeToElement(payload as string),
    (version, payload) => version == 1
        ? payload.GetString()
        : throw new JsonException("Unsupported note payload version."))
```

При изменении данных вызовите `Context.Documents.SetPayload(Context.Document.Id, newValue)`
в компоненте документа. Оболочка может объединить частые изменения в одну запись. Чтобы
дождаться отложенных записей при явном завершении работы или передаче состояния, вызовите
`LierneStatePersistence.FlushAsync()`.

При чтении Lierne пропускает документ, если его тип отсутствует или кодек не может прочитать
данные. Для обновления старой секции настроек или рабочей области хост может зарегистрировать
`LierneStateMigration`. Секция неподдерживаемой версии остаётся в хранилище.
