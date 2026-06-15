public interface IStudentService
{
    Task<Student?> GetByIdAsync(string studentId);
    Task<IReadOnlyList<Student>> GetAllAsync();

}