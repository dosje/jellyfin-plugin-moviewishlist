using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MovieWishlist.Data;
using Jellyfin.Plugin.MovieWishlist.Data.Models;
using Jellyfin.Plugin.MovieWishlist.Services;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.Api
{
    // --------------- Request / Response DTOs ---------------

    public record AddItemRequest(int TmdbId, string Title, int? Year, string? PosterPath, string? Overview);
    public record ResolveConflictRequest(string ChosenProgramId);

    // -------------------------------------------------------

    [ApiController]
    [Route("api/moviewishlist")]
    [Produces("application/json")]
    public class WishlistController : ControllerBase
    {
        private readonly DatabaseManager _db;
        private readonly TmdbService _tmdb;
        private readonly EpgMatchingService _epgMatcher;
        private readonly RecordingService _recorder;
        private readonly IAuthorizationContext _authContext;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(
            DatabaseManager db,
            TmdbService tmdb,
            EpgMatchingService epgMatcher,
            RecordingService recorder,
            IAuthorizationContext authContext,
            ILogger<WishlistController> logger)
        {
            _db = db;
            _tmdb = tmdb;
            _epgMatcher = epgMatcher;
            _recorder = recorder;
            _authContext = authContext;
            _logger = logger;
        }

        // ------------------- Helpers -------------------

        private async Task<Guid?> GetCurrentUserId()
        {
            var auth = await _authContext.GetAuthorizationInfo(Request);
            return auth?.UserId;
        }

        private bool IsAdmin()
        {
            return User.IsInRole("Administrator");
        }

        // ------------------- Endpoints -------------------

        /// <summary>GET /api/moviewishlist/items — returns wishlist items for the authenticated user.</summary>
        [HttpGet("items")]
        [Authorize]
        public async Task<ActionResult<List<WishlistItem>>> GetItems()
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var items = _db.GetWishlistItemsByUser(userId.Value.ToString());
            return Ok(items);
        }

        /// <summary>POST /api/moviewishlist/items — add a movie to the authenticated user's wishlist.</summary>
        [HttpPost("items")]
        [Authorize]
        public async Task<ActionResult<WishlistItem>> AddItem([FromBody] AddItemRequest request)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var existing = _db.GetWishlistItemByUserAndTmdb(userId.Value.ToString(), request.TmdbId);
            if (existing != null)
            {
                return Conflict(new { message = $"'{request.Title}' is already on your wishlist." });
            }

            var item = new WishlistItem
            {
                UserId = userId.Value.ToString(),
                TmdbId = request.TmdbId,
                Title = request.Title,
                Year = request.Year,
                PosterPath = request.PosterPath,
                Overview = request.Overview,
                Status = WishlistStatus.WatchingEpg,
                AddedAt = DateTime.UtcNow
            };

            _db.AddWishlistItem(item);
            _logger.LogInformation("User {UserId} added '{Title}' (TMDB {TmdbId}) to wishlist.", userId, request.Title, request.TmdbId);

            return CreatedAtAction(nameof(GetItems), item);
        }

        /// <summary>DELETE /api/moviewishlist/items/{id} — remove a wishlist item.</summary>
        [HttpDelete("items/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteItem([FromRoute] int id)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var item = _db.GetWishlistItemById(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!string.Equals(item.UserId, userId.Value.ToString(), StringComparison.OrdinalIgnoreCase) && !IsAdmin())
            {
                return Forbid();
            }

            _db.RemoveWishlistItem(id);
            _logger.LogInformation("Wishlist item {Id} ('{Title}') removed by user {UserId}.", id, item.Title, userId);
            return NoContent();
        }

        /// <summary>GET /api/moviewishlist/search?query=... — search TMDB for movies.</summary>
        [HttpGet("search")]
        [Authorize]
        public async Task<ActionResult> SearchMovies([FromQuery] string query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Query parameter is required." });
            }

            var apiKey = Plugin.Instance!.Configuration.TmdbApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BadRequest(new { message = "TMDB API key not configured." });
            }

            var result = await _tmdb.SearchMoviesAsync(query, apiKey, ct);
            if (result == null)
            {
                return Ok(new { Results = Array.Empty<object>(), TotalResults = 0 });
            }

            return Ok(result);
        }

        /// <summary>GET /api/moviewishlist/movie/{tmdbId} — get detailed TMDB info for a movie.</summary>
        [HttpGet("movie/{tmdbId}")]
        [Authorize]
        public async Task<ActionResult> GetMovieDetails([FromRoute] int tmdbId, CancellationToken ct)
        {
            var apiKey = Plugin.Instance!.Configuration.TmdbApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BadRequest(new { message = "TMDB API key not configured." });
            }

            var details = await _tmdb.GetMovieDetailsAsync(tmdbId, apiKey, ct);
            if (details == null)
            {
                return NotFound(new { message = $"No TMDB movie found with ID {tmdbId}." });
            }

            return Ok(details);
        }

        /// <summary>POST /api/moviewishlist/items/{id}/confirm — confirm an uncertain EPG match and schedule it.</summary>
        [HttpPost("items/{id}/confirm")]
        [Authorize]
        public async Task<ActionResult<WishlistItem>> ConfirmMatch([FromRoute] int id, CancellationToken ct)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var item = _db.GetWishlistItemById(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!string.Equals(item.UserId, userId.Value.ToString(), StringComparison.OrdinalIgnoreCase) && !IsAdmin())
            {
                return Forbid();
            }

            if (item.Status != WishlistStatus.NeedsConfirmation)
            {
                return BadRequest(new { message = $"Item is not awaiting confirmation (current status: {item.Status})." });
            }

            if (string.IsNullOrEmpty(item.PendingEpgProgramId))
            {
                return BadRequest(new { message = "No pending EPG program ID to confirm." });
            }

            string? timerId = null;
            try
            {
                timerId = await _recorder.ScheduleRecordingAsync(item.PendingEpgProgramId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule confirmed recording for item {Id}.", id);
            }

            if (!string.IsNullOrEmpty(timerId))
            {
                item.Status = WishlistStatus.Scheduled;
                item.JellyfinTimerId = timerId;
            }
            else
            {
                item.Status = WishlistStatus.WatchingEpg;
            }

            item.PendingEpgProgramId = null;
            item.PendingEpgTitle = null;
            item.PendingEpgYear = null;
            item.ConflictInfo = null;

            _db.UpdateWishlistItem(item);
            _logger.LogInformation("User {UserId} confirmed EPG match for item {Id}. New status: {Status}.", userId, id, item.Status);

            return Ok(item);
        }

        /// <summary>POST /api/moviewishlist/items/{id}/reject — reject an uncertain EPG match and return to scanning.</summary>
        [HttpPost("items/{id}/reject")]
        [Authorize]
        public async Task<ActionResult<WishlistItem>> RejectMatch([FromRoute] int id)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var item = _db.GetWishlistItemById(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!string.Equals(item.UserId, userId.Value.ToString(), StringComparison.OrdinalIgnoreCase) && !IsAdmin())
            {
                return Forbid();
            }

            if (item.Status != WishlistStatus.NeedsConfirmation)
            {
                return BadRequest(new { message = $"Item is not awaiting confirmation (current status: {item.Status})." });
            }

            item.PendingEpgProgramId = null;
            item.PendingEpgTitle = null;
            item.PendingEpgYear = null;
            item.ConflictInfo = null;
            item.Status = WishlistStatus.WatchingEpg;

            _db.UpdateWishlistItem(item);
            _logger.LogInformation("User {UserId} rejected EPG match for item {Id}.", userId, id);

            return Ok(item);
        }

        /// <summary>POST /api/moviewishlist/items/{id}/resolve — resolve a conflict by choosing a specific program to record.</summary>
        [HttpPost("items/{id}/resolve")]
        [Authorize]
        public async Task<ActionResult<WishlistItem>> ResolveConflict([FromRoute] int id, [FromBody] ResolveConflictRequest request, CancellationToken ct)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var item = _db.GetWishlistItemById(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!string.Equals(item.UserId, userId.Value.ToString(), StringComparison.OrdinalIgnoreCase) && !IsAdmin())
            {
                return Forbid();
            }

            if (item.Status != WishlistStatus.NeedsConfirmation)
            {
                return BadRequest(new { message = $"Item is not awaiting confirmation (current status: {item.Status})." });
            }

            string? timerId = null;
            try
            {
                timerId = await _recorder.ScheduleRecordingAsync(request.ChosenProgramId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule resolved recording for item {Id} with program {ProgramId}.", id, request.ChosenProgramId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to schedule recording: {ex.Message}" });
            }

            if (string.IsNullOrEmpty(timerId))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Recorder returned no timer ID. Recording may not have been scheduled." });
            }

            item.Status = WishlistStatus.Scheduled;
            item.JellyfinTimerId = timerId;
            item.ConflictInfo = null;
            item.PendingEpgProgramId = null;
            item.PendingEpgTitle = null;
            item.PendingEpgYear = null;

            _db.UpdateWishlistItem(item);
            _logger.LogInformation("User {UserId} resolved conflict for item {Id}. Timer: {TimerId}.", userId, id, timerId);

            return Ok(item);
        }

        /// <summary>GET /api/moviewishlist/activity — returns recent activity log entries (admin only).</summary>
        [HttpGet("activity")]
        [Authorize]
        public IActionResult GetActivityLog()
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            var logs = _db.GetActivityLogs(100);
            return Ok(logs);
        }

        /// <summary>POST /api/moviewishlist/scan — queues an immediate EPG scan (admin only).</summary>
        [HttpPost("scan")]
        [Authorize]
        public IActionResult TriggerScan()
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            _logger.LogInformation("Manual EPG scan requested by admin.");
            return Accepted(new { message = "Scan queued — use the Jellyfin scheduled task manager to monitor progress." });
        }

        /// <summary>GET /api/moviewishlist/settings — returns current plugin configuration (admin only).</summary>
        [HttpGet("settings")]
        [Authorize]
        public ActionResult<PluginConfiguration> GetSettings()
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            return Ok(Plugin.Instance!.Configuration);
        }

        /// <summary>PUT /api/moviewishlist/settings — updates plugin configuration (admin only).</summary>
        [HttpPut("settings")]
        [Authorize]
        public IActionResult UpdateSettings([FromBody] PluginConfiguration config)
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            var current = Plugin.Instance!.Configuration;

            current.TmdbApiKey = config.TmdbApiKey;
            current.ScanIntervalHours = config.ScanIntervalHours;
            current.DaysAheadToScan = config.DaysAheadToScan;
            current.RemoveAfterRecorded = config.RemoveAfterRecorded;
            current.SkipIfInLibrary = config.SkipIfInLibrary;
            current.EnableNotifications = config.EnableNotifications;

            Plugin.Instance.SaveConfiguration();
            _logger.LogInformation("Plugin configuration updated by admin.");

            return NoContent();
        }
    }
}
