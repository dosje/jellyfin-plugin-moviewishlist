using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.Services;

public class LibraryCheckService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LibraryCheckService> _logger;

    public LibraryCheckService(ILibraryManager libraryManager, ILogger<LibraryCheckService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public bool IsMovieInLibrary(string title, int? year)
    {
        var normalizedSearch = NormalizeTitle(title);

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            SearchTerm = title
        };

        var results = _libraryManager.GetItemList(query);

        foreach (var item in results)
        {
            var normalizedItem = NormalizeTitle(item.Name ?? string.Empty);

            if (!string.Equals(normalizedSearch, normalizedItem, StringComparison.OrdinalIgnoreCase))
                continue;

            if (year.HasValue && item.ProductionYear.HasValue)
            {
                if (Math.Abs(item.ProductionYear.Value - year.Value) > 1)
                    continue;
            }

            _logger.LogDebug("Found '{Title}' ({Year}) in library as '{LibraryTitle}' ({LibraryYear})",
                title, year, item.Name, item.ProductionYear);
            return true;
        }

        return false;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var normalized = title.Trim().ToLowerInvariant();

        // Strip leading articles
        if (normalized.StartsWith("the ", StringComparison.Ordinal))
            normalized = normalized[4..];
        else if (normalized.StartsWith("a ", StringComparison.Ordinal))
            normalized = normalized[2..];
        else if (normalized.StartsWith("an ", StringComparison.Ordinal))
            normalized = normalized[3..];

        // Remove punctuation
        var chars = normalized.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray();
        return new string(chars).Trim();
    }
}
