using Microsoft.AspNetCore.Mvc;
using TmsApi.Models;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController(IStudentService studentService) : ControllerBase
{
    // GET: api/students
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students = await studentService.GetAllAsync();
        return Ok(students);
    }

    // GET: api/students/STU-001
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var student = await studentService.GetByIdAsync(id);

        return student is not null
            ? Ok(student)
            : NotFound();
    }

    // POST: api/students
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Student student)
    {
        var createdStudent = await studentService.CreateAsync(student);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdStudent.StudentId },
            createdStudent);
    }

    // PUT: api/students/STU-001
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Student student)
    {
        var updatedStudent = await studentService.UpdateAsync(id, student);

        return updatedStudent is not null
            ? Ok(updatedStudent)
            : NotFound();
    }

    // DELETE: api/students/STU-001
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await studentService.DeleteAsync(id);

        return deleted
            ? NoContent()
            : NotFound();
    }
}