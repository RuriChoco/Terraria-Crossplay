# What is Crossplay?
![Build Crossplay Plugin](https://github.com/RuriChoco/Terraria-Crossplay/actions/workflows/dotnet-desktop.yml/badge.svg) [![Nightly Release](https://img.shields.io/github/actions/workflow/status/RuriChoco/Terraria-Crossplay/dotnet-desktop.yml?branch=main&label=Nightly%20Release)](https://github.com/RuriChoco/Terraria-Crossplay/releases/tag/nightly) [![GitHub Release](https://img.shields.io/github/v/release/RuriChoco/Terraria-Crossplay)](https://github.com/RuriChoco/Terraria-Crossplay/releases/latest)

Crossplay allows for cross-platform play between all 1.4.5+ versions, ultimately closing the gap between Terraria on mobile and PC devices. This plugin works by modifying incoming packets sent from the client (and outgoing packets sent from the server) to match whatever game version the packets are being sent to. 

It also includes built-in fixes for common anti-cheat (Bouncer) false positives experienced by crossplay clients, such as the Harpoon glitch.

## Supported Versions
- v1.4.5
- v1.4.5.1
- v1.4.5.2
- v1.4.5.3
- v1.4.5.4
- v1.4.5.5
- v1.4.5.6

## Features
- **Protocol Translation**: Seamlessly connects clients on different minor versions.
- **Bouncer Bypass**: Fixes "Added buff to NPC abnormally" (Automatic) and specific projectile kicks like the Harpoon (Permission-based).
- **Journey Mode Support**: Configurable support for Journey mode characters on non-Journey servers.

## Installation
Installation is very easy; Simply insert the plugin file (`Crossplay.dll`) into the `ServerPlugins` folder of your TShock install.

## Configuration
The plugin creates a `Crossplay.json` file in your `tshock` folder.

```json
{
  "support_journey_clients": false,
  "debug_mode": false,
  "whitelisted_projectiles": [
    33
  ]
}
```

| Option | Type | Description |
| :--- | :--- | :--- |
| `support_journey_clients` | `bool` | If true, allows Journey mode characters to join. |
| `debug_mode` | `bool` | Enables detailed logging to the console. |
| `whitelisted_projectiles` | `List<int>` | List of projectile IDs to bypass Bouncer checks for. Default is `[33]` (Harpoon). |

## Commands & Permissions

| Command | Permission | Description |
| :--- | :--- | :--- |
| `/crossplay reload` | `crossplay.settings` | Reloads the configuration file from disk. |
| N/A | `crossplay.bypass` | **Required** for players to benefit from the projectile bypass fixes. |

## Bugs & Issues
Bugs or other issues with this plugin should be reported as an issue to this page. Feel free to open a ticket!

## Credits
This plugin was created by **Moneylover3246**.

This project was made using Gemini and tested working.
