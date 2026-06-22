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
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}