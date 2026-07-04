using Microsoft.AspNetCore.Mvc;
using Tms.Api.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    public async Task<IActionResult> GetEnrollment(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(courseId, id, ct);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> EnrollStudent(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct)
    {
        // 1. Get parent course
        var course = await courseService.GetByIdAsync(courseId, ct);

        if (course is null)
        {
            return NotFound();
        }

        // 2. Capacity check
        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course is full",
                Status = StatusCodes.Status409Conflict,
                Detail = $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}."
            });
        }

        // 3. Create enrollment
        var enrollment = await enrollmentService.CreateAsync(courseId, request, ct);

        // 4. Return REST correct response
        return CreatedAtAction(
            nameof(GetEnrollment),
            new { courseId, id = enrollment.Id },
            enrollment);
    }
}