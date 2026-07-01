namespace TmsApi.Models;

public interface IStudentService
{
    // Read
    Task<IReadOnlyList<Student>> GetAllAsync();
    Task<Student?> GetByIdAsync(string studentId);

    // Create
    Task<Student> CreateAsync(Student student);

    // Update
    Task<Student?> UpdateAsync(string studentId, Student student);

    // Delete
    Task<bool> DeleteAsync(string studentId);
    
}