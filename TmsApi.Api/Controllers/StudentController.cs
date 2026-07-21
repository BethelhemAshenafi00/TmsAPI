using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/students")]
[Tags("Students")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

public class StudentsController(
    IStudentService studentService,
    LinkGenerator linkGenerator) : ControllerBase
{

    // =========================
    // GET STUDENT BY ID
    // =========================

    [HttpGet("{id:int}", Name = nameof(GetStudentById))]
    [ProducesResponseType(typeof(StudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a student by ID")]
    [EndpointDescription("Returns student details with HATEOAS links. Returns 404 if student does not exist.")]
    public async Task<IActionResult> GetStudentById(
        int id,
        CancellationToken ct)
    {
        var student = await studentService.GetByIdAsync(id, ct);

        if (student is null)
            return NotFound();


        var selfPath = linkGenerator.GetPathByName(
            HttpContext,
            nameof(GetStudentById),
            new { id })!;


        var links = new List<LinkDto>
        {
            new(selfPath, "self", "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE")
        };


        var detail = new StudentDetailDto
        {
            Id = student.Id,
            RegistrationNumber = student.RegistrationNumber,
            Name = student.Name,
            GPA = student.GPA,
            Links = links
        };


        return Ok(detail);
    }



    // =========================
    // CREATE STUDENT
    // =========================

    [HttpPost]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new student")]
    [EndpointDescription("Creates a student with a unique registration number.")]
    public async Task<IActionResult> Create(
        CreateStudentRequest request,
        CancellationToken ct)
    {

        if(await studentService.RegistrationNumberExistsAsync(
            request.RegistrationNumber, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Registration number already exists",
                Status = StatusCodes.Status409Conflict,
                Detail = $"Student with registration number '{request.RegistrationNumber}' already exists."
            });
        }


        var student = await studentService.CreateAsync(request, ct);


        return CreatedAtAction(
            nameof(GetStudentById),
            new { id = student.Id },
            student);
    }




    // =========================
    // UPDATE STUDENT
    // =========================



    // =========================
    // DELETE STUDENT
    // =========================

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Delete a student")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken ct)
    {

        var deleted = await studentService.DeleteAsync(id, ct);


        return deleted
            ? NoContent()
            : NotFound();
    }



    // =========================
    // LIST STUDENTS
    // =========================

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<StudentResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List students with pagination")]
    [EndpointDescription("Returns a paginated list of students.")]
    public async Task<IActionResult> GetStudents(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {

        var students = await studentService.GetStudentsAsync(request, ct);

        return Ok(students);
    }

}