// NOTE: TmdbService requires an internal constructor accepting HttpClient for testability; see TmdbServiceTests.cs comments.
//
// To make TmdbService testable, add the following internal constructor to TmdbService in the main project:
//
//   internal TmdbService(ILogger<TmdbService> logger, HttpClient httpClient)
//   {
//       _logger = logger;
//       _httpClient = httpClient;
//   }
//
// This allows tests to inject a MockHttpMessageHandler without changing the production code path.

using System.Net;
using System.Net.Http;
using System.Text;
using Jellyfin.Plugin.MovieWishlist.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.MovieWishlist.Tests;

public class TmdbServiceTests
{
    private readonly Mock<ILogger<TmdbService>> _loggerMock;

    public TmdbServiceTests()
    {
        _loggerMock = new Mock<ILogger<TmdbService>>();
    }

    private TmdbService CreateService(HttpResponseMessage response)
    {
        var handler = new MockHttpMessageHandler(response);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
        return new TmdbService(_loggerMock.Object, client);
    }

    [Fact]
    public async Task SearchMoviesAsync_ReturnsResults_OnSuccess()
    {
        const string json = """
            {
                "results": [
                    {
                        "id": 550,
                        "title": "Fight Club",
                        "release_date": "1999-10-15",
                        "vote_average": 8.4
                    }
                ],
                "total_results": 1
            }
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var service = CreateService(response);
        var result = await service.SearchMoviesAsync("Fight Club", "fakekey", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal("Fight Club", result.Results[0].Title);
    }

    [Fact]
    public async Task SearchMoviesAsync_ReturnsNull_OnHttpError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var service = CreateService(response);

        var result = await service.SearchMoviesAsync("anything", "badkey", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMovieDetailsAsync_ReturnsDetails_OnSuccess()
    {
        const string json = """
            {
                "id": 550,
                "title": "Fight Club",
                "release_date": "1999-10-15",
                "runtime": 139,
                "genres": [
                    { "id": 18, "name": "Drama" }
                ],
                "credits": { "cast": [] }
            }
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var service = CreateService(response);
        var result = await service.GetMovieDetailsAsync(550, "fakekey", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(139, result.Runtime);
        Assert.Single(result.Genres);
    }

    [Fact]
    public async Task GetMovieDetailsAsync_ReturnsNull_On404()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var service = CreateService(response);

        var result = await service.GetMovieDetailsAsync(99999, "fakekey", CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }
}
