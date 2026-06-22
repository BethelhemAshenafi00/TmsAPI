public interface IEnrollmentService
{
    Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode);
    Task<EnrollmentRecord?> GetByIdAsync(string id);
    Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync();
    Task<EnrollmentRecord?> UpdateAsync(
    string id,
    string studentId,
    string courseCode);
    
    Task<bool> DeleteAsync(string id);
}