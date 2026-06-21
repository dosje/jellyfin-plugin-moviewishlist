using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MovieWishlist.Data;
using Jellyfin.Plugin.MovieWishlist.Data.Models;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.Api
{
    public record AddSubscriptionRequest(string ChannelId, string ChannelName, int DaysOfWeek);
    public record UpdateSubscriptionRequest(int DaysOfWeek);

    [ApiController]
    [Route("api/moviewishlist")]
    [Produces("application/json")]
    public class SubscriptionController : ControllerBase
    {
        private readonly DatabaseManager _db;
        private readonly ILibraryManager _libraryManager;
        private readonly IAuthorizationContext _authContext;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            DatabaseManager db,
            ILibraryManager libraryManager,
            IAuthorizationContext authContext,
            ILogger<SubscriptionController> logger)
        {
            _db = db;
            _libraryManager = libraryManager;
            _authContext = authContext;
            _logger = logger;
        }

        private async Task<Guid?> GetCurrentUserId()
        {
            var auth = await _authContext.GetAuthorizationInfo(Request);
            return auth?.UserId;
        }

        /// <summary>GET /api/moviewishlist/channels — lists all available Live TV channels from the EPG.</summary>
        [HttpGet("channels")]
        [Authorize]
        public ActionResult GetChannels()
        {
            var channels = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.LiveTvChannel }
            });

            var result = channels
                .Select(c => new { id = c.Id.ToString(), name = c.Name ?? c.Id.ToString() })
                .OrderBy(c => c.name)
                .ToList();

            return Ok(result);
        }

        /// <summary>GET /api/moviewishlist/subscriptions — returns the authenticated user's channel subscriptions.</summary>
        [HttpGet("subscriptions")]
        [Authorize]
        public async Task<ActionResult> GetSubscriptions()
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var subs = _db.GetSubscriptionsByUser(userId.Value.ToString());
            return Ok(subs);
        }

        /// <summary>POST /api/moviewishlist/subscriptions — subscribes the authenticated user to a channel + days.</summary>
        [HttpPost("subscriptions")]
        [Authorize]
        public async Task<ActionResult> AddSubscription([FromBody] AddSubscriptionRequest request)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.ChannelId))
                return BadRequest(new { message = "ChannelId is required." });

            if (request.DaysOfWeek < 1 || request.DaysOfWeek > 127)
                return BadRequest(new { message = "DaysOfWeek must be a bitmask between 1 and 127." });

            var sub = new ChannelSubscription
            {
                UserId = userId.Value.ToString(),
                ChannelId = request.ChannelId,
                ChannelName = request.ChannelName ?? request.ChannelId,
                DaysOfWeek = request.DaysOfWeek,
                CreatedAt = DateTime.UtcNow
            };

            _db.AddSubscription(sub);

            if (sub.Id == 0)
            {
                // INSERT OR IGNORE fired — subscription already exists; return the existing one
                var existing = _db.GetSubscriptionsByUser(userId.Value.ToString())
                    .FirstOrDefault(s => string.Equals(s.ChannelId, request.ChannelId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    return Ok(existing);
            }

            _logger.LogInformation("User {UserId} subscribed to channel '{Channel}' (days: {Days}).", userId, sub.ChannelName, sub.DaysOfWeek);
            return CreatedAtAction(nameof(GetSubscriptions), sub);
        }

        /// <summary>PUT /api/moviewishlist/subscriptions/{id} — updates the subscribed days for an existing subscription.</summary>
        [HttpPut("subscriptions/{id}")]
        [Authorize]
        public async Task<ActionResult> UpdateSubscription([FromRoute] int id, [FromBody] UpdateSubscriptionRequest request)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var sub = _db.GetSubscriptionById(id);
            if (sub == null)
                return NotFound();

            if (!string.Equals(sub.UserId, userId.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (request.DaysOfWeek < 1 || request.DaysOfWeek > 127)
                return BadRequest(new { message = "DaysOfWeek must be a bitmask between 1 and 127." });

            sub.DaysOfWeek = request.DaysOfWeek;
            _db.UpdateSubscription(sub);

            _logger.LogInformation("User {UserId} updated subscription {Id} days to {Days}.", userId, id, request.DaysOfWeek);
            return Ok(sub);
        }

        /// <summary>DELETE /api/moviewishlist/subscriptions/{id} — removes a channel subscription.</summary>
        [HttpDelete("subscriptions/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteSubscription([FromRoute] int id)
        {
            var userId = await GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var sub = _db.GetSubscriptionById(id);
            if (sub == null)
                return NotFound();

            if (!string.Equals(sub.UserId, userId.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                return Forbid();

            _db.DeleteSubscription(id);
            _logger.LogInformation("User {UserId} removed subscription {Id} (channel '{Channel}').", userId, id, sub.ChannelName);
            return NoContent();
        }
    }
}
