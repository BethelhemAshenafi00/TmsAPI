using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Enrollments.Commands;

public class EnrollStudentHandler(
    IEnrollmentService enrollmentService,
    ICourseService courseService)
    : IRequestHandler<
        EnrollStudentCommand,
        Result<EnrollmentCreated, EnrollmentError>>
{
    public async Task<Result<EnrollmentCreated, EnrollmentError>> Handle(
        EnrollStudentCommand command,
        CancellationToken ct)
    {
        // 1. Find the course by course code
        var course = await courseService.GetByCodeAsync(
            command.CourseCode,
            ct);

        if (course is null)
        {
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.CourseNotFound(
                    command.CourseCode));
        }

        // 2. Check if the course is full
        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.CourseFull(
                    course.Title,
                    course.MaxCapacity));
        }

        // 3. Check if student is already enrolled
        if (await enrollmentService.ExistsAsync(
            command.StudentId,
            command.CourseCode,
            ct))
        {
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.AlreadyEnrolled(
                    command.StudentId,
                    command.CourseCode));
        }

        // 4. Create the enrollment
        var enrollment = new Enrollment
        {
            StudentId = command.StudentId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };

        // 5. Save the enrollment
        await enrollmentService.AddAsync(
            enrollment,
            ct);

        // 6. Return successful result
        return Result<EnrollmentCreated, EnrollmentError>.Success(
            new EnrollmentCreated(
                enrollment.Id,
                enrollment.StudentId,
                course.Code));
    }
}