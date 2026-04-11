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

    private VideoFile SeedVideo(Library lib, string fileName = "video.mp4", string? title = null, int? year = null, Studio? studio = null, bool? hasPoster = null, bool? hasFanart = null)
    {
        var vf = new VideoFile
        {
            Library = lib,
            FileName = fileName,
            FilePath = Path.Combine(lib.Path, fileName),
            FileSizeBytes = 1000,
            HasNfo = title != null,
            HasPoster = hasPoster ?? false,
            HasFanart = hasFanart ?? false,
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

    // ── RevealInFileManagerAsync ──────────────────────────────────────

    [Fact]
    public async Task RevealInFileManagerAsync_NotFound_ReturnsFail()
    {
        var result = await _svc.RevealInFileManagerAsync(999);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevealInFileManagerAsync_FileNotOnDisk_ReturnsFail()
    {
        var lib = SeedLibrary();
        var vf = SeedVideo(lib, "nonexistent.mp4");

        var result = await _svc.RevealInFileManagerAsync(vf.Id);

        Assert.False(result.Success);
        Assert.Contains("not found on disk", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevealInFileManagerAsync_FileExists_ReturnsOk()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "portrameta_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        var tmpFile = Path.Combine(tmpDir, "test.mp4");
        File.WriteAllBytes(tmpFile, [0]);

        try
        {
            var lib = new Library { Name = "TmpLib", Path = tmpDir };
            _db.Libraries.Add(lib);
            _db.SaveChanges();

            var vf = new VideoFile
            {
                Library = lib,
                FileName = "test.mp4",
                FilePath = tmpFile,
                FileSizeBytes = 1,
                ScannedAt = DateTime.UtcNow,
            };
            _db.VideoFiles.Add(vf);
            _db.SaveChanges();

            var result = await _svc.RevealInFileManagerAsync(vf.Id);

            // On CI/headless environments Process.Start may fail, but file lookup should succeed.
            // We accept both Ok (has display) and Fail with "Cannot open" (no display).
            Assert.True(result.Success || result.Error!.Contains("Cannot open"));
        }
        finally
        {
            File.Delete(tmpFile);
            Directory.Delete(tmpDir);
        }
    }

    // ── Multiple Boolean Quick Filters ────────────────────────────────

    [Fact]
    public async Task GetAllAsync_MultipleBooleanFilters_And()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "both.mp4", title: "Both", hasPoster: true, hasFanart: true);
        SeedVideo(lib, "poster-only.mp4", title: "PosterOnly", hasPoster: true, hasFanart: false);
        SeedVideo(lib, "fanart-only.mp4", title: "FanartOnly", hasPoster: false, hasFanart: true);
        SeedVideo(lib, "neither.mp4");

        var filter = new VideoFileFilter(HasPoster: true, HasFanart: true);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Both", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_BooleanFilter_NoPoster_NoFanart()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "both.mp4", title: "Both", hasPoster: true, hasFanart: true);
        SeedVideo(lib, "neither.mp4", hasPoster: false, hasFanart: false);

        var filter = new VideoFileFilter(HasPoster: false, HasFanart: false);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("neither.mp4", result.Data.Items[0].FileName);
    }

    [Fact]
    public async Task GetAllAsync_AllThreeBooleanFilters()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "full.mp4", title: "Full", hasPoster: true, hasFanart: true);
        SeedVideo(lib, "partial.mp4", title: "Partial", hasPoster: true, hasFanart: false);
        SeedVideo(lib, "none.mp4");

        var filter = new VideoFileFilter(HasNfo: true, HasPoster: true, HasFanart: true);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Full", result.Data.Items[0].Title);
    }

    // ── Advanced Filters ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_TextContains()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "Action Movie");
        SeedVideo(lib, "b.mp4", title: "Comedy Show");

        var filters = new List<AdvancedFilterItem> { new("title", "contains", "Action") };
        var filter = new VideoFileFilter(AdvancedFilters: filters);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Action Movie", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_NumericEquals()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "Old", year: 2000);
        SeedVideo(lib, "b.mp4", title: "New", year: 2024);

        var filters = new List<AdvancedFilterItem> { new("year", "eq", "2024") };
        var filter = new VideoFileFilter(AdvancedFilters: filters);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("New", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_NumericRange()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "Y2000", year: 2000);
        SeedVideo(lib, "b.mp4", title: "Y2010", year: 2010);
        SeedVideo(lib, "c.mp4", title: "Y2024", year: 2024);

        var filters = new List<AdvancedFilterItem>
        {
            new("year", "gte", "2005"),
            new("year", "lte", "2020"),
        };
        var filter = new VideoFileFilter(AdvancedFilters: filters, FilterLogic: "and");
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Y2010", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_BooleanIs()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "WithPoster", hasPoster: true);
        SeedVideo(lib, "b.mp4", title: "NoPoster", hasPoster: false);

        var filters = new List<AdvancedFilterItem> { new("hasPoster", "is", "true") };
        var filter = new VideoFileFilter(AdvancedFilters: filters);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("WithPoster", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_OrLogic()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "Alpha", year: 2000);
        SeedVideo(lib, "b.mp4", title: "Beta", year: 2024);
        SeedVideo(lib, "c.mp4", title: "Gamma", year: 2010);

        var filters = new List<AdvancedFilterItem>
        {
            new("year", "eq", "2000"),
            new("year", "eq", "2024"),
        };
        var filter = new VideoFileFilter(AdvancedFilters: filters, FilterLogic: "or");
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Items.Count);
        var titles = result.Data.Items.Select(i => i.Title).OrderBy(t => t).ToList();
        Assert.Equal(["Alpha", "Beta"], titles);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_MultipleBooleans_And()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4", title: "All", hasPoster: true, hasFanart: true);
        SeedVideo(lib, "b.mp4", title: "PosterOnly", hasPoster: true, hasFanart: false);
        SeedVideo(lib, "c.mp4", title: "None");

        var filters = new List<AdvancedFilterItem>
        {
            new("hasPoster", "is", "true"),
            new("hasFanart", "is", "true"),
        };
        var filter = new VideoFileFilter(AdvancedFilters: filters, FilterLogic: "and");
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("All", result.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_TextIsEmpty()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "titled.mp4", title: "Has Title");
        SeedVideo(lib, "untitled.mp4");

        var filters = new List<AdvancedFilterItem> { new("title", "isempty", "") };
        var filter = new VideoFileFilter(AdvancedFilters: filters);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("untitled.mp4", result.Data.Items[0].FileName);
    }

    [Fact]
    public async Task GetAllAsync_AdvancedFilter_UnknownField_Ignored()
    {
        var lib = SeedLibrary();
        SeedVideo(lib, "a.mp4");

        var filters = new List<AdvancedFilterItem> { new("nonexistent", "eq", "x") };
        var filter = new VideoFileFilter(AdvancedFilters: filters);
        var result = await _svc.GetAllAsync(filter, 1, 50);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
    }
}
