using Jellyfin.Plugin.MovieWishlist.Data;
using Jellyfin.Plugin.MovieWishlist.ScheduledTasks;
using Jellyfin.Plugin.MovieWishlist.Services;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MovieWishlist;

/// <summary>
/// Registers all plugin services with the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IServiceProvider applicationServiceProvider)
    {
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<TmdbService>();
        services.AddSingleton<EpgMatchingService>();
        services.AddSingleton<RecordingService>();
        services.AddSingleton<LibraryCheckService>();

        services.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, EpgScanTask>();
    }
}
