using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using Tms.Api.Dtos;
using TmsApi.Entities;

namespace TmsApi.Services;

public class EnrollmentService(
    TmsDbContext context,
    ILogger<EnrollmentService> logger) : IEnrollmentService
{
    // =========================
    // GET BY ID (DTO projection)
    // =========================
    public Task<EnrollmentResponseDto?> GetByIdAsync(
        int courseId,
        int id,
        CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.CourseId,
                e.StudentId,
                e.EnrolledAt))
            .FirstOrDefaultAsync(ct);

    // =========================
    // CREATE ENROLLMENT
    // =========================
    public async Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created enrollment {EnrollmentId} for Course {CourseId} and Student {StudentId}",
            enrollment.Id,
            courseId,
            request.StudentId);

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }
}