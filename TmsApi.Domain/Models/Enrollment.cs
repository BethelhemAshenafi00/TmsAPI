
public record EnrollmentRecord(
    string Id,
    string StudentId,
    string CourseCode,
    DateTime EnrolledAt);

public record CreateEnrollmentRequest(string StudentId, string CourseCode);