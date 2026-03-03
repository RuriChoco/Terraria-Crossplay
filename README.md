# What is Crossplay?

![Build Crossplay Plugin](https://img.shields.io/github/actions/workflow/status/RuriChoco/Terraria-Crossplay/dotnet-desktop.yml?style=flat-square) [![GitHub Release](https://img.shields.io/github/v/release/RuriChoco/Terraria-Crossplay?style=flat-square)](https://github.com/RuriChoco/Terraria-Crossplay/releases/latest) [![GitHub Downloads](https://img.shields.io/github/downloads/RuriChoco/Terraria-Crossplay/total?style=flat-square)](https://github.com/RuriChoco/Terraria-Crossplay/releases)

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

### Linux / macOS Users

If you see a `Could not locate clrjit library` error, you must launch the server using the system .NET runtime instead of the standalone executable. Run this command in your server folder:

```bash
DOTNET_ROLL_FORWARD=Major dotnet TShock.Server.dll
```

## Configuration
The plugin creates a `Crossplay.json` file in your `tshock` folder.

```json
{
  "support_journey_clients": false,
  "debug_mode": false,
  "whitelisted_projectiles": [ 33 ],
  "enable_item_limits": true,
  "max_dropped_items": 200,
  "item_despawn_seconds": 180,
  "enable_version_check_command": true,
  "show_startup_banner": true,
  "enable_npc_buff_fix": true
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
| `/crossplay clear` | `crossplay.clear` | Manually removes all dropped items from the world. |
| `/crossplay version <player>` | `crossplay.check` | Checks the client version of a connected player. |
| `/crossplay status` | `crossplay.settings` | Displays the current status of all crossplay features. |
| `/crossplay resetconfig` | `crossplay.settings` | Resets the configuration file to its default values. |
| N/A | `crossplay.bypass` | **Required** for players to benefit from the projectile bypass fixes. |

## Bugs & Issues
Bugs or other issues with this plugin should be reported as an issue to this page. Feel free to open a ticket!

## Credits
This plugin was created by **Moneylover3246**.

This project was made using Gemini and tested working.
