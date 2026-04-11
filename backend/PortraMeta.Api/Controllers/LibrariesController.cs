using Microsoft.AspNetCore.Mvc;
using PortraMeta.Core.Interfaces;

namespace PortraMeta.Api.Controllers;

[ApiController]
[Route("api/libraries")]
public class LibrariesController(ILibraryService libraryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await libraryService.GetAllAsync(ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await libraryService.GetByIdAsync(id, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : NotFound(new { error = result.Error, success = false });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLibraryRequest request, CancellationToken ct)
    {
        var result = await libraryService.CreateAsync(request, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await libraryService.DeleteAsync(id, ct);
        return result.Success
            ? NoContent()
            : NotFound(new { error = result.Error, success = false });
    }

    [HttpPost("{id:int}/scan")]
    public async Task<IActionResult> Scan(int id, CancellationToken ct)
    {
        var result = await libraryService.ScanAsync(id, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpGet("{id:int}/scan-status")]
    public IActionResult GetScanStatus(int id)
    {
        var progress = libraryService.GetScanProgress(id);
        return Ok(new { data = progress, success = true });
    }

    [HttpGet("{id:int}/subdirectories")]
    public async Task<IActionResult> GetSubdirectories(int id, CancellationToken ct)
    {
        var result = await libraryService.GetSubdirectoriesAsync(id, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : NotFound(new { error = result.Error, success = false });
    }

    [HttpGet("{id:int}/excluded-folders")]
    public async Task<IActionResult> GetExcludedFolders(int id, CancellationToken ct)
    {
        var result = await libraryService.GetExcludedFoldersAsync(id, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : NotFound(new { error = result.Error, success = false });
    }

    [HttpPut("{id:int}/excluded-folders")]
    public async Task<IActionResult> SetExcludedFolders(
        int id, [FromBody] SetExcludedFoldersRequest request, CancellationToken ct)
    {
        var result = await libraryService.SetExcludedFoldersAsync(id, request.Paths, ct);
        return result.Success
            ? NoContent()
            : NotFound(new { error = result.Error, success = false });
    }
}

public record SetExcludedFoldersRequest(IReadOnlyList<string> Paths);
