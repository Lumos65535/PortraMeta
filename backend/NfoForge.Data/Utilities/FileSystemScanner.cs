using NfoForge.Core.Constants;

namespace NfoForge.Data.Utilities;

public class FileSystemScanner
{
    public async Task<IEnumerable<FileInfo>> FindVideoFilesRecursiveAsync(
        string libraryPath, CancellationToken ct = default)
    {
        var result = new List<FileInfo>();
        await ScanDirectoryRecursiveAsync(libraryPath, result, ct);
        return result;
    }

    private async Task ScanDirectoryRecursiveAsync(
        string path, List<FileInfo> videos, CancellationToken ct)
    {
        try
        {
            var dir = new DirectoryInfo(path);

            // Get files in current directory
            foreach (var file in dir.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                if (VideoFormats.IsVideoFile(file.Name))
                    videos.Add(file);
            }

            // Recursively scan subdirectories
            foreach (var subdir in dir.GetDirectories())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await ScanDirectoryRecursiveAsync(subdir.FullName, videos, ct);
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

    public bool HasNfoFile(string videoFilePath)
    {
        var nfoPath = $"{videoFilePath}.nfo";
        return File.Exists(nfoPath);
    }

    public bool HasPosterFile(string videoFilePath)
    {
        var posterPath = $"{videoFilePath}.poster.jpg";
        return File.Exists(posterPath);
    }
}
