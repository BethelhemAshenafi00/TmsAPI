namespace TmsApi.Models;

public class CreateEnrollmentRequest
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public decimal? Grade { get; set; }
}