using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace TmsApi.Infrastructure.Services;

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
            e.Student.Name,
            e.Course.Title,
            e.Status,
            e.EnrolledAt))
        .FirstOrDefaultAsync(ct);


    // =========================
    // GET ALL BY COURSE
    // =========================
    public Task<List<EnrollmentResponseDto>> GetByCourseAsync(
    int courseId,
    string? status = null,
    CancellationToken ct = default) =>
    context.Enrollments
        .AsNoTracking()
        .Where(e =>
            e.CourseId == courseId &&
            (string.IsNullOrEmpty(status) ||
             e.Status.ToLower() == status.ToLower()))
        .Select(e => new EnrollmentResponseDto(
            e.Id,
            e.CourseId,
            e.StudentId,
            e.Student.Name,
            e.Course.Title,
            e.Status,
            e.EnrolledAt))
        .ToListAsync(ct);


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
            EnrolledAt = DateTime.UtcNow,
            Status = "Pending"
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


    // =========================
    // CHECK IF STUDENT ALREADY ENROLLED
    // =========================
    public async Task<bool> ExistsAsync(
        int studentId,
        string courseCode,
        CancellationToken ct)
    {
        return await context.Enrollments
            .AsNoTracking()
            .AnyAsync(
                e => e.StudentId == studentId &&
                     e.Course.Code == courseCode,
                ct);
    }


    // =========================
    // ADD ENROLLMENT
    // =========================
    public async Task AddAsync(
        Enrollment enrollment,
        CancellationToken ct)
    {
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);
    }


    // =========================
    // GET ENROLLMENTS BY STUDENT
    // =========================
    public async Task<List<Enrollment>> GetByStudentIdAsync(
        int studentId,
        CancellationToken ct)
    {
        return await context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .ToListAsync(ct);
    }

    // =========================
    // APPROVE ENROLLMENT
    // =========================
    public async Task<EnrollmentResponseDto?> ApproveAsync(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id && e.CourseId == courseId, ct);

        if (enrollment is null)
            return null;

        enrollment.Status = "Approved";
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Approved enrollment {EnrollmentId} for Course {CourseId}",
            enrollment.Id,
            courseId);

        return await GetByIdAsync(courseId, id, ct);
    }

    // =========================
    // REJECT ENROLLMENT
    // =========================
    public async Task<EnrollmentResponseDto?> RejectAsync(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id && e.CourseId == courseId, ct);

        if (enrollment is null)
            return null;

        enrollment.Status = "Rejected";
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Rejected enrollment {EnrollmentId} for Course {CourseId}",
            enrollment.Id,
            courseId);

        return await GetByIdAsync(courseId, id, ct);
    }
}