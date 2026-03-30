using Microsoft.EntityFrameworkCore;
using NfoForge.Core.Interfaces;
using NfoForge.Core.Models;
using NfoForge.Data.Entities;

namespace NfoForge.Data.Services;

public class LibraryService(AppDbContext db) : ILibraryService
{
    public async Task<Result<IReadOnlyList<LibraryDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var libs = await db.Libraries
            .OrderBy(l => l.Name)
            .Select(l => new LibraryDto(l.Id, l.Name, l.Path, l.CreatedAt))
            .ToListAsync(ct);
        return Result<IReadOnlyList<LibraryDto>>.Ok(libs);
    }

    public async Task<Result<LibraryDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result<LibraryDto>.Fail("Library not found");
        return Result<LibraryDto>.Ok(new LibraryDto(lib.Id, lib.Name, lib.Path, lib.CreatedAt));
    }

    public async Task<Result<LibraryDto>> CreateAsync(CreateLibraryRequest request, CancellationToken ct = default)
    {
        if (!Directory.Exists(request.Path))
            return Result<LibraryDto>.Fail($"Path does not exist: {request.Path}");

        var lib = new Library { Name = request.Name, Path = request.Path };
        db.Libraries.Add(lib);
        await db.SaveChangesAsync(ct);
        return Result<LibraryDto>.Ok(new LibraryDto(lib.Id, lib.Name, lib.Path, lib.CreatedAt));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result.Fail("Library not found");
        db.Libraries.Remove(lib);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<Result<string>> ScanAsync(int id, CancellationToken ct = default)
    {
        // Placeholder — scan implementation will be added in next iteration
        return Task.FromResult(Result<string>.Ok($"Scan queued for library {id}"));
    }
}
