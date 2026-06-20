# Changelog

All notable changes to Movie Wishlist DVR are documented in this file.

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
