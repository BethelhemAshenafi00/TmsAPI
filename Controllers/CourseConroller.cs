using Microsoft.AspNetCore.Mvc;
using Tms.Api.Dtos;
using TmsApi.Services;
namespace Tms.Api.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courseService) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);
        return course is not null ? Ok(course) : NotFound();
    }
    [HttpPost]
    [HttpPost]
    public async Task<IActionResult> CreateCourse(
    CreateCourseRequest request,
    CancellationToken ct)
    {
        if (await courseService.CodeExistsAsync(request.Code, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course code already exists",
                Status = StatusCodes.Status409Conflict,
                Detail = $"A course with code '{request.Code}' is already registered."
            });
        }

        var result = await courseService.CreateAsync(request, ct);

        return CreatedAtAction(
            nameof(GetCourseById),
            new { id = result.Id },
            result);
    }
    [HttpGet]
    public async Task<IActionResult> GetCourses(
    [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }
}
