# DED Launcher

Лаунчер Minecraft для Windows на C# (.NET 8, WPF) + CmlLib.Core.

## Структура

- `MinecraftLauncher/` — сам лаунчер (WPF)
- `CmlLib.Core-master/` — форк движка CmlLib.Core (MIT)

## Сборка

```
dotnet publish MinecraftLauncher\MinecraftLauncher\MinecraftLauncher.csproj -c Release -r win-x64 --self-contained true
```

Результат: `MinecraftLauncher\MinecraftLauncher\bin\Release\net8.0-windows\win-x64\publish\`

## Возможности

- Версии: релизы, снапшоты, старые (1.16.5 → 1.21.11)
- Загрузчики: Vanilla, Fabric, Forge, OptiFine
- Моды: Modrinth + CurseForge, включение/выключение
- Ресурспаки и шейдеры
- Профили с изоляцией
- Друзья: чат, группы, приглашения на сервер
- Скин и плащ (встроенный Fabric-мод)
- Microsoft + оффлайн вход

## Обновления

Версии публикуются в Telegram-канале [@NeiroDEDmod](https://t.me/NeiroDEDmod). Формат поста: `#update 2.0.0` + ссылка на архив.
