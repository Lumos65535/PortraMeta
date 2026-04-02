using PortraMeta.Data.Utilities;

namespace PortraMeta.Tests;

public class FileSystemScannerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "portrameta_test_" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemScanner _scanner = new();

    public FileSystemScannerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateFile(string relativePath)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [0]);
        return fullPath;
    }

    // ── Static path helper tests ──────────────────────────────────────

    [Fact]
    public void NfoPath_ReturnsCorrectPath()
    {
        var result = FileSystemScanner.NfoPath("/videos/movie.mp4");
        Assert.Equal("/videos/movie.nfo", result);
    }

    [Fact]
    public void PosterPath_ReturnsCorrectPath()
    {
        var result = FileSystemScanner.PosterPath("/videos/movie.mp4");
        Assert.Equal("/videos/movie-poster.jpg", result);
    }

    [Fact]
    public void FanartPath_ReturnsCorrectPath()
    {
        var result = FileSystemScanner.FanartPath("/videos/movie.mp4");
        Assert.Equal("/videos/movie-fanart.jpg", result);
    }

    // ── FindVideoFilesRecursiveAsync ──────────────────────────────────

    [Fact]
    public async Task FindVideoFiles_FindsAllFormats()
    {
        CreateFile("a.mp4");
        CreateFile("b.mkv");
        CreateFile("c.avi");
        CreateFile("d.mov");

        var files = (await _scanner.FindVideoFilesRecursiveAsync(_tempDir)).ToList();

        Assert.Equal(4, files.Count);
    }

    [Fact]
    public async Task FindVideoFiles_RecursesSubdirs()
    {
        CreateFile("root.mp4");
        CreateFile("sub1/deep.mkv");
        CreateFile("sub1/sub2/deeper.avi");

        var files = (await _scanner.FindVideoFilesRecursiveAsync(_tempDir)).ToList();

        Assert.Equal(3, files.Count);
    }

    [Fact]
    public async Task FindVideoFiles_RespectsExclusions()
    {
        CreateFile("keep/video.mp4");
        CreateFile("skip/video.mp4");

        var excluded = new HashSet<string> { Path.GetFullPath(Path.Combine(_tempDir, "skip")) };
        var files = (await _scanner.FindVideoFilesRecursiveAsync(_tempDir, excluded)).ToList();

        Assert.Single(files);
        Assert.Contains("keep", files[0].FullName);
    }

    [Fact]
    public async Task FindVideoFiles_IgnoresNonVideo()
    {
        CreateFile("readme.txt");
        CreateFile("image.jpg");
        CreateFile("video.mp4");

        var files = (await _scanner.FindVideoFilesRecursiveAsync(_tempDir)).ToList();

        Assert.Single(files);
        Assert.Equal("video.mp4", files[0].Name);
    }

    [Fact]
    public async Task FindVideoFiles_EmptyDir_ReturnsEmpty()
    {
        var files = (await _scanner.FindVideoFilesRecursiveAsync(_tempDir)).ToList();

        Assert.Empty(files);
    }

    // ── HasNfoFile / HasPosterFile / HasFanartFile ────────────────────

    [Fact]
    public void HasNfoFile_True_WhenExists()
    {
        var videoPath = CreateFile("movie.mp4");
        CreateFile("movie.nfo");

        Assert.True(_scanner.HasNfoFile(videoPath));
    }

    [Fact]
    public void HasNfoFile_False_WhenMissing()
    {
        var videoPath = CreateFile("movie.mp4");

        Assert.False(_scanner.HasNfoFile(videoPath));
    }

    [Fact]
    public void HasPosterFile_FindsJpg()
    {
        var videoPath = CreateFile("movie.mp4");
        CreateFile("movie-poster.jpg");

        Assert.True(_scanner.HasPosterFile(videoPath));
    }

    [Fact]
    public void HasPosterFile_FindsPng()
    {
        var videoPath = CreateFile("movie.mp4");
        CreateFile("movie-poster.png");

        Assert.True(_scanner.HasPosterFile(videoPath));
    }

    [Fact]
    public void HasPosterFile_False_WhenMissing()
    {
        var videoPath = CreateFile("movie.mp4");

        Assert.False(_scanner.HasPosterFile(videoPath));
    }

    [Fact]
    public void HasFanartFile_FindsJpeg()
    {
        var videoPath = CreateFile("movie.mp4");
        CreateFile("movie-fanart.jpeg");

        Assert.True(_scanner.HasFanartFile(videoPath));
    }

    [Fact]
    public void HasFanartFile_False_WhenMissing()
    {
        var videoPath = CreateFile("movie.mp4");

        Assert.False(_scanner.HasFanartFile(videoPath));
    }

    // ── GetSubdirectories ─────────────────────────────────────────────

    [Fact]
    public void GetSubdirectories_ReturnsSorted()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "charlie"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "alpha"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "bravo"));

        var subdirs = _scanner.GetSubdirectories(_tempDir);

        Assert.Equal(3, subdirs.Count);
        Assert.EndsWith("alpha", subdirs[0]);
        Assert.EndsWith("bravo", subdirs[1]);
        Assert.EndsWith("charlie", subdirs[2]);
    }

    [Fact]
    public void GetSubdirectories_EmptyDir_ReturnsEmpty()
    {
        var subdirs = _scanner.GetSubdirectories(_tempDir);

        Assert.Empty(subdirs);
    }
}
