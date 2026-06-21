namespace Jellyfin.Plugin.MovieWishlist.Data.Models;

public class ChannelSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    // Bitmask using 1 << (int)DayOfWeek: Sunday=1, Monday=2, Tuesday=4, Wednesday=8, Thursday=16, Friday=32, Saturday=64. 127=all days.
    public int DaysOfWeek { get; set; } = 127;
    public DateTime CreatedAt { get; set; }
}
