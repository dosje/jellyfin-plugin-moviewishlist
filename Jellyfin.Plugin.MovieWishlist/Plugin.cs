using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MovieWishlist;

/// <summary>
/// Main plugin class for Movie Wishlist DVR.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Gets the singleton instance of this plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Movie Wishlist DVR";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a8f3c2d1-e4b5-4f76-9abc-def012345678");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths provider.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "MovieWishlistDiscover",
                EmbeddedResourcePath = "Jellyfin.Plugin.MovieWishlist.Web.discover.html",
                EnableInMainMenu = true
            },
            new PluginPageInfo
            {
                Name = "MovieWishlistWatchlist",
                EmbeddedResourcePath = "Jellyfin.Plugin.MovieWishlist.Web.watchlist.html",
                EnableInMainMenu = false
            },
            new PluginPageInfo
            {
                Name = "MovieWishlistConfirmations",
                EmbeddedResourcePath = "Jellyfin.Plugin.MovieWishlist.Web.confirmations.html",
                EnableInMainMenu = false
            },
            new PluginPageInfo
            {
                Name = "MovieWishlistSettings",
                EmbeddedResourcePath = "Jellyfin.Plugin.MovieWishlist.Web.settings.html",
                EnableInMainMenu = false
            },
            new PluginPageInfo
            {
                Name = "MovieWishlistActivity",
                EmbeddedResourcePath = "Jellyfin.Plugin.MovieWishlist.Web.activity.html",
                EnableInMainMenu = false
            },
            new PluginPageInfo
            {
                Name = "MovieWishlistSubscriptions",
                EmbeddedResourcePath = "Jellyfin.Plugin.MovieWishlist.Web.subscriptions.html",
                EnableInMainMenu = false
            }
        };
    }
}
