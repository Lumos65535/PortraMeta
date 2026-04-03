using Microsoft.Extensions.Logging.Abstractions;
using PortraMeta.Data.Services;

namespace PortraMeta.Tests;

public class NfoParserTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "portrameta_test_" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task ParseAsync_ExtendedFields_AllParsed()
    {
        var path = WriteNfo("""
            <?xml version="1.0" encoding="UTF-8"?>
            <movie>
              <title>Extended Movie</title>
              <sorttitle>Extended, The</sorttitle>
              <director>Director One</director>
              <director>Director Two</director>
              <genre>Action</genre>
              <genre>Drama</genre>
              <runtime>142</runtime>
              <mpaa>PG-13</mpaa>
              <premiered>2023-07-21</premiered>
              <userrating>8</userrating>
              <tag>favorite</tag>
              <tag>classic</tag>
              <outline>A short outline</outline>
              <tagline>Best movie ever</tagline>
              <credits>Writer One</credits>
              <credits>Writer Two</credits>
              <country>USA</country>
              <country>UK</country>
              <set><name>Marvel Collection</name></set>
              <dateadded>2024-01-15 10:30:45</dateadded>
              <top250>42</top250>
              <ratings>
                <rating name="imdb" max="10">
                  <value>8.5</value>
                  <votes>123456</votes>
                </rating>
                <rating name="tmdb" max="10">
                  <value>7.9</value>
                  <votes>5678</votes>
                </rating>
              </ratings>
              <uniqueid type="imdb">tt1234567</uniqueid>
              <uniqueid type="tmdb">12345</uniqueid>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Equal("Extended Movie", result.Title);
        Assert.Equal("Extended, The", result.SortTitle);
        Assert.Equal(["Director One", "Director Two"], result.Directors);
        Assert.Equal(["Action", "Drama"], result.Genres);
        Assert.Equal(142, result.Runtime);
        Assert.Equal("PG-13", result.Mpaa);
        Assert.Equal("2023-07-21", result.Premiered);
        Assert.Equal(8, result.UserRating);
        Assert.Equal(["favorite", "classic"], result.Tags);
        Assert.Equal("A short outline", result.Outline);
        Assert.Equal("Best movie ever", result.Tagline);
        Assert.Equal(["Writer One", "Writer Two"], result.Credits);
        Assert.Equal(["USA", "UK"], result.Countries);
        Assert.Equal("Marvel Collection", result.Set);
        Assert.Equal("2024-01-15 10:30:45", result.DateAdded);
        Assert.Equal(42, result.Top250);

        // Ratings
        Assert.Equal(2, result.Ratings.Count);
        Assert.Equal("imdb", result.Ratings[0].Name);
        Assert.Equal(8.5m, result.Ratings[0].Value);
        Assert.Equal(123456, result.Ratings[0].Votes);
        Assert.Equal(10, result.Ratings[0].Max);
        Assert.Equal("tmdb", result.Ratings[1].Name);
        Assert.Equal(7.9m, result.Ratings[1].Value);

        // UniqueIds
        Assert.Equal(2, result.UniqueIds.Count);
        Assert.Equal("imdb", result.UniqueIds[0].Type);
        Assert.Equal("tt1234567", result.UniqueIds[0].Value);
        Assert.Equal("tmdb", result.UniqueIds[1].Type);
        Assert.Equal("12345", result.UniqueIds[1].Value);
    }

    [Fact]
    public async Task ParseAsync_LegacyRatingAndImdbId_Parsed()
    {
        var path = WriteNfo("""
            <movie>
              <title>Legacy</title>
              <rating>7.5</rating>
              <imdbid>tt9999999</imdbid>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Single(result.Ratings);
        Assert.Equal("default", result.Ratings[0].Name);
        Assert.Equal(7.5m, result.Ratings[0].Value);

        Assert.Single(result.UniqueIds);
        Assert.Equal("imdb", result.UniqueIds[0].Type);
        Assert.Equal("tt9999999", result.UniqueIds[0].Value);
    }

    [Fact]
    public async Task ParseAsync_SetPlainText_Parsed()
    {
        var path = WriteNfo("""
            <movie>
              <title>T</title>
              <set>Simple Collection</set>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Equal("Simple Collection", result.Set);
    }

    [Fact]
    public async Task ParseAsync_EmptyExtendedFields_DefaultsToEmpty()
    {
        var path = WriteNfo("""
            <movie>
              <title>Minimal</title>
            </movie>
            """);

        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result);
        Assert.Empty(result.Directors);
        Assert.Empty(result.Genres);
        Assert.Null(result.Runtime);
        Assert.Null(result.Mpaa);
        Assert.Null(result.Premiered);
        Assert.Empty(result.Ratings);
        Assert.Null(result.UserRating);
        Assert.Empty(result.UniqueIds);
        Assert.Empty(result.Tags);
        Assert.Null(result.SortTitle);
        Assert.Null(result.Outline);
        Assert.Null(result.Tagline);
        Assert.Empty(result.Credits);
        Assert.Empty(result.Countries);
        Assert.Null(result.Set);
        Assert.Null(result.DateAdded);
        Assert.Null(result.Top250);
    }
}
