using Microsoft.Extensions.Logging.Abstractions;
using PortraMeta.Core.Interfaces;
using PortraMeta.Data;
using PortraMeta.Data.Entities;
using PortraMeta.Data.Services;
using NSubstitute;

namespace PortraMeta.Tests;

public class VideoServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
    private readonly INfoService _nfoService;
    private readonly VideoService _svc;

    public VideoServiceTests()
    {
        (_db, _conn) = Helpers.TestDbContext.Create();
        _nfoService = Substitute.For<INfoService>();
        _svc = new VideoService(_db, _nfoService, NullLogger<VideoService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private Library SeedLibrary(string name = "TestLib")
    {
        var lib = new Library { Name = name, Path = "/test" };
        _db.Libraries.Add(lib);
        _db.SaveChanges();
        return lib;
    }

    private VideoFile SeedVideo(Library lib, string fileName = "video.mp4", string? title = null, int? year = null, Studio? studio = null)
    {
        var vf = new VideoFile
        {
            Library = lib,
            FileName = fileName,
            FilePath = Path.Combine(lib.Path, fileName),
            FileSizeBytes = 1000,
            HasNfo = title != null,
            Title = title,
            Year = year,
            Studio = studio,
            ScannedAt = DateTime.UtcNow,
        };
        _db.VideoFiles.Add(vf);
        _db.SaveChanges();
        return vf;
    }

    // ── GetAllAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_NoFilter_ReturnsAll()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4");
        SeedVideo(lib, "b.mp4");
        SeedVideo(lib, "c.mp4");

        var result = await _svc.GetAllAsync(new VideoFileFilter(), 1, 50);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Total);
        Assert.Equal(3, result.Data.Items.Count);
    }

    [Fact]
    public async Task GetAllAsync_FilterByHasNfo()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "with-nfo.mp4", title: "Has NFO");
        SeedVideo(lib, "no-nfo.mp4");

        var result = await _svc.GetAllAsync(new VideoFileFilter(HasNfo: true), 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Has NFO", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_SearchByTitle()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "Action Movie");
        SeedVideo(lib, "b.mp4", title: "Comedy Film");

        var result = await _svc.GetAllAsync(new VideoFileFilter(Search: "action"), 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Action Movie", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_Pagination()
    {
        var lib = SeedLibrary();
        for (int i = 0; i < 5; i++)
            SeedVideo(lib, $"v{i}.mp4");

        var page1 = await _svc.GetAllAsync(new VideoFileFilter(), 1, 2);
        var page2 = await _svc.GetAllAsync(new VideoFileFilter(), 2, 2);

        Assert.Equal(5, page1.Data!.Total);
        Assert.Equal(2, page1.Data.Items.Count);
        Assert.Equal(2, page2.Data!.Items.Count);
        Assert.NotEqual(page1.Data.Items[0].FileName, page2.Data.Items[0].FileName);
    }

    [Fact]
    public async Task GetAllAsync_SortByYear()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "old.mp4", title: "Old", year: 2000);
        SeedVideo(lib, "new.mp4", title: "New", year: 2024);

        var asc = await _svc.GetAllAsync(new VideoFileFilter(SortBy: "year"), 1, 50);
        var desc = await _svc.GetAllAsync(new VideoFileFilter(SortBy: "year", SortDesc: true), 1, 50);

        Assert.Equal(2000, asc.Data!.Items[0].Year);
        Assert.Equal(2024, desc.Data!.Items[0].Year);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsVideo()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4", title: "Test");

        var result = await _svc.GetByIdAsync(vf.Id);

        Assert.True(result.Success);
        Assert.Equal("Test", result.Data!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsFail()
    {
        var result = await _svc.GetByIdAsync(999);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesActors()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4");
        var actor = new Actor { Name = "Test Actor" };
        _db.Actors.Add(actor);
        _db.VideoActors.Add(new VideoActor { VideoFile = vf, Actor = actor, Role = "Lead", Order = 0 });
        _db.SaveChanges();

        var result = await _svc.GetByIdAsync(vf.Id);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Actors!);
        Assert.Equal("Test Actor", result.Data.Actors![0].Name);
        Assert.Equal("Lead", result.Data.Actors[0].Role);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4");

        var result = await _svc.UpdateAsync(vf.Id, new UpdateVideoRequest("New Title", null, 2025, null, null));

        Assert.True(result.Success);
        Assert.Equal("New Title", result.Data!.Title);
        Assert.Equal(2025, result.Data.Year);
        await _nfoService.Received(1).WriteAsync(Arg.Any<string>(), Arg.Any<VideoFileDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_CreatesNewStudio()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4");

        var result = await _svc.UpdateAsync(vf.Id, new UpdateVideoRequest(null, null, null, null, "New Studio"));

        Assert.True(result.Success);
        Assert.Equal("New Studio", result.Data!.StudioName);
        Assert.Single(_db.Studios);
    }

    [Fact]
    public async Task UpdateAsync_ClearsStudio()
    {
        var studio = new Studio { Name = "Old Studio" };
        _db.Studios.Add(studio);
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4", studio: studio);

        var result = await _svc.UpdateAsync(vf.Id, new UpdateVideoRequest(null, null, null, null, null));

        Assert.True(result.Success);
        Assert.Null(result.Data!.StudioName);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesActors()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4");

        var actors = new List<ActorRequest> { new("Alice", "Lead", 0), new("Bob", null, 1) };
        var result = await _svc.UpdateAsync(vf.Id, new UpdateVideoRequest(null, null, null, null, null, actors));

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Actors!.Count);
        Assert.Equal("Alice", result.Data.Actors[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFail()
    {
        var result = await _svc.UpdateAsync(999, new UpdateVideoRequest("T", null, null, null, null));

        Assert.False(result.Success);
    }

    // ── BatchUpdateAsync ──────────────────────────────────────────────

    [Fact]
    public async Task BatchUpdateAsync_UpdatesMultiple()
    {
        var lib = SeedLibrary();
        var v1 = SeedVideo(lib, "a.mp4");
        var v2 = SeedVideo(lib, "b.mp4");
        var v3 = SeedVideo(lib, "c.mp4");

        var result = await _svc.BatchUpdateAsync(new BatchUpdateVideoRequest(
            [v1.Id, v2.Id, v3.Id], Title: "Batch"));

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Updated);
        Assert.Empty(result.Data.Failed);
    }

    [Fact]
    public async Task BatchUpdateAsync_PartialFailure()
    {
        var lib = SeedLibrary();
        var v1 = SeedVideo(lib, "a.mp4");

        var result = await _svc.BatchUpdateAsync(new BatchUpdateVideoRequest(
            [v1.Id, 999], Title: "X"));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Updated);
        Assert.Single(result.Data.Failed);
        Assert.Equal(999, result.Data.Failed[0]);
    }

    [Fact]
    public async Task BatchUpdateAsync_EmptyIds_ReturnsZero()
    {
        var result = await _svc.BatchUpdateAsync(new BatchUpdateVideoRequest([], Title: "X"));

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Updated);
    }

    // ── BatchDeleteAsync ──────────────────────────────────────────────

    [Fact]
    public async Task BatchDeleteAsync_MetadataMode_ClearsFlags()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4", title: "T");
        vf.HasNfo = true;
        vf.HasPoster = true;
        _db.SaveChanges();

        var result = await _svc.BatchDeleteAsync(new BatchDeleteRequest([vf.Id], DeleteMode.Metadata));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Deleted);

        var updated = await _db.VideoFiles.FindAsync(vf.Id);
        Assert.NotNull(updated);
        Assert.False(updated!.HasNfo);
        Assert.False(updated.HasPoster);
        Assert.Null(updated.Title);
    }

    [Fact]
    public async Task BatchDeleteAsync_VideoMode_RemovesRecord()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4");

        var result = await _svc.BatchDeleteAsync(new BatchDeleteRequest([vf.Id], DeleteMode.Video));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Deleted);
        Assert.Null(await _db.VideoFiles.FindAsync(vf.Id));
    }

    [Fact]
    public async Task BatchDeleteAsync_AllMode_RemovesEverything()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "test.mp4", title: "T");
        vf.HasNfo = true;
        _db.SaveChanges();

        var result = await _svc.BatchDeleteAsync(new BatchDeleteRequest([vf.Id], DeleteMode.All));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Deleted);
        Assert.Null(await _db.VideoFiles.FindAsync(vf.Id));
    }
}
