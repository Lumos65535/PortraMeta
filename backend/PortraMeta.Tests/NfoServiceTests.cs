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
}
