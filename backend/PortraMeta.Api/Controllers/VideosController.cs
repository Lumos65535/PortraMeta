using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortraMeta.Core.Interfaces;

namespace PortraMeta.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideosController(IVideoService videoService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        var result = await videoService.GetFilterOptionsAsync(ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? has_nfo,
        [FromQuery] bool? has_poster,
        [FromQuery] bool? has_fanart,
        [FromQuery] int? library_id,
        [FromQuery] int? studio_id,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 50,
        [FromQuery] string? sort_by = null,
        [FromQuery] bool sort_desc = false,
        [FromQuery] string? filters = null,
        [FromQuery] string filter_logic = "and",
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        page_size = Math.Clamp(page_size, 1, 500);

        IReadOnlyList<AdvancedFilterItem>? advancedFilters = null;
        if (!string.IsNullOrEmpty(filters))
        {
            try
            {
                advancedFilters = JsonSerializer.Deserialize<List<AdvancedFilterItem>>(filters, JsonOpts);
            }
            catch (JsonException)
            {
                return BadRequest(new { error = "Invalid filters JSON format", success = false });
            }
        }

        var filter = new VideoFileFilter(has_nfo, has_poster, has_fanart, library_id, studio_id, search, sort_by, sort_desc, advancedFilters, filter_logic);
        var result = await videoService.GetAllAsync(filter, page, page_size, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    private const int MaxBatchSize = 500;

    [HttpPut("batch")]
    public async Task<IActionResult> BatchUpdate([FromBody] BatchUpdateVideoRequest request, CancellationToken ct)
    {
        if (request.Ids.Length == 0)
            return BadRequest(new { error = "No IDs provided", success = false });
        if (request.Ids.Length > MaxBatchSize)
            return BadRequest(new { error = $"Batch size exceeds maximum of {MaxBatchSize}", success = false });

        var result = await videoService.BatchUpdateAsync(request, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpPost("batch/delete")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest request, CancellationToken ct)
    {
        if (request.Ids.Length == 0)
            return BadRequest(new { error = "No IDs provided", success = false });
        if (request.Ids.Length > MaxBatchSize)
            return BadRequest(new { error = $"Batch size exceeds maximum of {MaxBatchSize}", success = false });

        var result = await videoService.BatchDeleteAsync(request, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await videoService.GetByIdAsync(id, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : NotFound(new { error = result.Error, success = false });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVideoRequest request, CancellationToken ct)
    {
        var result = await videoService.UpdateAsync(id, request, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : NotFound(new { error = result.Error, success = false });
    }

    [HttpGet("{id:int}/poster")]
    public async Task<IActionResult> GetPoster(int id, CancellationToken ct)
    {
        var result = await videoService.GetPosterPathAsync(id, ct);
        if (!result.Success)
            return NotFound(new { error = result.Error, success = false });

        var ext = Path.GetExtension(result.Data!).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return PhysicalFile(result.Data!, mime);
    }

    [HttpPost("{id:int}/poster")]
    [RequestSizeLimit(10 * 1024 * 1024 + 4096)]
    public async Task<IActionResult> UploadPoster(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided", success = false });

        using var stream = file.OpenReadStream();
        var result = await videoService.UploadPosterAsync(id, stream, file.ContentType, file.Length, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error, success = false })
                : BadRequest(new { error = result.Error, success = false });
    }

    [HttpGet("{id:int}/fanart")]
    public async Task<IActionResult> GetFanart(int id, CancellationToken ct)
    {
        var result = await videoService.GetFanartPathAsync(id, ct);
        if (!result.Success)
            return NotFound(new { error = result.Error, success = false });

        var ext = Path.GetExtension(result.Data!).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return PhysicalFile(result.Data!, mime);
    }

    [HttpPost("{id:int}/poster/from-path")]
    public async Task<IActionResult> ImportPosterFromPath(int id, [FromBody] ImportFromPathRequest request, CancellationToken ct)
    {
        var result = await videoService.ImportPosterFromPathAsync(id, request.Path, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error, success = false })
                : BadRequest(new { error = result.Error, success = false });
    }

    [HttpPost("{id:int}/fanart")]
    [RequestSizeLimit(10 * 1024 * 1024 + 4096)]
    public async Task<IActionResult> UploadFanart(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided", success = false });

        using var stream = file.OpenReadStream();
        var result = await videoService.UploadFanartAsync(id, stream, file.ContentType, file.Length, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error, success = false })
                : BadRequest(new { error = result.Error, success = false });
    }

    [HttpPost("{id:int}/reveal")]
    public async Task<IActionResult> RevealInFileManager(int id, CancellationToken ct)
    {
        var result = await videoService.RevealInFileManagerAsync(id, ct);
        return result.Success
            ? Ok(new { success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpPost("{id:int}/open")]
    public async Task<IActionResult> OpenVideoFile(int id, CancellationToken ct)
    {
        var result = await videoService.OpenVideoFileAsync(id, ct);
        return result.Success
            ? Ok(new { success = true })
            : BadRequest(new { error = result.Error, success = false });
    }

    [HttpPost("{id:int}/fanart/from-path")]
    public async Task<IActionResult> ImportFanartFromPath(int id, [FromBody] ImportFromPathRequest request, CancellationToken ct)
    {
        var result = await videoService.ImportFanartFromPathAsync(id, request.Path, ct);
        return result.Success
            ? Ok(new { data = result.Data, success = true })
            : result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error, success = false })
                : BadRequest(new { error = result.Error, success = false });
    }
}
