using Microsoft.AspNetCore.Mvc;
using NfoForge.Core.Interfaces;

namespace NfoForge.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideosController(IVideoService videoService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? has_nfo,
        [FromQuery] bool? has_poster,
        [FromQuery] int? library_id,
        [FromQuery] int? studio_id,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 50,
        CancellationToken ct = default)
    {
        var filter = new VideoFileFilter(has_nfo, has_poster, library_id, studio_id, search);
        var result = await videoService.GetAllAsync(filter, page, page_size, ct);
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
}
