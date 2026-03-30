namespace NfoForge.Core.Interfaces;

public record NfoData(
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? Studio,
    IReadOnlyList<NfoActor> Actors
);

public record NfoActor(string Name, string? Role, int Order);

public interface INfoParser
{
    /// <summary>
    /// Parses a Kodi Movie NFO file. Returns null if the file is missing or not valid XML.
    /// </summary>
    Task<NfoData?> ParseAsync(string nfoPath, CancellationToken ct = default);
}
