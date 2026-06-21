using System.Net.Http;
using System.Text.Json;
using Jellyfin.Plugin.MovieWishlist.Services.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.Services;

public class TmdbService
{
    private readonly ILogger<TmdbService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TmdbService(ILogger<TmdbService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
    }

    // Secondary constructor for testability — allows injecting a mock HttpClient.
    public TmdbService(ILogger<TmdbService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<TmdbSearchResponse?> SearchMoviesAsync(string query, string apiKey, CancellationToken ct)
    {
        try
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"search/movie?api_key={apiKey}&query={encodedQuery}&include_adult=false";
            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<TmdbSearchResponse>(stream, _jsonOptions, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while searching TMDB for query '{Query}'", query);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error while searching TMDB for query '{Query}'", query);
            return null;
        }
    }

    public async Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, string apiKey, CancellationToken ct)
    {
        try
        {
            var url = $"movie/{tmdbId}?api_key={apiKey}&append_to_response=credits";
            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<TmdbMovieDetails>(stream, _jsonOptions, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while fetching TMDB details for movie ID {TmdbId}", tmdbId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error while fetching TMDB details for movie ID {TmdbId}", tmdbId);
            return null;
        }
    }
}
