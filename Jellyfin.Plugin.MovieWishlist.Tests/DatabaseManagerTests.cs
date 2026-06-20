using MediaBrowser.Common.Configuration;
using Moq;
using Xunit;
using Jellyfin.Plugin.MovieWishlist.Data;
using Jellyfin.Plugin.MovieWishlist.Data.Models;

namespace Jellyfin.Plugin.MovieWishlist.Tests;

public class DatabaseManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly DatabaseManager _db;

    public DatabaseManagerTests()
    {
        // Use a unique temp directory per test instance so parallel tests don't share the db file.
        _tempDir = Path.Combine(Path.GetTempPath(), $"wishlist-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "moviewishlist.db");

        var appPaths = new Mock<IApplicationPaths>();
        appPaths.Setup(p => p.DataPath).Returns(_tempDir);

        _db = new DatabaseManager(appPaths.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void AddAndRetrieveWishlistItem_RoundTrips()
    {
        var item = new WishlistItem
        {
            UserId = "user1",
            TmdbId = 550,
            Title = "Fight Club",
            Year = 1999,
            Status = WishlistStatus.WatchingEpg,
            AddedAt = DateTime.UtcNow
        };

        _db.AddWishlistItem(item);

        var items = _db.GetWishlistItemsByUser("user1");

        Assert.Single(items);
        Assert.Equal("user1", items[0].UserId);
        Assert.Equal(550, items[0].TmdbId);
        Assert.Equal("Fight Club", items[0].Title);
        Assert.Equal(1999, items[0].Year);
        Assert.Equal(WishlistStatus.WatchingEpg, items[0].Status);
    }

    [Fact]
    public void GetWishlistItemByUserAndTmdb_ReturnsNull_WhenNotFound()
    {
        var result = _db.GetWishlistItemByUserAndTmdb("user1", 999);

        Assert.Null(result);
    }

    [Fact]
    public void GetWishlistItemByUserAndTmdb_ReturnsItem_WhenFound()
    {
        var item = new WishlistItem
        {
            UserId = "user2",
            TmdbId = 278,
            Title = "The Shawshank Redemption",
            AddedAt = DateTime.UtcNow
        };
        _db.AddWishlistItem(item);

        var result = _db.GetWishlistItemByUserAndTmdb("user2", 278);

        Assert.NotNull(result);
        Assert.Equal(278, result.TmdbId);
    }

    [Fact]
    public void UpdateWishlistItem_ChangesStatus()
    {
        var item = new WishlistItem
        {
            UserId = "user1",
            TmdbId = 101,
            Title = "Inception",
            Status = WishlistStatus.WatchingEpg,
            AddedAt = DateTime.UtcNow
        };
        _db.AddWishlistItem(item);

        item.Status = WishlistStatus.Scheduled;
        item.JellyfinTimerId = "timer-123";
        _db.UpdateWishlistItem(item);

        var retrieved = _db.GetWishlistItemById(item.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(WishlistStatus.Scheduled, retrieved.Status);
        Assert.Equal("timer-123", retrieved.JellyfinTimerId);
    }

    [Fact]
    public void RemoveWishlistItem_DeletesItem()
    {
        var item = new WishlistItem
        {
            UserId = "user1",
            TmdbId = 200,
            Title = "Pulp Fiction",
            AddedAt = DateTime.UtcNow
        };
        _db.AddWishlistItem(item);

        _db.RemoveWishlistItem(item.Id);

        var result = _db.GetWishlistItemById(item.Id);
        Assert.Null(result);
    }

    [Fact]
    public void GetWishlistItemsByTmdbId_ReturnsAllUsers()
    {
        _db.AddWishlistItem(new WishlistItem { UserId = "user1", TmdbId = 500, Title = "Movie A", AddedAt = DateTime.UtcNow });
        _db.AddWishlistItem(new WishlistItem { UserId = "user2", TmdbId = 500, Title = "Movie A", AddedAt = DateTime.UtcNow });

        var results = _db.GetWishlistItemsByTmdbId(500);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void AddActivityLog_AndRetrieve()
    {
        var log = new ActivityLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "Scan",
            Message = "Test"
        };

        _db.AddActivityLog(log);

        var logs = _db.GetActivityLogs(10);

        Assert.True(logs.Count >= 1);
        Assert.Equal("Scan", logs[0].EventType);
    }

    [Fact]
    public void GetActivityLogs_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            _db.AddActivityLog(new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = "Scan",
                Message = $"Entry {i}"
            });
        }

        var logs = _db.GetActivityLogs(3);

        Assert.True(logs.Count <= 3);
    }
}
