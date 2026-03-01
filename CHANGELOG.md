# Changelog

## v2.6.1

### Changed
- Optimized packet handling to reduce CPU usage and rubber banding.
- Further optimized `OnSendToClient` to reduce CPU usage when players are connected.
- Replaced `MemoryMarshal` with `BitConverter` in `NetModuleHandler` for slight performance gain.
- Fixed buffer overflow issues in connection handling.
- Added Gemini attribution to README.

## v2.6

### Added

- **Configurable Projectile Whitelist**: You can now configure which projectiles bypass Bouncer checks in `Crossplay.json` (Default: Harpoon).
- **Reload Command**: Added `/crossplay reload` to reload settings on the fly (Permission: `crossplay.settings`).
- **Permission System**: Added `crossplay.bypass` permission. Players need this to use the projectile fixes.
- **Console Logging**: Added warnings when unauthorized players try to use bypassed projectiles.

### Changed

- Updated internal protocol handling to support the new configuration options.
