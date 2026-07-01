using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public class CourseService
{
    private readonly TmsDbContext _context;

    public CourseService(TmsDbContext context)
    {
        _context = context;
    }

    // GET BY ID
    public async Task<Course?> GetByIdAsync(int id)
    {
        return await _context.Courses
        .FirstOrDefaultAsync(c => c.Id == id);
    }

    // GET ALL
    public async Task<IReadOnlyList<Course>> GetAllAsync()
{
    return await _context.Courses.ToListAsync();
}

    // CREATE
    public async Task<Course> CreateAsync(Course course)
    {
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();
        return course;
    }

    // UPDATE
    public async Task<Course?> UpdateAsync(int id, Course updatedCourse)
    {
        var existingCourse =await  _context.Courses
        .FirstOrDefaultAsync(c => c.Id == id);


        if (existingCourse == null)
        {
            return null;
        }

        existingCourse.Title = updatedCourse.Title;
        existingCourse.Code = updatedCourse.Code;
        existingCourse.Capacity = updatedCourse.Capacity;
        await _context.SaveChangesAsync();

        return existingCourse;
    }

    // DELETE
    public async Task<bool> DeleteAsync(int id)
    {
        var course = await _context.Courses
        .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
        {
            return false;
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        return true;
    }
     public async Task<List<Course>> GetTop5CoursesAsync()
    {
        return await _context.Enrollments
            .GroupBy(e => new { e.Course.Id, e.Course.Title, e.Course.Code }) // Group by course
            .Select(g => new Course
            {
                Id = g.Key.Id,
                Title = g.Key.Title,
                Code = g.Key.Code,
                Capacity = g.Count() //How many students in each course
            })
            .OrderByDescending(x => x.Capacity)
            .Take(5) // Show only the best 5 courses
            .ToListAsync();
    }
}