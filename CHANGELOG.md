## [0.9.2] - 2026-08-26
### Added
- Automatic update checking on startup from GitHub releases.
- Seamless executable hot-swap replacement and automatic application restart upon updating.
- Theme persistence: `DarkMode` is now saved to `config.json` on exit and restored on startup.
- `CheckForUpdates` configuration setting to toggle automatic update checks.
- Automatic `config.json` schema migration to safely add new configuration options on startup without resetting custom settings.

## [0.9.1] - 2026-08-13
### Added
- Default command mappings for `openshock` and `pishock` events

### Fixed
- Commands with zero intensity or zero duration are now silently skipped instead of firing
- Command duration is now capped at a maximum of 10,000 ms (10 seconds)

## [0.9.0] - 2026-08-12
### Added
- Initial release