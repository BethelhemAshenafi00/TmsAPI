public class StudentService : IStudentService
{ 
    private readonly List<Student> _students;     
        
        public StudentService(){
            _students = new List<Student>
            {
                new Student { StudentId = "STU-001", StudentName = "Abebe", Age = 20, Gpa = 3.5m },
                new Student { StudentId = "STU-002", StudentName = "Alemu", Age = 22, Gpa = 3.8m },
                new Student { StudentId = "STU-003", StudentName = "Mulu", Age = 19, Gpa = 3.2m }
            };
        }


    public Task<IReadOnlyList<Student>> GetAllAsync()
    {
        return Task.FromResult((IReadOnlyList<Student>)_students);
    }

    public Task<Student?> GetByIdAsync(string studentId)
    {
        var student = _students.FirstOrDefault(s => s.StudentId == studentId);
        return Task.FromResult(student);
    }
}