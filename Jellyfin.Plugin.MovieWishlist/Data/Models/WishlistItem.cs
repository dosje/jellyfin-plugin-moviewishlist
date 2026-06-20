namespace Jellyfin.Plugin.MovieWishlist.Data.Models;

public class WishlistItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int TmdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? PosterPath { get; set; }
    public string? Overview { get; set; }
    public WishlistStatus Status { get; set; } = WishlistStatus.WatchingEpg;
    public DateTime AddedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? ScheduledChannel { get; set; }
    public bool IsHd { get; set; }
    public string? JellyfinTimerId { get; set; }
    public string? PendingEpgProgramId { get; set; }
    public string? PendingEpgTitle { get; set; }
    public int? PendingEpgYear { get; set; }
    public string? ConflictInfo { get; set; }
    public string? LinkedUserIds { get; set; }
}
