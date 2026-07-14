using System.ComponentModel.DataAnnotations;
namespace TmsApi.Dtos;
public record CreateAssessmentRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }
    [Required, Range(0, 100)]
    public required decimal MaxScore { get; init; }
    [Required, Range(0, 1)]
    public required decimal Weight { get; init; }
    [Required]
    public required int CourseId { get; init; }
    //[Required, MaxLength(200)]
    //public Course course { get; init; } = null!;
}