using NfoForge.Core.Models;

namespace NfoForge.Core.Interfaces;

public record LibraryDto(int Id, string Name, string Path, DateTime CreatedAt);
public record CreateLibraryRequest(string Name, string Path);

public interface ILibraryService
{
    Task<Result<IReadOnlyList<LibraryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<LibraryDto>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<LibraryDto>> CreateAsync(CreateLibraryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<string>> ScanAsync(int id, CancellationToken ct = default);

    /// <summary>Returns immediate subdirectories of the library root path.</summary>
    Task<Result<IReadOnlyList<string>>> GetSubdirectoriesAsync(int id, CancellationToken ct = default);

    /// <summary>Returns the set of excluded subdirectory paths for a library.</summary>
    Task<Result<IReadOnlyList<string>>> GetExcludedFoldersAsync(int id, CancellationToken ct = default);

    /// <summary>Replaces the full set of excluded folder paths for a library.</summary>
    Task<Result> SetExcludedFoldersAsync(int id, IReadOnlyList<string> paths, CancellationToken ct = default);
}
