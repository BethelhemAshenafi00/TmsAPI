using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Identity;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Infrastructure.Services;

public class CourseService : ICourseService
{
    private readonly TmsDbContext _context;
    private readonly UserManager<TmsUser> _userManager;

    public CourseService(TmsDbContext context, UserManager<TmsUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // =========================
    // PRIVATE: resolve instructor info from InstructorId
    // =========================

    private async Task<(string? Name, string? Email)> GetInstructorInfoAsync(string? instructorId)
    {
        if (string.IsNullOrEmpty(instructorId))
            return (null, null);

        var user = await _userManager.FindByIdAsync(instructorId);
        if (user is null)
            return (null, null);

        return ($"{user.FirstName} {user.LastName}".Trim(), user.Email);
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course is null)
            return null;

        var (name, email) = await GetInstructorInfoAsync(course.InstructorId);

        return new CourseResponseDto(
            course.Id,
            course.Code,
            course.Title,
            course.MaxCapacity,
            course.Enrollments.Count,
            course.InstructorId,
            name,
            email);
    }

    // =========================
    // GET BY CODE
    // =========================
    public async Task<CourseResponseDto?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Code == code, ct);

        if (course is null)
            return null;

        var (name, email) = await GetInstructorInfoAsync(course.InstructorId);

        return new CourseResponseDto(
            course.Id,
            course.Code,
            course.Title,
            course.MaxCapacity,
            course.Enrollments.Count,
            course.InstructorId,
            name,
            email);
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<CourseResponseDto>> GetAllAsync(CancellationToken ct)
    {
        var courses = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Enrollments)
            .ToListAsync(ct);

        var result = new List<CourseResponseDto>(courses.Count);
        foreach (var c in courses)
        {
            var (name, email) = await GetInstructorInfoAsync(c.InstructorId);
            result.Add(new CourseResponseDto(
                c.Id, c.Code, c.Title, c.MaxCapacity,
                c.Enrollments.Count, c.InstructorId, name, email));
        }
        return result;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity,
            InstructorId = request.InstructorId
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(course.Id, ct))!;
    }

    // =========================
    // UPDATE (partial)
    // =========================
    public async Task<CourseResponseDto?> UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course is null)
            return null;

        if (request.Title is not null)
            course.Title = request.Title;

        if (request.MaxCapacity.HasValue)
            course.MaxCapacity = request.MaxCapacity.Value;

        if (request.InstructorId is not null)
            course.InstructorId = request.InstructorId;

        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    // =========================
    // ASSIGN INSTRUCTOR
    // =========================
    public async Task<bool> AssignInstructorAsync(int courseId, string instructorId, CancellationToken ct)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId, ct);

        if (course is null)
            return false;

        course.InstructorId = instructorId;
        await _context.SaveChangesAsync(ct);

        return true;
    }

    // =========================
    // DELETE
    // =========================
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var course = await _context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course is null)
            return false;

        if (course.Enrollments.Any())
            throw new InvalidOperationException(
                "Cannot delete course because it has active student enrollments.");

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    // =========================
    // CODE EXISTS
    // =========================
    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        return await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Code == code, ct);
    }

    // =========================
    // PAGED LIST
    // =========================
    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct)
    {
        IQueryable<Course> query = _context.Courses
            .AsNoTracking()
            .Include(c => c.Enrollments);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, $"%{request.Search}%") ||
                EF.Functions.ILike(c.Code, $"%{request.Search}%"));
        }

        var totalCount = await query.CountAsync(ct);

        query = request.OrderBy switch
        {
            "Code" => request.Descending
                ? query.OrderByDescending(c => c.Code)
                : query.OrderBy(c => c.Code),

            "MaxCapacity" => request.Descending
                ? query.OrderByDescending(c => c.MaxCapacity)
                : query.OrderBy(c => c.MaxCapacity),

            _ => request.Descending
                ? query.OrderByDescending(c => c.Title)
                : query.OrderBy(c => c.Title)
        };

        var courses = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = new List<CourseResponseDto>(courses.Count);
        foreach (var c in courses)
        {
            var (name, email) = await GetInstructorInfoAsync(c.InstructorId);
            items.Add(new CourseResponseDto(
                c.Id, c.Code, c.Title, c.MaxCapacity,
                c.Enrollments.Count, c.InstructorId, name, email));
        }

        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
