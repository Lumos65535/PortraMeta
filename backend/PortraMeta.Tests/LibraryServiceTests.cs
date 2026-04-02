using Microsoft.Extensions.Logging.Abstractions;
using PortraMeta.Core.Interfaces;
using PortraMeta.Data;
using PortraMeta.Data.Entities;
using PortraMeta.Data.Services;
using PortraMeta.Data.Utilities;
using NSubstitute;

namespace PortraMeta.Tests;

public class LibraryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
    private readonly string _tempDir;
    private readonly INfoParser _nfoParser;
    private readonly LibraryService _svc;

    public LibraryServiceTests()
    {
        (_db, _conn) = Helpers.TestDbContext.Create();
        _tempDir = Path.Combine(Path.GetTempPath(), "portrameta_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _nfoParser = Substitute.For<INfoParser>();
        _svc = new LibraryService(_db, new FileSystemScanner(), _nfoParser, NullLogger<LibraryService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateFile(string relativePath, string? content = null)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content ?? "");
        return fullPath;
    }

    // ── GetAllAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsOrdered()
    {
        _db.Libraries.Add(new Library { Name = "Beta", Path = "/b" });
        _db.Libraries.Add(new Library { Name = "Alpha", Path = "/a" });
        await _db.SaveChangesAsync();

        var result = await _svc.GetAllAsync();

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal("Alpha", result.Data[0].Name);
        Assert.Equal("Beta", result.Data[1].Name);
    }

    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidPath_CreatesLibrary()
    {
        var result = await _svc.CreateAsync(new CreateLibraryRequest("Test", _tempDir));

        Assert.True(result.Success);
        Assert.Equal("Test", result.Data!.Name);
        Assert.Single(_db.Libraries);
    }

    [Fact]
    public async Task CreateAsync_InvalidPath_ReturnsFail()
    {
        var result = await _svc.CreateAsync(new CreateLibraryRequest("Bad", "/nonexistent/path"));

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Error!);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesLibrary()
    {
        var lib = new Library { Name = "ToDelete", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        var result = await _svc.DeleteAsync(lib.Id);

        Assert.True(result.Success);
        Assert.Empty(_db.Libraries);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFail()
    {
        var result = await _svc.DeleteAsync(999);

        Assert.False(result.Success);
    }

    // ── SetExcludedFoldersAsync ───────────────────────────────────────

    [Fact]
    public async Task SetExcludedFolders_ReplacesAll()
    {
        var lib = new Library { Name = "Lib", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        // Create subdirectories within the library so paths are valid
        var dirA = Path.Combine(_tempDir, "a");
        var dirB = Path.Combine(_tempDir, "b");
        var dirC = Path.Combine(_tempDir, "c");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        Directory.CreateDirectory(dirC);

        // Set initial exclusions
        await _svc.SetExcludedFoldersAsync(lib.Id, [dirA, dirB]);
        Assert.Equal(2, _db.ExcludedFolders.Count(e => e.LibraryId == lib.Id));

        // Replace with new set
        await _svc.SetExcludedFoldersAsync(lib.Id, [dirC]);
        var folders = _db.ExcludedFolders.Where(e => e.LibraryId == lib.Id).ToList();
        Assert.Single(folders);
        Assert.EndsWith("c", folders[0].Path);
    }

    [Fact]
    public async Task SetExcludedFolders_PathOutsideLibrary_ReturnsFail()
    {
        var lib = new Library { Name = "Lib", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        var result = await _svc.SetExcludedFoldersAsync(lib.Id, ["/etc", "../outside"]);

        Assert.False(result.Success);
        Assert.Contains("within the library directory", result.Error!);
    }

    // ── ScanAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ScanAsync_NewFiles_AddsToDb()
    {
        CreateFile("movie1.mp4");
        CreateFile("movie2.mkv");

        var lib = new Library { Name = "Lib", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        var result = await _svc.ScanAsync(lib.Id);

        Assert.True(result.Success);
        Assert.Equal(2, _db.VideoFiles.Count());
    }

    [Fact]
    public async Task ScanAsync_WithNfo_ParsesMetadata()
    {
        CreateFile("movie.mp4");
        CreateFile("movie.nfo", """
            <movie>
              <title>Parsed Title</title>
              <year>2023</year>
            </movie>
            """);

        _nfoParser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new NfoData("Parsed Title", null, 2023, null, null, []));

        var lib = new Library { Name = "Lib", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        var result = await _svc.ScanAsync(lib.Id);

        Assert.True(result.Success);
        var vf = _db.VideoFiles.Single();
        Assert.Equal("Parsed Title", vf.Title);
        Assert.Equal(2023, vf.Year);
    }

    [Fact]
    public async Task ScanAsync_ExcludedFolder_SkipsFiles()
    {
        var excludedDir = Path.Combine(_tempDir, "excluded");
        Directory.CreateDirectory(excludedDir);
        CreateFile("keep/video.mp4");
        CreateFile("excluded/skip.mp4");

        var lib = new Library { Name = "Lib", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        _db.ExcludedFolders.Add(new ExcludedFolder
        {
            LibraryId = lib.Id,
            Path = Path.GetFullPath(excludedDir)
        });
        await _db.SaveChangesAsync();

        var result = await _svc.ScanAsync(lib.Id);

        Assert.True(result.Success);
        Assert.Single(_db.VideoFiles);
        Assert.Contains("keep", _db.VideoFiles.Single().FilePath);
    }

    [Fact]
    public async Task ScanAsync_LibraryNotFound_ReturnsFail()
    {
        var result = await _svc.ScanAsync(999);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task ScanAsync_WithStudioAndActors_CreatesEntities()
    {
        CreateFile("movie.mp4");
        CreateFile("movie.nfo");

        _nfoParser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new NfoData("Title", null, null, null, "Studio X",
                [new NfoActor("Alice", "Lead", 0)]));

        var lib = new Library { Name = "Lib", Path = _tempDir };
        _db.Libraries.Add(lib);
        await _db.SaveChangesAsync();

        await _svc.ScanAsync(lib.Id);

        Assert.Single(_db.Studios);
        Assert.Equal("Studio X", _db.Studios.Single().Name);
        Assert.Single(_db.Actors);
        Assert.Equal("Alice", _db.Actors.Single().Name);
    }
}
