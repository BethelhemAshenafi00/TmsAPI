using TmsApi.Domain.Entities;

public class Student
{
    public int Id { get; set; }
   public string RegistrationNumber { get; set; } = string.Empty;
    public required string Name { get; set; }
    public decimal GPA { get; set; }
    public bool IsActive { get; set; }
    public uint Version { get; set; }

    // Link Student to Identity user without depending on the Infrastructure project.
    public string UserId { get; set; } = string.Empty;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}


