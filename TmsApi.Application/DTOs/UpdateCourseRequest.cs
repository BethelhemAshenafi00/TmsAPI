using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

public record UpdateCourseRequest
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [Range(1, 200)]
    public int? MaxCapacity { get; init; }

    /// <summary>Optional. Identity user ID of the instructor to assign.</summary>
    public string? InstructorId { get; init; }
}
