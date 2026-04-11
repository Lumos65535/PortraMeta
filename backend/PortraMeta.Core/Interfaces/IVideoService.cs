using System.Text.Json.Serialization;
using PortraMeta.Core.Models;

namespace PortraMeta.Core.Interfaces;

public record ActorDto(int Id, string Name, string? Role, int Order);

public record RatingDto(string Name, decimal Value, int Votes, int Max = 10);

public record VideoFileDto(
    int Id,
    int LibraryId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    bool HasNfo,
    bool HasPoster,
    bool HasFanart,
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? StudioName,
    DateTime ScannedAt,
    IReadOnlyList<ActorDto>? Actors = null,
    DateTime? FileModifiedAt = null,
    // Tier 1
    IReadOnlyList<string>? Directors = null,
    IReadOnlyList<string>? Genres = null,
    int? Runtime = null,
    string? Mpaa = null,
    string? Premiered = null,
    IReadOnlyList<RatingDto>? Ratings = null,
    int? UserRating = null,
    IDictionary<string, string>? UniqueIds = null,
    IReadOnlyList<string>? Tags = null,
    string? SortTitle = null,
    // Tier 2
    string? Outline = null,
    string? Tagline = null,
    IReadOnlyList<string>? Credits = null,
    IReadOnlyList<string>? Countries = null,
    // Tier 3
    string? SetName = null,
    string? DateAdded = null,
    int? Top250 = null
);

public record VideoFileFilter(
    bool? HasNfo = null,
    bool? HasPoster = null,
    bool? HasFanart = null,
    int? LibraryId = null,
    int? StudioId = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false,
    IReadOnlyList<AdvancedFilterItem>? AdvancedFilters = null,
    string FilterLogic = "and"
);

public record AdvancedFilterItem(string Field, string Op, string Value);

public record ActorRequest(string Name, string? Role, int Order);

public record RatingRequest(string Name, decimal Value, int Votes, int Max = 10);

public record UpdateVideoRequest(
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? StudioName,
    IReadOnlyList<ActorRequest>? Actors = null,
    // Tier 1
    IReadOnlyList<string>? Directors = null,
    IReadOnlyList<string>? Genres = null,
    int? Runtime = null,
    string? Mpaa = null,
    string? Premiered = null,
    IReadOnlyList<RatingRequest>? Ratings = null,
    int? UserRating = null,
    IDictionary<string, string>? UniqueIds = null,
    IReadOnlyList<string>? Tags = null,
    string? SortTitle = null,
    // Tier 2
    string? Outline = null,
    string? Tagline = null,
    IReadOnlyList<string>? Credits = null,
    IReadOnlyList<string>? Countries = null,
    // Tier 3
    string? SetName = null,
    string? DateAdded = null,
    int? Top250 = null
);

public record ImportFromPathRequest(string Path);

public record BatchUpdateVideoRequest(
    int[] Ids,
    string? Title = null,
    string? OriginalTitle = null,
    int? Year = null,
    string? Plot = null,
    string? StudioName = null,
    IReadOnlyList<string>? Directors = null,
    IReadOnlyList<string>? Genres = null,
    int? Runtime = null,
    string? Mpaa = null,
    string? Premiered = null,
    int? UserRating = null,
    IReadOnlyList<string>? Tags = null,
    string? SortTitle = null,
    string? Outline = null,
    string? Tagline = null,
    IReadOnlyList<string>? Credits = null,
    IReadOnlyList<string>? Countries = null,
    string? SetName = null,
    string? DateAdded = null,
    int? Top250 = null
);

public record BatchUpdateResult(int Updated, int[] Failed);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeleteMode { Metadata, Video, All }

public record BatchDeleteRequest(int[] Ids, DeleteMode Mode);

public record BatchDeleteResult(int Deleted, int[] Failed);

public interface IVideoService
{
    Task<Result<PagedResult<VideoFileDto>>> GetAllAsync(
        VideoFileFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<Result<VideoFileDto>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<VideoFileDto>> UpdateAsync(int id, UpdateVideoRequest request, CancellationToken ct = default);
    Task<Result<string>> GetPosterPathAsync(int id, CancellationToken ct = default);
    Task<Result<VideoFileDto>> UploadPosterAsync(
        int id, Stream imageStream, string contentType, long contentLength,
        CancellationToken ct = default);
    Task<Result<string>> GetFanartPathAsync(int id, CancellationToken ct = default);
    Task<Result<VideoFileDto>> UploadFanartAsync(
        int id, Stream imageStream, string contentType, long contentLength,
        CancellationToken ct = default);
    Task<Result<VideoFileDto>> ImportPosterFromPathAsync(int id, string path, CancellationToken ct = default);
    Task<Result<VideoFileDto>> ImportFanartFromPathAsync(int id, string path, CancellationToken ct = default);
    Task<Result<BatchUpdateResult>> BatchUpdateAsync(BatchUpdateVideoRequest request, CancellationToken ct = default);
    Task<Result<BatchDeleteResult>> BatchDeleteAsync(BatchDeleteRequest request, CancellationToken ct = default);
    Task<Result> RevealInFileManagerAsync(int id, CancellationToken ct = default);
    Task<Result> OpenVideoFileAsync(int id, CancellationToken ct = default);
}
