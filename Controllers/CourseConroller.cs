using Microsoft.AspNetCore.Mvc;
using TmsApi.Models;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CourseController(ICourseService courseService) : ControllerBase
{
    // GET: api/course
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var courses = await courseService.GetAllAsync();
        return Ok(courses);
    }

    // GET: api/course/CS-001
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var course = await courseService.GetByIdAsync(id);

        if (course == null)
        {
            return NotFound();
        }

        return Ok(course);
    }

    // POST: api/course
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TmsApi.Models.Course course)
    {
        var createdCourse = await courseService.CreateAsync(course);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdCourse.Id },
            createdCourse
        );
    }

    // PUT: api/course/CS-001
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] TmsApi.Models.Course course)
    {
        var updatedCourse = await courseService.UpdateAsync(id, course);

        if (updatedCourse == null)
        {
            return NotFound();
        }

        return Ok(updatedCourse);
    }

    // DELETE: api/course/CS-001
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await courseService.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}