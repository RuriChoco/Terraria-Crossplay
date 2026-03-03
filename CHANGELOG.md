# Changelog

## v2.6.1

### Changed
- Optimized packet handling to reduce CPU usage and rubber banding.
- Further optimized `OnSendToClient` to reduce CPU usage when players are connected.
- Replaced `MemoryMarshal` with `BitConverter` in `NetModuleHandler` for slight performance gain.
- Fixed buffer overflow issues in connection handling.
- Fixed "ghost items" and pickup issues for mobile players by filtering unsupported item drops.
- Added Item Limiter to reduce server lag (configurable max items and despawn time).
- Added `/crossplay clear` command to manually remove all dropped items.
- Added `/crossplay version` command and granular permissions (`crossplay.clear`, `crossplay.check`).
- Added configuration options for `show_startup_banner` and `enable_npc_buff_fix`.
- Added `/crossplay resetconfig` command to restore the default configuration.
- Optimized item limiter to reduce memory allocations.
- Added Gemini attribution to README.

## v2.6

### Added

- **Configurable Projectile Whitelist**: You can now configure which projectiles bypass Bouncer checks in `Crossplay.json` (Default: Harpoon).
- **Reload Command**: Added `/crossplay reload` to reload settings on the fly (Permission: `crossplay.settings`).
- **Permission System**: Added `crossplay.bypass` permission. Players need this to use the projectile fixes.
- **Console Logging**: Added warnings when unauthorized players try to use bypassed projectiles.

### Changed

- Updated internal protocol handling to support the new configuration options.
