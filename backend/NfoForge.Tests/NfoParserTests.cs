using Microsoft.Extensions.Logging.Abstractions;
using NfoForge.Data.Services;

namespace NfoForge.Tests;

public class NfoParserTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "nfoforge_test_" + Guid.NewGuid().ToString("N"));
    private readonly NfoParser _parser = new(NullLogger<NfoParser>.Instance);

    public NfoParserTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteNfo(string xml)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".nfo");
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public async Task ParseAsync_ValidNfo_AllFields()
    {
        var path = WriteNfo("""
            <?xml version="1.0" encoding="UTF-8"?>
            <movie>
              <title>Test Movie</title>
              <originaltitle>Original Title</originaltitle>
              <year>2023</year>
              <plot>A test plot</plot>
              <studio>Test Studio</studio>
              <actor>
                <name>Actor One</name>
                <role>Role A</role>
                <order>0</order>
              </actor>
              <actor>
                <name>Actor Two</name>
                <role>Role B</role>
                <order>1</order>
              </actor>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Equal("Test Movie", result.Title);
        Assert.Equal("Original Title", result.OriginalTitle);
        Assert.Equal(2023, result.Year);
        Assert.Equal("A test plot", result.Plot);
        Assert.Equal("Test Studio", result.Studio);
        Assert.Equal(2, result.Actors.Count);
        Assert.Equal("Actor One", result.Actors[0].Name);
        Assert.Equal("Role A", result.Actors[0].Role);
        Assert.Equal(0, result.Actors[0].Order);
        Assert.Equal("Actor Two", result.Actors[1].Name);
    }

    [Fact]
    public async Task ParseAsync_MissingOptionalFields_ReturnsNullForMissing()
    {
        var path = WriteNfo("""
            <movie>
              <title>Only Title</title>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Equal("Only Title", result.Title);
        Assert.Null(result.OriginalTitle);
        Assert.Null(result.Year);
        Assert.Null(result.Plot);
        Assert.Null(result.Studio);
        Assert.Empty(result.Actors);
    }

    [Fact]
    public async Task ParseAsync_MultipleActors_PreservesOrder()
    {
        var path = WriteNfo("""
            <movie>
              <title>T</title>
              <actor><name>C</name><order>2</order></actor>
              <actor><name>A</name><order>0</order></actor>
              <actor><name>B</name><order>1</order></actor>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Equal(3, result.Actors.Count);
        Assert.Equal("C", result.Actors[0].Name);
        Assert.Equal(2, result.Actors[0].Order);
        Assert.Equal("A", result.Actors[1].Name);
        Assert.Equal(0, result.Actors[1].Order);
    }

    [Fact]
    public async Task ParseAsync_ActorWithoutName_Filtered()
    {
        var path = WriteNfo("""
            <movie>
              <title>T</title>
              <actor><name>Valid</name><order>0</order></actor>
              <actor><name></name><order>1</order></actor>
              <actor><role>No name</role><order>2</order></actor>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Single(result.Actors);
        Assert.Equal("Valid", result.Actors[0].Name);
    }

    [Fact]
    public async Task ParseAsync_InvalidYear_ReturnsNull()
    {
        var path = WriteNfo("""
            <movie>
              <title>T</title>
              <year>not-a-number</year>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Null(result.Year);
    }

    [Fact]
    public async Task ParseAsync_FileNotFound_ReturnsNull()
    {
        var result = await _parser.ParseAsync("/nonexistent/path/file.nfo");

        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_InvalidXml_ReturnsNull()
    {
        var path = WriteNfo("this is not xml <broken>");

        var result = await _parser.ParseAsync(path);

        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_NotMovieRoot_ReturnsNull()
    {
        var path = WriteNfo("""
            <tvshow>
              <title>TV Show</title>
            </tvshow>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_WhitespaceValues_TrimmedToNull()
    {
        var path = WriteNfo("""
            <movie>
              <title>   </title>
              <originaltitle>  </originaltitle>
              <plot>
              </plot>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Null(result.Title);
        Assert.Null(result.OriginalTitle);
        Assert.Null(result.Plot);
    }

    [Fact]
    public async Task ParseAsync_ActorWithoutOrder_UsesIndex()
    {
        var path = WriteNfo("""
            <movie>
              <title>T</title>
              <actor><name>First</name></actor>
              <actor><name>Second</name></actor>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Equal(0, result.Actors[0].Order);
        Assert.Equal(1, result.Actors[1].Order);
    }
}
