namespace TmsApi.Application.DTOs;

public record EnrollmentResponseDto(
    int Id,
    int CourseId,
    int StudentId,
    string StudentName,
    string CourseName,
    string Status,
    DateTime EnrolledAt
);