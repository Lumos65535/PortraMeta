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
}
