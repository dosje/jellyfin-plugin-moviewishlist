using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MovieWishlist;

/// <summary>
/// Plugin configuration for Movie Wishlist DVR.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the TMDB API key used for movie lookups.
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how often (in hours) the EPG is scanned for wishlist matches.
    /// </summary>
    public int ScanIntervalHours { get; set; } = 6;

    /// <summary>
    /// Gets or sets how many days ahead to look in the EPG when scanning.
    /// </summary>
    public int DaysAheadToScan { get; set; } = 7;

    /// <summary>
    /// Gets or sets a value indicating whether a movie is removed from the wishlist
    /// after a recording has been successfully scheduled.
    /// </summary>
    public bool RemoveAfterRecorded { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether movies already present in the
    /// Jellyfin library are skipped when scheduling recordings.
    /// </summary>
    public bool SkipIfInLibrary { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether in-app notifications are sent
    /// when a recording is scheduled.
    /// </summary>
    public bool EnableNotifications { get; set; } = true;
}
