# TerraNet

Host Terraria worlds from your phone or PC and let friends join from anywhere.

TerraNet runs a Terraria server on your own device and gives it a public address on `play.hacks.win`, so friends can join without port forwarding. It plays like vanilla by default, and every extra is opt-in.

Download: https://terra.hacks.win

## Features

- Android app and a single `TerraNet.exe` for Windows
- Create, import, export and rename worlds, or host the world you already opened in Terraria
- Your own public address and port, reserved for your device
- Vanilla by default, with every TShock option one tap away: PvP, spawn protection, anti-cheat, accounts, UUID login, server-side characters, plugins
- Custom welcome message with Terraria color tags
- Starts with your device and keeps running in the background if you want it to
- Updates itself, English and Spanish, light and dark pixel art themes

## How it works

1. The app runs a dedicated server (TShock on top of OTAPI) on the device. On Android it runs on the .NET Mono runtime with ahead-of-time compiled hot paths.
2. A small agent plugin reports progress, players and state back to the app.
3. The app opens a TLS tunnel to the relay on a VPS. The relay assigns your device a port between 2000 and 2999 and forwards players to it.

## Repository layout

| Path | What it is |
| --- | --- |
| `src/TerraNet.App` | Shared Avalonia UI for every platform |
| `src/TerraNet.Android` | Android head: foreground service, boot receiver, installer |
| `src/TerraNet.Desktop` | Windows head: single exe, tray, in-process server mode |
| `src/TerraNet.Core` | Worlds, settings, server hosting, tunnel client, updates |
| `src/TerraNet.Protocol` | Relay protocol shared by the app and the relay |
| `src/TerraNet.Relay` | Relay service that runs on the VPS |
| `src/TerraNet.ServerAgent` | TShock plugin that talks to the app |
| `src/native/tnhost` | Tiny native launcher for the .NET runtime on Android |
| `tools/TerraNet.PackTool` | Builds the server packs (TShock, runtime, AOT, fixes) |
| `tools/art` | Generates icons, sprites and sounds from code |
| `tools/release` | Builds and publishes a release |
| `web` | Download page and update API (Cloudflare Worker) |

## Building

Requirements: .NET 10 SDK, the Android workload, Android NDK (for the native launcher and AOT), Python 3 with Pillow (art), Node.js (web).

```sh
dotnet run --project tools/TerraNet.PackTool -c Release -- android-arm64
dotnet run --project tools/TerraNet.PackTool -c Release -- win-x64
dotnet publish src/TerraNet.Desktop -c Release
dotnet publish src/TerraNet.Android -c Release
```

Releases are built and published with `tools/release/release.sh <version>`.

## Credits

TerraNet stands on the shoulders of these projects:

- [Terraria](https://terraria.org) by Re-Logic. TerraNet is not affiliated with or endorsed by Re-Logic. You need your own copy of Terraria to play.
- [TShock](https://github.com/Pryaxis/TShock) and the TerrariaServer API by the TShock team (Pryaxis), GPL-3.0
- [OTAPI / Open Terraria API](https://github.com/SignatureBeef/Open-Terraria-API) by SignatureBeef and contributors, GPL-3.0
- [MonoMod](https://github.com/MonoMod/MonoMod), MIT, used by OTAPI
- [.NET](https://github.com/dotnet/runtime) and the Mono runtime by Microsoft and the .NET Foundation, MIT
- [Avalonia UI](https://github.com/AvaloniaUI/Avalonia), MIT
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), MIT
- [Mono.Cecil](https://github.com/jbevain/cecil), MIT
- [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw), Apache-2.0, and [SQLite](https://sqlite.org), public domain
- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET), MIT, shipped with TShock
- [SharpCompress](https://github.com/adamhathcock/sharpcompress), MIT
- [OpenSSL](https://www.openssl.org), Apache-2.0, packaged by [Termux](https://termux.dev)
- [Pixelify Sans](https://github.com/eifetx/Pixelify-Sans) font, SIL Open Font License 1.1
- [Pillow](https://python-pillow.org) for the art tools, [Cloudflare Workers](https://workers.cloudflare.com) for the website

All sprites, icons and sounds in `art/` are drawn and synthesized by code in this repository.

## License

TerraNet is free software under the [GNU General Public License v3.0](LICENSE), the same license as TShock and OTAPI. Third-party components keep their own licenses.

---

## En español

TerraNet te deja abrir mundos de Terraria desde tu celular o PC y que tus amigos entren desde cualquier lugar por `play.hacks.win`, sin abrir puertos. Por defecto se juega como vanilla y todo lo demás es opcional. Descarga en https://terra.hacks.win. Es software libre bajo la licencia GPL-3.0, igual que TShock y OTAPI. No está afiliado a Re-Logic.
