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
}
