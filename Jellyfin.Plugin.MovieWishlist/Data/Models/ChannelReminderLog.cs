namespace Jellyfin.Plugin.MovieWishlist.Data.Models;

public class ChannelReminderLog
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public string ProgramId { get; set; } = string.Empty;
    public string ProgramTitle { get; set; } = string.Empty;
    public DateTime ProgramStart { get; set; }
    public DateTime NotifiedAt { get; set; }
}
