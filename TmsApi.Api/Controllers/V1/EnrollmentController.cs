using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using Asp.Versioning;

namespace TmsApi.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/courses/{courseId:int}/enrollments")]
[Tags("Enrollments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class EnrollmentController(
    IEnrollmentService enrollmentService,
    LinkGenerator linkGenerator) : ControllerBase
{
    // =========================
    // GET ENROLLMENT BY ID
    // =========================
    [HttpGet("{id:int}", Name = nameof(GetEnrollmentById))]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get an enrollment by ID")]
    [EndpointDescription("Returns enrollment details for a specific course. Returns 404 if the enrollment does not exist.")]
    public async Task<IActionResult> GetEnrollmentById(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(
            courseId,
            id,
            ct);

        if (enrollment is null)
            return NotFound();

        return Ok(enrollment);
    }


    // =========================
    // CREATE ENROLLMENT
    // =========================
    [HttpPost(Name = nameof(CreateEnrollment))]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new enrollment")]
    [EndpointDescription("Enrolls a student in the specified course. Returns 409 if the student is already enrolled.")]
    public async Task<IActionResult> CreateEnrollment(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct)
    {
        var result = await enrollmentService.CreateAsync(
            courseId,
            request,
            ct);

        return CreatedAtAction(
            nameof(GetEnrollmentById),
            new
            {
                courseId,
                id = result.Id
            },
            result);
    }


    // =========================
    // LIST COURSE ENROLLMENTS
    // =========================
    [HttpGet(Name = "ListCourseEnrollments")]
    [ProducesResponseType(typeof(List<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List course enrollments")]
    [EndpointDescription("Returns all students enrolled in the specified course.")]
    public async Task<IActionResult> GetCourseEnrollments(
        int courseId,
        [FromQuery] string? status,
        CancellationToken ct)
        {
            var result = await enrollmentService.GetByCourseAsync(
                courseId,
                status,
                ct);

        return Ok(result);
    }
 // =======================================
 // Approval 
 // ==================================

    [HttpPost("{id:int}/approve", Name = nameof(ApproveEnrollment))]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Approve an enrollment")]
    [EndpointDescription("Approves a pending enrollment for the specified course. Returns 404 if the enrollment does not exist.")]
    public async Task<IActionResult> ApproveEnrollment(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var result = await enrollmentService.ApproveAsync(
            courseId,
            id,
            ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

   
}