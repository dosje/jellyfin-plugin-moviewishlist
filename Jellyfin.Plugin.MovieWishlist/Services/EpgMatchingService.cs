using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MovieWishlist.Data.Models;
using Jellyfin.Plugin.MovieWishlist.Services.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.Services;

public class EpgMatchingService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<EpgMatchingService> _logger;

    public EpgMatchingService(ILibraryManager libraryManager, ILogger<EpgMatchingService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public Task<List<EpgMatch>> FindMatchesAsync(List<WishlistItem> items, int daysAhead, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var programs = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.LiveTvProgram },
            IsMovie = true,
            MinStartDate = now,
            MaxStartDate = now.AddDays(daysAhead)
        });

        _logger.LogDebug("EPG scan found {Count} upcoming movie programs over {Days} days", programs.Count, daysAhead);

        var eligibleStatuses = new HashSet<WishlistStatus>
        {
            WishlistStatus.WatchingEpg,
            WishlistStatus.NeedsConfirmation,
            WishlistStatus.Missed
        };

        var results = new List<EpgMatch>();

        foreach (var item in items.Where(i => eligibleStatuses.Contains(i.Status)))
        {
            var normalizedWish = NormalizeTitle(item.Title);
            var candidates = new List<EpgMatch>();

            foreach (var program in programs)
            {
                var programName = program.Name ?? string.Empty;
                var normalizedProgram = NormalizeTitle(programName);

                bool titleMatch = string.Equals(normalizedWish, normalizedProgram, StringComparison.Ordinal)
                    || (normalizedWish.Length <= 10 && LevenshteinDistance(normalizedWish, normalizedProgram) <= 2);

                if (!titleMatch)
                    continue;

                int? programYear = program.ProductionYear;

                bool yearMatch = !item.Year.HasValue
                    || !programYear.HasValue
                    || Math.Abs(programYear.Value - item.Year.Value) <= 1;

                // ChannelId is a non-nullable Guid on BaseItem
                var channelId = program.ChannelId.ToString();

                // LiveTvProgram does not carry ChannelName; resolve via library
                var channelItem = program.ChannelId != Guid.Empty
                    ? _libraryManager.GetItemById(program.ChannelId)
                    : null;
                var channelName = channelItem?.Name ?? channelId;
                var isHd = channelName.Contains("HD", StringComparison.OrdinalIgnoreCase);

                var startTime = (program as IHasStartDate)?.StartDate ?? DateTime.MinValue;
                var endTime = program.EndDate ?? DateTime.MinValue;

                candidates.Add(new EpgMatch
                {
                    WishlistItem = item,
                    // Jellyfin expects the item Guid (N-format) as the timer ProgramId
                    ProgramId = program.Id.ToString("N"),
                    ProgramTitle = programName,
                    ProgramYear = programYear,
                    StartTime = startTime,
                    EndTime = endTime,
                    ChannelId = channelId,
                    ChannelName = channelName,
                    IsHd = isHd,
                    // Confidence determined after we know how many candidates there are
                    Confidence = MatchConfidence.Confident
                });
            }

            if (candidates.Count == 0)
                continue;

            // Determine confidence: uncertain if year is missing/out of tolerance or multiple title matches
            bool multipleDistinctTitles = candidates
                .Select(c => NormalizeTitle(c.ProgramTitle))
                .Distinct()
                .Count() > 1;

            foreach (var candidate in candidates)
            {
                bool yearInTolerance = !item.Year.HasValue
                    || !candidate.ProgramYear.HasValue
                    || Math.Abs(candidate.ProgramYear.Value - item.Year.Value) <= 1;

                candidate.Confidence = (!yearInTolerance || multipleDistinctTitles)
                    ? MatchConfidence.Uncertain
                    : MatchConfidence.Confident;
            }

            // Pick best: prefer Confident, then HD, then earliest start time
            var best = candidates
                .OrderBy(c => c.Confidence)          // Confident (0) before Uncertain (1)
                .ThenByDescending(c => c.IsHd)
                .ThenBy(c => c.StartTime)
                .First();

            results.Add(best);

            _logger.LogDebug(
                "Matched wishlist item '{Title}' ({Year}) to EPG program '{ProgramTitle}' on '{Channel}' at {Start} [{Confidence}]",
                item.Title, item.Year, best.ProgramTitle, best.ChannelName, best.StartTime, best.Confidence);
        }

        return Task.FromResult(results);
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var normalized = title.Trim().ToLowerInvariant();

        if (normalized.StartsWith("the ", StringComparison.Ordinal))
            normalized = normalized[4..];
        else if (normalized.StartsWith("a ", StringComparison.Ordinal))
            normalized = normalized[2..];
        else if (normalized.StartsWith("an ", StringComparison.Ordinal))
            normalized = normalized[3..];

        var chars = normalized.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray();
        return new string(chars).Trim();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++)
            dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++)
            dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }
}
