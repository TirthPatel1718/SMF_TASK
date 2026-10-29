using Microsoft.AspNetCore.Mvc;
using StreamCopy.Services;

namespace StreamCopy.Controllers;

[ApiController]
[Route("api/copy")]
public class FileCopyController : ControllerBase
{
    private readonly FileCopyService _copyService;

    public FileCopyController(FileCopyService copyService)
    {
        _copyService = copyService;
    }

    [HttpPost("upload-and-copy")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> UploadAndCopy(IFormFile file, [FromForm] string destination)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file selected.");

        if (string.IsNullOrWhiteSpace(destination))
            return BadRequest("Destination folder is required.");

        if (_copyService.IsBusy)
            return BadRequest("A copy operation is already running.");

        _copyService.PrepareOperation();

        try
        {
            await using var stream = file.OpenReadStream();
            await _copyService.CopyStreamAsync(stream, destination, file.FileName, file.Length);

            
            return Ok(new { status = "completed", message = "Transfer finished." });
        }
        catch (OperationCanceledException)
        {
            return Ok(new { status = "cancelled", message = "Operation cancelled." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("pause")]
    public IActionResult Pause()
    {
        if (!_copyService.IsBusy) return BadRequest("No active operation.");
        _copyService.Pause();
        return Ok();
    }

    [HttpPost("resume")]
    public IActionResult Resume()
    {
        if (!_copyService.IsBusy) return BadRequest("No active operation.");
        _copyService.Resume();
        return Ok();
    }

    [HttpPost("cancel")]
    public IActionResult Cancel()
    {
        if (!_copyService.IsBusy) return BadRequest("No active operation.");
        _copyService.Cancel();
        return Ok();
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            isBusy = _copyService.IsBusy,
            isPaused = _copyService.IsPaused,
            progressPercentage = _copyService.ProgressPercentage
        });
    }
}