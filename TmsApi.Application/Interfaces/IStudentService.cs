using TmsApi.Application.DTOs;
namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<StudentResponseDto> GetByIdAsync(int id, CancellationToken ct);
    Task<StudentResponseDto> CreateAsync(CreateStudentRequest request, CancellationToken ct);
    Task<bool> RegistrationNumberExistsAsync(string registrationNumber, CancellationToken ct);
    Task<PagedResponse<StudentResponseDto>> GetStudentsAsync(PagedRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);   



    // // Read
    // Task<IReadOnlyList<Student>> GetAllAsync();
    // Task<Student?> GetByIdAsync(string studentId);

    // // Create
    // Task<Student> CreateAsync(Student student);

    // // Update
    // Task<Student?> UpdateAsync(string studentId, Student student);

    // // Delete
    // Task<bool> DeleteAsync(string studentId);
    
}