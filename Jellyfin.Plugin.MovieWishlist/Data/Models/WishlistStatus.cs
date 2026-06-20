namespace Jellyfin.Plugin.MovieWishlist.Data.Models;

public enum WishlistStatus
{
    WatchingEpg = 0,
    Scheduled = 1,
    Recorded = 2,
    AlreadyInLibrary = 3,
    Missed = 4,
    NeedsConfirmation = 5
}
