using NfoForge.Core.Constants;

namespace NfoForge.Data.Utilities;

public class FileSystemScanner
{
    // ── Static path helpers ────────────────────────────────────────────────

    /// <summary>video.mp4 → video.nfo</summary>
    public static string NfoPath(string videoFilePath) =>
        Path.ChangeExtension(videoFilePath, ".nfo");

    /// <summary>video.mp4 → video-poster.jpg</summary>
    public static string PosterPath(string videoFilePath) =>
        Path.Combine(
            Path.GetDirectoryName(videoFilePath)!,
            Path.GetFileNameWithoutExtension(videoFilePath) + "-poster.jpg");

    /// <summary>video.mp4 → video-fanart.jpg</summary>
    public static string FanartPath(string videoFilePath) =>
        Path.Combine(
            Path.GetDirectoryName(videoFilePath)!,
            Path.GetFileNameWithoutExtension(videoFilePath) + "-fanart.jpg");

    // ── Instance methods ───────────────────────────────────────────────────

    public async Task<IEnumerable<FileInfo>> FindVideoFilesRecursiveAsync(
        string libraryPath,
        IReadOnlySet<string>? excludedPaths = null,
        CancellationToken ct = default)
    {
        var result = new List<FileInfo>();
        await ScanDirectoryRecursiveAsync(libraryPath, result, excludedPaths, ct);
        return result;
    }

    private async Task ScanDirectoryRecursiveAsync(
        string path, List<FileInfo> videos, IReadOnlySet<string>? excludedPaths, CancellationToken ct)
    {
        try
        {
            var dir = new DirectoryInfo(path);

            foreach (var file in dir.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                if (VideoFormats.IsVideoFile(file.Name))
                    videos.Add(file);
            }

            foreach (var subdir in dir.GetDirectories())
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = Path.GetFullPath(subdir.FullName);
                if (excludedPaths is not null && excludedPaths.Contains(fullPath))
                    continue;

                try
                {
                    await ScanDirectoryRecursiveAsync(subdir.FullName, videos, excludedPaths, ct);
                }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Returns immediate subdirectory paths under the given root.</summary>
    public IReadOnlyList<string> GetSubdirectories(string libraryPath)
    {
        try
        {
            return Directory.GetDirectories(libraryPath)
                .Select(Path.GetFullPath)
                .Order()
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public bool HasNfoFile(string videoFilePath) => File.Exists(NfoPath(videoFilePath));
    public bool HasPosterFile(string videoFilePath) => File.Exists(PosterPath(videoFilePath));
    public bool HasFanartFile(string videoFilePath) => File.Exists(FanartPath(videoFilePath));
}
