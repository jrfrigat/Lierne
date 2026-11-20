# Настройка и сохранение рабочей области

[English](../en/workspace.md)

`LierneShell` собирает IDE из каталога плагинов. Для собственного содержимого приложения
используйте стартовую страницу или стандартные слоты оболочки. Плагин может добавить панель
команд, элемент строки состояния или компонент для одного из слотов.

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

Для шаблона заголовка нужна сборка Flare с `FlareDocumentTab.HeaderContent` и
`FlareIdeLayout.DocumentHeaderContent`. С Flare 0.59.1 из NuGet вкладки показывают обычный текстовый заголовок.

## Выберите хранилище

Подключите хранилище для восстановления настроек, сочетаний клавиш, расположения панелей и
открытых документов. `LierneShell` загружает сохранённое состояние при переходе в интерактивный
режим.

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
расположение окон инструментов, позиции и выбор документов. `LierneFileStateStorage` заменяет
каждый файл атомарно. Для другого хранилища реализуйте `ILierneStateStorage`.

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
