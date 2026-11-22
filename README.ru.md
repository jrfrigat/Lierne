<p align="center">
  <img src="assets/banner.svg" alt="Lierne" width="860">
</p>

# Lierne

<p align="center"><a href="README.md">English</a> · <strong>Русский</strong></p>

[![CI](https://github.com/jrfrigat/Lierne/actions/workflows/ci.yml/badge.svg)](https://github.com/jrfrigat/Lierne/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Lierne.svg)](https://www.nuget.org/packages/Lierne/)
[![Загрузки NuGet](https://img.shields.io/nuget/dt/Lierne.svg)](https://www.nuget.org/packages/Lierne/)
[![.NET 11](https://img.shields.io/badge/.NET-11-512BD4)](https://dotnet.microsoft.com/)
[![Лицензия MIT](https://img.shields.io/badge/license-MIT-267A62)](LICENSE)

Lierne - каркас рабочих пространств для доменных IDE на .NET 11, Blazor и
[Flare](https://github.com/jrfrigat/Flare). Он не содержит редактор кода: документы - доменные
объекты с подключаемыми представлениями. Плагины объявляют документы, окна инструментов, команды
и настройки. Lierne проверяет их и собирает общую рабочую область. Первое приложение на
фреймворке - Quas, IDE для работы с базами данных.

- **Композиция при запуске.** Регистрируйте плагины и объявляйте их зависимости в хосте.
  Каталог сообщает о дубликатах идентификаторов, отсутствующих зависимостях, циклах и ошибочных ссылках до запуска интерфейса.
- **Общие команды.** Вызывайте одни и те же команды из меню, ленты и с клавиатуры.
  Добавляйте переводы и настройки в плагины, которые их используют.
- **Восстановление рабочей области.** Подключите браузерное или файловое хранилище для
  сохранения настроек, расположения панелей и документов с поддержкой восстановления.

## Установка

```sh
dotnet add package Lierne
dotnet add package Flare.Blazor
# добавьте один или несколько пакетов темы Flare на ваш выбор
```

Пакет `Lierne` подключает компоненты IDE Flare. Добавьте `Flare.Blazor` и один или несколько
пакетов темы Flare на ваш выбор, если хост ещё их не использует: приложение подключает свои темы
и палитры, а настройки Themes в Lierne только выбирают среди них. Lierne не поставляет тему
и не требует конкретную.

## Быстрый старт

Основной пакет NuGet - [Lierne](https://www.nuget.org/packages/Lierne/). При работе с исходниками
добавьте ссылку на проект `src/Lierne/Lierne.csproj`.

Запустите интерактивный пример с .NET 11 SDK.

```sh
dotnet run --project samples/Lierne.SampleIde
```

Выберите **New note**, введите текст и перезагрузите страницу. Заметка восстановится.
Один плагин также добавляет окно инструментов, сочетание клавиш и страницу настроек.

## Документация

- [Подключение Blazor-хоста](docs/articles/ru/getting-started.md)
- [Композиция плагинов](docs/articles/ru/plugins.md)
- [Настройка и сохранение рабочей области](docs/articles/ru/workspace.md)
- [Архитектурная справка](docs/architecture.md)

## Лицензия

[MIT](LICENSE)
