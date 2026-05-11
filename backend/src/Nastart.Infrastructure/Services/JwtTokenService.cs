using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Infrastructure.Services;

public class JwtTokenService(IConfiguration config) : ITokenService
{
    public string GenerateToken(Guid userId, string email)
    {

        var secret = config["Jwt:SecretKey"] ?? throw new InvalidOperationException(
            "Jwt:SecretKey is not configured. Set it via user-secret (dev) or " +
            "Jwt__SecretKey environment variable (production).");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("email", email)
            ]),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            SigningCredentials = creds
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }

}