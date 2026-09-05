namespace TmsApi.Application.DTOs;

/// <summary>Request body for the PUT /api/courses/{id}/instructor endpoint.</summary>
public record AssignInstructorRequest(
    /// <summary>The Identity user ID (GUID string) of the instructor to assign.</summary>
    string InstructorId
);
