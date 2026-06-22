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

    // GET BY ID
    public Task<Course?> GetByIdAsync(string courseId)
    {
        var course = _courses.FirstOrDefault(c => c.Id == courseId);
        return Task.FromResult(course);
    }

    // GET ALL
    public Task<IReadOnlyList<Course>> GetAllAsync()
    {
        return Task.FromResult((IReadOnlyList<Course>)_courses);
    }

    // CREATE
    public Task<Course> CreateAsync(Course course)
    {
        _courses.Add(course);
        return Task.FromResult(course);
    }

    // UPDATE
    public Task<Course?> UpdateAsync(string courseId, Course updatedCourse)
    {
        var existingCourse = _courses.FirstOrDefault(c => c.Id == courseId);

        if (existingCourse == null)
        {
            return Task.FromResult<Course?>(null);
        }

        existingCourse.Title = updatedCourse.Title;
        existingCourse.Capacity = updatedCourse.Capacity;

        return Task.FromResult<Course?>(existingCourse);
    }

    // DELETE
    public Task<bool> DeleteAsync(string courseId)
    {
        var course = _courses.FirstOrDefault(c => c.Id == courseId);

        if (course == null)
        {
            return Task.FromResult(false);
        }

        _courses.Remove(course);
        return Task.FromResult(true);
    }
}