using System.ComponentModel.DataAnnotations;
namespace TmsApi.Application.DTOs;
public record CreateStudentRequest
{      public required string Name { get; init; }
    [Range(0, 4.0, ErrorMessage = "GPA must be between 0 and 4.0")]
    public decimal GPA { get; init; }
}