using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;
[ApiController]
[Tags("Certificates")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
[Route("api/[controller]")]
public class CertificateController (ICertificateService certificateService, LinkGenerator linkGenerator): ControllerBase
{
    // =========================
    // GET CERTIFICATE BY ID
    // =========================
    [HttpGet("{id:int}", Name = nameof(GetCertificateById))]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a certificate by ID")]
    [EndpointDescription("Returns certificate details with HATEOAS links. Returns 404 if the certificate does not exist.")]
    public async Task<IActionResult> GetCertificateById(
        int id,
        CancellationToken ct)
    {
        var certificate = await certificateService.GetByIdAsync(id, ct);

        if (certificate is null)
            return NotFound();

        var selfPath = linkGenerator.GetPathByName(
            HttpContext,
            nameof(GetCertificateById),
            new { id })!;

        var links = new List<LinkDto>
        {
            new(selfPath, "self", "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE")
        };

        var detail = new CertificateDetailDto
        {
            Id = certificate.Id,
            SerialNumber = certificate.SerialNumber,
            IssuedAt = certificate.IssuedAt,
            StudentId = certificate.StudentId,
            CourseId = certificate.CourseId,
            Links = links
        };
        return Ok(detail);
    }
    //Create Certificate
    [HttpPost]
    [ProducesResponseType(typeof(CertificateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Create a new certificate")]
    [EndpointDescription("Creates a new certificate and returns its details with HATEOAS links.")]
    public async Task<IActionResult> CreateCertificate(
        [FromBody] CreateCertificateRequest request,
        CancellationToken ct)
    {
        var certificate = await certificateService.CreateAsync(request, ct);

        var selfPath = linkGenerator.GetPathByName(
            HttpContext,
            nameof(GetCertificateById),
            new { id = certificate.Id })!;

        var links = new List<LinkDto>
        {
            new(selfPath, "self", "GET"),
            new(selfPath, "update", "PUT"),
            new(selfPath, "delete", "DELETE")
        };

        var detail = new CertificateDetailDto
        {
            Id = certificate.Id,
            SerialNumber = certificate.SerialNumber,
            IssuedAt = certificate.IssuedAt,
            StudentId = certificate.StudentId,
            CourseId = certificate.CourseId,
            Links = links
        };

        return Created(selfPath, detail);
    }
    // List all certificates
    [HttpGet(Name = nameof(ListCertificates))]
    [ProducesResponseType(typeof(List<CertificateResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List all certificates")]
    [EndpointDescription("Returns a list of all certificates.")]
    public async Task<IActionResult> ListCertificates(CancellationToken ct)
    {
        var certificates = await certificateService.ListAsync(ct);
        return Ok(certificates);
    }
    
}