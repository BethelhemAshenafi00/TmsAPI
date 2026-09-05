using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TmsApi.Infrastructure.Identity;

namespace TmsApi.Infrastructure.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateJwt(
        TmsUser user,
        IList<string> roles)
    {
        // =====================================================
        // USER CLAIMS
        // =====================================================

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id
            ),

            new Claim(
                ClaimTypes.Email,
                user.Email ?? string.Empty
            ),

            new Claim(
                ClaimTypes.Name,
                $"{user.FirstName} {user.LastName}".Trim()
            ),

            new Claim(
                "FirstName",
                user.FirstName ?? string.Empty
            ),

            new Claim(
                "LastName",
                user.LastName ?? string.Empty
            )
        };

        // =====================================================
        // ROLES
        // =====================================================

        foreach (var role in roles)
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    role
                )
            );
        }

        // =====================================================
        // JWT CONFIGURATION
        // =====================================================

        var jwtKey = _config["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "JWT configuration is missing: Jwt:Key"
            );
        }

        var issuer = _config["Jwt:Issuer"];

        var audience = _config["Jwt:Audience"];

        var expiryMinutesValue =
            _config["Jwt:ExpiryMinutes"];

        if (!double.TryParse(
                expiryMinutesValue,
                out var expiryMinutes))
        {
            expiryMinutes = 60;
        }

        // =====================================================
        // SIGNING KEY
        // =====================================================

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        );

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

        // =====================================================
        // CREATE JWT
        // =====================================================

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                expiryMinutes
            ),
            signingCredentials: credentials
        );

        // =====================================================
        // RETURN TOKEN
        // =====================================================

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}