using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class authController : ControllerBase
{
    private readonly UserManager<TmsUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly TmsDbContext _context;
    private readonly TokenService _tokenService;

    public authController(
        UserManager<TmsUser> userManager,
        RoleManager<IdentityRole> roleManager,
        TmsDbContext context,
        TokenService tokenService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tokenService = tokenService;
    }


    // =====================================================
    // REGISTER
    // =====================================================

    // Public registration — always creates a Student auth.
    // Role is NOT accepted from the client to prevent privilege escalation.
    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Role
    );

    private const string StudentRole = "Student";
    private const string InstructorRole = "Instructor";
    private const string AdminRole = "Admin";

    private static readonly HashSet<string> AllowedRegistrationRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            StudentRole,
            InstructorRole,
            AdminRole
        };
[HttpPost("register")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Register(
    [FromBody] RegisterRequest request,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(request.Role))
        return BadRequest(new { detail = "Role is required." });

    if (!AllowedRegistrationRoles.Contains(request.Role))
        return BadRequest(new
        {
            detail = "Invalid role. Allowed roles: Student, Instructor, Admin."
        });

    var role = AllowedRegistrationRoles.Single(
        allowedRole => string.Equals(
            allowedRole,
            request.Role,
            StringComparison.OrdinalIgnoreCase));

    // Anonymous users can ONLY register as Student.
    // Instructor/Admin accounts must be created by an Admin.
    if (role != StudentRole && !User.IsInRole(AdminRole))
    {
        return Forbid();
    }

    var existingUser =
        await _userManager.FindByEmailAsync(request.Email);

    if (existingUser != null)
    {
        return Ok(new
        {
            message = "Registration request received."
        });
    }

    await using var transaction =
        await _context.Database.BeginTransactionAsync(ct);

    var user = new TmsUser
    {
        UserName = request.Email,
        Email = request.Email,
        FirstName = request.FirstName,
        LastName = request.LastName
    };

    var createUserResult =
        await _userManager.CreateAsync(
            user,
            request.Password);

    if (!createUserResult.Succeeded)
    {
        var errors =
            createUserResult.Errors
                .Select(e => e.Description);

        return BadRequest(new { errors });
    }

    // Create role if it does not exist
    if (!await _roleManager.RoleExistsAsync(role))
    {
        var createRoleResult =
            await _roleManager.CreateAsync(
                new IdentityRole(role));

        if (!createRoleResult.Succeeded)
        {
            var errors =
                createRoleResult.Errors
                    .Select(e => e.Description);

            return BadRequest(new { errors });
        }
    }

    // Assign requested role
    var addRoleResult =
        await _userManager.AddToRoleAsync(
            user,
            role);

    if (!addRoleResult.Succeeded)
    {
        var errors =
            addRoleResult.Errors
                .Select(e => e.Description);

        return BadRequest(new { errors });
    }

    // Create Student profile only for Student accounts
    if (role == StudentRole)
    {
        var student = new Student
        {
            Name = user.DisplayName,
            UserId = user.Id,
            GPA = 0,
            IsActive = true
        };

        _context.Students.Add(student);

        await _context.SaveChangesAsync(ct);

        student.RegistrationNumber =
            $"STU-{DateTime.UtcNow.Year}-{student.Id:D4}";

        await _context.SaveChangesAsync(ct);
    }

    await transaction.CommitAsync(ct);

    return Ok(new
    {
        message = "Registration successful.",
        role
    });
}


    // =====================================================
    // LOGIN
    // =====================================================

    public record LoginRequest(
        string Email,
        string Password
    );


    [HttpPost("login")]
    [EnableRateLimiting("AuthLimiter")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var user =
            await _userManager.FindByEmailAsync(
                request.Email
            );


        if (user == null)
        {
            return Unauthorized(new
            {
                detail = "Invalid credentials."
            });
        }


        // =================================================
        // CHECK LOCKOUT
        // =================================================

        if (await _userManager.IsLockedOutAsync(user))
        {
            return StatusCode(423, new
            {
                detail =
                    "auth locked due to multiple failed login attempts. Try again in 15 minutes."
            });
        }


        // =================================================
        // CHECK PASSWORD
        // =================================================

        var validPassword =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password
            );


        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);

            return Unauthorized(new
            {
                detail = "Invalid credentials."
            });
        }


        // =================================================
        // SUCCESSFUL LOGIN
        // =================================================

        await _userManager.ResetAccessFailedCountAsync(
            user
        );
        // Generate JWT access token
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateJwt(user, roles);

        // create refresh token
        var refreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            ExpiresAt =
                    DateTime.UtcNow.AddDays(7),

            IsUsed = false,

            IsRevoked = false
        };
        _context.RefreshTokens.Add(refreshToken);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            accessToken,

            refreshToken =
                refreshToken.Token
        });
    }

    public record RefreshRequest(
    string RefreshToken
);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request)
    {
        var storedToken =
            await _context.RefreshTokens
                .FirstOrDefaultAsync(
                    rt => rt.Token ==
                          request.RefreshToken);

        if (storedToken == null)
        {
            return Unauthorized(new
            {
                detail = "Invalid refresh token."
            });
        }

        // Token reuse = possible theft
        if (storedToken.IsUsed)
        {
            var userTokens =
                await _context.RefreshTokens
                    .Where(rt =>
                        rt.UserId ==
                        storedToken.UserId)
                    .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
            }

            await _context.SaveChangesAsync();

            return Unauthorized(new
            {
                detail =
                    "Token theft detected. All user sessions revoked."
            });
        }

        // Expired or revoked token
        if (
            storedToken.IsRevoked ||
            storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(new
            {
                detail =
                    "Refresh token expired or revoked."
            });
        }

        // Mark old refresh token as used
        storedToken.IsUsed = true;

        // Find user
        var user =
            await _userManager.FindByIdAsync(
                storedToken.UserId);

        if (user == null)
        {
            return Unauthorized(new
            {
                detail = "User auth not found."
            });
        }

        // Get current roles
        var roles =
            await _userManager.GetRolesAsync(user);

        // Generate new access token
        var newAccessToken =
            _tokenService.GenerateJwt(
                user,
                roles);

        // Generate new refresh token
        var newRefreshToken = new RefreshToken
        {
            Token = Guid.NewGuid()
                .ToString("N"),

            UserId = user.Id,

            ExpiresAt =
                DateTime.UtcNow.AddDays(7),

            IsUsed = false,

            IsRevoked = false
        };

        _context.RefreshTokens.Add(
            newRefreshToken);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            accessToken =
                newAccessToken,

            refreshToken =
                newRefreshToken.Token
        });
    }


}
