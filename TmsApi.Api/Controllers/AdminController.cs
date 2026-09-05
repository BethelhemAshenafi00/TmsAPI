using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    UserManager<TmsUser> userManager,
    TmsDbContext context)
    : ControllerBase
{
    public record UserDto(
        string Id,
        string Email,
        string? FirstName,
        string? LastName,
        IList<string> Roles
    );

    // =====================================================
    // LIST USERS
    // GET /api/admin/users?role=Instructor
    // =====================================================

    [HttpGet("users")]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List users by role")]
    [EndpointDescription(
        "Admin-only. Returns all users, optionally filtered by role (Student, Instructor, Admin).")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role,
        CancellationToken ct)
    {
        IList<TmsUser> users;

        if (!string.IsNullOrWhiteSpace(role))
        {
            users = await userManager.GetUsersInRoleAsync(role);
        }
        else
        {
            users = await userManager.Users
                .AsNoTracking()
                .ToListAsync(ct);
        }

        var result = new List<UserDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            result.Add(new UserDto(u.Id, u.Email!, u.FirstName, u.LastName, roles));
        }

        return Ok(result);
    }


    // =====================================================
    // GET ALL ENROLLMENTS (cross-course)
    // GET /api/admin/enrollments?status=Pending
    // =====================================================

    public record AdminEnrollmentDto(
        int Id,
        int CourseId,
        string CourseCode,
        string CourseTitle,
        int StudentId,
        string StudentName,
        string Status,
        DateTime EnrolledAt
    );

    [HttpGet("enrollments")]
    [ProducesResponseType(typeof(List<AdminEnrollmentDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List all enrollments across all courses")]
    [EndpointDescription(
        "Admin-only. Returns all enrollments, optionally filtered by status (Pending, Approved, etc.).")]
    public async Task<IActionResult> GetAllEnrollments(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Include(e => e.Student)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var enrollments = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new AdminEnrollmentDto(
                e.Id,
                e.CourseId,
                e.Course.Code,
                e.Course.Title,
                e.StudentId,
                e.Student.Name,
                e.Status,
                e.EnrolledAt))
            .ToListAsync(ct);

        return Ok(enrollments);
    }
}
