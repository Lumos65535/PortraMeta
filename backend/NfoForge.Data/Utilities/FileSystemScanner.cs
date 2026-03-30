using NfoForge.Core.Constants;

namespace NfoForge.Data.Utilities;

public class FileSystemScanner
{
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
                catch (UnauthorizedAccessException)
                {
                    // Silently skip directories we can't access
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Silently continue if we can't access this directory
        }
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

    public bool HasNfoFile(string videoFilePath)
    {
        // Standard naming: video.mp4 → video.nfo (not video.mp4.nfo)
        var nfoPath = Path.ChangeExtension(videoFilePath, ".nfo");
        return File.Exists(nfoPath);
    }

    public bool HasPosterFile(string videoFilePath)
    {
        // Standard naming: video.mp4 → video.poster.jpg (not video.mp4.poster.jpg)
        var posterPath = Path.ChangeExtension(videoFilePath, ".poster.jpg");
        return File.Exists(posterPath);
    }
}
