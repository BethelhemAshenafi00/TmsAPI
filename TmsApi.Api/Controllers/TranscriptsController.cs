using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v2/transcripts")]
public class TranscriptsController : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("transcripts")]
    public IActionResult RequestTranscript([FromBody] object? _)
    {
        // Stub for Exercise 4.
        // Exercise 5 will replace this with:
        // - Background job enqueueing
        // - 202 Accepted
        // - Location header
        // - Status URL

        return Ok();
    }
}