# Композиция плагинов

[English](../en/plugins.md)

Хост выбирает плагины при запуске. Каждый плагин реализует `ILiernePlugin`, объявляет стабильный
идентификатор и зависимости и регистрирует вклады через `ILiernePluginBuilder`. Метод
`AddLierne` вызывает `Configure` в порядке зависимостей и создаёт общий каталог.
Сервисы выполнения, в том числе команды, документы, настройки и наблюдаемый контекст,
относятся к текущей области приложения.

## Добавьте документ и команду

Этот плагин открывает документ по команде. Добавьте его в хост через
`options.AddPlugin<WelcomePlugin>()`. Компонент `WelcomeDocument` получает экземпляр
документа и сервисы области через `Context`.

```csharp
using Lierne;
using Microsoft.Extensions.DependencyInjection;

public sealed class WelcomePlugin : ILiernePlugin
{
    private static readonly DocumentTypeId Welcome = new("sample.welcome");
    private static readonly CommandId Open = new("sample.welcome.open");
    private static readonly RibbonGroupId Group = new("sample.welcome.ribbon");

    public PluginId Id => new("sample.welcome");
    public StringKey DisplayName => StringKey.Literal("Welcome");
    public IReadOnlyList<PluginId> DependsOn => [];

    public void Configure(ILiernePluginBuilder builder)
    {
        builder.AddDocument(new DocumentDescriptor(
            Welcome, StringKey.Literal("Welcome"), null, typeof(WelcomeDocument)));

        builder.AddCommand(new CommandDescriptor(Open, StringKey.Literal("Open welcome")),
            static (context, _) =>
            {
                var documents = context.Services.GetRequiredService<ILierneDocumentService>();
                var result = documents.Open(Welcome);
                return Task.FromResult(result.Success
                    ? LierneCommandResult.Ok(result.Data)
                    : LierneCommandResult.Fail(result.ErrorCode!, result.ErrorMessage!));
            });

        builder.AddRibbonGroup(new RibbonGroupDescriptor(Group, DisplayName));
        builder.AddRibbonItem(new RibbonItemDescriptor(
            new RibbonItemId("sample.welcome.open.ribbon"), Group, Open));
    }
}
```

```razor
@inherits Lierne.LierneDocumentComponent

<p>@Context.Localizer.Get(Context.Document.Title)</p>
```

## Выберите вклад

| Задача | Регистрация |
| --- | --- |
| Открываемый документ или окно инструментов | `AddDocument` или `AddToolWindow` |
| Команда и сочетание клавиш | `AddCommand` с `CommandDescriptor` |
| Пункт ленты или меню, который вызывает команду | `AddRibbonItem` или `AddMenuItem` |
| Своя панель команд, элемент строки состояния или содержимое оболочки | `AddToolbar`, `AddStatusBarItem` или `AddShellContent` |
| Страница параметров и типизированные настройки | `AddSettingsPage` и `AddSetting` |
| Переводы или секция импорта и экспорта настроек | `AddStrings` или `AddSettingsTransfer` |

Окно инструментов наследуется от `LierneToolWindowComponent` и может располагаться слева,
справа или внизу. Документ наследуется от `LierneDocumentComponent`, его можно перемещать между
центром и боковыми областями. Оба компонента получают сервисы текущей области через свой `Context`.

Используйте `StringKey.Literal` для постоянного текста или подключите каталог ресурсов через
`AddStrings(new LierneStringCatalog(namespace, resourceManager))`. Нейтральный файл ресурсов
служит запасным вариантом перевода. Хост может зарегистрировать собственный `ILierneIconResolver`
для преобразования ключей в иконки Flare.

Сохраняйте идентификаторы между версиями. Каталог сообщает о дубликатах, отсутствующих
зависимостях, циклах и неверных ссылках на команды и группы через `LierneCompositionException`.
Команда может объявить тип аргумента и предикат `CanExecute`. Обычные пункты меню и ленты
вызывают команды без аргументов. Для команды с аргументом подключите свой компонент.
