# Jellyfin Movie Wishlist DVR — Plugin Specification

## Concept Summary

A Jellyfin-native plugin that provides a beautiful media discovery and wishlist interface (similar to Seerr / Overseerr), but instead of sending requests to Radarr for downloading, it monitors Jellyfin's built-in Live TV EPG and automatically schedules a DVR recording when a wishlisted film appears in the broadcast schedule.

Everything is self-contained inside Jellyfin. No Radarr, no Sonarr, no TVHeadend, no external middleware required — only a TMDB API key. Each user has their own personal watchlist, in the style of Seerr.

---

## Background & Why This Does Not Already Exist

Jellyfin has a fully functional, free, built-in Live TV and DVR system. It supports tuners such as HDHomeRun, EPG data via XMLTV or Schedules Direct, and can record broadcasts to local storage. However, there is no way to say "record this film whenever it airs" — the user must manually browse the EPG and schedule each recording.

Seerr (github.com/seerr-team/seerr) solves the equivalent problem for downloading: users browse a polished TMDB-powered interface, add requests, and Radarr / Sonarr handle the rest automatically. No equivalent exists for broadcast DVR recording inside Jellyfin.

The only existing tool close to this concept is `tvheadend-movie-wishlist` (github.com/skittlesvampir/tvheadend-movie-wishlist), a Python script that matches a TMDB list against a TVHeadend EPG and schedules recordings. However, it:

- Requires TVHeadend as a separate backend (not Jellyfin-native)
- Is a bare Python script with no UI
- Has only 1 GitHub star, no releases, and a known API bug limiting it to 20 films
- Has no active maintenance

There is **no** Jellyfin plugin that does this. The official Jellyfin plugin catalogue, the awesome-jellyfin community list, and broad GitHub searches all confirm this is an unfilled gap.

---

## Target Environment

- **EPG provider:** EPG.best (the user's configured guide-data source). Title matching, year tolerance, and channel/HD detection should be tuned against EPG.best's data formatting and channel naming conventions.
- **Tuner:** A standard Jellyfin-supported Live TV setup (e.g. HDHomeRun or IPTV) with Live TV and EPG.best already configured in Jellyfin before the plugin is used.
- **Users:** Multiple household users, each with their own personal watchlist (see Multi-User Model below).

---

## How It Appears in Jellyfin

The plugin is **not** a separate app, a new URL, or a new Live TV channel. It lives entirely inside the existing Jellyfin interface.

**In the sidebar (visible to permitted users):**

```
🎬  Movie Wishlist
```

Clicking it opens the full custom UI — search, browse, and watchlist — all within Jellyfin's existing look and feel.

**In the admin dashboard:**

```
Plugins → Movie Wishlist → Settings
```

Where the admin configures the TMDB API key, scan interval, and other options.

The closest real-world analogy is the **Sky Wishlist** feature on a Sky box: browse upcoming films, add them to a wishlist, and the box records them automatically when they air — except this lives inside Jellyfin and is fully free and open source.

---

## User Flow

1. User clicks **Movie Wishlist** in the Jellyfin sidebar.
2. A page opens — like a mini Seerr — with a search bar and a poster grid.
3. They search for a film and see rich TMDB-powered cards with posters, year, and rating.
4. They click a card to open a detail view with full backdrop image, synopsis, cast, genres, and runtime.
5. They click **Add to Watchlist**.
6. They never leave Jellyfin.

Everything after this is automatic and invisible to the user.

---

## How It Works in the Background

Completely invisible to the user:

1. Every few hours the plugin wakes up (a scheduled background task).
2. It pulls the current EPG from Jellyfin Live TV (next 7–14 days, depending on the EPG provider).
3. It compares every film on the watchlist against upcoming broadcasts.
4. On a match, it calls Jellyfin's internal API to schedule a recording.
5. It sends the user a Jellyfin notification, e.g. "Inception is airing on BBC One at 9pm Saturday — recording scheduled."

---

## Key Behaviour 1 — Films Not Yet on the EPG

This is handled by a **persistent watchlist plus a recurring scan**.

- A film is added to the watchlist even if it is not currently in the EPG.
- The plugin keeps checking the EPG every few hours, indefinitely, until the film appears.
- When a broadcaster eventually schedules the film and it enters the EPG window, the next scan finds the match and schedules the recording automatically.
- The film stays on the watchlist until it is recorded or manually removed — there is **no timeout**. It will wait weeks or months if necessary.

The only limitation is that EPG data typically only covers 7–14 days ahead, so the plugin cannot "see" a broadcast until it enters that window. Because it scans on a recurring schedule, it will catch the film as soon as it appears.

---

## Key Behaviour 2 — Film Already on the Server

Before doing anything, the plugin checks the existing Jellyfin library using `ILibraryManager`.

Logic per scan cycle:

```
For each film on the watchlist:
  → Check Jellyfin library — does this film already exist?
  → Yes: skip entirely, mark as "Already in Library", optionally remove from watchlist
  → No:  check the EPG for an upcoming broadcast
```

This means:

- Films already owned (via download, rip, or previous recording) are automatically skipped.
- No duplicate recordings.
- No wasted disk space.
- The check runs every scan cycle, so a film added to the library manually between scans will be skipped on the next scan.

---

## Watchlist Status Model

Each film on the watchlist has one of the following statuses:

| Status | Meaning |
|---|---|
| **Watching EPG** | Added to watchlist, not yet seen in EPG, still checking |
| **Scheduled** | Found in EPG, recording timer created in Jellyfin |
| **Recorded** | Recording completed, file exists in Jellyfin |
| **Already in Library** | Film was already on the server, skipped |
| **Missed** | Was scheduled but the recording failed — optional retry |
| **Needs Confirmation** | An uncertain match or a tuner conflict requires the user to decide |

---

## Multi-User Model

Each user has their own personal watchlist, in the style of Seerr.

- A user only sees and manages their own watchlist; they cannot add to or remove from another user's list.
- Watchlist entries are keyed by Jellyfin user ID in the database.
- The background scan processes every user's watchlist on each cycle.
- If two different users wishlist the same film, the plugin should schedule a **single** recording and satisfy both users' entries from it (no duplicate recording), notifying both.
- Notifications are sent to the owning user(s) of the matched film.
- The admin settings page (TMDB key, scan interval, etc.) remains admin-only and global. Each user only manages their own watchlist, not plugin settings.

---

## HD Preference and Multiple Airings

When a film airs more than once within the EPG window:

- **HD is always preferred.** If the same film is available on both an HD and an SD channel, schedule the HD broadcast.
- HD vs SD is determined from the EPG.best channel data / channel naming (e.g. an "HD" suffix or equivalent flag). The matching logic should detect this robustly given EPG.best conventions.
- If only SD airings exist, record SD rather than miss the film.
- Among multiple HD airings, prefer the earliest upcoming slot (so the film is captured sooner), unless it conflicts with the tuner (see Recording Conflicts below).

---

## Recording Conflicts

If the tuner cannot record all matched films at once (e.g. several wishlist films air at overlapping times and exceed the available tuners), the plugin must **not** silently drop a recording.

- When a conflict is detected, **ask the user what to do** rather than deciding automatically.
- The user is notified of the clash and presented with the conflicting films / slots so they can choose which to record.
- Where a non-conflicting alternative airing of a film exists later in the EPG window, the plugin may offer that as an option (e.g. "record the Sunday HD airing instead").
- Any film not resolved by the user's choice remains on the watchlist and continues to be watched for future airings.

---

## EPG Data Quality Fallback

EPG.best data is generally good, but title or year mismatches can still occur.

- When the plugin finds a **likely but uncertain** match (e.g. title matches but year is missing or outside tolerance, or an ambiguous duplicate title), it should **not** auto-schedule. Instead it should flag the match and **ask the user to confirm** before scheduling.
- When EPG data is missing expected fields needed to make a confident decision, surface this to the user rather than guessing.
- Confident matches (title + year within tolerance, unambiguous) schedule automatically as normal.

---

## Tech Stack

- **Language:** C# (.NET) — required for Jellyfin plugins
- **Plugin framework:** Jellyfin Plugin API using `IPlugin`, `IHasWebPages`, `IScheduledTask`
- **Frontend:** HTML, CSS, JavaScript served via Jellyfin's plugin web pages system
- **Database:** SQLite via Jellyfin's existing plugin data storage
- **Starting point:** github.com/jellyfin/jellyfin-plugin-template

---

## Jellyfin APIs To Use

- `ILiveTvManager` — query the EPG for upcoming programme listings
- `ITimerService` — create DVR recording timers
- `ILibraryManager` — check whether a film already exists in the library so it can be skipped
- `INotificationManager` — notify the user when a recording is scheduled
- `IScheduledTaskWorker` — run periodic background EPG checks
- `IApplicationPaths` — store plugin data

---

## External API — TMDB

TMDB (themoviedb.org) provides all film metadata, posters, backdrops, and ratings. It is the same data source used by Seerr, Radarr, and Jellyfin itself.

- Search: `https://api.themoviedb.org/3/search/movie`
- Details: `https://api.themoviedb.org/3/movie/{id}`
- Images: `https://image.tmdb.org/t/p/w500{poster_path}`
- The user supplies their own TMDB API key in plugin settings.

---

## EPG Matching Logic

1. Pull upcoming EPG entries from Jellyfin Live TV (EPG.best) for the next 7–14 days.
2. Normalize titles: strip punctuation, lowercase, and handle "The" and "A" prefix variants. Tune against EPG.best's title formatting.
3. Match watchlist films against the EPG by normalized title **and** production year, with a tolerance of ±1 year.
4. Classify each match:
   - **Confident** (title + year within tolerance, unambiguous) → proceed to scheduling.
   - **Uncertain** (missing/out-of-tolerance year, or ambiguous duplicate title) → flag and ask the user to confirm before scheduling.
5. For confident matches, select the best airing: **HD preferred over SD**, then earliest upcoming slot, subject to tuner availability.
6. Check for tuner conflicts. If a conflict exists, ask the user to choose rather than dropping any recording.
7. Call Jellyfin's timer API to schedule the chosen recording.
8. Mark the film as **Scheduled** in the plugin database and prevent duplicate scheduling. If multiple users wishlisted the same film, schedule once and link all their entries to it.
9. After the recording completes, check the Jellyfin library; if found, mark as **Recorded** and optionally remove from the watchlist(s).

---

## Pages and UI

Design inspiration is Seerr (seerr.dev) — dark-themed, card-based, clean poster-grid layout.

### Page 1 — Discover / Search
- Search bar that queries TMDB in real time.
- Results shown as movie cards with poster, title, year, and rating.
- Clicking a card opens a detail view with full backdrop image, synopsis, cast, genres, and runtime.
- "Add to Watchlist" button on the detail view.

### Page 2 — Watchlist
- The **current user's own** poster grid of watchlisted films (each user sees only their own list).
- Status badge on each card: Watching EPG / Scheduled / Recorded / Already in Library / Missed / Needs Confirmation.
- Scheduled date and time (and channel, with HD indicated) shown when status is Scheduled.
- "Remove from watchlist" button on each card.

### Page 2b — Confirmations & Conflicts (per user)
- Surfaces items needing user input:
  - **Uncertain matches** — show the candidate EPG entry and ask "Is this the right film?" with confirm / reject.
  - **Tuner conflicts** — show the clashing films and slots, and let the user choose which to record (and offer any later alternative airing where available).
- Resolving an item updates its status; unresolved films stay on the watchlist and keep being watched.

### Page 3 — Settings (admin only)
- TMDB API key input.
- EPG scan interval dropdown: 1hr / 3hr / 6hr / 12hr / 24hr.
- Days ahead to scan EPG (default 7).
- Toggle: Remove from watchlist after recorded (default on).
- Toggle: Skip if film already exists in Jellyfin library (default on).
- Notification preference toggle.
- "Scan Now" button to manually trigger an EPG check.

### Page 4 — Activity Log (admin only)
- List of recent EPG scans with timestamps and results.
- List of matches found and recordings scheduled.

---

## V1 Scope

- Movies only — no TV series.
- Per-user watchlists (multi-user, Seerr-style) — each household user manages their own list.
- HD-preferred recording, falling back to SD only when no HD airing exists.
- User-prompted handling of tuner conflicts and uncertain EPG matches (no silent drops, no wrong-film auto-records).
- Jellyfin built-in DVR only — no Radarr, Sonarr, or TVHeadend integration.
- Jellyfin web UI plugin — no separate mobile app.
- Requires Live TV and EPG.best to already be configured in Jellyfin.

---

## V2 Ideas (not required now)

- TV show and series support (match series episodes via EPG).
- Smarter conflict resolution (automatic priority rules, so the user is asked less often).
- Trakt / Letterboxd / external watchlist import.
- Notification when a film aired but the recording was missed, with automatic retry on the next airing.

---

## Reference Links

| Project | URL | Purpose |
|---|---|---|
| Seerr | github.com/seerr-team/seerr | UI / UX inspiration, request-management pattern |
| tvheadend-movie-wishlist | github.com/skittlesvampir/tvheadend-movie-wishlist | Concept inspiration (TVHeadend version of this idea) |
| Jellyfin plugin template | github.com/jellyfin/jellyfin-plugin-template | Starting point for plugin structure |
| Jellyfin plugin docs | jellyfin.org/docs/general/server/plugins | Plugin development reference |
| TMDB API docs | developers.themoviedb.org | Film metadata and images |

---

## Build Request

Scaffold the full plugin including:

- The C# backend.
- The plugin manifest.
- The scheduled task for EPG scanning.
- The SQLite data model for the watchlist (with the status model above).
- The library-existence check to avoid duplicate recordings.
- The persistent recurring-scan logic so films not yet in the EPG are recorded when they later appear.
- All four frontend pages, with CSS styled to match a dark, Seerr-like aesthetic.

The plugin should be structured ready to publish as a Jellyfin plugin repository.
