using TmsApi.Models;

namespace TmsApi.Models;

public class StudentService : IStudentService
{
    private readonly List<Student> _students;

    public StudentService()
    {
        _students = new List<Student>
        {
            new Student { StudentId = "STU-001", StudentName = "Abebe", Age = 20, Gpa = 3.5m },
            new Student { StudentId = "STU-002", StudentName = "Alemu", Age = 22, Gpa = 3.8m },
            new Student { StudentId = "STU-003", StudentName = "Mulu", Age = 19, Gpa = 3.2m }
        };
    }

    // GET ALL
    public Task<IReadOnlyList<Student>> GetAllAsync()
    {
        return Task.FromResult((IReadOnlyList<Student>)_students);
    }

    // GET BY ID
    public Task<Student?> GetByIdAsync(string studentId)
    {
        var student = _students.FirstOrDefault(s => s.StudentId == studentId);
        return Task.FromResult(student);
    }

    // CREATE
    public Task<Student> CreateAsync(Student student)
    {
        _students.Add(student);
        return Task.FromResult(student);
    }

    // UPDATE
    public Task<Student?> UpdateAsync(string studentId, Student updatedStudent)
    {
        var existingStudent = _students.FirstOrDefault(s => s.StudentId == studentId);

        if (existingStudent is null)
        {
            return Task.FromResult<Student?>(null);
        }

        existingStudent.StudentName = updatedStudent.StudentName;
        existingStudent.Age = updatedStudent.Age;
        existingStudent.Gpa = updatedStudent.Gpa;

        return Task.FromResult<Student?>(existingStudent);
    }

    // DELETE
    public Task<bool> DeleteAsync(string studentId)
    {
        var student = _students.FirstOrDefault(s => s.StudentId == studentId);

        if (student is null)
        {
            return Task.FromResult(false);
        }

        _students.Remove(student);
        return Task.FromResult(true);
    }
}