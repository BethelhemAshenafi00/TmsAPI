using System.ComponentModel.DataAnnotations;
namespace TmsApi.Application.DTOs;

public record CreateCertificateRequest
{
    [Required, MaxLength(200)]
    public required string SerialNumber { get; init; }
    [Required]
    public required int StudentId { get; init; }
    [Required]
    public required int CourseId { get; init; }
    [Required]
    public required DateTime IssuedAt { get; init; }
}