using System.ComponentModel.DataAnnotations;
namespace TmsApi.Dtos;
public record CreateStudentRequest
{
    [Required, RegularExpression(@"^[A-Z]{2}\d{6}$", ErrorMessage = "StudentId must follow the pattern XX000000 (e.g., AB123456).")]
    public required string StudentId { get; init; }
    [Required, MaxLength(200)]
    public required string RegistrationNumber { get; init; }
    [Required, MaxLength(200)]
    public required string Name { get; init; }
    [Range(0, 4.0, ErrorMessage = "GPA must be between 0 and 4.0")]
    public decimal GPA { get; init; }
    [Required]
    public bool IsActive { get; init; }
    [Required]
    public uint Version { get; init; }
}