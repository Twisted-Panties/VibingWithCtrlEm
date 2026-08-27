## [0.9.2] - 2026-08-26
### Added
- Automatic update checking on startup from GitHub releases.
- In-app update dialog showing version comparison, changelog, and live download progress.
- Safe executable hot-swap replacement and automatic application restart.
- `CheckForUpdates` configuration setting in `config.json` with automatic schema migration on startup.

## [0.9.1] - 2026-08-13
### Added
- Default command mappings for `openshock` and `pishock` events

### Fixed
- Commands with zero intensity or zero duration are now silently skipped instead of firing
- Command duration is now capped at a maximum of 10,000 ms (10 seconds)

## [0.9.0] - 2026-08-12
### Added
- Initial release