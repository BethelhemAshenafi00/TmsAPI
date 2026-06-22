using Microsoft.AspNetCore.Mvc;
[ApiController]
[Route("api/[controller]")]
public class EnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{




    //GET/api/enrollments returns all enrollment records
    [HttpGet]
    public async Task<IActionResult> GetAll(){
    var enrollments = await enrollmentService.GetAllAsync();
    return Ok(enrollments);
    }

    //GET/api/enrollments/{id} returns one or 404
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var record = await enrollmentService.GetByIdAsync(id);
        return record is not null ? Ok(record) : NotFound();

    }

    //POST with 201 + Location
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        var record = await enrollmentService.EnrollAsync(request.StudentId, request.CourseCode);
        return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
    }

    //DELETE/api/enrollments/{id} returns 204 or 404
    [HttpDelete("{id}")]
    public async Task <IActionResult> DeleteAsync(string id)
    {
        var deleted = await enrollmentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // PUT/api/enrollments/{id}

[HttpPut("{id}")]
public async Task<IActionResult> Update(
    string id,
    [FromBody] CreateEnrollmentRequest request)
{
    var updated = await enrollmentService.UpdateAsync(
        id,
        request.StudentId,
        request.CourseCode);

    if (updated is null)
    {
        return NotFound();
    }

    return Ok(updated);
}

}