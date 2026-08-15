// File: TmsApi.Api/Controllers/GradesController.cs
using Microsoft.AspNetCore.Mvc;

namespace TmsApi.Api.Controllers;

public class GradePayload
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public double Score { get; set; }
}

public class GradeResult
{
    public string Id { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public double Score { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class GradesController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GradeResult>> PostGrade([FromBody] GradePayload payload)
    {
        // 2-second delay simulates network/database latency
        // so you can test rapid clicking with exhaustMap
        await Task.Delay(2000);

        var result = new GradeResult
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            StudentId = payload.StudentId,
            CourseId = payload.CourseId,
            Score = payload.Score
        };

        return Ok(result);
    }
}