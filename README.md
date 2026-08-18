# DED Launcher

Minecraft launcher for Windows built with C# (.NET 8, WPF) and CmlLib.Core.

## Structure

- `MinecraftLauncher/` — launcher application (WPF)
- `CmlLib.Core-master/` — fork of the CmlLib.Core engine (MIT)

## Build

```
dotnet publish MinecraftLauncher\MinecraftLauncher\MinecraftLauncher.csproj -c Release -r win-x64 --self-contained true
```

Output: `MinecraftLauncher\MinecraftLauncher\bin\Release\net8.0-windows\win-x64\publish\`

## Features

- Game versions: releases, snapshots, legacy (1.16.5 - 1.21.11)
- Loaders: Vanilla, Fabric, Forge, OptiFine
- Mods: Modrinth + CurseForge, enable/disable
- Resource packs and shaders
- Isolated profiles
- Friends: chat, groups, server invites
- Skin and cape (built-in Fabric mod)
- Microsoft and offline accounts

## Updates

Releases are announced in the Telegram channel [@NeiroDEDmod](https://t.me/NeiroDEDmod). Post format: `#update 2.0.0` + download link.
