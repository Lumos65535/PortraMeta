using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using PortraMeta.Core.Interfaces;
using PortraMeta.Data.Services;

namespace PortraMeta.Tests;

public class NfoServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "portrameta_test_" + Guid.NewGuid().ToString("N"));
    private readonly NfoService _service = new();

    public NfoServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string GetNfoPath() => Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".nfo");

    private static VideoFileDto MakeDto(
        string? title = null, string? originalTitle = null, int? year = null,
        string? plot = null, string? studioName = null,
        IReadOnlyList<ActorDto>? actors = null) =>
        new(1, 1, "test.mp4", "/test/test.mp4", 1000, true, false, false,
            title, originalTitle, year, plot, studioName, DateTime.UtcNow, actors);

    [Fact]
    public async Task WriteAsync_AllFields_ValidXml()
    {
        var path = GetNfoPath();
        var dto = MakeDto("Title", "OT", 2023, "Plot text", "Studio X",
            [new ActorDto(1, "Actor A", "Role A", 0)]);

        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        var root = doc.Root!;
        Assert.Equal("movie", root.Name.LocalName);
        Assert.Equal("Title", root.Element("title")!.Value);
        Assert.Equal("OT", root.Element("originaltitle")!.Value);
        Assert.Equal("2023", root.Element("year")!.Value);
        Assert.Equal("Plot text", root.Element("plot")!.Value);
        Assert.Equal("Studio X", root.Element("studio")!.Value);

        var actor = root.Element("actor")!;
        Assert.Equal("Actor A", actor.Element("name")!.Value);
        Assert.Equal("Role A", actor.Element("role")!.Value);
        Assert.Equal("0", actor.Element("order")!.Value);
    }

    [Fact]
    public async Task WriteAsync_NullOptionalFields_WritesEmptyElements()
    {
        var path = GetNfoPath();
        var dto = MakeDto();

        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        var root = doc.Root!;
        Assert.Equal(string.Empty, root.Element("title")!.Value);
        Assert.Equal(string.Empty, root.Element("year")!.Value);
        Assert.Equal(string.Empty, root.Element("studio")!.Value);
    }

    [Fact]
    public async Task WriteAsync_WithActors_OrderedCorrectly()
    {
        var path = GetNfoPath();
        var actors = new List<ActorDto>
        {
            new(3, "Charlie", null, 2),
            new(1, "Alice", "Lead", 0),
            new(2, "Bob", "Support", 1),
        };
        var dto = MakeDto("T", actors: actors);

        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        var actorElements = doc.Root!.Elements("actor").ToList();
        Assert.Equal(3, actorElements.Count);
        Assert.Equal("Alice", actorElements[0].Element("name")!.Value);
        Assert.Equal("Bob", actorElements[1].Element("name")!.Value);
        Assert.Equal("Charlie", actorElements[2].Element("name")!.Value);
    }

    [Fact]
    public async Task WriteAsync_NoActors_NoActorElements()
    {
        var path = GetNfoPath();
        var dto = MakeDto("T", actors: []);

        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        Assert.Empty(doc.Root!.Elements("actor"));
    }

    [Fact]
    public async Task WriteAsync_NullActors_NoActorElements()
    {
        var path = GetNfoPath();
        var dto = MakeDto("T");

        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        Assert.Empty(doc.Root!.Elements("actor"));
    }

    [Fact]
    public async Task WriteAsync_Roundtrip_ParseBackIdentical()
    {
        var path = GetNfoPath();
        var dto = MakeDto("My Movie", "Original", 2024, "A great plot", "Big Studio",
            [
                new ActorDto(1, "Alice", "Lead", 0),
                new ActorDto(2, "Bob", null, 1),
            ]);

        await _service.WriteAsync(path, dto);

        var parser = new NfoParser(NullLogger<NfoParser>.Instance);
        var parsed = await parser.ParseAsync(path);

        Assert.NotNull(parsed);
        Assert.Equal("My Movie", parsed.Title);
        Assert.Equal("Original", parsed.OriginalTitle);
        Assert.Equal(2024, parsed.Year);
        Assert.Equal("A great plot", parsed.Plot);
        Assert.Equal("Big Studio", parsed.Studio);
        Assert.Equal(2, parsed.Actors.Count);
        Assert.Equal("Alice", parsed.Actors[0].Name);
        Assert.Equal("Lead", parsed.Actors[0].Role);
        Assert.Equal("Bob", parsed.Actors[1].Name);
        Assert.Null(parsed.Actors[1].Role);
    }

    [Fact]
    public async Task WriteAsync_HasXmlDeclaration()
    {
        var path = GetNfoPath();
        await _service.WriteAsync(path, MakeDto("T"));

        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("<?xml", content);
        Assert.Contains("utf-8", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteAsync_ExtendedFields_WrittenCorrectly()
    {
        var path = GetNfoPath();
        var dto = MakeDto("Extended") with
        {
            Directors = ["Dir One", "Dir Two"],
            Genres = ["Action", "Drama"],
            Runtime = 142,
            Mpaa = "PG-13",
            Premiered = "2023-07-21",
            Ratings = [new RatingDto("imdb", 8.5m, 123456, 10)],
            UserRating = 8,
            UniqueIds = new Dictionary<string, string> { ["imdb"] = "tt1234567", ["tmdb"] = "12345" },
            Tags = ["favorite", "classic"],
            SortTitle = "Extended, The",
            Outline = "Short outline",
            Tagline = "Best movie ever",
            Credits = ["Writer One"],
            Countries = ["USA", "UK"],
            SetName = "Marvel Collection",
            DateAdded = "2024-01-15",
            Top250 = 42,
        };

        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        var root = doc.Root!;

        Assert.Equal(["Dir One", "Dir Two"], root.Elements("director").Select(e => e.Value).ToList());
        Assert.Equal(["Action", "Drama"], root.Elements("genre").Select(e => e.Value).ToList());
        Assert.Equal("142", root.Element("runtime")!.Value);
        Assert.Equal("PG-13", root.Element("mpaa")!.Value);
        Assert.Equal("2023-07-21", root.Element("premiered")!.Value);
        Assert.Equal("8", root.Element("userrating")!.Value);
        Assert.Equal("Extended, The", root.Element("sorttitle")!.Value);
        Assert.Equal("Short outline", root.Element("outline")!.Value);
        Assert.Equal("Best movie ever", root.Element("tagline")!.Value);
        Assert.Equal(["Writer One"], root.Elements("credits").Select(e => e.Value).ToList());
        Assert.Equal(["USA", "UK"], root.Elements("country").Select(e => e.Value).ToList());
        Assert.Equal("42", root.Element("top250")!.Value);
        Assert.Equal("2024-01-15", root.Element("dateadded")!.Value);

        // Ratings
        var ratingsEl = root.Element("ratings")!;
        var rating = ratingsEl.Element("rating")!;
        Assert.Equal("imdb", rating.Attribute("name")!.Value);
        Assert.Equal("8.5", rating.Element("value")!.Value);
        Assert.Equal("123456", rating.Element("votes")!.Value);

        // UniqueIds
        var uids = root.Elements("uniqueid").ToList();
        Assert.Equal(2, uids.Count);

        // Set
        Assert.Equal("Marvel Collection", root.Element("set")!.Element("name")!.Value);

        // Tags
        Assert.Equal(["favorite", "classic"], root.Elements("tag").Select(e => e.Value).ToList());
    }

    [Fact]
    public async Task WriteAsync_ExtendedRoundtrip_ParseBackIdentical()
    {
        var path = GetNfoPath();
        var dto = MakeDto("Roundtrip") with
        {
            Directors = ["Christopher Nolan"],
            Genres = ["Sci-Fi", "Thriller"],
            Runtime = 148,
            Mpaa = "PG-13",
            Premiered = "2010-07-16",
            Ratings = [new RatingDto("imdb", 8.8m, 2000000, 10)],
            UserRating = 9,
            UniqueIds = new Dictionary<string, string> { ["imdb"] = "tt1375666" },
            Tags = ["dream"],
            SortTitle = "Inception",
            Outline = "A thief enters dreams",
            Tagline = "Your mind is the scene of the crime",
            Credits = ["Christopher Nolan"],
            Countries = ["USA"],
            SetName = "Nolan Films",
            DateAdded = "2024-06-01",
            Top250 = 13,
        };

        await _service.WriteAsync(path, dto);

        var parser = new NfoParser(NullLogger<NfoParser>.Instance);
        var parsed = await parser.ParseAsync(path);

        Assert.NotNull(parsed);
        Assert.Equal("Roundtrip", parsed.Title);
        Assert.Equal(["Christopher Nolan"], parsed.Directors);
        Assert.Equal(["Sci-Fi", "Thriller"], parsed.Genres);
        Assert.Equal(148, parsed.Runtime);
        Assert.Equal("PG-13", parsed.Mpaa);
        Assert.Equal("2010-07-16", parsed.Premiered);
        Assert.Equal(9, parsed.UserRating);
        Assert.Equal("Inception", parsed.SortTitle);
        Assert.Equal("A thief enters dreams", parsed.Outline);
        Assert.Equal("Your mind is the scene of the crime", parsed.Tagline);
        Assert.Equal(["Christopher Nolan"], parsed.Credits);
        Assert.Equal(["USA"], parsed.Countries);
        Assert.Equal("Nolan Films", parsed.Set);
        Assert.Equal("2024-06-01", parsed.DateAdded);
        Assert.Equal(13, parsed.Top250);
        Assert.Equal(["dream"], parsed.Tags);

        Assert.Single(parsed.Ratings);
        Assert.Equal("imdb", parsed.Ratings[0].Name);
        Assert.Equal(8.8m, parsed.Ratings[0].Value);

        Assert.Single(parsed.UniqueIds);
        Assert.Equal("imdb", parsed.UniqueIds[0].Type);
        Assert.Equal("tt1375666", parsed.UniqueIds[0].Value);
    }

    [Fact]
    public async Task WriteAsync_PreservesUnknownElements_RoundTrip()
    {
        var path = GetNfoPath();
        // Write an initial NFO with a custom unknown element
        var initialXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <movie>
              <title>Original</title>
              <customelement>Custom Value</customelement>
              <anothertag attr="123">Data</anothertag>
            </movie>
            """;
        await File.WriteAllTextAsync(path, initialXml);

        // Now write over it via the service
        var dto = MakeDto("Updated Title");
        await _service.WriteAsync(path, dto);

        var doc = XDocument.Load(path);
        var root = doc.Root!;

        // Known field was updated
        Assert.Equal("Updated Title", root.Element("title")!.Value);

        // Unknown elements were preserved
        Assert.Equal("Custom Value", root.Element("customelement")!.Value);
        Assert.Equal("Data", root.Element("anothertag")!.Value);
        Assert.Equal("123", root.Element("anothertag")!.Attribute("attr")!.Value);
    }
}
