namespace TmsApi.Models;

public class CourseService : ICourseService
{
    private readonly List<Course> _courses;

    public CourseService()
    {
        _courses = new List<Course>
        {
            new Course { Id = "CS-001", Title = "Introduction to Full-Stack Development", Capacity = 30 },
            new Course { Id = "CS-002", Title = "CSharp Programming", Capacity = 25 },
            new Course { Id = "CS-003", Title = "TypeScript Fundamentals", Capacity = 20 }
        };
    }

    public Task<Course?> GetByIdAsync(string courseId)
    {
        var course = _courses.FirstOrDefault(c => c.Id == courseId);
        return Task.FromResult(course);
    }

    public Task<IReadOnlyList<Course>> GetAllAsync()
    {
        return Task.FromResult((IReadOnlyList<Course>)_courses);
    }
}