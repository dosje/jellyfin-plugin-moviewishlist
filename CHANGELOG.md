# Changelog

All notable changes to Movie Wishlist DVR are documented in this file.

## [1.0.1.0] - 2026-06-21

### Fixed
- Changed target framework from `net8.0` to `net9.0` — Jellyfin 10.9.11 NuGet packages only ship net9.0 reference assemblies; the net8.0 fallback was missing `IPluginServiceRegistrator` and several other types.
- `IPluginServiceRegistrator` namespace corrected to `MediaBrowser.Controller.Plugins` (was `MediaBrowser.Common.Plugins`); method signature updated to accept `IServerApplicationHost` instead of `IServiceProvider`.
- Removed `INotificationManager` usage from `EpgScanTask` — `MediaBrowser.Controller.Notifications` is not exposed in the Jellyfin 10.9.11 NuGet packages; scheduling and match notifications are now written to the server log.
- Fixed `InternalItemsQuery` missing using — added `MediaBrowser.Controller.Entities` import to `EpgMatchingService`.
- Fixed `BaseItem.StartDate` compilation error — `StartDate` lives on `LiveTvProgram` via `IHasStartDate`, not on `BaseItem`; code now casts via `program as IHasStartDate`.
- Fixed EPG confidence logic — when the wishlist item has a production year but the EPG program does not, the match is now correctly marked `Uncertain` (previously incorrectly `Confident`).
- Added `<ExcludeAssets>runtime</ExcludeAssets>` to Jellyfin package references in the plugin project to prevent bundling server-provided assemblies in the plugin output.
- Removed `ExcludeAssets>runtime` from the test project — the test runner is standalone and requires the Jellyfin DLLs in the output directory.
- Made `TmdbService` secondary constructor `public` so the test project can access it without `InternalsVisibleTo`.
- Fixed `EpgMatchingServiceTests` to use `LiveTvProgram` instead of `Episode` for fake EPG programs — only `LiveTvProgram` implements `IHasStartDate` and carries `StartDate`.
- Updated CI/CD workflows from .NET 8 to .NET 9.

## [1.0.0.0] - 2026-06-20

### Added
- Multi-user personal watchlists — each Jellyfin user maintains their own independent list of wishlisted films.
- TMDB-powered movie search and details — search by title with full metadata, poster images, and release year fetched from The Movie Database API.
- Automatic EPG scan with configurable interval — the plugin polls the Live TV Electronic Programme Guide on a user-defined schedule and matches broadcast listings against wishlisted titles.
- HD-preferred recording scheduling — when both SD and HD airings are available, the scheduler automatically prefers the HD broadcast.
- Duplicate prevention — checks both the existing Jellyfin media library and the active DVR timer queue before scheduling to avoid redundant recordings.
- Uncertain match and tuner conflict user prompts — when a title match confidence is below threshold, or all tuners are occupied, the user is prompted in-app to confirm or defer the recording.
- In-app Jellyfin notifications — users receive Jellyfin dashboard notifications when a wishlist match is found, when a recording is scheduled, and when scheduling fails.
- Activity log — all plugin actions (scans, matches, scheduled timers, errors) are written to the Jellyfin activity log for audit and troubleshooting.
