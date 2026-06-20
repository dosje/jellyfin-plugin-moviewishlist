using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MovieWishlist.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.MovieWishlist.Tests;

public class LibraryCheckServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly LibraryCheckService _service;

    public LibraryCheckServiceTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        var logger = new Mock<ILogger<LibraryCheckService>>();
        _service = new LibraryCheckService(_libraryManagerMock.Object, logger.Object);
    }

    private void SetupLibrary(params BaseItem[] items)
    {
        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(items.ToList());
    }

    [Fact]
    public void IsMovieInLibrary_ReturnsTrue_WhenExactMatch()
    {
        var movie = new Movie { Name = "Fight Club", ProductionYear = 1999 };
        SetupLibrary(movie);

        var result = _service.IsMovieInLibrary("Fight Club", 1999);

        Assert.True(result);
    }

    [Fact]
    public void IsMovieInLibrary_ReturnsFalse_WhenNoMatch()
    {
        SetupLibrary();

        var result = _service.IsMovieInLibrary("Inception", 2010);

        Assert.False(result);
    }

    [Fact]
    public void IsMovieInLibrary_ReturnsTrue_WhenYearOffByOne()
    {
        var movie = new Movie { Name = "Interstellar", ProductionYear = 2014 };
        SetupLibrary(movie);

        var result = _service.IsMovieInLibrary("Interstellar", 2015);

        Assert.True(result);
    }

    [Fact]
    public void IsMovieInLibrary_ReturnsFalse_WhenYearOffByMore()
    {
        var movie = new Movie { Name = "Interstellar", ProductionYear = 2014 };
        SetupLibrary(movie);

        var result = _service.IsMovieInLibrary("Interstellar", 2020);

        Assert.False(result);
    }

    [Fact]
    public void IsMovieInLibrary_ReturnsTrue_WithArticleVariation()
    {
        var movie = new Movie { Name = "The Matrix", ProductionYear = 1999 };
        SetupLibrary(movie);

        // "Matrix" normalizes to "matrix"; "The Matrix" normalizes to "matrix" — should match
        var result = _service.IsMovieInLibrary("Matrix", 1999);

        Assert.True(result);
    }

    [Fact]
    public void IsMovieInLibrary_ReturnsTrue_WhenNoYearProvided()
    {
        var movie = new Movie { Name = "Pulp Fiction", ProductionYear = 1994 };
        SetupLibrary(movie);

        // No year provided — year check is skipped, title match is enough
        var result = _service.IsMovieInLibrary("Pulp Fiction", null);

        Assert.True(result);
    }
}
