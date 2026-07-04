using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using Tms.Api.Dtos;
using TmsApi.Entities;
using TmsApi.Services;

namespace TmsApi.Services;

public class CourseService : ICourseService
{
    private readonly TmsDbContext _context;

    public CourseService(TmsDbContext context)
    {
        _context = context;
    }

    // =========================
    // GET BY ID (DTO OUTPUT)
    // =========================
    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .FirstOrDefaultAsync(ct);
    }

    // =========================
    // CREATE (DTO INPUT → DTO OUTPUT)
    // =========================
    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(course.Id, ct))!;
    }
public async Task<bool> CodeExistsAsync(string code, CancellationToken ct)
{
    return await _context.Courses
        .AsNoTracking()
        .AnyAsync(c => c.Code == code, ct);
}
}