using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
[Tags("Enrollments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentService enrollmentService) : ControllerBase
{

    // =========================
    // GET ALL ENROLLMENTS FOR COURSE
    // =========================
    [HttpGet(Name = nameof(GetEnrollments))]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List enrolments for a course")]
    [EndpointDescription("Returns a list of all enrollments for a specific course. Returns 404 if the course does not exist.")]
    public async Task<IActionResult> GetEnrollments(
        int courseId,
        CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(courseId, ct);

        if (course is null)
        {
            return NotFound();
        }

        var enrollments = await enrollmentService
            .GetByCourseAsync(courseId, ct);

        return Ok(enrollments);
    }


    // =========================
    // GET ONE ENROLLMENT
    // =========================
    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one enrolment for a course")]
    [EndpointDescription("Returns the details of a specific enrollment for a course. Returns 404 if the enrollment does not exist.")]
    public async Task<IActionResult> GetEnrollment(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(courseId, id, ct);

        return enrollment is not null 
            ? Ok(enrollment) 
            : NotFound();
    }


    // =========================
    // CREATE ENROLLMENT
    // =========================
    [HttpPost]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Enrol a student in a course")]
    [EndpointDescription("Returns 404 if the course does not exist, 409 if the course has reached MaxCapacity.")]
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