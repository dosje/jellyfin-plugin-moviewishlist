using Jellyfin.Plugin.MovieWishlist.Data.Models;

namespace Jellyfin.Plugin.MovieWishlist.Services.Dto;

public enum MatchConfidence
{
    Confident,
    Uncertain
}

public class EpgMatch
{
    public WishlistItem WishlistItem { get; set; } = null!;
    public string ProgramId { get; set; } = string.Empty;
    public string ProgramTitle { get; set; } = string.Empty;
    public int? ProgramYear { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public bool IsHd { get; set; }
    public MatchConfidence Confidence { get; set; }
}
