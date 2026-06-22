namespace TmsApi.Models;

public interface ICourseService
{
    Task<Course?> GetByIdAsync(string courseId);
    Task<IReadOnlyList<Course>> GetAllAsync();
    Task<Course> CreateAsync(Course course);
    Task<Course?> UpdateAsync(string courseId, Course updatedCourse);
    Task<bool> DeleteAsync(string courseId);
}