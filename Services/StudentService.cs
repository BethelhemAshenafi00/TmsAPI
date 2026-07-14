using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;

namespace TmsApi.Services;

public class StudentService : IStudentService
{
    private readonly TmsDbContext _context;
    private readonly ILogger<StudentService> _logger;

    public StudentService(
        TmsDbContext context,
        ILogger<StudentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================
    // GET STUDENT BY ID
    // =========================
    public async Task<StudentResponseDto> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponseDto(
                s.Id,
                s.RegistrationNumber,
                s.Name,
                s.GPA,
                s.IsActive,
                s.Version))
            .FirstOrDefaultAsync(ct);

        if (student is null)
            throw new KeyNotFoundException(
                $"Student with id {id} not found.");

        return student;
    }

    // =========================
    // CREATE STUDENT
    // =========================
    public async Task<StudentResponseDto> CreateAsync(
        CreateStudentRequest request,
        CancellationToken ct)
    {
        var student = new Student
        {
            RegistrationNumber = request.RegistrationNumber,
            Name = request.Name,
            GPA = request.GPA,
            IsActive = true
        };

        _context.Students.Add(student);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created student {StudentId}",
            student.Id);

        return await GetByIdAsync(student.Id, ct);
    }

    // =========================
    // CHECK REGISTRATION NUMBER
    // =========================
    public async Task<bool> RegistrationNumberExistsAsync(
        string registrationNumber,
        CancellationToken ct)
    {
        return await _context.Students.AnyAsync(
            s => s.RegistrationNumber == registrationNumber,
            ct);
    }

    // =========================
    // GET STUDENTS PAGED
    // =========================
    public async Task<PagedResponse<StudentResponseDto>> GetStudentsAsync(
        PagedRequest request,
        CancellationToken ct)
    {
        var query = _context.Students.AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var pageSize = Math.Min(request.PageSize, 50);
        var page = request.Page > 0 ? request.Page : 1;

        var students = await query
            .OrderBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentResponseDto(
                s.Id,
                s.RegistrationNumber,
                s.Name,
                s.GPA,
                s.IsActive,
                s.Version))
            .ToListAsync(ct);

        return new PagedResponse<StudentResponseDto>
        {
            Items = students,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // =========================
    // DELETE STUDENT
    // =========================
    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken ct)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (student is null)
            return false;

        _context.Students.Remove(student);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted student {StudentId}",
            id);

        return true;
    }
}