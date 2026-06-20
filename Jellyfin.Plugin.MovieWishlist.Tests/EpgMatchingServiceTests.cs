using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MovieWishlist.Data.Models;
using Jellyfin.Plugin.MovieWishlist.Services;
using Jellyfin.Plugin.MovieWishlist.Services.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.MovieWishlist.Tests;

public class EpgMatchingServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly EpgMatchingService _service;

    public EpgMatchingServiceTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        var logger = new Mock<ILogger<EpgMatchingService>>();
        _service = new EpgMatchingService(_libraryManagerMock.Object, logger.Object);
    }

    /// <summary>
    /// Creates a fake EPG program BaseItem with the required properties set via the
    /// public setters that BaseItem exposes. LiveTvProgram inherits from BaseItem,
    /// so we use a plain BaseItem with the same writable fields for test isolation.
    /// </summary>
    private static BaseItem MakeProgram(string name, int? year, DateTime startTime, Guid channelId)
    {
        // Use the concrete Video type (which has all the properties we need)
        // rather than trying to instantiate the sealed LiveTvProgram.
        var program = new MediaBrowser.Controller.Entities.TV.Episode();
        program.Name = name;
        program.ProductionYear = year;
        program.StartDate = startTime;
        program.EndDate = startTime.AddHours(2);
        program.ChannelId = channelId;
        return program;
    }

    private static BaseItem MakeChannel(string name)
    {
        return new Folder { Name = name };
    }

    private static WishlistItem MakeWishlistItem(string title, int? year, WishlistStatus status = WishlistStatus.WatchingEpg)
        => new WishlistItem
        {
            Title = title,
            Year = year,
            Status = status,
            AddedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task FindMatchesAsync_ReturnsConfidentMatch_WhenTitleAndYearMatch()
    {
        var channelId = Guid.NewGuid();
        var program = MakeProgram("Inception", 2010, DateTime.UtcNow.AddHours(2), channelId);

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem> { program });

        _libraryManagerMock
            .Setup(m => m.GetItemById(channelId))
            .Returns(MakeChannel("BBC Two"));

        var wishlist = new List<WishlistItem> { MakeWishlistItem("Inception", 2010) };

        var matches = await _service.FindMatchesAsync(wishlist, 7, CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal(MatchConfidence.Confident, matches[0].Confidence);
    }

    [Fact]
    public async Task FindMatchesAsync_ReturnsUncertainMatch_WhenYearMissing()
    {
        var channelId = Guid.NewGuid();
        // Program has no year — year match is indeterminate → Uncertain
        var program = MakeProgram("Inception", null, DateTime.UtcNow.AddHours(2), channelId);

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem> { program });

        _libraryManagerMock
            .Setup(m => m.GetItemById(channelId))
            .Returns(MakeChannel("BBC Two"));

        var wishlist = new List<WishlistItem> { MakeWishlistItem("Inception", 2010) };

        var matches = await _service.FindMatchesAsync(wishlist, 7, CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal(MatchConfidence.Uncertain, matches[0].Confidence);
    }

    [Fact]
    public async Task FindMatchesAsync_PrefersHdChannel()
    {
        var sdChannelId = Guid.NewGuid();
        var hdChannelId = Guid.NewGuid();

        var sdProgram = MakeProgram("Inception", 2010, DateTime.UtcNow.AddHours(1), sdChannelId);
        var hdProgram = MakeProgram("Inception", 2010, DateTime.UtcNow.AddHours(3), hdChannelId);

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem> { sdProgram, hdProgram });

        _libraryManagerMock
            .Setup(m => m.GetItemById(sdChannelId))
            .Returns(MakeChannel("ITV"));

        _libraryManagerMock
            .Setup(m => m.GetItemById(hdChannelId))
            .Returns(MakeChannel("ITV HD"));

        var wishlist = new List<WishlistItem> { MakeWishlistItem("Inception", 2010) };

        var matches = await _service.FindMatchesAsync(wishlist, 7, CancellationToken.None);

        Assert.Single(matches);
        Assert.True(matches[0].IsHd);
        Assert.Equal("ITV HD", matches[0].ChannelName);
    }

    [Fact]
    public async Task FindMatchesAsync_SkipsItemsNotInEligibleStatus()
    {
        var channelId = Guid.NewGuid();
        var program = MakeProgram("Inception", 2010, DateTime.UtcNow.AddHours(2), channelId);

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem> { program });

        _libraryManagerMock
            .Setup(m => m.GetItemById(channelId))
            .Returns(MakeChannel("BBC One"));

        // Status=Scheduled is NOT an eligible status — should produce no matches
        var wishlist = new List<WishlistItem> { MakeWishlistItem("Inception", 2010, WishlistStatus.Scheduled) };

        var matches = await _service.FindMatchesAsync(wishlist, 7, CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_NormalizesArticles_ThePrefixStripped()
    {
        var channelId = Guid.NewGuid();
        // EPG title has "The " prefix; wishlist title does not
        var program = MakeProgram("The Matrix", 1999, DateTime.UtcNow.AddHours(2), channelId);

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem> { program });

        _libraryManagerMock
            .Setup(m => m.GetItemById(channelId))
            .Returns(MakeChannel("Channel 4"));

        var wishlist = new List<WishlistItem> { MakeWishlistItem("Matrix", 1999) };

        var matches = await _service.FindMatchesAsync(wishlist, 7, CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal(MatchConfidence.Confident, matches[0].Confidence);
    }
}
