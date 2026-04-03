namespace PortraMeta.Core.Interfaces;

public record NfoRating(string Name, decimal Value, int Votes, int Max = 10);
public record NfoUniqueId(string Type, string Value);
public record NfoActor(string Name, string? Role, int Order);

public record NfoData(
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? Studio,
    IReadOnlyList<NfoActor> Actors,
    // Tier 1
    IReadOnlyList<string> Directors,
    IReadOnlyList<string> Genres,
    int? Runtime,
    string? Mpaa,
    string? Premiered,
    IReadOnlyList<NfoRating> Ratings,
    int? UserRating,
    IReadOnlyList<NfoUniqueId> UniqueIds,
    IReadOnlyList<string> Tags,
    string? SortTitle,
    // Tier 2
    string? Outline,
    string? Tagline,
    IReadOnlyList<string> Credits,
    IReadOnlyList<string> Countries,
    // Tier 3
    string? Set,
    string? DateAdded,
    int? Top250
);

public interface INfoParser
{
    /// <summary>
    /// Parses a Kodi Movie NFO file. Returns null if the file is missing or not valid XML.
    /// </summary>
    Task<NfoData?> ParseAsync(string nfoPath, CancellationToken ct = default);
}
