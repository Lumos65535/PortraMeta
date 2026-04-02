namespace PortraMeta.Core.Interfaces;

public interface INfoService
{
    /// <summary>
    /// Writes a Kodi Movie NFO file for the given video metadata.
    /// </summary>
    Task WriteAsync(string nfoPath, VideoFileDto video, CancellationToken ct = default);
}
