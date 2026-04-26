namespace PortraMeta.Core.Interfaces;

public record MediaInfoResult(
    double? DurationSeconds,
    string? VideoCodec,
    string? AudioCodec,
    int? Width,
    int? Height,
    double? FrameRate,
    long? BitRate
);

public interface IMediaInfoService
{
    Task<MediaInfoResult?> ProbeAsync(string filePath, CancellationToken ct = default);
}
