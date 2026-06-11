using Microsoft.AspNetCore.Mvc;
[ApiController]
[Route("api/[controller]")]
public class EnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    // create new enrollment
   /*  [HttpPost]
    public async Task<IActionResult> Create([FromBody] EnrollmentRecord request)
    {
        var result = await enrollmentService.EnrollAsync(
            request.StudentId,
            request.CourseCode);
        return Ok(result);
    } */




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
}