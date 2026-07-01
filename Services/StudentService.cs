using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public class StudentService
{
    private readonly TmsDbContext _context;

    public StudentService(TmsDbContext context)
    {
        _context = context;
    }


public async Task ShowEnrollmentCountsNPlusOneAsync(
    CancellationToken cancellationToken = default)
{
    var students = await _context.Students
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    foreach (var s in students)
    {
        var count = await _context.Enrollments
            .AsNoTracking()
            .CountAsync(
                e => e.StudentId == s.Id,
                cancellationToken);

        Console.WriteLine(
            $"{s.Name}: {count} enrollments");
    }
}
    // GET ALL
    public async Task<IReadOnlyList<Student>> GetAllAsync()
    {
        return await _context.Students.ToListAsync();
    }

    // GET BY ID
    public async Task<Student?> GetByIdAsync(int id)
    {
        return await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    // CREATE
    public async Task<Student> CreateAsync(Student student)
    {
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        return student;
    }

    // UPDATE
    public async Task<Student?> UpdateAsync(int id, Student updatedStudent)
    {
        var existingStudent = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id);

        if (existingStudent == null)
        {
            return null;
        }

        existingStudent.Name = updatedStudent.Name;
        existingStudent.RegistrationNumber = updatedStudent.RegistrationNumber;
        existingStudent.GPA = updatedStudent.GPA;
        existingStudent.IsActive = updatedStudent.IsActive;

        await _context.SaveChangesAsync();

        return existingStudent;
    }

    // DELETE
    public async Task<bool> DeleteAsync(int id)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return false;
        }

        _context.Students.Remove(student);

        await _context.SaveChangesAsync();

        return true;
    }

    // PAGINATION
    public async Task<List<Student>> GetStudentsPageAsync(int page)
    {
        const int pageSize = 20;

        return await _context.Students
            .OrderBy(s => s.Name) // order by name
            .Skip((page - 1) * pageSize) // skip from previous page 
            .Take(pageSize) // return 
            .ToListAsync();
    }
public async Task ShowStudentEnrollmentCountsAsync()
{
    var report = await _context.Students
        .AsNoTracking()
        .Select(s => new
        {
            s.Name,
            EnrollmentCount = s.Enrollments.Count
        })
        .ToListAsync();

    foreach (var r in report)
    {
        Console.WriteLine($"{r.Name}: {r.EnrollmentCount} enrollments");
    }
}
   
}