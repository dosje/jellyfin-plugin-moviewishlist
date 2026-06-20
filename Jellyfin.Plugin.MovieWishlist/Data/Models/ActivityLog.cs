namespace Jellyfin.Plugin.MovieWishlist.Data.Models;

public class ActivityLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? WishlistItemId { get; set; }
}
