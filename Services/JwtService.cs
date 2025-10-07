using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProjectPlanning.Web.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProjectPlanning.Web.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }

    public class JwtService : IJwtService
    {
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public JwtService(IConfiguration configuration)
        {
            _jwtKey = configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key not configured");
            _jwtIssuer = configuration["Jwt:Issuer"] ?? "ProjectPlanning";
            _jwtAudience = configuration["Jwt:Audience"] ?? "ProjectPlanningUsers";
        }

        public string GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, user.Email), // 👈 este es el que lee User.FindFirst(ClaimTypes.Email)
                new Claim("isOfferingOng", user.IsOfferingOng.ToString().ToLower())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));

            if (key.KeySize < 128)
                throw new ArgumentOutOfRangeException("Jwt:Key", "The JWT key must be at least 16 characters long (128 bits).");

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
