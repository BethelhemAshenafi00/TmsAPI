namespace TmsApi.Models;

public interface ICourseService
{
    Task<Course?> GetByIdAsync(string courseId);
    Task<IReadOnlyList<Course>> GetAllAsync();
}