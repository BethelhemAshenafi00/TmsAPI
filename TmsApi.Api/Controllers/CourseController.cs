using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v1/courses")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status500InternalServerError)]
public class CoursesController(
    ICourseService courseService,
    LinkGenerator linkGenerator,
    TmsDbContext context,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID")]
    [EndpointDescription(
        "Returns course details with HATEOAS links. Returns 404 if the course does not exist.")]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);

        if (course is null)
            return NotFound();

        var selfPath = linkGenerator.GetPathByName(
            HttpContext, nameof(GetCourseById), new { id })!;

        var enrollmentsPath = linkGenerator.GetPathByName(
            HttpContext, "ListCourseEnrollments", new { courseId = id })!;

        var links = new List<LinkDto>
        {
            new(selfPath, "self",   "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE"),
            new(enrollmentsPath, "enrollments", "GET")
        };

        if (course.EnrollmentCount < course.MaxCapacity)
            links.Add(new LinkDto(enrollmentsPath, "enroll", "POST"));

        var detail = new CourseDetailDto
        {
            Id             = course.Id,
            Code           = course.Code,
            Title          = course.Title,
            MaxCapacity    = course.MaxCapacity,
            EnrollmentCount = course.EnrollmentCount,
            InstructorId   = course.InstructorId,
            InstructorName = course.InstructorName,
            InstructorEmail= course.InstructorEmail,
            Links          = links
        };

        return Ok(detail);
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription(
        "Admin-only. Creates a course with a unique code. Returns 409 if the course code already exists.")]
    public async Task<IActionResult> CreateCourse(
        CreateCourseRequest request,
        CancellationToken ct)
    {
        if (await courseService.CodeExistsAsync(request.Code, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Course code already exists",
                Status = StatusCodes.Status409Conflict,
                Detail = $"A course with code '{request.Code}' is already registered."
            });
        }

        var result = await courseService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
    }


    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CourseResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List courses with pagination")]
    [EndpointDescription(
        "Returns a paginated, optionally filtered list of TMS courses. PageSize is capped at 50.")]
    public async Task<IActionResult> GetCourses(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        var result = await courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }


    [Authorize(Roles = "Instructor,Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Update a course")]
    [EndpointDescription(
        "Instructors can update only courses assigned to them. Admins can update any course.")]
    public async Task<IActionResult> UpdateCourse(
        int id,
        [FromBody] UpdateCourseRequest request,
        CancellationToken ct)
    {
        var course = await context.Courses.FindAsync(new object[] { id }, ct);

        if (course is null)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Course not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Course with ID {id} was not found."
            });
        }

        var authResult = await authorizationService.AuthorizeAsync(
            User, course, "CanEditCourse");

        if (!authResult.Succeeded)
            return Forbid();

        var updated = await courseService.UpdateAsync(id, request, ct);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }


    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/instructor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Assign an instructor to a course")]
    [EndpointDescription(
        "Admin-only. Sets or replaces the instructor assigned to the specified course.")]
    public async Task<IActionResult> AssignInstructor(
        int id,
        [FromBody] AssignInstructorRequest request,
        CancellationToken ct)
    {
        var success = await courseService.AssignInstructorAsync(id, request.InstructorId, ct);

        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Course not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Course with ID {id} was not found."
            });
        }

        return NoContent();
    }


    [Authorize(Roles = "Instructor,Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Delete a course")]
    [EndpointDescription("Deletes a course. Returns 409 if the course has active enrollments.")]
    public async Task<IActionResult> DeleteCourse(int id, CancellationToken ct)
    {
        try
        {
            var deleted = await courseService.DeleteAsync(id, ct);

            if (!deleted)
            {
                return NotFound(new ProblemDetails
                {
                    Title  = "Course not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Course with ID {id} was not found."
                });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Course cannot be deleted",
                Status = StatusCodes.Status409Conflict,
                Detail = ex.Message
            });
        }
    }
}
