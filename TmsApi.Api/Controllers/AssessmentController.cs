using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;
[ApiController]
[Tags("Assessments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
[Route("api/[controller]")]
public class AssessmentController (IAssessmentService assessmentService, LinkGenerator linkGenerator): ControllerBase
{
    // =========================
    // GET ASSESSMENT BY ID
    // =========================
    [HttpGet("{id:int}", Name = nameof(GetAssessmentById))]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get an assessment by ID")]
    [EndpointDescription("Returns assessment details with HATEOAS links. Returns 404 if the assessment does not exist.")]
    public async Task<IActionResult> GetAssessmentById(
        int id,
        CancellationToken ct)
    {
        var assessment = await assessmentService.GetByIdAsync(id, ct);

        if (assessment is null)
            return NotFound();

        var selfPath = linkGenerator.GetPathByName(
            HttpContext,
            nameof(GetAssessmentById),
            new { id })!;

        var links = new List<LinkDto>
        {
            new(selfPath, "self", "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE")
        };

        var detail = new AssessmentDetailDto
        {
            Id = assessment.Id,
            Title = assessment.Title,
            MaxScore = assessment.MaxScore,
            Weight = assessment.Weight,
            CourseId = assessment.CourseId,
            Links = links
        };
        return Ok(detail);
    }

    // Create assessment
    [HttpPost]
    [ProducesResponseType(typeof(AssessmentDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Create a new assessment")]
    [EndpointDescription("Creates a new assessment and returns its details with HATEOAS links.")]
    public async Task<IActionResult> Create(
        CreateAssessmentRequest createDto,
        CancellationToken ct)
    {
        var assessment = await assessmentService.CreateAsync(createDto, ct);

        var selfPath = linkGenerator.GetPathByName(
            HttpContext,
            nameof(GetAssessmentById),
            new { id = assessment.Id })!;

        var links = new List<LinkDto>
        {
            new(selfPath, "self", "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE")
        };

        var detail = new AssessmentDetailDto
        {
            Id = assessment.Id,
            Title = assessment.Title,
            MaxScore = assessment.MaxScore,
            Weight = assessment.Weight,
            //Course = assessment.Course,
            CourseId = assessment.CourseId,
            Links = links
        };

        return Created(selfPath, detail);
    }
    //list all assessments
  [HttpGet]
  [ProducesResponseType(typeof(IEnumerable<AssessmentDetailDto>), StatusCodes.Status200OK)]
  [EndpointSummary("List all assessments")]
  [EndpointDescription("Returns a list of all assessments with basic details.")]
  public async Task<IActionResult> List(
    [FromQuery] PagedRequest request,
    CancellationToken ct)
  {
      var assessments = await assessmentService.ListAsync(ct);
      return Ok(assessments);
  }
}