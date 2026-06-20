using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MovieWishlist.Services.Dto;

public class TmdbMovieResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    public int? Year => ReleaseDate?.Length >= 4 ? int.Parse(ReleaseDate[..4]) : null;
}

public class TmdbGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class TmdbCastMember
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("character")]
    public string? Character { get; set; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; set; }
}

public class TmdbCredits
{
    [JsonPropertyName("cast")]
    public List<TmdbCastMember> Cast { get; set; } = new();
}

public class TmdbMovieDetails : TmdbMovieResult
{
    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }

    [JsonPropertyName("runtime")]
    public int Runtime { get; set; }

    [JsonPropertyName("genres")]
    public List<TmdbGenre> Genres { get; set; } = new();

    [JsonPropertyName("credits")]
    public TmdbCredits? Credits { get; set; }
}

public class TmdbSearchResponse
{
    [JsonPropertyName("results")]
    public List<TmdbMovieResult> Results { get; set; } = new();

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }
}
