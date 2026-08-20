using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<CourseResponseDto?> GetByIdAsync(
        int id,
        CancellationToken ct);
    Task<List<CourseResponseDto>> GetAllAsync(
     CancellationToken ct);

    Task<CourseResponseDto?> GetByCodeAsync(
        string code,
        CancellationToken ct);

    Task<CourseResponseDto> CreateAsync(
        CreateCourseRequest request,
        CancellationToken ct);
    Task<bool> DeleteAsync(
        int id,
        CancellationToken ct);

    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken ct);

    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct);
}