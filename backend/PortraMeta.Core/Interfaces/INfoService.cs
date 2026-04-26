namespace PortraMeta.Core.Interfaces;

public interface INfoService
{
    /// <summary>
    /// Writes a Kodi Movie NFO file for the given video metadata.
    /// </summary>
    Task WriteAsync(string nfoPath, VideoFileDto video, CancellationToken ct = default);

    /// <summary>
    /// Writes or updates the &lt;fileinfo&gt; section in an existing NFO file with media info data.
    /// Does nothing if the NFO file does not exist.
    /// </summary>
    Task WriteFileInfoAsync(string nfoPath, MediaInfoResult mediaInfo, CancellationToken ct = default);
}
